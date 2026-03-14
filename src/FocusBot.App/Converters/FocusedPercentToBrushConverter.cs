using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

public sealed class FocusedPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not double d)
            return Application.Current.Resources["FbTextSecondaryBrush"];

        var percent = d * 100;

        var resourceKey = percent switch
        {
            < 50 => "FbMisalignedAccentBrush",
            <= 70 => "FbNeutralAccentBrush",
            _ => "FbAlignedAccentBrush"
        };

        return Application.Current.Resources[resourceKey] as SolidColorBrush
               ?? (SolidColorBrush)Application.Current.Resources["FbTextSecondaryBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}

