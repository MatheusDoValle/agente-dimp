using System.Collections.Frozen;
using System.Globalization;

namespace Api.Agente.Domain.Validation.Common;

public static class FieldValueRules
{
    private static readonly FrozenSet<string> ValidUfs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA",
        "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN",
        "RS", "RO", "RR", "SC", "SP", "SE", "TO", "EX"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool ValidateDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 8) return false;

        return DateOnly.TryParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    public static bool ValidateYearMonth(string? value, out int year, out int month)
    {
        year = 0;
        month = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 6) return false;

        if (int.TryParse(value[..4], NumberStyles.None, CultureInfo.InvariantCulture, out year) &&
            int.TryParse(value[4..], NumberStyles.None, CultureInfo.InvariantCulture, out month))
        {
            return year >= 1900 && year <= 2100 && month >= 1 && month <= 12;
        }

        return false;
    }

    public static bool ValidateTime(string? value, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length != 6) return false;

        return TimeOnly.TryParseExact(value, "HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    public static bool ValidateDecimal(string? value, int? maxDecimalPlaces, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var normalized = value.Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
        {
            return false;
        }

        if (maxDecimalPlaces.HasValue)
        {
            var commaIndex = value.IndexOf(',');
            if (commaIndex >= 0)
            {
                var decimals = value.Length - commaIndex - 1;
                if (decimals > maxDecimalPlaces.Value) return false;
            }
        }

        return true;
    }

    public static bool ValidateInteger(string? value, int minDigits, int maxDigits, out long number)
    {
        number = 0;
        if (string.IsNullOrWhiteSpace(value) || value.Length < minDigits || value.Length > maxDigits) return false;

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c)) return false;
        }

        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    public static bool ValidateAlpha(string? value, int maxLength)
    {
        if (value is null) return true;
        return value.Length <= maxLength;
    }

    public static bool ValidateExactLength(string? value, int exactLength)
    {
        if (value is null) return false;
        return value.Length == exactLength;
    }

    public static bool ValidateUf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return ValidUfs.Contains(value.Trim());
    }

    public static bool ValidateCpf(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var digits = value.Where(char.IsAsciiDigit).ToArray();
        if (digits.Length != 11) return false;

        // Reject repeated digits (00000000000, 11111111111, etc.)
        if (digits.All(d => d == digits[0])) return false;

        var sum1 = 0;
        for (var i = 0; i < 9; i++) sum1 += (digits[i] - '0') * (10 - i);
        var rem1 = sum1 % 11;
        var digit1 = rem1 < 2 ? 0 : 11 - rem1;
        if (digits[9] - '0' != digit1) return false;

        var sum2 = 0;
        for (var i = 0; i < 10; i++) sum2 += (digits[i] - '0') * (11 - i);
        var rem2 = sum2 % 11;
        var digit2 = rem2 < 2 ? 0 : 11 - rem2;
        return digits[10] - '0' == digit2;
    }

    public static bool ValidateCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        var digits = value.Where(char.IsAsciiDigit).ToArray();
        if (digits.Length != 14) return false;

        if (digits.All(d => d == digits[0])) return false;

        int[] mult1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum1 = 0;
        for (var i = 0; i < 12; i++) sum1 += (digits[i] - '0') * mult1[i];
        var rem1 = sum1 % 11;
        var digit1 = rem1 < 2 ? 0 : 11 - rem1;
        if (digits[12] - '0' != digit1) return false;

        int[] mult2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
        var sum2 = 0;
        for (var i = 0; i < 13; i++) sum2 += (digits[i] - '0') * mult2[i];
        var rem2 = sum2 % 11;
        var digit2 = rem2 < 2 ? 0 : 11 - rem2;
        return digits[13] - '0' == digit2;
    }

    public static bool ValidateCpfOrCnpj(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var len = value.Count(char.IsAsciiDigit);
        return len == 11 ? ValidateCpf(value) : len == 14 && ValidateCnpj(value);
    }
}
