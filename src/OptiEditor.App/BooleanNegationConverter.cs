using Microsoft.UI.Xaml.Data;

namespace OptiEditor.App;

public sealed class BooleanNegationConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value is bool boolean && !boolean;
    public object ConvertBack(object value, Type targetType, object parameter, string language) => value is bool boolean && !boolean;
}
