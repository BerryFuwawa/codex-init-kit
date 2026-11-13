using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexKit {
internal static class SilverTheme {
    internal static readonly Brush Canvas=Solid("#191C23"),Text=Solid("#F2F3F7"),Muted=Solid("#B8BFCC"),Mint=Solid("#A5E7D5"),Dark=Solid("#1B2029");
    internal static readonly Brush Panel=Gradient("#4C5361","#303641"),Primary=Gradient("#FAFCFF","#B3C0D2"),Rail=Gradient("#343A46","#20252F");
    internal static Brush Solid(string hex){var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);brush.Freeze();return brush;}
    internal static Brush Gradient(string top,string bottom){var brush=new LinearGradientBrush(((SolidColorBrush)Solid(top)).Color,((SolidColorBrush)Solid(bottom)).Color,new Point(0,0),new Point(.7,1));brush.Freeze();return brush;}
    internal static Brush Legacy(string hex){
        switch(hex.ToUpperInvariant()){
            case "#10141E":case "#101620":case "#151C28":return Canvas;
            case "#1B2230":case "#293348":case "#151B27":case "#202938":return Solid("#343B48");
            case "#EDF2FC":return Text;
            case "#A4B0C5":return Muted;
            case "#9EBCFF":return Solid("#D3DFF7");
            case "#2E4063":return Solid("#627085");
            case "#70DFC0":return Mint;
            default:return Solid(hex);
        }
    }
}

internal static class Motion {
    internal static bool Enabled(bool preview){return !preview&&SystemParameters.ClientAreaAnimation;}
    static DoubleAnimationUsingKeyFrames Curve(double from,double to,int duration){
        var animation=new DoubleAnimationUsingKeyFrames{Duration=TimeSpan.FromMilliseconds(duration)};
        animation.KeyFrames.Add(new DiscreteDoubleKeyFrame(from,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(duration)),new KeySpline(.22,1,.36,1)));
        return animation;
    }
    internal static void Value(DependencyObject target,DependencyProperty property,double value,int duration,bool enabled){
        var element=target as UIElement;var transform=target as Transform;
        if(!enabled){if(element!=null)element.BeginAnimation(property,null);else if(transform!=null)transform.BeginAnimation(property,null);target.SetValue(property,value);return;}
        var from=(double)target.GetValue(property);var animation=Curve(from,value,duration);
        if(element!=null)element.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace);
        else if(transform!=null)transform.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace);
    }
    internal static void Enter(FrameworkElement element,bool preview,int duration){
        if(!Enabled(preview))return;
        var translate=element.RenderTransform as TranslateTransform;
        if(translate==null){translate=new TranslateTransform();element.RenderTransform=translate;}
        translate.BeginAnimation(TranslateTransform.YProperty,Curve(7,0,duration),HandoffBehavior.SnapshotAndReplace);
        element.BeginAnimation(UIElement.OpacityProperty,Curve(.35,1,duration),HandoffBehavior.SnapshotAndReplace);
    }
    internal static void Button(Button button,bool preview){
        var scale=new ScaleTransform(1,1);button.RenderTransformOrigin=new Point(.5,.5);button.RenderTransform=scale;
        Action<double,int> animate=(value,duration)=>{Value(scale,ScaleTransform.ScaleXProperty,value,duration,Enabled(preview));Value(scale,ScaleTransform.ScaleYProperty,value,duration,Enabled(preview));};
        button.MouseEnter+=(s,e)=>Value(button,UIElement.OpacityProperty,.9,120,Enabled(preview));
        button.MouseLeave+=(s,e)=>{animate(1,140);Value(button,UIElement.OpacityProperty,1,140,Enabled(preview));};
        button.PreviewMouseLeftButtonDown+=(s,e)=>animate(.975,90);
        button.PreviewMouseLeftButtonUp+=(s,e)=>animate(1,140);
        button.LostMouseCapture+=(s,e)=>animate(1,140);
    }
    internal static void Dialog(Window window,bool preview){window.Loaded+=(s,e)=>{var root=window.Content as FrameworkElement;if(root!=null)Enter(root,preview,240);};}
    internal static void Close(Window window,bool? result,bool preview){
        if(!Enabled(preview)){if(result.HasValue)window.DialogResult=result;else window.Close();return;}
        var animation=Curve(window.Opacity,0,120);
        animation.Completed+=(s,e)=>{if(result.HasValue)window.DialogResult=result;else window.Close();};
        window.BeginAnimation(UIElement.OpacityProperty,animation,HandoffBehavior.SnapshotAndReplace);
    }
}
}
