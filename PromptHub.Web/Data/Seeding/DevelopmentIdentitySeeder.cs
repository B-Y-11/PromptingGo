using Microsoft.AspNetCore.Identity;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Security;

namespace PromptHub.Web.Data.Seeding;

public static class DevelopmentIdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        var roleManager =
            serviceProvider.GetRequiredService<
                RoleManager<IdentityRole>>();

        var userManager =
            serviceProvider.GetRequiredService<
                UserManager<ApplicationUser>>();

        var roles = new[]
        {
            ApplicationRoles.Administrator,
            ApplicationRoles.Moderator
        };

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(
                roleName))
            {
                continue;
            }

            var result =
                await roleManager.CreateAsync(
                    new IdentityRole(roleName));

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(error =>
                        error.Description));

                throw new InvalidOperationException(
                    $"Не удалось создать роль {roleName}: {errors}");
            }
        }

        var adminEmail =
            configuration[
                "DevelopmentAdmin:Email"];

        if (string.IsNullOrWhiteSpace(
            adminEmail))
        {
            return;
        }

        var adminUser =
            await userManager.FindByEmailAsync(
                adminEmail);

        // Пользователь сначала должен
        // зарегистрироваться обычным способом.
        if (adminUser is null)
        {
            return;
        }

        if (!await userManager.IsInRoleAsync(
            adminUser,
            ApplicationRoles.Administrator))
        {
            var result =
                await userManager.AddToRoleAsync(
                    adminUser,
                    ApplicationRoles.Administrator);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    "; ",
                    result.Errors.Select(error =>
                        error.Description));

                throw new InvalidOperationException(
                    $"Не удалось назначить администратора: {errors}");
            }
        }
    }
}
