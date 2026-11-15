using FluentAssertions;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class ResilientRequestAnalyzerTests
{
    private static readonly AnalysisInput Input =
        new("We need food", RequestCategory.Food, UrgencyLevel.Medium, 3, true);

    private class ThrowingStrategy : IAnalysisStrategy
    {
        public string Name => "external-ai";
        public Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default) =>
            throw new HttpRequestException("service unavailable");
    }

    private class SlowStrategy : IAnalysisStrategy
    {
        public string Name => "slow-ai";
        public async Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), ct);
            throw new InvalidOperationException("never reached");
        }
    }

    private class GoodStrategy : IAnalysisStrategy
    {
        public string Name => "good";
        public Task<AnalysisResult> AnalyzeAsync(AnalysisInput input, CancellationToken ct = default) =>
            Task.FromResult(new AnalysisResult(RequestCategory.Food, UrgencyLevel.Medium, 40, 0.8, "ok", "good"));
    }

    [Fact]
    public async Task FirstStrategySucceeds_NoFallbackFlag()
    {
        var sut = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new GoodStrategy() });

        var result = await sut.AnalyzeAsync(Input);

        result.UsedFallback.Should().BeFalse();
        result.ModelName.Should().Be("good");
    }

    [Fact]
    public async Task FirstStrategyThrows_NextStrategyIsUsed_AndFailureIsRecorded()
    {
        var sut = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new ThrowingStrategy(), new GoodStrategy() });

        var result = await sut.AnalyzeAsync(Input);

        result.UsedFallback.Should().BeTrue();
        result.ModelName.Should().Be("good");
        result.Rationale.Should().Contain("external-ai failed");
    }

    [Fact]
    public async Task SlowStrategy_TimesOut_AndFallbackIsUsed()
    {
        var sut = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new SlowStrategy(), new GoodStrategy() })
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        var result = await sut.AnalyzeAsync(Input);

        result.UsedFallback.Should().BeTrue();
        result.Rationale.Should().Contain("slow-ai failed");
    }

    [Fact]
    public async Task AllStrategiesFail_ReturnsManualReviewResult_WithZeroConfidence()
    {
        var sut = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new ThrowingStrategy() });

        var result = await sut.AnalyzeAsync(Input);

        result.UsedFallback.Should().BeTrue();
        result.Confidence.Should().Be(0.0);
        result.ModelName.Should().Be("manual-fallback");
        result.SuggestedCategory.Should().Be(Input.DeclaredCategory);
        result.Rationale.Should().Contain("Manual review required");
    }

    [Fact]
    public async Task CallerCancellation_IsNotSwallowed()
    {
        var sut = new ResilientRequestAnalyzer(new IAnalysisStrategy[] { new SlowStrategy(), new GoodStrategy() });
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var act = () => sut.AnalyzeAsync(Input, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
