using PhoneNumbers;

namespace PromptHub.Web.Services.Identity;

public sealed class PhoneNumberNormalizer
    : IPhoneNumberNormalizer
{
    private readonly PhoneNumberUtil
        _phoneNumberUtil;

    public PhoneNumberNormalizer()
    {
        _phoneNumberUtil =
            PhoneNumberUtil.GetInstance();
    }

    public bool TryNormalize(
        string? phoneNumber,
        out string normalizedPhoneNumber)
    {
        normalizedPhoneNumber =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                phoneNumber))
        {
            return false;
        }

        try
        {
            var parsedNumber =
                _phoneNumberUtil.Parse(
                    phoneNumber.Trim(),
                    null);

            if (!_phoneNumberUtil.IsPossibleNumber(
                    parsedNumber) ||
                !_phoneNumberUtil.IsValidNumber(
                    parsedNumber))
            {
                return false;
            }

            normalizedPhoneNumber =
                _phoneNumberUtil.Format(
                    parsedNumber,
                    PhoneNumberFormat.E164);

            return true;
        }
        catch (NumberParseException)
        {
            return false;
        }
    }
}
