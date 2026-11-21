using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexKit {
internal sealed class ActivityIndicator : FrameworkElement {
    readonly RotateTransform rotation=new RotateTransform();
    string state="pending";bool allowMotion=true;
    internal ActivityIndicator(){Width=20;Height=20;RenderTransformOrigin=new Point(.5,.5);RenderTransform=rotation;UseLayoutRounding=true;Loaded+=(s,e)=>{SystemParameters.StaticPropertyChanged+=ParametersChanged;RefreshMotion();};Unloaded+=(s,e)=>{SystemParameters.StaticPropertyChanged-=ParametersChanged;Stop();};IsVisibleChanged+=(s,e)=>RefreshMotion();}
    void ParametersChanged(object sender,System.ComponentModel.PropertyChangedEventArgs e){if(e.PropertyName=="ClientAreaAnimation")RefreshMotion();}
    internal string State {get{return state;}set{if(state==value)return;state=value;InvalidateVisual();RefreshMotion();}}
    internal bool AllowMotion {get{return allowMotion;}set{allowMotion=value;RefreshMotion();}}
    internal double Angle {get{return rotation.Angle;}}
    internal bool Animating {get;private set;}
    void Stop(){rotation.BeginAnimation(RotateTransform.AngleProperty,null);rotation.Angle=0;Animating=false;}
    void RefreshMotion(){Stop();if(state=="running"&&allowMotion&&IsLoaded&&IsVisible&&SystemParameters.ClientAreaAnimation){rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,TimeSpan.FromSeconds(1.2)){RepeatBehavior=RepeatBehavior.Forever});Animating=true;}}
    protected override void OnRender(DrawingContext dc){
        base.OnRender(dc);
        double size=Math.Min(ActualWidth,ActualHeight);
        if(size<1)return;
        dc.PushTransform(new ScaleTransform(size/24,size/24));
        var center=new Point(12,12);
        var color=state=="incomplete"||state=="failed"?MorandiTheme.Warning:state=="complete"?MorandiTheme.Accent:MorandiTheme.Muted;
        var pen=new Pen(color,2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        switch(state){
            case "running":
                dc.DrawEllipse(null,new Pen(MorandiTheme.Border,1.8),center,8,8);
                dc.DrawGeometry(null,pen,Geometry.Parse("M4,12 A8,8 0 0 1 18.9,8"));
                dc.DrawEllipse(color,null,new Point(18.9,8),2,2);
                break;
            case "complete":
                dc.DrawEllipse(null,pen,center,9,9);
                dc.DrawGeometry(null,pen,Geometry.Parse("M7.5,12.2 L10.1,14.7 Q10.9,15.4 11.6,14.6 L16.7,9.3"));
                break;
            case "incomplete":
            case "failed":
                dc.DrawGeometry(null,pen,Geometry.Parse("M9.7,5.4 Q12,1.3 14.3,5.4 L20.4,16.3 Q22.2,20 18,20 H6 Q1.8,20 3.6,16.3 Z M12,9.1 V12.8"));
                dc.DrawEllipse(color,null,new Point(12,16.2),1.1,1.1);
                break;
            case "skipped":
                dc.DrawEllipse(null,pen,center,8.5,8.5);
                dc.DrawLine(pen,new Point(8,12),new Point(16,12));
                break;
            default:
                dc.DrawEllipse(null,new Pen(MorandiTheme.Border,1.6),center,8.5,8.5);
                dc.DrawEllipse(color,null,new Point(9,12),1.2,1.2);
                dc.DrawEllipse(color,null,new Point(15,12),1.2,1.2);
                break;
        }
        dc.Pop();
    }
}
}
