using System.Text.RegularExpressions;

namespace WebAPI.OpenFinance.Helpers
{
    // Regex-based validation for sign-up input. Internal: used only within this project.
    internal static class ValidationHelper
    {
        internal static bool IsValidEmail(string email)
        {
            string regexPattern = @"^([\w\.\-]+)@([\w\-]+)((\.(\w){2,3})+)$";
            return Regex.IsMatch(email, regexPattern);
        }

        // At least 8 characters, with an uppercase, a lowercase, a digit, and a special character.
        internal static bool IsValidPassword(string password)
        {
            string regexPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$";
            return Regex.IsMatch(password, regexPattern);
        }

        // Letters and spaces only, at least 3 characters.
        internal static bool IsValidName(string name)
        {
            string regexPattern = @"^[a-zA-Z\s]{3,}$";
            return Regex.IsMatch(name, regexPattern);
        }

        // At least 5 characters.
        internal static bool IsValidAddress(string address)
        {
            return address.Length >= 5;
        }
    }
}
