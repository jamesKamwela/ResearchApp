using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using ResearchApp.ViewModels;
using System.Collections;

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

}

