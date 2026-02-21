using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

public class NullableIntToFocusScoreFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int percent)
            return $"Focus: {percent}%";
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
