using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromptHub.Web.Data;
using PromptHub.Web.Data.Seeding;
using PromptHub.Web.Models.Entities;
using PromptHub.Web.Services.Notifications;
using PromptHub.Web.Services.PromptRendering;
using PromptHub.Web.Services.PromptStats;
using PromptHub.Web.Services.PromptTemplates;
using PromptHub.Web.Services.Slugs;
using PromptHub.Web.Services.MediaStorage;
using PromptHub.Web.Services.PromptMedia;
using PromptHub.Web.Services.Identity;
using System.Threading.RateLimiting;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using PromptHub.Web.Services.Moderation;
using PromptHub.Web.Services.Privacy;
using PromptHub.Web.Configuration;
using PromptHub.Web.Services.AuthorSupport;
using PromptHub.Web.Services.Search;
using PromptHub.Web.Services.Email;
using PromptHub.Web.Services.Security;

var builder = WebApplication.CreateBuilder(args);

var legalOptions =
    builder.Services
        .AddOptions<LegalOptions>()
        .Bind(
            builder.Configuration.GetSection(
                LegalOptions.SectionName));

if (builder.Environment.IsProduction())
{
    legalOptions
        .ValidateDataAnnotations()
        .ValidateOnStart();
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();


builder.Services
    .AddDefaultIdentity<ApplicationUser>(
        options =>
        {

            options.SignIn.RequireConfirmedEmail =
                true;

            // -----------------------------------------------------------------
            // EN: Email remains optional at the Identity model level because
            //     phone authentication is controlled separately by a feature flag.
            //     Email uniqueness is enforced by the application logic.
            // RU: На уровне Identity email остаётся необязательным, поскольку
            //     телефонная аутентификация управляется отдельным feature flag.
            //     Уникальность email контролируется логикой приложения.
            // -----------------------------------------------------------------

            options.User.RequireUniqueEmail =
                false;

            // -----------------------------------------------------------------
            // EN: Account lockout limits repeated password guesses.
            // RU: Блокировка аккаунта ограничивает повторные попытки подбора пароля.
            // -----------------------------------------------------------------

            options.Lockout.AllowedForNewUsers =
                true;

            options.Lockout.MaxFailedAccessAttempts =
                5;

            options.Lockout.DefaultLockoutTimeSpan =
                TimeSpan.FromMinutes(
                    15);
        })
    .AddUserValidator<OptionalEmailUserValidator>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<
        ApplicationDbContext>();



builder.Services.Configure<
    DataProtectionTokenProviderOptions>(
        options =>
        {
            options.TokenLifespan =
                TimeSpan.FromHours(
                    24);
        });

builder.Services.Configure<
    SecurityStampValidatorOptions>(
        options =>
        {
            options.ValidationInterval =
                TimeSpan.FromMinutes(
                    5);
        });

var unisenderGoOptions =
    builder.Services
        .AddOptions<UnisenderGoOptions>()
        .Bind(
            builder.Configuration.GetSection(
                UnisenderGoOptions.SectionName));

if (builder.Environment.IsProduction())
{
    unisenderGoOptions
        .ValidateDataAnnotations()
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(
                    options.ApiKey),
            "UnisenderGo:ApiKey is required in Production.")
        .ValidateOnStart();

    builder.Services.AddHttpClient<
        ITransactionalEmailClient,
        UnisenderGoEmailClient>();

    builder.Services.AddTransient<
        IEmailSender<ApplicationUser>,
        ProductionIdentityEmailSender>();

    builder.Services.AddTransient<
        IAccountSecurityEmailSender,
        ProductionAccountSecurityEmailSender>();
}
else
{
    builder.Services.AddSingleton<
        IEmailSender<ApplicationUser>,
        DevelopmentIdentityEmailSender>();

    builder.Services.AddSingleton<
        IAccountSecurityEmailSender,
        DevelopmentAccountSecurityEmailSender>();
}


