using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace PromptHub.Web.Services.Email;

public sealed class UnisenderGoEmailClient
    : ITransactionalEmailClient
{
    private readonly HttpClient
        _httpClient;

    private readonly UnisenderGoOptions
        _options;

    private readonly ILogger<UnisenderGoEmailClient>
        _logger;

    public UnisenderGoEmailClient(
        HttpClient httpClient,
        IOptions<UnisenderGoOptions> options,
        ILogger<UnisenderGoEmailClient> logger)
    {
        _httpClient =
            httpClient;

        _options =
            options.Value;

        _logger =
            logger;

        var normalizedBaseUrl =
            _options.BaseUrl.EndsWith(
                "/",
                StringComparison.Ordinal)
                ? _options.BaseUrl
                : _options.BaseUrl + "/";

        _httpClient.BaseAddress =
            new Uri(
                normalizedBaseUrl,
                UriKind.Absolute);

        _httpClient.Timeout =
            TimeSpan.FromSeconds(
                _options.RequestTimeoutSeconds);
    }

    public async Task SendAsync(
        TransactionalEmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.RecipientEmail);

        ArgumentException.ThrowIfNullOrWhiteSpace(
            message.Subject);

        var requestPayload =
            new EmailSendRequest(
                new EmailSendMessage
                {
                    Recipients =
                    [
                        new EmailRecipient
                        {
                            Email =
                                message.RecipientEmail
                        }
                    ],

                    Tags =
                        message.Tags?
                            .Where(tag =>
                                !string.IsNullOrWhiteSpace(
                                    tag))
                            .Distinct(
                                StringComparer.OrdinalIgnoreCase)
                            .Take(4)
                            .ToArray(),

                    Body =
                        new EmailBody
                        {
                            Html =
                                message.HtmlBody,

                            PlainText =
                                message.PlainTextBody
                        },

                    Subject =
                        message.Subject,

                    FromEmail =
                        _options.FromEmail,

                    FromName =
                        _options.FromName,

                    GlobalLanguage =
                        "ru",

                    TemplateEngine =
                        "none",


                    TrackLinks =
                        _options.DisableTracking
                            ? 0
                            : null,

                    TrackRead =
                        _options.DisableTracking
                            ? 0
                            : null,

                    SkipUnsubscribe =
                        _options.SkipUnsubscribeBlock
                            ? 1
                            : null
                });

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                "email/send.json");

        request.Headers.TryAddWithoutValidation(
            "X-API-KEY",
            _options.ApiKey);

        request.Content =
            JsonContent.Create(
                requestPayload);

        using var response =
            await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

        var responseBody =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var apiError =
                TryReadApiError(
                    responseBody);

            _logger.LogError(
                "Unisender Go rejected transactional email. HTTP={StatusCode}, ApiCode={ApiCode}.",
                (int)response.StatusCode,
                apiError.Code);

            throw new InvalidOperationException(
                "Сервис отправки электронной почты временно недоступен.");
        }

        EmailSendResponse? result;

        try
        {
            result =
                JsonSerializer.Deserialize<EmailSendResponse>(
                    responseBody);
        }
        catch (JsonException exception)
        {
            _logger.LogError(
                exception,
                "Unisender Go returned an unreadable response.");

            throw new InvalidOperationException(
                "Сервис отправки электронной почты вернул некорректный ответ.",
                exception);
        }

        if (result is null ||
            !string.Equals(
                result.Status,
                "success",
                StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError(
                "Unisender Go returned a non-success response for transactional email.");

            throw new InvalidOperationException(
                "Сервис отправки электронной почты не принял письмо.");
        }

        if (result.FailedEmails is { Count: > 0 })
        {
            var failureReason =
                result.FailedEmails.Values
                    .FirstOrDefault()
                ?? "unknown";

            _logger.LogWarning(
                "Unisender Go did not accept the transactional recipient. Reason={Reason}.",
                failureReason);

            throw new InvalidOperationException(
                "Сервис отправки электронной почты не принял адрес получателя.");
        }
    }

    private static ApiErrorResponse TryReadApiError(
        string responseBody)
    {
        try
        {
            return JsonSerializer.Deserialize<ApiErrorResponse>(
                       responseBody)
                   ?? new ApiErrorResponse();
        }
        catch (JsonException)
        {
            return new ApiErrorResponse();
        }
    }

    private sealed record EmailSendRequest(
        [property: JsonPropertyName("message")]
        EmailSendMessage Message);

    private sealed class EmailSendMessage
    {
        [JsonPropertyName("recipients")]
        public EmailRecipient[] Recipients { get; init; }
            = [];

        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? Tags { get; init; }

        [JsonPropertyName("body")]
        public EmailBody Body { get; init; }
            = new();

        [JsonPropertyName("subject")]
        public string Subject { get; init; }
            = string.Empty;

        [JsonPropertyName("from_email")]
        public string FromEmail { get; init; }
            = string.Empty;

        [JsonPropertyName("from_name")]
        public string FromName { get; init; }
            = string.Empty;

        [JsonPropertyName("global_language")]
        public string GlobalLanguage { get; init; }
            = "ru";

        [JsonPropertyName("template_engine")]
        public string TemplateEngine { get; init; }
            = "none";


        [JsonPropertyName("track_links")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TrackLinks { get; init; }

        [JsonPropertyName("track_read")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TrackRead { get; init; }

        [JsonPropertyName("skip_unsubscribe")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SkipUnsubscribe { get; init; }
    }

    private sealed class EmailRecipient
    {
        [JsonPropertyName("email")]
        public string Email { get; init; }
            = string.Empty;
    }

    private sealed class EmailBody
    {
        [JsonPropertyName("html")]
        public string Html { get; init; }
            = string.Empty;

        [JsonPropertyName("plaintext")]
        public string PlainText { get; init; }
            = string.Empty;
    }

    private sealed class EmailSendResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; init; }
            = string.Empty;

        [JsonPropertyName("job_id")]
        public string JobId { get; init; }
            = string.Empty;

        [JsonPropertyName("failed_emails")]
        public Dictionary<string, string>? FailedEmails { get; init; }
    }

    private sealed class ApiErrorResponse
    {
        [JsonPropertyName("code")]
        public int? Code { get; init; }
    }
}
