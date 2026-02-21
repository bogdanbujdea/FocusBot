using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

public class FocusScorePercentFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int percent)
            return $"Focus: {percent}%";
        return "Focus: 0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
