using System;
using System.Globalization;
using System.Windows.Data;

namespace WhenPlugin.When.Converters;

public sealed class BoolToAllowTriggersAndConditionsConverter : IValueConverter {
    // input: HideTriggerRunnerTriggersAndConditions
    // output: AllowTriggers/AllowConditions (so invert)
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
        return value is bool hide ? !hide : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
        throw new NotSupportedException();
    }
}