var smartCaptchaOptions =
    builder.Services
        .AddOptions<SmartCaptchaOptions>()
        .Bind(
            builder.Configuration.GetSection(
                SmartCaptchaOptions.SectionName))
        .ValidateDataAnnotations();

if (builder.Environment.IsProduction())
{
    smartCaptchaOptions
        .Validate(
            options =>
                options.Enabled,
            "SmartCaptcha must be enabled in Production.")
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(
                    options.ClientKey) &&
                !string.IsNullOrWhiteSpace(
                    options.ServerKey),
            "SmartCaptcha client and server keys are required in Production.")
        .ValidateOnStart();
}

builder.Services.AddHttpClient<
    ISmartCaptchaVerificationService,
    YandexSmartCaptchaVerificationService>();

// -----------------------------------------------------------------------------
// EN: Phone authentication is controlled by a feature flag. Expired OTP
//     challenges are still cleaned up to limit retention of phone numbers.
// RU: Телефонная аутентификация управляется feature flag. Просроченные
//     OTP-challenge всё равно очищаются, чтобы ограничить хранение номеров.
// -----------------------------------------------------------------------------

builder.Services
    .AddOptions<PhoneAuthenticationFeatureOptions>()
    .Bind(
        builder.Configuration.GetSection(
            PhoneAuthenticationFeatureOptions.SectionName));

var phoneAuthenticationEnabled =
    builder.Configuration.GetValue<bool>(
        $"{PhoneAuthenticationFeatureOptions.SectionName}:Enabled");

var phoneOtpOptions =
    builder.Services
        .AddOptions<PhoneOtpOptions>()
        .Bind(
            builder.Configuration.GetSection(
                PhoneOtpOptions.SectionName))
        .ValidateDataAnnotations();

if (phoneAuthenticationEnabled)
{
    phoneOtpOptions
        .Validate(
            options =>
                !string.IsNullOrWhiteSpace(
                    options.HashingKey) &&
                options.HashingKey.Length >= 32,
            "PhoneOtp:HashingKey must contain at least 32 characters.")
        .ValidateOnStart();

    builder.Services.AddSingleton<
        IPhoneNumberNormalizer,
        PhoneNumberNormalizer>();

    builder.Services.AddSingleton<
        IPhoneOtpSender,
        DevelopmentPhoneOtpSender>();

    builder.Services.AddScoped<
        IPhoneOtpService,
        PhoneOtpService>();

    builder.Services.AddSingleton<
        IPhoneRegistrationContinuationProtector,
        PhoneRegistrationContinuationProtector>();

    builder.Services.AddSingleton<
        IPhoneCredentialSetupContinuationProtector,
        PhoneCredentialSetupContinuationProtector>();

    builder.Services.AddSingleton<
        IPhoneChangeContinuationProtector,
        PhoneChangeContinuationProtector>();
}

builder.Services.AddHostedService<
    PhoneOtpCleanupService>();

builder.Services.AddScoped<
    IAuthorSupportService,
    AuthorSupportService>();

builder.Services.AddScoped<
    InitialCatalogImporter>();

builder.Services.AddSingleton<
    IPromptTemplateRenderer,
    PromptTemplateRenderer>();

builder.Services.AddSingleton<
    IPromptTemplateVariableDetector,
    PromptTemplateVariableDetector>();

builder.Services.AddSingleton<
    IVisualPromptTemplateCompiler,
    VisualPromptTemplateCompiler>();

builder.Services.AddSingleton<
    ISlugGenerator,
    SlugGenerator>();

builder.Services.AddScoped<IUserNotificationService, UserNotificationService>();


builder.Services.AddScoped<
    IPromptDuplicateDetectionService,
    PromptDuplicateDetectionService>();

builder.Services.AddScoped<IPromptStatsService, PromptStatsService>();


builder.Services.AddScoped<
    IPromptCatalogSearchService,
    PromptCatalogSearchService>();

builder.Services.AddSingleton<
    IPromptMetricActorKeyProvider,
    PromptMetricActorKeyProvider>();

