using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "\xE70D" : "\xE70E";
        }
        return "\xE70E";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
