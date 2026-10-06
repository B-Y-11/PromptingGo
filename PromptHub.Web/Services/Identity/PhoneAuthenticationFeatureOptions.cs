namespace PromptHub.Web.Services.Identity;

public sealed class PhoneAuthenticationFeatureOptions
{
    public const string SectionName =
        "Features:PhoneAuthentication";

    public bool Enabled { get; set; }
        = false;
}
