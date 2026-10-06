using System.Collections.Concurrent;
using PromptHub.Web.Services.Identity;

namespace PromptHub.Tests.Infrastructure.Identity;

public sealed class TestPhoneOtpSender
    : IPhoneOtpSender
{
    private readonly ConcurrentDictionary<string, string>
        _latestCodes =
            new();

    private int
        _sendCount;

    public int SendCount =>
        Volatile.Read(
            ref _sendCount);

    public Task SendAsync(
        string phoneNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        _latestCodes[phoneNumber] =
            code;

        Interlocked.Increment(
            ref _sendCount);

        return Task.CompletedTask;
    }

    public string GetLatestCode(
        string phoneNumber)
    {
        if (_latestCodes.TryGetValue(
                phoneNumber,
                out var code))
        {
            return code;
        }

        throw new InvalidOperationException(
            $"OTP for phone {phoneNumber} was not captured.");
    }

    public void Reset()
    {
        _latestCodes.Clear();

        Interlocked.Exchange(
            ref _sendCount,
            0);
    }
}