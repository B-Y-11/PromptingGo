using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using PromptHub.Web.Models.Entities;

namespace PromptHub.Web.Services.Identity;

public sealed class OptionalEmailUserValidator
    : IUserValidator<ApplicationUser>
{
    private readonly IdentityErrorDescriber
        _errors;

    public OptionalEmailUserValidator(
        IdentityErrorDescriber errors)
    {
        _errors =
            errors;
    }

    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user)
    {
        var email =
            await manager.GetEmailAsync(
                user);

        // ---------------------------------------------------------------------
        // EN: Phone-only accounts are allowed to have no email.
        // RU: Аккаунтам только с телефоном разрешено не иметь email.
        // ---------------------------------------------------------------------

        if (string.IsNullOrWhiteSpace(
                email))
        {
            return IdentityResult.Success;
        }

        if (!new EmailAddressAttribute()
                .IsValid(email))
        {
            return IdentityResult.Failed(
                _errors.InvalidEmail(
                    email));
        }

        var existingUser =
            await manager.FindByEmailAsync(
                email);

        if (existingUser is not null &&
            existingUser.Id != user.Id)
        {
            return IdentityResult.Failed(
                _errors.DuplicateEmail(
                    email));
        }

        return IdentityResult.Success;
    }
}
