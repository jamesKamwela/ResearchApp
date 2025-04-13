using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System.Linq;
using System.Text.RegularExpressions;

public static class ValidationHelper
{
    /// <summary>
    /// Validates a text field and ensures it does not consist only of digits.
    /// </summary>
    /// <param name="newTextValue">The new text value entered by the user.</param>
    /// <param name="minLength">The minimum allowed length.</param>
    /// <param name="maxLength">The maximum allowed length.</param>
    /// <returns>True if the text is valid; otherwise, false.</returns>
    public static bool ValidateText(string newTextValue, int minLength = 3, int maxLength = 100)
    {
        // Check if newTextValue is null or empty
        if (string.IsNullOrEmpty(newTextValue))
        {
            return false;
        }

        // Check if the text consists only of digits
        bool isOnlyNumbers = newTextValue.All(char.IsDigit);

        // Check if the text length is within the specified range and does not consist only of digits
        if (newTextValue.Length < minLength ||
            newTextValue.Length > maxLength ||
            isOnlyNumbers)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a numeric field.
    /// </summary>
    /// <param name="newTextValue">The new text value entered by the user.</param>
    /// <param name="minValue">The minimum allowed value.</param>
    /// <param name="maxValue">The maximum allowed value.</param>
    /// <param name="isPhoneNumber">Whether the field is a phone number.</param>
    /// <returns>True if the number is valid; otherwise, false.</returns>
    public static bool ValidateNumber(string newTextValue, double minValue = 0, double maxValue = 1000000000000, bool isPhoneNumber = false)
    {
        // Check if newTextValue is null or empty
        if (string.IsNullOrEmpty(newTextValue))
        {
            return false;
        }

        // Check if the text is a valid number (including decimals)
        bool isValidNumber = double.TryParse(newTextValue, out double value);

        // If it's a phone number, ensure it contains only digits and is within the specified length
        if (isPhoneNumber)
        {
            if (!Regex.IsMatch(newTextValue, @"^\d{10,15}$"))
            {
                return false;
            }
        }
        // For non-phone numbers, check if it's a valid number and within the specified range
        else if (!isValidNumber || value < minValue || value > maxValue)
        {
            return false;
        }

        return true;
    }
}