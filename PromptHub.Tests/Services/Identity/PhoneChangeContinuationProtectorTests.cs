using Microsoft.AspNetCore.DataProtection;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Services.Identity;

public sealed class PhoneChangeContinuationProtectorTests
{
    private static PhoneChangeContinuationProtector
        CreateProtector()
    {
        return new PhoneChangeContinuationProtector(
            new EphemeralDataProtectionProvider());
    }

    [Fact]
    public void Protect_AndTryUnprotect_ReturnOriginalAuthorization()
    {
        var protector =
            CreateProtector();

        var authorization =
            new PhoneChangeAuthorization(
                "user-123",
                "+79991234567",
                "security-stamp-123");

        var protectedValue =
            protector.Protect(
                authorization);

        var result =
            protector.TryUnprotect(
                protectedValue,
                out var restoredAuthorization);

        Assert.True(
            result);

        Assert.NotNull(
            restoredAuthorization);

        Assert.Equal(
            authorization,
            restoredAuthorization);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryUnprotect_ReturnsFalse_ForMissingValue(
        string? protectedValue)
    {
        var protector =
            CreateProtector();

        var result =
            protector.TryUnprotect(
                protectedValue,
                out var authorization);

        Assert.False(
            result);

        Assert.Null(
            authorization);
    }

    [Fact]
    public void TryUnprotect_ReturnsFalse_ForInvalidValue()
    {
        var protector =
            CreateProtector();

        var result =
            protector.TryUnprotect(
                "not-a-valid-protected-value",
                out var authorization);

        Assert.False(
            result);

        Assert.Null(
            authorization);
    }

    [Fact]
    public void TryUnprotect_ReturnsFalse_ForTamperedValue()
    {
        var protector =
            CreateProtector();

        var authorization =
            new PhoneChangeAuthorization(
                "user-123",
                "+79991234567",
                "security-stamp-123");

        var protectedValue =
            protector.Protect(
                authorization);

        var tamperedValue =
            Tamper(
                protectedValue);

        var result =
            protector.TryUnprotect(
                tamperedValue,
                out var restoredAuthorization);

        Assert.False(
            result);

        Assert.Null(
            restoredAuthorization);
    }

    [Theory]
    [InlineData("", "+79991234567", "stamp")]
    [InlineData("user-123", "", "stamp")]
    [InlineData("user-123", "+79991234567", "")]
    public void TryUnprotect_ReturnsFalse_WhenAuthorizationIsIncomplete(
        string userId,
        string phoneNumber,
        string securityStamp)
    {
        var protector =
            CreateProtector();

        var authorization =
            new PhoneChangeAuthorization(
                userId,
                phoneNumber,
                securityStamp);

        var protectedValue =
            protector.Protect(
                authorization);

        var result =
            protector.TryUnprotect(
                protectedValue,
                out var restoredAuthorization);

        Assert.False(
            result);

        Assert.NotNull(
            restoredAuthorization);
    }

    private static string Tamper(
        string value)
    {
        var chars =
            value.ToCharArray();

        var index =
            chars.Length / 2;

        chars[index] =
            chars[index] == 'A'
                ? 'B'
                : 'A';

        return new string(
            chars);
    }
}