builder.Services.AddScoped<
    IPromptMetricService,
    PromptMetricService>();

builder.Services
    .AddOptions<PromptMetricCleanupOptions>()
    .Bind(
        builder.Configuration.GetSection(
            PromptMetricCleanupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<
    PromptMetricCleanupService>();


builder.Services.AddScoped<
    IPromptCardCoverService,
    PromptCardCoverService>();



builder.Services
    .AddOptions<MediaStorageOptions>()
    .Bind(
        builder.Configuration.GetSection(
            MediaStorageOptions.SectionName))
    .Validate(
        options =>
            options.MaxImageSizeBytes > 0 &&
            options.MaxImagesPerPrompt > 0,
        "Некорректно настроены ограничения загрузки медиа.")
    .Validate(
        options =>
            options.MaxImageWidth > 0 &&
            options.MaxImageHeight > 0 &&
            options.MaxImagePixelCount > 0,
        "Некорректно настроены ограничения размеров изображения.")
    .Validate(
        options =>
            options.ThumbnailMaxWidth > 0 &&
            options.ThumbnailMaxHeight > 0 &&
            options.ThumbnailWebpQuality is >= 1 and <= 100,
        "Некорректно настроена генерация миниатюр.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.AvatarRootFolder) &&
            options.MaxAvatarImageSizeBytes > 0 &&
            options.AvatarMaxWidth > 0 &&
            options.AvatarMaxHeight > 0 &&
            options.AvatarWebpQuality is >= 1 and <= 100,
        "Некорректно настроено хранение аватаров профиля.")
    .ValidateOnStart();

// -----------------------------------------------------------------------------
// EN: One scoped local storage instance exposed through operational interfaces
// RU: Один scoped-экземпляр локального хранилища для рабочих интерфейсов
// -----------------------------------------------------------------------------

builder.Services.AddScoped<
    LocalMediaStorageService>();

builder.Services.AddScoped<
    IMediaStorageService>(
        serviceProvider =>
            serviceProvider.GetRequiredService<
                LocalMediaStorageService>());

builder.Services.AddScoped<
    ILocalMediaStorageCatalog>(
        serviceProvider =>
            serviceProvider.GetRequiredService<
                LocalMediaStorageService>());

// -----------------------------------------------------------------------------
// EN: Application clock used by time-dependent services
// RU: Системные часы для сервисов, зависящих от времени
// -----------------------------------------------------------------------------

builder.Services.AddSingleton(
    TimeProvider.System);


builder.Services.AddScoped<
    IPromptMediaCleanupService,
    PromptMediaCleanupService>();

builder.Services
    .AddOptions<PromptMediaCleanupOptions>()
    .Bind(
        builder.Configuration.GetSection(
            PromptMediaCleanupOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();



builder.Services.AddScoped<
    IPromptMediaOrphanCleanupService,
    PromptMediaOrphanCleanupService>();



builder.Services.AddScoped<
    IPromptMediaAccessService,
    PromptMediaAccessService>();



builder.Services.AddSingleton<
    IPromptMediaModerationStateService,
    PromptMediaModerationStateService>();



builder.Services.AddSingleton<
    IPromptMediaArrangementService,
    PromptMediaArrangementService>();



builder.Services.AddSingleton<
    IImageThumbnailGenerator,
    SkiaImageThumbnailGenerator>();


builder.Services.AddSingleton<
    ICookieConsentService,
    CookieConsentService>();

// -----------------------------------------------------------------------------
// EN: Anti-abuse protection for phone authentication.
// RU: Защита телефонной аутентификации от злоупотреблений.
// -----------------------------------------------------------------------------

builder.Services.AddRateLimiter(
    options =>
    {
        options.RejectionStatusCode =
            StatusCodes.Status429TooManyRequests;

        // ---------------------------------------------------------------------
        // EN: Public phone OTP request protection.
        // RU: Защита публичных запросов телефонного OTP.
        // ---------------------------------------------------------------------

        options.AddPolicy(
            "PhoneOtpRequest",
            httpContext =>
            {
                if (!HttpMethods.IsPost(
                        httpContext.Request.Method))
                {
                    return RateLimitPartition
                        .GetNoLimiter(
                            "phone-otp-non-post");
                }

                var remoteIp =
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown";

                return RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            remoteIp,

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        5,

                                    Window =
                                        TimeSpan.FromMinutes(
                                            10),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                });
            });

        // ---------------------------------------------------------------------
        // EN: Protection for sensitive phone-change verification requests.
        // RU: Защита запросов подтверждения при смене номера телефона.
        // ---------------------------------------------------------------------

        options.AddPolicy(
            "ChangePhoneVerification",
            httpContext =>
            {
                if (!HttpMethods.IsPost(
                        httpContext.Request.Method))
                {
                    return RateLimitPartition
                        .GetNoLimiter(
                            "change-phone-non-post");
                }

                var userId =
                    httpContext.User.FindFirstValue(
                        ClaimTypes.NameIdentifier)
                    ?? "anonymous";

                var remoteIp =
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown";

                var partitionKey =
                    $"{userId}:{remoteIp}";

                return RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            partitionKey,

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        5,

                                    Window =
                                        TimeSpan.FromMinutes(
                                            10),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                });
            });

        // ---------------------------------------------------------------------
        // EN: Protection for public email/password Identity endpoints.
        // RU: Защита публичных Identity-операций с email и паролем.
        // ---------------------------------------------------------------------

        options.AddPolicy(
            "IdentityPublicPost",
            httpContext =>
            {
                if (!HttpMethods.IsPost(
                        httpContext.Request.Method))
                {
                    return RateLimitPartition
                        .GetNoLimiter(
                            "identity-public-non-post");
                }

                var remoteIp =
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown";

                var path =
                    httpContext.Request.Path
                        .Value?
                        .ToLowerInvariant()
                    ?? "unknown";

                return RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            $"{remoteIp}:{path}",

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        10,

                                    Window =
                                        TimeSpan.FromMinutes(
                                            10),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                });
            });

        // ---------------------------------------------------------------------
        // EN: Public catalog search protection. Normal catalog/category/tag
        //     browsing is not limited by this policy; only requests with a
        //     non-empty search query consume permits.
        //
        // RU: Защита публичного поиска по каталогу. Обычный просмотр каталога,
        //     категорий и тегов не ограничивается — лимит применяется только
        //     когда передан непустой search-запрос.
        // ---------------------------------------------------------------------

        options.AddPolicy(
            "CatalogSearch",
            httpContext =>
            {
                var hasSearch =
                    httpContext.Request.Query
                        .TryGetValue(
                            "search",
                            out var searchValues) &&
                    searchValues.Any(value =>
                        !string.IsNullOrWhiteSpace(
                            value));

                if (!HttpMethods.IsGet(
                        httpContext.Request.Method) ||
                    !hasSearch)
                {
                    return RateLimitPartition
                        .GetNoLimiter(
                            "catalog-non-search");
                }

                var remoteIp =
                    httpContext.Connection
                        .RemoteIpAddress?
                        .ToString()
                    ?? "unknown";

                return RateLimitPartition
                    .GetFixedWindowLimiter(
                        partitionKey:
                            remoteIp,

                        factory:
                            _ =>
                                new FixedWindowRateLimiterOptions
                                {
                                    PermitLimit =
                                        30,

                                    Window =
                                        TimeSpan.FromMinutes(
                                            1),

                                    QueueLimit =
                                        0,

                                    AutoReplenishment =
                                        true
                                });
            });
    });



