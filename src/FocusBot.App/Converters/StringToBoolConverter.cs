using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts a string to bool: non-null/non-empty string returns true, otherwise false.
/// Useful for controlling visibility of error messages.
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
