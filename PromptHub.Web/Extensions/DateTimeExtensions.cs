using System.Globalization;

namespace PromptHub.Web.Extensions;

public static class DateTimeExtensions
{
    public static string ToUtcIsoString(
        this DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc =>
                value,

            DateTimeKind.Local =>
                value.ToUniversalTime(),

            _ =>
                DateTime.SpecifyKind(
                    value,
                    DateTimeKind.Utc)
        };

        return utcValue.ToString(
            "O",
            CultureInfo.InvariantCulture);
    }

    public static string? ToUtcIsoString(
        this DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToUtcIsoString()
            : null;
    }
}
