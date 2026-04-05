using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

public sealed class PercentToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            int i => new GridLength(Math.Max(0, i), GridUnitType.Star),
            long l => new GridLength(Math.Max(0, l), GridUnitType.Star),
            double d when d > 0 => new GridLength(d, GridUnitType.Star),
            _ => new GridLength(0, GridUnitType.Star),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is GridLength { IsStar: true } gridLength)
        {
            return gridLength.Value;
        }

        return 0d;
    }
}
