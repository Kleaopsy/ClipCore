using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using Windows.UI;

namespace ClipCore.Assets.Converters
{
    public class FavoriteIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isFavorite)
            {
                return isFavorite ? "\uE735" : "\uE734"; // Filled star : Empty star
            }
            return "\uE734";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class FavoriteColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isFavorite && isFavorite)
            {
                return new SolidColorBrush(Color.FromArgb(255, 255, 185, 0)); // Altın sarısı
            }
            return new SolidColorBrush(Color.FromArgb(255, 150, 150, 150)); // Gri
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}