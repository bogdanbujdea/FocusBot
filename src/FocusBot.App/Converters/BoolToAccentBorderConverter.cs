using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts a boolean to a border brush (true = accent color, false = transparent).
/// Used to highlight the currently selected plan card.
/// </summary>
public class BoolToAccentBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true)
        {
            // Return accent brush from app resources
            if (Application.Current.Resources.TryGetValue("FbAccentBrush", out var brush))
            {
                return brush;
            }
            // Fallback to a default accent color
            return new SolidColorBrush(Microsoft.UI.Colors.Purple);
        }
        // Return transparent brush for non-selected cards
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
