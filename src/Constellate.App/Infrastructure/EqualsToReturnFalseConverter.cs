using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Constellate.App
{
    /// <summary>
    /// Returns false (disabled) when the bound value equals ConverterParameter; true otherwise.
    /// Used to visually indicate the active SplitCount or SlideIndex by disabling the matching button.
    /// </summary>
    public sealed class EqualsToReturnFalseConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is null)
                return true; // default to enabled

            try
            {
                var v = System.Convert.ToInt32(value, CultureInfo.InvariantCulture);
                var p = System.Convert.ToInt32(parameter, CultureInfo.InvariantCulture);
                return v != p;
            }
            catch
            {
                return true;
            }
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // One-way usage only
            return value;
        }
    }
}
