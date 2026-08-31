using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;

namespace MBW.App.Converters
{
    public sealed class AttachmentTypeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var label = value as string;
            var key = label switch
            {
                "Shared" => "AccentFillColorDefaultBrush",
                "Individual" => "SystemFillColorSuccess",
                _ => "TextFillColorSecondary"
            };

            return GetBrush(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();

        private static Brush GetBrush(string resourceKey)
        {
            if (Microsoft.UI.Xaml.Application.Current.Resources[resourceKey] is Brush brush)
            {
                return brush;
            }

            if (Microsoft.UI.Xaml.Application.Current.Resources["TextFillColorSecondary"] is Brush fallback)
            {
                return fallback;
            }

            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }
}
