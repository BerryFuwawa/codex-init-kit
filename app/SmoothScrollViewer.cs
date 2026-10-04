using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CodexKit {
public sealed class SmoothScrollViewer : ScrollViewer {
    static readonly DependencyProperty AnimatedOffsetProperty=DependencyProperty.Register("AnimatedOffset",typeof(double),typeof(SmoothScrollViewer),new PropertyMetadata(0.0,(d,e)=>((SmoothScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));
    double targetOffset;
    bool animating;
    public SmoothScrollViewer(){CanContentScroll=false;PanningMode=PanningMode.VerticalOnly;}
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e){
        // Leave nested text areas and popup lists to their own scrolling controls.
        var source=e.OriginalSource as DependencyObject;
        while(source!=null&&source!=this){if(source is ScrollViewer)return;if(source is Visual||source is System.Windows.Media.Media3D.Visual3D)source=VisualTreeHelper.GetParent(source);else source=LogicalTreeHelper.GetParent(source);}
        if(SystemParameters.WheelScrollLines==0||ScrollableHeight<=0){base.OnPreviewMouseWheel(e);return;}
        var distance=SystemParameters.WheelScrollLines<0?ViewportHeight:SystemParameters.WheelScrollLines*18.0;
        AnimateTo((animating?targetOffset:VerticalOffset)-e.Delta/120.0*distance);
        e.Handled=true;
    }
    internal void AnimateTo(double offset){
        targetOffset=Math.Max(0,Math.Min(ScrollableHeight,offset));
        var current=VerticalOffset;
        BeginAnimation(AnimatedOffsetProperty,null);
        SetValue(AnimatedOffsetProperty,current);
        if(!SystemParameters.ClientAreaAnimation){animating=false;SetValue(AnimatedOffsetProperty,targetOffset);return;}
        animating=true;
        var animation=new DoubleAnimation(current,targetOffset,TimeSpan.FromMilliseconds(180)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        animation.Completed+=(s,e)=>{animating=false;SetValue(AnimatedOffsetProperty,targetOffset);};
        BeginAnimation(AnimatedOffsetProperty,animation,HandoffBehavior.SnapshotAndReplace);
    }
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e){StopAnimation();base.OnPreviewMouseDown(e);}
    protected override void OnPreviewKeyDown(KeyEventArgs e){StopAnimation();base.OnPreviewKeyDown(e);}
    protected override void OnContentChanged(object oldContent,object newContent){StopAnimation();base.OnContentChanged(oldContent,newContent);}
    void StopAnimation(){var offset=VerticalOffset;BeginAnimation(AnimatedOffsetProperty,null);animating=false;targetOffset=offset;SetValue(AnimatedOffsetProperty,offset);}
    internal static void Verify(){
        var view=new SmoothScrollViewer{Content=new Border{Height=4000},VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        view.Measure(new Size(400,400));view.Arrange(new Rect(0,0,400,400));view.UpdateLayout();
        if(view.ScrollableHeight<=0)throw new InvalidOperationException("Scroll test has no extent");
        view.AnimateTo(300);Pump(65);view.UpdateLayout();
        if(SystemParameters.ClientAreaAnimation&&(view.VerticalOffset<=0||view.VerticalOffset>=299))throw new InvalidOperationException("Wheel scrolling did not animate through intermediate offsets");
        var offset=view.VerticalOffset;view.AnimateTo(600);view.UpdateLayout();if(SystemParameters.ClientAreaAnimation&&Math.Abs(view.VerticalOffset-offset)>2)throw new InvalidOperationException("Retargeting scroll animation jumped");
        Pump(240);view.UpdateLayout();if(Math.Abs(view.VerticalOffset-600)>2)throw new InvalidOperationException("Scroll animation did not reach target");
        view.AnimateTo(2000);Pump(40);view.UpdateLayout();view.StopAnimation();offset=view.VerticalOffset;Pump(210);view.UpdateLayout();if(Math.Abs(view.VerticalOffset-offset)>2)throw new InvalidOperationException("Cancelled scroll animation continued");
    }
    static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
}