var app = builder.Build();



// -----------------------------------------------------------------------------
// EN: One-time initial catalog import
// RU: Одноразовый импорт стартового каталога
// -----------------------------------------------------------------------------

var initialCatalogImportRequested =
    args.Any(argument =>
        string.Equals(
            argument,
            "--import-initial-catalog",
            StringComparison.OrdinalIgnoreCase));

if (initialCatalogImportRequested)
{
    var authorEmail = args
        .FirstOrDefault(argument =>
            argument.StartsWith(
                "--author-email=",
                StringComparison.OrdinalIgnoreCase))?
        .Split('=', 2)[1]
        .Trim();

    if (string.IsNullOrWhiteSpace(authorEmail))
    {
        authorEmail =
            builder.Configuration[
                "DevelopmentAdmin:Email"];
    }

    if (string.IsNullOrWhiteSpace(authorEmail))
    {
        Console.Error.WriteLine(
            "Укажите автора: --author-email=user@example.com");

        Environment.ExitCode = 64;
        return;
    }

    using var scope =
        app.Services.CreateScope();

    var importer =
        scope.ServiceProvider
            .GetRequiredService<InitialCatalogImporter>();

    var result = await importer.ImportAsync(
        authorEmail,
        CancellationToken.None);

    if (!result.IsSuccess)
    {
        Console.Error.WriteLine(
            "Импорт стартового каталога отменён.");

        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"- {error}");
        }

        Environment.ExitCode = 2;
        return;
    }

    Console.WriteLine(
        $"Добавлено промптов: {result.ImportedCount}");

    Console.WriteLine(
        $"Пропущено существующих: {result.SkippedCount}");

    Environment.ExitCode = 0;
    return;
}


