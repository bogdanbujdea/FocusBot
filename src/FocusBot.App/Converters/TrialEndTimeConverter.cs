using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts a DateTime? to a formatted string for displaying trial end time.
/// </summary>
public class TrialEndTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dateTime)
        {
            // Convert UTC to local time for display
            var localTime = dateTime.ToLocalTime();
            return localTime.ToString("MMM d, h:mm tt");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
