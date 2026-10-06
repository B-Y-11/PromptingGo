using PromptHub.Web.Services.AuthorSupport;

namespace PromptHub.Tests.Services.AuthorSupport;

public sealed class AuthorSupportReferenceParserTests
{

    [Fact]
    public void TryNormalizeCloudTips_ReturnsLayoutId_ForValidUrl()
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeCloudTips(
                    "https://pay.cloudtips.ru/p/demo-layout-id",
                    out var layoutId,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            "demo-layout-id",
            layoutId);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }


    [Fact]
    public void TryNormalizeCloudTips_AllowsTrailingSlashAndQuery()
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeCloudTips(
                    "https://pay.cloudtips.ru/p/demo-layout-id/?amount=100",
                    out var layoutId,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            "demo-layout-id",
            layoutId);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }


    [Fact]
    public void BuildCloudTipsUrl_BuildsOfficialPaymentUrl()
    {
        var result =
            AuthorSupportReferenceParser
                .BuildCloudTipsUrl(
                    "demo-layout-id");

        Assert.Equal(
            "https://pay.cloudtips.ru/p/demo-layout-id",
            result);
    }



    [Theory]
    [InlineData(
        "http://pay.cloudtips.ru/p/demo-layout-id")]
    [InlineData(
        "https://example.com/p/demo-layout-id")]
    [InlineData(
        "https://pay.cloudtips.ru/demo-layout-id")]
    [InlineData(
        "https://pay.cloudtips.ru/pay/demo-layout-id")]
    [InlineData(
        "https://pay.cloudtips.ru/p/")]
    public void TryNormalizeCloudTips_RejectsInvalidUrl(
        string value)
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeCloudTips(
                    value,
                    out var layoutId,
                    out var errorMessage);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            layoutId);

        Assert.False(
            string.IsNullOrWhiteSpace(
                errorMessage));
    }



    [Fact]
    public void TryNormalizeYooMoney_ReturnsWallet_ForValidNumber()
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeYooMoney(
                    "410011234567890",
                    out var walletNumber,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            "410011234567890",
            walletNumber);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }


    [Fact]
    public void TryNormalizeYooMoney_RemovesWhitespace()
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeYooMoney(
                    "4100 1123 4567 890",
                    out var walletNumber,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            "410011234567890",
            walletNumber);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }



    [Theory]
    [InlineData(
        "123456789012345")]
    [InlineData(
        "4100123")]
    [InlineData(
        "41001123456789ABC")]
    [InlineData(
        "https://yoomoney.ru/410011234567890")]
    [InlineData(
        "2200123456789012")]
    public void TryNormalizeYooMoney_RejectsInvalidNumber(
        string value)
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeYooMoney(
                    value,
                    out var walletNumber,
                    out var errorMessage);

        Assert.False(
            result);

        Assert.Equal(
            string.Empty,
            walletNumber);

        Assert.False(
            string.IsNullOrWhiteSpace(
                errorMessage));
    }



    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeCloudTips_AllowsEmptyValue(
        string? value)
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeCloudTips(
                    value,
                    out var layoutId,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            string.Empty,
            layoutId);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeYooMoney_AllowsEmptyValue(
        string? value)
    {
        var result =
            AuthorSupportReferenceParser
                .TryNormalizeYooMoney(
                    value,
                    out var walletNumber,
                    out var errorMessage);

        Assert.True(
            result);

        Assert.Equal(
            string.Empty,
            walletNumber);

        Assert.Equal(
            string.Empty,
            errorMessage);
    }
}
