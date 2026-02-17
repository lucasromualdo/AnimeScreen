using System.Globalization;
using MyAnimeScreen.App.Validation;

namespace MyAnimeScreen.App.Tests.Validation;

public sealed class NumericValidationRulesTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("12")]
    public void NonNegativeIntegerValidationRule_AcceptsValidValues(string value)
    {
        var rule = new NonNegativeIntegerValidationRule();

        var result = rule.Validate(value, CultureInfo.GetCultureInfo("pt-BR"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    public void NonNegativeIntegerValidationRule_RejectsInvalidValues(string value)
    {
        var rule = new NonNegativeIntegerValidationRule();

        var result = rule.Validate(value, CultureInfo.GetCultureInfo("pt-BR"));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("10")]
    [InlineData("7,5")]
    [InlineData("7.5")]
    public void PersonalScoreValidationRule_AcceptsSupportedFormats(string value)
    {
        var rule = new PersonalScoreValidationRule();

        var result = rule.Validate(value, CultureInfo.GetCultureInfo("pt-BR"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-0.1")]
    [InlineData("10.01")]
    [InlineData("11")]
    public void PersonalScoreValidationRule_RejectsOutOfRangeOrInvalidValues(string value)
    {
        var rule = new PersonalScoreValidationRule();

        var result = rule.Validate(value, CultureInfo.GetCultureInfo("pt-BR"));

        Assert.False(result.IsValid);
    }
}
