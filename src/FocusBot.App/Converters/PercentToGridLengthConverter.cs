using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

public sealed class PercentToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is double d && d > 0)
        {
            return new GridLength(d, GridUnitType.Star);
        }

        return new GridLength(0, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is GridLength gridLength && gridLength.IsStar)
        {
            return gridLength.Value;
        }

        return 0d;
    }
}

