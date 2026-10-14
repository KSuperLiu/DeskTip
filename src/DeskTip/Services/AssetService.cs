using System.Windows.Media.Imaging;
namespace DeskTip.Services;
public static class AssetService
{
    public static BitmapSource Pet { get; } = Load();
    private static BitmapSource Load()
    {
        var image = new BitmapImage(); image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/mizu.png");
        image.DecodePixelWidth = 384; image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit(); image.Freeze(); return image;
    }
}
