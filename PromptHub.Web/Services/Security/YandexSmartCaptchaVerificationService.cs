using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PromptHub.Web.Services.Security;

public sealed class YandexSmartCaptchaVerificationService
    : ISmartCaptchaVerificationService
{
    private readonly HttpClient
        _httpClient;

    private readonly SmartCaptchaOptions
        _options;

    private readonly ILogger<YandexSmartCaptchaVerificationService>
        _logger;

    public YandexSmartCaptchaVerificationService(
        HttpClient httpClient,
        IOptions<SmartCaptchaOptions> options,
        ILogger<YandexSmartCaptchaVerificationService> logger)
    {
        _httpClient =
            httpClient;

        _options =
            options.Value;

        _logger =
            logger;

        _httpClient.Timeout =
            TimeSpan.FromSeconds(
                _options.RequestTimeoutSeconds);
    }

    public async Task<bool> VerifyAsync(
        string? token,
        IPAddress? remoteIpAddress,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(
                token))
        {
            return false;
        }

        var formValues =
            new Dictionary<string, string>
            {
                ["secret"] =
                    _options.ServerKey,

                ["token"] =
                    token
            };

        if (remoteIpAddress is not null)
        {
            formValues["ip"] =
                remoteIpAddress.ToString();
        }

        try
        {
            using var content =
                new FormUrlEncodedContent(
                    formValues);

            using var response =
                await _httpClient.PostAsync(
                    _options.ValidationEndpoint,
                    content,
                    cancellationToken);

            // -----------------------------------------------------------------
            // EN: Yandex recommends fail-open handling for service HTTP errors
            //     so an external CAPTCHA outage does not lock out real users.
            //     Local rate limiting and Identity lockout remain active.
            // RU: Yandex рекомендует пропускать запрос при HTTP-сбое сервиса,
            //     чтобы внешняя недоступность CAPTCHA не блокировала реальных
            //     пользователей. Локальные rate limit и lockout продолжают
            //     работать.
            // -----------------------------------------------------------------

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SmartCaptcha validation returned HTTP {StatusCode}; request is allowed in fail-open mode.",
                    (int)response.StatusCode);

                return true;
            }

            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            SmartCaptchaResponse? result;

            try
            {
                result =
                    JsonSerializer.Deserialize<SmartCaptchaResponse>(
                        responseBody);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(
                    exception,
                    "SmartCaptcha returned an unreadable successful HTTP response; verification is rejected.");

                return false;
            }

            if (result is null)
            {
                _logger.LogWarning(
                    "SmartCaptcha returned an empty successful HTTP response; verification is rejected.");

                return false;
            }

            return string.Equals(
                result.Status,
                "ok",
                StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "SmartCaptcha validation timed out; request is allowed in fail-open mode.");

            return true;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(
                exception,
                "SmartCaptcha validation request failed; request is allowed in fail-open mode.");

            return true;
        }
    }

    private sealed class SmartCaptchaResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; }
            = string.Empty;
    }
}
