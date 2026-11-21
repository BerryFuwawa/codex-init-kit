using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexKit {
static class Brand {
    static readonly DrawingImage vector=CreateVector();
    public static ImageSource VectorLogo {get{return vector;}}
    static Pen RoundPen(Brush brush,double width) {
        return new Pen(brush,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};
    }
    static DrawingImage CreateVector(){
        var group=new DrawingGroup();using(var dc=group.Open()){
            var tile=new SolidColorBrush(Color.FromRgb(32,43,61));
            var blue=new SolidColorBrush(Color.FromRgb(173,203,241));
            var cream=new SolidColorBrush(Color.FromRgb(249,244,233));
            var mint=new SolidColorBrush(Color.FromRgb(165,223,200));
            var ink=new SolidColorBrush(Color.FromRgb(40,74,72));
            dc.DrawRoundedRectangle(tile,null,new Rect(0,0,256,256),62,62);
            var loop=RoundPen(blue,17);
            dc.DrawGeometry(null,loop,Geometry.Parse("M143 202 C96 212 48 180 46 135 C43 91 76 54 120 51 C147 49 171 59 187 78"));
            dc.DrawGeometry(null,loop,Geometry.Parse("M187 52 L189 74 Q190 82 182 81 L160 78"));
            var face=RoundPen(cream,14);
            dc.DrawGeometry(null,face,Geometry.Parse("M89 99 L106 113 Q111 117 106 121 L90 135 M147 114 L147 116"));
            dc.DrawGeometry(null,RoundPen(cream,11),Geometry.Parse("M115 147 Q131 160 147 145"));
            dc.DrawEllipse(mint,null,new Point(185,184),36,36);
            dc.DrawGeometry(null,RoundPen(ink,11),Geometry.Parse("M168 183 L178 193 Q181 196 184 192 L203 172"));
        }group.Freeze();var image=new DrawingImage(group);image.Freeze();return image;
    }
    public static BitmapFrame WindowIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.app.ico")){var decoder=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);var frame=decoder.Frames[decoder.Frames.Count-1];frame.Freeze();return frame;}}
    public static BitmapImage Logo() {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.logo.png")) {
            if(stream==null)throw new InvalidOperationException("Missing logo resource");
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
        }
    }
}
}
