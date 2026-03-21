using FocusBot.Core.Helpers;
using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts total elapsed seconds (long) to "HH:mm:ss" display string.
/// </summary>
public class ElapsedSecondsToTimeStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var totalSeconds = value switch
        {
            long l => l,
            int i => (long)i,
            _ => 0L
        };
        return TimeFormatHelper.FormatElapsed(totalSeconds);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
