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
    protected override void OnRender(DrawingContext dc){base.OnRender(dc);double size=Math.Min(ActualWidth,ActualHeight);if(size<1)return;double unit=size/24;dc.PushTransform(new ScaleTransform(unit,unit));var color=state=="incomplete"||state=="failed"?MorandiTheme.Warning:state=="complete"?MorandiTheme.Accent:MorandiTheme.Muted;var pen=new Pen(color,2){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
        if(state=="running"){dc.DrawEllipse(null,new Pen(MorandiTheme.Border,2),new Point(12,12),8,8);dc.DrawGeometry(null,pen,Geometry.Parse("M12,4 A8,8 0 1 1 4,12"));}
        else if(state=="complete")dc.DrawGeometry(null,pen,Geometry.Parse("M5,12 L10,17 L19,7"));
        else if(state=="incomplete"||state=="failed"){dc.DrawGeometry(null,pen,Geometry.Parse("M12,3 L22,21 L2,21 Z M12,9 L12,14 M12,17 L12,18"));}
        else if(state=="skipped")dc.DrawGeometry(null,pen,Geometry.Parse("M5,12 L19,12"));
        else dc.DrawEllipse(null,new Pen(MorandiTheme.Border,1.5),new Point(12,12),7,7);dc.Pop();}
}
}
