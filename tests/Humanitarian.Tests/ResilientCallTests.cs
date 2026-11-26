using FluentAssertions;
using Humanitarian.Application.Common;
using Xunit;

namespace Humanitarian.Tests;

public class ResilientCallTests
{
    private static Task NoDelay(TimeSpan _, CancellationToken __) => Task.CompletedTask;

    [Fact]
    public async Task Retries_UntilTheCallSucceeds()
    {
        var calls = 0;
        var result = await ResilientCall.ExecuteAsync(
            _ => ++calls < 3 ? throw new InvalidOperationException("temporary") : Task.FromResult("ok"),
            maxAttempts: 3, timeout: TimeSpan.FromSeconds(1), baseDelay: TimeSpan.Zero, delay: NoDelay);

        result.Should().Be("ok");
        calls.Should().Be(3);
    }

    [Fact]
    public async Task ThrowsExternalServiceException_WhenEveryAttemptFails()
    {
        var calls = 0;
        Func<Task> act = () => ResilientCall.ExecuteAsync<string>(
            _ => { calls++; throw new InvalidOperationException("down"); },
            maxAttempts: 2, timeout: TimeSpan.FromSeconds(1), baseDelay: TimeSpan.Zero, delay: NoDelay);

        var ex = await act.Should().ThrowAsync<ExternalServiceException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task BackOffDoublesBetweenAttempts()
    {
        var delays = new List<TimeSpan>();
        Func<Task> act = () => ResilientCall.ExecuteAsync<string>(
            _ => throw new InvalidOperationException("down"),
            maxAttempts: 3, timeout: TimeSpan.FromSeconds(1), baseDelay: TimeSpan.FromMilliseconds(100),
            delay: (d, _) => { delays.Add(d); return Task.CompletedTask; });

        await act.Should().ThrowAsync<ExternalServiceException>();
        delays.Should().Equal(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public async Task AnAttemptThatTakesTooLong_IsTimedOutAndCountsAsAFailure()
    {
        Func<Task> act = () => ResilientCall.ExecuteAsync<string>(
            async token => { await Task.Delay(TimeSpan.FromSeconds(5), token); return "late"; },
            maxAttempts: 1, timeout: TimeSpan.FromMilliseconds(50), baseDelay: TimeSpan.Zero, delay: NoDelay);

        await act.Should().ThrowAsync<ExternalServiceException>();
    }

    [Fact]
    public async Task CallerCancellation_IsNotHiddenAsAServiceFailure()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => ResilientCall.ExecuteAsync<string>(
            token => { token.ThrowIfCancellationRequested(); return Task.FromResult("x"); },
            maxAttempts: 3, timeout: TimeSpan.FromSeconds(1), baseDelay: TimeSpan.Zero, ct: cts.Token, delay: NoDelay);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
