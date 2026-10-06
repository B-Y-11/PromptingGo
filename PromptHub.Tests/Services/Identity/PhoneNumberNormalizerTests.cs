using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Services.Identity;

public sealed class PhoneNumberNormalizerTests
{
    private readonly PhoneNumberNormalizer
        _normalizer =
            new();


    [Fact]
    public void TryNormalize_ReturnsE164_ForRussianNumber()
    {
        var result =
            _normalizer.TryNormalize(
                "+7 999 123-45-67",
                out var normalized);

        Assert.True(
            result);

        Assert.Equal(
            "+79991234567",
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsE164_WhenNumberContainsFormatting()
    {
        var result =
            _normalizer.TryNormalize(
                " +7 (999) 123-45-67 ",
                out var normalized);

        Assert.True(
            result);

        Assert.Equal(
            "+79991234567",
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsE164_ForInternationalNumber()
    {
        var result =
            _normalizer.TryNormalize(
                "+1 202-555-0123",
                out var normalized);

        Assert.True(
            result);

        Assert.Equal(
            "+12025550123",
            normalized);
    }


    [Fact]
    public void TryNormalize_ReturnsFalse_ForNull()
    {
        var result =
            _normalizer.TryNormalize(
                null,
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_ForEmptyString()
    {
        var result =
            _normalizer.TryNormalize(
                string.Empty,
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_ForWhitespace()
    {
        var result =
            _normalizer.TryNormalize(
                "   ",
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_WhenInternationalPrefixIsMissing()
    {
        var result =
            _normalizer.TryNormalize(
                "9991234567",
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_ForObviouslyInvalidNumber()
    {
        var result =
            _normalizer.TryNormalize(
                "+123",
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }

    [Fact]
    public void TryNormalize_ReturnsFalse_ForText()
    {
        var result =
            _normalizer.TryNormalize(
                "not-a-phone",
                out var normalized);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            normalized);
    }
}