// -----------------------------------------------------------------------------
// EN: One-time command for expired prompt media cleanup
// RU: Одноразовая команда очистки просроченных медиа промптов
// -----------------------------------------------------------------------------

var promptMediaCleanupRequested =
    args.Any(argument =>
        string.Equals(
            argument,
            "--cleanup-prompt-media",
            StringComparison.OrdinalIgnoreCase));

var promptMediaOrphanCleanupRequested =
    args.Any(argument =>
        string.Equals(
            argument,
            "--cleanup-orphan-prompt-media",
            StringComparison.OrdinalIgnoreCase));

if (promptMediaCleanupRequested &&
    promptMediaOrphanCleanupRequested)
{
    Console.Error.WriteLine(
        "Нельзя одновременно запускать retention cleanup " +
        "и очистку потерянных файлов.");

    Environment.ExitCode =
        64;

    return;
}


// -----------------------------------------------------------------------------
// EN: One-time command for orphan prompt media cleanup
// RU: Одноразовая команда очистки потерянных файлов медиа
// -----------------------------------------------------------------------------

if (promptMediaOrphanCleanupRequested)
{
    var dryRun =
        args.Any(argument =>
            string.Equals(
                argument,
                "--dry-run",
                StringComparison.OrdinalIgnoreCase));

    using var scope =
        app.Services.CreateScope();

    var cleanupService =
        scope.ServiceProvider
            .GetRequiredService<
                IPromptMediaOrphanCleanupService>();

    Console.WriteLine(
        dryRun
            ? "Проверка потерянных файлов без удаления."
            : "Запущена очистка потерянных файлов медиа.");

    var result =
        await cleanupService.CleanupAsync(
            dryRun,
            CancellationToken.None);

    var eligibleMegabytes =
        result.EligibleBytes /
        1024d /
        1024d;

    var deletedMegabytes =
        result.DeletedBytes /
        1024d /
        1024d;

    Console.WriteLine();
    Console.WriteLine(
        $"Просканировано файлов: {result.ScannedFileCount}");

    Console.WriteLine(
        $"Связано с базой: {result.ReferencedFileCount}");

    Console.WriteLine(
        $"Недавних несвязанных файлов: " +
        $"{result.RecentUnreferencedFileCount}");

    Console.WriteLine(
        $"Потерянных файлов: {result.EligibleOrphanCount}");

    Console.WriteLine(
        $"Объём потерянных файлов: " +
        $"{eligibleMegabytes:0.##} МБ");

    if (!result.IsDryRun)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Обработано: {result.ProcessedCount}");

        Console.WriteLine(
            $"Удалено: {result.DeletedCount}");

        Console.WriteLine(
            $"Освобождено: {deletedMegabytes:0.##} МБ");

        Console.WriteLine(
            $"Ошибок: {result.FailedCount}");
    }

    if (result.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Ошибки:");

        foreach (var error in result.Errors)
        {
            Console.WriteLine(
                $"{error.StorageKey}: {error.Message}");
        }
    }

    Environment.ExitCode =
        result.FailedCount == 0
            ? 0
            : 2;

    return;
}

