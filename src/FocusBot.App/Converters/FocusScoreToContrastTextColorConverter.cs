using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

public class FocusScoreToContrastTextColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int score)
            return Application.Current.Resources["FbTextSecondaryBrush"];

        var resourceKey = score switch
        {
            >= 6 => "FbFocusStatusAlignedTextBrush",
            >= 4 => "FbFocusStatusUnclearTextBrush",
            _ => "FbFocusStatusMisalignedTextBrush"
        };

        return Application.Current.Resources[resourceKey] as SolidColorBrush
               ?? Application.Current.Resources["FbTextSecondaryBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
