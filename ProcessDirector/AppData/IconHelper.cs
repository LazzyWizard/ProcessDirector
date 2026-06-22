using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProcessDirector.AppData
{
    public static class IconHelper
    {
        private static readonly Dictionary<string, ImageSource> _iconCacheByPath = new Dictionary<string, ImageSource>();

        public static ImageSource GetProcessIcon(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return null;

            if (_iconCacheByPath.TryGetValue(executablePath, out ImageSource cached))
                return cached;

            try
            {
                using (Icon icon = Icon.ExtractAssociatedIcon(executablePath))
                {
                    if (icon != null)
                    {
                        var imageSource = ConvertIconToImageSource(icon);
                        _iconCacheByPath[executablePath] = imageSource;
                        return imageSource;
                    }
                }
            }
            catch { }

            return null;
        }

        public static ImageSource GetProcessIcon(Process process)
        {
            try
            {
                string exePath = process.MainModule?.FileName;
                return GetProcessIcon(exePath);
            }
            catch { return null; }
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