using Microsoft.UI.Xaml.Data;

namespace FocusBot.App.Converters;

/// <summary>
/// Converts int? to "85%" or "—" when null.
/// </summary>
public class NullableIntToPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int i)
            return $"{i}%";
        return "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotImplementedException();
}
