using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

public class FocusScoreToBackgroundBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not int score)
            return Application.Current.Resources["FbFocusStatusUnclearBackgroundBrush"];

        var resourceKey = score switch
        {
            >= 6 => "FbFocusStatusAlignedBackgroundBrush",
            >= 4 => "FbFocusStatusUnclearBackgroundBrush",
            _ => "FbFocusStatusMisalignedBackgroundBrush"
        };

        return Application.Current.Resources[resourceKey] as SolidColorBrush
               ?? Application.Current.Resources["FbFocusStatusUnclearBackgroundBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
