using System.Text.RegularExpressions;

namespace ManagementSystem.Api.Common
{
    public static class PhoneHelper
    {
        private static readonly Regex DisallowedCharsRegex = new Regex(@"[^\d\s\.\-\(\)]", RegexOptions.Compiled);
        private static readonly Regex SeparatorsRegex = new Regex(@"[\s\.\-\(\)]+", RegexOptions.Compiled);

        public static bool TryNormalizePhone(string? rawPhone, out string canonicalPhone)
        {
            if (string.IsNullOrWhiteSpace(rawPhone))
            {
                canonicalPhone = string.Empty;
                return true;
            }

            var trimmed = rawPhone.Trim();

            if (trimmed.StartsWith("+"))
            {
                trimmed = trimmed.Substring(1);
                if (trimmed.Contains('+'))
                {
                    canonicalPhone = string.Empty;
                    return false;
                }
            }
            else if (trimmed.Contains('+'))
            {
                canonicalPhone = string.Empty;
                return false;
            }

            if (DisallowedCharsRegex.IsMatch(trimmed))
            {
                canonicalPhone = string.Empty;
                return false;
            }

            var digits = SeparatorsRegex.Replace(trimmed, "");

            if (digits.Length == 10 && digits.StartsWith("0"))
            {
                canonicalPhone = digits;
                return true;
            }

            if (digits.Length == 11 && digits.StartsWith("84"))
            {
                canonicalPhone = "0" + digits.Substring(2);
                return true;
            }

            canonicalPhone = string.Empty;
            return false;
        }

        public static string NormalizePhone(string? rawPhone)
        {
            return TryNormalizePhone(rawPhone, out var canonicalPhone) ? canonicalPhone : string.Empty;
        }

        public static bool IsValidPhone(string? rawPhone)
        {
            return TryNormalizePhone(rawPhone, out var canonicalPhone) && !string.IsNullOrEmpty(canonicalPhone);
        }
    }
}
