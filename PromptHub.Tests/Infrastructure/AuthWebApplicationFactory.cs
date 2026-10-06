using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PromptHub.Tests.Infrastructure.Identity;
using PromptHub.Web.Data;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Infrastructure;

public sealed class AuthWebApplicationFactory
    : WebApplicationFactory<Program>
{
    private readonly string
        _connectionString;

    public TestPhoneOtpSender PhoneOtpSender { get; }
        = new();

    public AuthWebApplicationFactory(
        string connectionString)
    {
        _connectionString =
            connectionString;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment(
            "Testing");

        // ---------------------------------------------------------------------
        // EN: Deterministic configuration used only by integration tests.
        // RU: Детерминированная конфигурация только для integration-тестов.
        // ---------------------------------------------------------------------

        builder.ConfigureAppConfiguration(
            (_, configuration) =>
            {
                configuration.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] =
                            _connectionString,


                        ["PhoneOtp:LifetimeMinutes"] =
                            "5",

                        ["PhoneOtp:ResendCooldownSeconds"] =
                            "60",

                        ["PhoneOtp:MaxRequestsPerHour"] =
                            "5",

                        ["PhoneOtp:MaxRequestsPerUserPerHour"] =
                            "10",

                        ["PhoneOtp:MaxVerificationAttempts"] =
                            "5",

                        ["PhoneOtp:HashingKey"] =
                            "PromptHub.Tests.PhoneOtp.HashingKey.2026"
                    });
            });

        builder.ConfigureServices(
            services =>
            {
                // -------------------------------------------------------------
                // EN: Use an isolated PostgreSQL database for integration tests.
                // RU: Для интеграционных тестов используем изолированную PostgreSQL-базу.
                // -------------------------------------------------------------

                services.RemoveAll<
                    DbContextOptions<ApplicationDbContext>>();

                services.RemoveAll<
                    ApplicationDbContext>();

                services.AddDbContext<ApplicationDbContext>(
                    options =>
                    {
                        options.UseNpgsql(
                            _connectionString);

                        options.EnableDetailedErrors();
                    });

                // -------------------------------------------------------------
                // EN: Never send real SMS from integration tests.
                // RU: Integration-тесты никогда не отправляют реальные SMS.
                // -------------------------------------------------------------

                services.RemoveAll<
                    IPhoneOtpSender>();

                services.AddSingleton<IPhoneOtpSender>(
                    PhoneOtpSender);

                // -------------------------------------------------------------
                // EN: Background OTP cleanup is irrelevant for isolated tests
                //     and could race with database recreation.
                //
                // RU: Фоновая очистка OTP здесь не нужна и может конфликтовать
                //     с пересозданием тестовой БД.
                // -------------------------------------------------------------

                var cleanupService =
                    services.FirstOrDefault(
                        descriptor =>
                            descriptor.ServiceType ==
                                typeof(IHostedService) &&
                            descriptor.ImplementationType ==
                                typeof(PhoneOtpCleanupService));

                if (cleanupService is not null)
                {
                    services.Remove(
                        cleanupService);
                }
            });
    }
}