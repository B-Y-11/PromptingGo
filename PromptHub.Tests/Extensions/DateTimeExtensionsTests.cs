using PromptHub.Web.Extensions;

namespace PromptHub.Tests.Extensions;

public sealed class DateTimeExtensionsTests
{
    [Fact]
    public void ToUtcIsoString_FormatsUtcDate()
    {
        var value = new DateTime(
            2026,
            7,
            3,
            12,
            30,
            0,
            DateTimeKind.Utc);

        var result =
            value.ToUtcIsoString();

        Assert.Equal(
            "2026-07-03T12:30:00.0000000Z",
            result);
    }

    [Fact]
    public void ToUtcIsoString_TreatsUnspecifiedAsUtc()
    {
        var value = new DateTime(
            2026,
            7,
            3,
            12,
            30,
            0,
            DateTimeKind.Unspecified);

        var result =
            value.ToUtcIsoString();

        Assert.Equal(
            "2026-07-03T12:30:00.0000000Z",
            result);
    }

    [Fact]
    public void ToUtcIsoString_ReturnsNullForNullableNull()
    {
        DateTime? value = null;

        var result =
            value.ToUtcIsoString();

        Assert.Null(result);
    }
}