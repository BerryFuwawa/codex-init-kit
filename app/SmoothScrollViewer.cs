using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexKit {
public sealed class SmoothScrollViewer : ScrollViewer {
    ScrollViewer scrollTarget;
    double targetOffset,position,velocity;
    long lastFrame;
    bool animating;
    public SmoothScrollViewer(){CanContentScroll=false;PanningMode=PanningMode.VerticalOnly;Unloaded+=(s,e)=>StopAnimation();}
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e){
        if(SystemParameters.WheelScrollLines==0){base.OnPreviewMouseWheel(e);return;}
        ScrollViewer target=this;var source=e.OriginalSource as DependencyObject;
        while(source!=null&&source!=this){
            var nested=source as ScrollViewer;
            if(nested!=null&&CanMove(nested,e.Delta)){
                if(nested is SmoothScrollViewer)return;
                target=nested;break;
            }
            source=(source is Visual||source is System.Windows.Media.Media3D.Visual3D)?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
        }
        if(!CanMove(target,e.Delta)){base.OnPreviewMouseWheel(e);return;}
        var distance=SystemParameters.WheelScrollLines<0?target.ViewportHeight:SystemParameters.WheelScrollLines*18.0;
        var delta=-e.Delta/120.0*distance;
        var same=animating&&scrollTarget==target;
        var reverse=same&&(targetOffset-position)*delta<0;
        if(reverse)StopAnimation();
        MoveTo(target,(same&&!reverse?targetOffset:target.VerticalOffset)+delta);
        e.Handled=true;
    }
    static bool CanMove(ScrollViewer view,int delta){return view.ScrollableHeight>0&&(delta<0?view.VerticalOffset<view.ScrollableHeight-.1:view.VerticalOffset>.1);}
    internal bool IsMoving {get{return animating;}}
    internal void AnimateTo(double offset){MoveTo(this,offset);}
    void MoveTo(ScrollViewer target,double offset){
        var next=Math.Max(0,Math.Min(target.ScrollableHeight,offset));
        if(!SystemParameters.ClientAreaAnimation){StopAnimation();target.ScrollToVerticalOffset(next);return;}
        if(!animating||scrollTarget!=target){
            StopAnimation();scrollTarget=target;position=target.VerticalOffset;velocity=0;lastFrame=Stopwatch.GetTimestamp();
            animating=true;scrollTarget.ScrollChanged+=TargetScrolled;CompositionTarget.Rendering+=RenderFrame;
        }
        targetOffset=next;
    }
    void RenderFrame(object sender,EventArgs e){
        var now=Stopwatch.GetTimestamp();var dt=Math.Min(.032,(now-lastFrame)/(double)Stopwatch.Frequency);lastFrame=now;
        if(dt<=0||scrollTarget==null)return;
        targetOffset=Math.Max(0,Math.Min(scrollTarget.ScrollableHeight,targetOffset));
        // Critically damped motion retains velocity across successive wheel input.
        const double response=22;var displacement=position-targetOffset;var momentum=velocity+response*displacement;var decay=Math.Exp(-response*dt);
        position=targetOffset+(displacement+momentum*dt)*decay;
        velocity=(velocity-response*momentum*dt)*decay;
        position=Math.Max(0,Math.Min(scrollTarget.ScrollableHeight,position));
        scrollTarget.ScrollToVerticalOffset(position);
        if(Math.Abs(position-targetOffset)<.15&&Math.Abs(velocity)<2){
            scrollTarget.ScrollToVerticalOffset(targetOffset);FinishAnimation();
        }
    }
    void TargetScrolled(object sender,ScrollChangedEventArgs e){
        if(!animating||e.OriginalSource!=scrollTarget||e.VerticalChange==0)return;
        var direction=Math.Sign(targetOffset-position);
        if(direction!=0&&(e.VerticalChange*direction<-.5||(scrollTarget.VerticalOffset-position)*direction>1))StopAnimation();
    }
    void FinishAnimation(){CompositionTarget.Rendering-=RenderFrame;if(scrollTarget!=null)scrollTarget.ScrollChanged-=TargetScrolled;animating=false;velocity=0;scrollTarget=null;}
    internal void StopAnimation(){
        if(!animating)return;
        var target=scrollTarget;FinishAnimation();
        if(target!=null)target.ScrollToVerticalOffset(target.VerticalOffset);
    }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e){StopAnimation();base.OnPreviewMouseDown(e);}
    protected override void OnPreviewKeyDown(KeyEventArgs e){StopAnimation();base.OnPreviewKeyDown(e);}
    protected override void OnPreviewTouchDown(TouchEventArgs e){StopAnimation();base.OnPreviewTouchDown(e);}
    protected override void OnContentChanged(object oldContent,object newContent){StopAnimation();base.OnContentChanged(oldContent,newContent);}
    internal static void Verify(){
        var view=new SmoothScrollViewer{Content=new Border{Height=4000},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var host=new Window{Content=view,Width=420,Height=420,Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};host.Show();host.UpdateLayout();
        try{
            if(view.ScrollableHeight<=0)throw new InvalidOperationException("Scroll test has no extent");
            view.AnimateTo(300);Pump(70);view.UpdateLayout();
            if(SystemParameters.ClientAreaAnimation&&(view.VerticalOffset<=0||view.VerticalOffset>=299))throw new InvalidOperationException("Wheel scrolling did not animate through intermediate offsets");
            var offset=view.VerticalOffset;var speed=view.velocity;view.AnimateTo(600);view.UpdateLayout();
            if(SystemParameters.ClientAreaAnimation&&(Math.Abs(view.VerticalOffset-offset)>2||view.velocity!=speed))throw new InvalidOperationException("Retargeting lost scroll position or velocity");
            Pump(600);view.UpdateLayout();if(Math.Abs(view.VerticalOffset-600)>2)throw new InvalidOperationException("Scroll animation did not reach target");
            view.AnimateTo(2000);Pump(45);view.UpdateLayout();view.StopAnimation();offset=view.VerticalOffset;Pump(210);view.UpdateLayout();if(Math.Abs(view.VerticalOffset-offset)>2||view.animating)throw new InvalidOperationException("Cancelled scroll animation continued");
        }finally{view.StopAnimation();host.Close();}
    }
    static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
}
