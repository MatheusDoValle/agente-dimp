using Api.Agente.Domain.Validation.Common;
using Xunit;

namespace Api.Agente.UnitTests.Validation;

public class CommonRulesTests
{
    [Theory]
    [InlineData("20260601", true)]
    [InlineData("20260228", true)]
    [InlineData("20260229", false)] // 2026 is not a leap year
    [InlineData("20261301", false)] // invalid month
    [InlineData("20260001", false)]
    [InlineData("20260631", false)] // June has 30 days
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void ValidatesDateCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateDate(input, out _));
    }

    [Theory]
    [InlineData("202606", true)]
    [InlineData("202601", true)]
    [InlineData("202612", true)]
    [InlineData("202613", false)]
    [InlineData("202600", false)]
    [InlineData("202", false)]
    public void ValidatesYearMonthCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateYearMonth(input, out _, out _));
    }

    [Theory]
    [InlineData("120000", true)]
    [InlineData("235959", true)]
    [InlineData("000000", true)]
    [InlineData("240000", false)]
    [InlineData("126000", false)]
    [InlineData("120060", false)]
    public void ValidatesTimeCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateTime(input, out _));
    }

    [Theory]
    [InlineData("100,50", 2, true, 100.50)]
    [InlineData("0,00", 2, true, 0.00)]
    [InlineData("-50,25", 2, true, -50.25)]
    [InlineData("100,555", 2, false, 0)] // too many decimals
    [InlineData("abc", 2, false, 0)]
    public void ValidatesDecimalCorrectly(string input, int maxDecimals, bool expected, double expectedValue)
    {
        var valid = FieldValueRules.ValidateDecimal(input, maxDecimals, out var val);
        Assert.Equal(expected, valid);
        if (expected)
        {
            Assert.Equal((decimal)expectedValue, val);
        }
    }

    [Theory]
    [InlineData("SP", true)]
    [InlineData("RJ", true)]
    [InlineData("EX", true)]
    [InlineData("XX", false)]
    [InlineData("", false)]
    public void ValidatesUfCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateUf(input));
    }

    [Theory]
    [InlineData("00000000000191", true)] // standard test CNPJ
    [InlineData("11111111111111", false)] // repeated digits
    [InlineData("00000000000192", false)] // wrong check digit
    [InlineData("123", false)]
    public void ValidatesCnpjCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateCnpj(input));
    }

    [Theory]
    [InlineData("11111111111", false)] // repeated digits
    [InlineData("12345678909", true)]  // valid test CPF
    [InlineData("12345678900", false)] // invalid check digit
    public void ValidatesCpfCorrectly(string input, bool expected)
    {
        Assert.Equal(expected, FieldValueRules.ValidateCpf(input));
    }
}
