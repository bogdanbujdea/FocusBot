using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

public class FocusScoreToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int score)
            return Application.Current.Resources["FbNeutralAccentBrush"];

        var resourceKey = score switch
        {
            >= 6 => "FbAlignedAccentBrush",
            >= 4 => "FbNeutralAccentBrush",
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
