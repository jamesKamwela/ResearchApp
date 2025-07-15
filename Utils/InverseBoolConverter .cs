using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ResearchApp.ViewModels;
using System.Collections;
using ResearchApp.Models;
using System.Windows.Input;


namespace ResearchApp.Utils
{
    #region Boolean Converters

    /// <summary>
    /// Inverts a boolean value (true → false, false → true)
    /// </summary>
    public class InverseBoolConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;

        // Can still convert back since it's the same operation
        public override object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => !(bool)value;
    }

    /// <summary>
    /// Converts a boolean to one of two text options (format: "TrueText|FalseText")
    /// </summary>
    public class BoolToTextConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string options)
            {
                var parts = options.Split('|');
                return boolValue ? parts[0] : parts.Length > 1 ? parts[1] : string.Empty;
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Applies a Style when value is true, returns null when false
    /// </summary>
    public class BoolToStyleConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool isSelected && isSelected ? parameter : null;
    }


    #endregion

    #region Event Converters

    /// <summary>
    /// Passes through SelectionChangedEventArgs for event-to-command binding
    /// </summary>
    public class SelectionChangedConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value; // Pass through the SelectionChangedEventArgs
    }

    #endregion

    #region Other Converters
    #endregion


    #region load more convertors
    public class IndexConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter is CollectionView collectionView &&
                collectionView.ItemsSource is IList items)
            {
                var index = items.IndexOf(value) + 1; // 1-based numbering
                return index.ToString();
            }
            return string.Empty;
        }
    }
    #endregion

    public class ItemTappedEventArgsConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ItemTappedEventArgs eventArgs)
            {
                return eventArgs.Item;
            }
            return null;
        }

        // No need to override ConvertBack since OneWayConverter provides default implementation
    }


    public class BoolToObjectConverter : OneWayConverter
    {
        public object? TrueValue { get; set; }
        public object? FalseValue { get; set; }
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueValue : FalseValue;
            }
            return null;
        }

    }

    public class BoolToColorConverter : OneWayConverter
    {
        public Color TrueColor { get; set; } 
        public Color FalseColor { get; set; } 
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return Colors.Transparent; // Default color if not a boolean
        }
    }
    public class TurkishLiraConverter : IValueConverter
    {
        private static readonly CultureInfo TurkishCulture = new CultureInfo("tr-TR");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal amount)
            {
                return $"TL {amount.ToString("N2", TurkishCulture)}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class BoolToTextColorConverter : OneWayConverter
    {
        public Color TrueColor { get; set; } 
        public Color FalseColor { get; set; } 
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return Colors.Transparent; // Default color if not a boolean
        }
    }
    public class BoolToEmptyViewConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "No jobs available" : null;
            }
            return null;
        }
    }
    public class NullToBoolConverter : IValueConverter
    {
        /// <summary>
        /// Converts null to bool with optional parameter control
        /// </summary>
        /// <param name="value">Input value</param>
        /// <param name="targetType">bool (expected)</param>
        /// <param name="parameter">
        /// Optional control parameters:
        /// "invert" - inverts the logic
        /// "nullable" - returns null when input is null
        /// </param>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? result = value != null;

            // Handle parameter options
            if (parameter is string param)
            {
                if (param.Equals("invert", StringComparison.OrdinalIgnoreCase))
                    result = !result;
                else if (param.Equals("nullable", StringComparison.OrdinalIgnoreCase))
                    return value == null ? null : (object)true;
            }

            return result;
        }

        /// <summary>
        /// Converts back from bool to null (when false)
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                // Handle parameter options
                if (parameter is string param &&
                    param.Equals("invert", StringComparison.OrdinalIgnoreCase))
                {
                    return boolValue ? null : new object(); // non-null
                }

                return boolValue ? new object() : null;
            }
            return null;
        }
    }

    public class BoolToVisibilityConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? "Visible" : "Collapsed";
            }
            return "Collapsed"; // Default to Collapsed if not a boolean
        }
    }
    public class StringEqualsConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue && parameter is string compareTo)
            {
                return string.Equals(strValue, compareTo, StringComparison.OrdinalIgnoreCase);
            }
            return false; // Default to false if types don't match
        }
    }

    public class CustomPeriodVisibilityConverter: OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string period && parameter is string compareTo)
            {
                return string.Equals(period, compareTo, StringComparison.OrdinalIgnoreCase) ? "Visible" : "Collapsed";
            }
            return "Collapsed"; // Default to Collapsed if types don't match
        }
    }
    public class StringToDoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace(value as string))
                return 0.0;

            if (double.TryParse(value.ToString(), out double result))
                return result;

            return 0.0;
        }
    }

    public class IndexToColorConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Safely get the index
            if (value is not int index)
            {
                return GetDefaultColor();
            }

            // Determine which color resource to use
            var resourceKey = index % 2 == 0
                ? "TableRowBackgroundColor"
                : "TableRowAltBackgroundColor";

            // Try to get the color resource
            if (Application.Current?.Resources?.TryGetValue(resourceKey, out var color) == true)
            {
                return color;
            }

            // Fallback to default color
            return GetDefaultColor();
        }

        private object GetDefaultColor()
        {
            // First try the default row color
            if (Application.Current?.Resources?.TryGetValue("TableRowBackgroundColor", out var defaultColor) == true)
            {
                return defaultColor;
            }

            // Ultimate fallback
            return Colors.White;
        }
    }
    public class RowToBoolConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int row)
            {
                return row % 2 == 1; // Returns true for odd rows (alternate)
            }
            return false;
        }

    }
    public class ItemToIndexConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] != null && values[1] is IList list)
            {
                var item = values[0];
                var index = list.IndexOf(item);

                if (index >= 0)
                {
                    // Determine which color resource to use
                    var resourceKey = index % 2 == 0
                        ? "TableRowBackgroundColor"
                        : "TableRowAltBackgroundColor";

                    // Try to get the color resource
                    if (Application.Current?.Resources?.TryGetValue(resourceKey, out var color) == true)
                    {
                        return color;
                    }
                }
            }

            return GetDefaultColor();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private object GetDefaultColor()
        {
            // First try the default row color
            if (Application.Current?.Resources?.TryGetValue("TableRowBackgroundColor", out var defaultColor) == true)
            {
                return defaultColor;
            }
            // Ultimate fallback
            return Colors.White;
        }
    }

    public class RowToColorConverter : IValueConverter
    {
        public Color EvenRowColor { get; set; } = Colors.White;
        public Color OddRowColor { get; set; } = Color.FromArgb("#F9F9F9");

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int row)
            {
                return row % 2 == 0 ? EvenRowColor : OddRowColor;
            }
            return EvenRowColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    public class InvertedBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !(bool)value;
        }
    }

      public class StringToBoolConverter : OneWayConverter
    {
        public override object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                return !string.IsNullOrEmpty(strValue);
            }
            return false; // Default to false if not a string
        }
    }


    


    
}

    





