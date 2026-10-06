using Microsoft.AspNetCore.DataProtection;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Services.Identity;

public sealed class PhoneRegistrationContinuationProtectorTests
{
    private static PhoneRegistrationContinuationProtector
        CreateProtector()
    {
        return new PhoneRegistrationContinuationProtector(
            new EphemeralDataProtectionProvider());
    }

    [Fact]
    public void Protect_AndTryUnprotect_ReturnOriginalChallengeId()
    {
        var protector =
            CreateProtector();

        var challengeId =
            Guid.NewGuid();

        var protectedValue =
            protector.Protect(
                challengeId);

        var result =
            protector.TryUnprotect(
                protectedValue,
                out var restoredChallengeId);

        Assert.True(
            result);

        Assert.Equal(
            challengeId,
            restoredChallengeId);
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
                out var challengeId);

        Assert.False(
            result);

        Assert.Equal(
            Guid.Empty,
            challengeId);
    }

    [Fact]
    public void TryUnprotect_ReturnsFalse_ForInvalidValue()
    {
        var protector =
            CreateProtector();

        var result =
            protector.TryUnprotect(
                "not-a-valid-protected-value",
                out var challengeId);

        Assert.False(
            result);

        Assert.Equal(
            Guid.Empty,
            challengeId);
    }

    [Fact]
    public void TryUnprotect_ReturnsFalse_ForTamperedValue()
    {
        var protector =
            CreateProtector();

        var protectedValue =
            protector.Protect(
                Guid.NewGuid());

        var tamperedValue =
            Tamper(
                protectedValue);

        var result =
            protector.TryUnprotect(
                tamperedValue,
                out var challengeId);

        Assert.False(
            result);

        Assert.Equal(
            Guid.Empty,
            challengeId);
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