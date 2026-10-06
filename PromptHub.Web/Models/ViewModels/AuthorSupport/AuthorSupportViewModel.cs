namespace PromptHub.Web.Models.ViewModels.AuthorSupport;

public sealed class AuthorSupportViewModel
{
    public string AuthorName { get; set; }
        = string.Empty;

    public string? CloudTipsUrl { get; set; }

    public string? YooMoneyWallet { get; set; }

    public bool HasCloudTips =>
        !string.IsNullOrWhiteSpace(
            CloudTipsUrl);

    public bool HasYooMoney =>
        !string.IsNullOrWhiteSpace(
            YooMoneyWallet);

    public bool IsAvailable =>
        HasCloudTips ||
        HasYooMoney;
}
