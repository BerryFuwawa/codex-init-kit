using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexKit {
static class Brand {
    static readonly DrawingImage vector=CreateVector();
    public static ImageSource VectorLogo {get{return vector;}}
    static DrawingImage CreateVector(){
        var group=new DrawingGroup();using(var dc=group.Open()){
            var dark=new SolidColorBrush(Color.FromRgb(21,27,39));var blue=new SolidColorBrush(Color.FromRgb(158,188,255));var white=new SolidColorBrush(Color.FromRgb(237,242,252));var mint=new SolidColorBrush(Color.FromRgb(112,223,192));
            dc.DrawRoundedRectangle(dark,null,new Rect(0,0,256,256),52,52);
            var loop=new Pen(blue,20){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
            dc.DrawGeometry(null,loop,Geometry.Parse("M171,61 C147,43 94,42 65,74 C35,108 39,160 72,188 C96,209 127,214 154,205"));
            dc.DrawGeometry(blue,null,Geometry.Parse("M183,45 L199,82 L159,77 Z"));
            var terminal=new Pen(white,13){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.DrawGeometry(null,terminal,Geometry.Parse("M94,99 L118,123 L94,147 M135,148 L154,148"));
            var tick=new Pen(mint,16){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.DrawGeometry(null,tick,Geometry.Parse("M166,179 L184,197 L216,162"));
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
