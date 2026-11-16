using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodexKit {
internal static class MorandiTheme {
    internal static readonly Brush Canvas=Solid("#192330"),Text=Solid("#ECEEF0"),Muted=Solid("#AEB8C4"),Accent=Solid("#AFBED0"),Dark=Text,OnPrimary=Solid("#172535");
    internal static readonly Brush Panel=Solid("#253343"),Primary=Solid("#9DAFC4"),Rail=Solid("#1E2B3B"),Border=Solid("#3B4B5D"),Input=Solid("#1C2937"),Locked=Solid("#2B3A4C"),Selection=Solid("#3B5066"),Disabled=Solid("#344051"),Danger=Solid("#93675F"),Warning=Solid("#C5AE83");
    internal static readonly Brush Group=Solid("#2A3A4C");
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    internal static void StyleWindow(Window window){
        var handle=new WindowInteropHelper(window).Handle;int dark=1;
        DwmSetWindowAttribute(handle,20,ref dark,4);
        var background=((SolidColorBrush)Canvas).Color;int caption=background.R|(background.G<<8)|(background.B<<16);
        var foreground=((SolidColorBrush)Text).Color;int ink=foreground.R|(foreground.G<<8)|(foreground.B<<16);
        DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref ink,4);
    }
    internal static Brush Solid(string hex){var brush=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);brush.Freeze();return brush;}
    internal static Brush Legacy(string hex){
        switch(hex.ToUpperInvariant()){
            case "#10141E":return Canvas;
            case "#101620":return Input;
            case "#151C28":return Solid("#2A3A4C");
            case "#1B2230":return Panel;
            case "#293348":return Solid("#2B3A4C");
            case "#151B27":return Rail;
            case "#202938":return Locked;
            case "#EDF2FC":case "#14231C":return Text;
            case "#A4B0C5":return Muted;
            case "#9EBCFF":case "#70DFC0":return Accent;
            case "#2E4063":return Selection;
            case "#2D394D":case "#39465D":return Border;
            case "#E9C779":return Warning;
            case "#383838":return Disabled;
            case "#A6A6A6":return Solid("#909CA7");
            case "#963E4A":case "#833F4C":return Danger;
            case "#101B31":return OnPrimary;
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
    internal static void Dialog(Window window,bool preview){window.SourceInitialized+=(s,e)=>MorandiTheme.StyleWindow(window);window.Loaded+=(s,e)=>{var root=window.Content as FrameworkElement;if(root!=null)Enter(root,preview,240);};}
    internal static void Close(Window window,bool? result,bool preview){
        if(!Enabled(preview)){if(result.HasValue)window.DialogResult=result;else window.Close();return;}
        var animation=Curve(window.Opacity,0,120);
        animation.Completed+=(s,e)=>{if(result.HasValue)window.DialogResult=result;else window.Close();};
        window.BeginAnimation(UIElement.OpacityProperty,animation,HandoffBehavior.SnapshotAndReplace);
    }
}
}
