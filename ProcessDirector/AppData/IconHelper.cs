using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProcessDirector.AppData
{
    public static class IconHelper
    {
        public static ImageSource GetProcessIcon(Process process)
        {
            try
            {
                string exePath = null;

                try
                {
                    exePath = process.MainModule?.FileName;
                }
                catch { }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using (Icon icon = Icon.ExtractAssociatedIcon(exePath))
                    {
                        if (icon != null)
                        {
                            return ConvertIconToImageSource(icon);
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private static BitmapImage ConvertIconToImageSource(Icon icon)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                icon.Save(stream);
                stream.Seek(0, SeekOrigin.Begin);

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = stream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
    }
}