if (promptMediaCleanupRequested)
{
    var dryRun =
        args.Any(argument =>
            string.Equals(
                argument,
                "--dry-run",
                StringComparison.OrdinalIgnoreCase));

    using var scope =
        app.Services.CreateScope();

    var cleanupService =
        scope.ServiceProvider
            .GetRequiredService<
                IPromptMediaCleanupService>();

    Console.WriteLine(
        dryRun
            ? "Проверка очистки медиа без удаления файлов."
            : "Запущена очистка медиа с истёкшим сроком хранения.");

    var result =
        await cleanupService.CleanupAsync(
            dryRun,
            CancellationToken.None);

    var eligibleMegabytes =
        result.EligibleBytes /
        1024d /
        1024d;

    var purgedMegabytes =
        result.PurgedBytes /
        1024d /
        1024d;

    Console.WriteLine();
    Console.WriteLine(
        $"Доступно для очистки: {result.EligibleCount}");

    Console.WriteLine(
        $"Архивных: {result.EligibleArchivedCount}");

    Console.WriteLine(
        $"Отклонённых: {result.EligibleRejectedCount}");

    Console.WriteLine(
        $"Объём: {eligibleMegabytes:0.##} МБ");

    if (!result.IsDryRun)
    {
        Console.WriteLine();
        Console.WriteLine(
            $"Обработано: {result.ProcessedCount}");

        Console.WriteLine(
            $"Файлы удалены: {result.PurgedCount}");

        Console.WriteLine(
            $"Освобождено: {purgedMegabytes:0.##} МБ");

        Console.WriteLine(
            $"Ошибок: {result.FailedCount}");

        Console.WriteLine(
            $"Осталось: {result.RemainingEligibleCount}");
    }

    if (result.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Ошибки:");

        foreach (var error in result.Errors)
        {
            Console.WriteLine(
                $"MediaId={error.MediaId}: {error.Message}");
        }
    }

    Environment.ExitCode =
        result.FailedCount == 0
            ? 0
            : 2;

    return;
}



if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();

    using var scope =
    app.Services.CreateScope();

    await DevelopmentIdentitySeeder.SeedAsync(
    scope.ServiceProvider,
    app.Configuration);

}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseStaticFiles();

// -----------------------------------------------------------------------------
// EN: Keep phone-authentication endpoints unreachable while the feature is disabled.
// RU: Пока телефонная аутентификация выключена, её endpoints недоступны.
// -----------------------------------------------------------------------------

if (!phoneAuthenticationEnabled)
{
    var blockedPhoneAuthenticationPathPrefixes =
        new[]
        {
            "/Identity/Account/Phone",
            "/Identity/Account/Manage/LinkPhone",
            "/Identity/Account/Manage/ChangePhone",
            "/Identity/Account/Manage/AddEmailPassword"
        };

    app.Use(
        async (context, next) =>
        {
            var requestPath =
                context.Request.Path.Value
                ?? string.Empty;

            var isBlockedPhoneAuthenticationPath =
                blockedPhoneAuthenticationPathPrefixes.Any(
                    prefix =>
                        requestPath.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase));

            if (isBlockedPhoneAuthenticationPath)
            {
                context.Response.StatusCode =
                    StatusCodes.Status404NotFound;

                return;
            }

            await next();
        });
}

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
   .WithStaticAssets();

app.Run();



// -----------------------------------------------------------------------------
// EN: Exposes the application entry point to integration tests.
// RU: Открывает точку входа приложения для интеграционных тестов.
// -----------------------------------------------------------------------------

public partial class Program
{
}
