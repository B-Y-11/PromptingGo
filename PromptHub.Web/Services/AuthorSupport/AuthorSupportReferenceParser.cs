using System.Text.RegularExpressions;

namespace PromptHub.Web.Services.AuthorSupport;

public static partial class AuthorSupportReferenceParser
{
    private const string CloudTipsHost =
        "pay.cloudtips.ru";

    public static bool TryNormalizeCloudTips(
        string? value,
        out string layoutId,
        out string errorMessage)
    {
        layoutId =
            string.Empty;

        errorMessage =
            string.Empty;

        var input =
            value?.Trim();

        if (string.IsNullOrWhiteSpace(
                input))
        {
            return true;
        }

        if (!Uri.TryCreate(
                input,
                UriKind.Absolute,
                out var uri) ||
            !string.Equals(
                uri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                uri.Host,
                CloudTipsHost,
                StringComparison.OrdinalIgnoreCase) ||
            !uri.IsDefaultPort)
        {
            errorMessage =
                "Укажите персональную ссылку CloudTips.";
            return false;
        }

        var segments =
            uri.AbsolutePath
                .Trim('/')
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length != 2 ||
            !string.Equals(
                segments[0],
                "p",
                StringComparison.OrdinalIgnoreCase))
        {
            errorMessage =
                "Ссылка CloudTips имеет неверный формат.";
            return false;
        }

        var candidate =
            segments[1];

        if (!CloudTipsLayoutIdRegex()
                .IsMatch(candidate))
        {
            errorMessage =
                "Не удалось определить идентификатор страницы CloudTips.";
            return false;
        }

        layoutId =
            candidate;

        return true;
    }

    public static bool TryNormalizeYooMoney(
        string? value,
        out string walletNumber,
        out string errorMessage)
    {
        walletNumber =
            string.Empty;

        errorMessage =
            string.Empty;

        var input =
            value?.Trim();

        if (string.IsNullOrWhiteSpace(
                input))
        {
            return true;
        }

        var normalized =
            string.Concat(
                input.Where(character =>
                    !char.IsWhiteSpace(character)));

        if (!YooMoneyWalletRegex()
                .IsMatch(normalized))
        {
            errorMessage =
                "Введите номер кошелька ЮMoney: от 11 до 20 цифр, начинающийся с 4100.";
            return false;
        }

        walletNumber =
            normalized;

        return true;
    }

    public static string BuildCloudTipsUrl(
        string layoutId)
    {
        return
            $"https://pay.cloudtips.ru/p/{layoutId}";
    }

    [GeneratedRegex(
        "^[A-Za-z0-9_-]{4,100}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        CloudTipsLayoutIdRegex();

    [GeneratedRegex(
        "^4100[0-9]{7,16}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex
        YooMoneyWalletRegex();
}
