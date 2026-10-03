using System;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace CodexKit {
static class Brand {
    public static BitmapImage Logo() {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.logo.png")) {
            if(stream==null)throw new InvalidOperationException("Missing logo resource");
            var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image;
        }
    }
}
}
