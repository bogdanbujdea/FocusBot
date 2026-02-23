using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts a focus score percentage (0-100, or null) to a brush for visual indicators.
/// High (>=60) = aligned, medium (>=40) = neutral, low (&lt;40) = misaligned; null = neutral.
/// </summary>
public class NullableFocusScorePercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int percent)
            return Application.Current.Resources["FbNeutralAccentBrush"];

        var resourceKey = percent switch
        {
            >= 60 => "FbAlignedAccentBrush",
            >= 40 => "FbNeutralAccentBrush",
            _ => "FbMisalignedAccentBrush"
        };

        return Application.Current.Resources[resourceKey] as SolidColorBrush
               ?? Application.Current.Resources["FbNeutralAccentBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
