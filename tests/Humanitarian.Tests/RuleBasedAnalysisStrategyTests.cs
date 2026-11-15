using FluentAssertions;
using Humanitarian.Application.Requests.Ai;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class RuleBasedAnalysisStrategyTests
{
    private readonly RuleBasedAnalysisStrategy _sut = new();

    [Fact]
    public async Task LifeThreateningText_IsRecommendedAsCritical_WithExplainableRationale()
    {
        var input = new AnalysisInput(
            "Our family is trapped on the roof and the water is rising. We have a baby.",
            RequestCategory.Shelter, UrgencyLevel.High, 5, true);

        var result = await _sut.AnalyzeAsync(input);

        result.SuggestedCategory.Should().Be(RequestCategory.Rescue);
        result.SuggestedUrgency.Should().Be(UrgencyLevel.Critical);
        result.PriorityScore.Should().BeGreaterOrEqualTo(85);
        result.Rationale.Should().Contain("trapped");
        result.UsedFallback.Should().BeFalse();
    }

    [Fact]
    public async Task MedicalText_IsClassifiedAsMedicine_AndKeepsLowUrgency()
    {
        var input = new AnalysisInput("I need insulin for my father.", RequestCategory.Other, UrgencyLevel.Low, 1, true);

        var result = await _sut.AnalyzeAsync(input);

        result.SuggestedCategory.Should().Be(RequestCategory.Medicine);
        result.SuggestedUrgency.Should().Be(UrgencyLevel.Low);
    }

    [Fact]
    public async Task NoKeywords_KeepsDeclaredCategory_WithLowConfidence()
    {
        var input = new AnalysisInput("Hello we need support at our place", RequestCategory.Food, UrgencyLevel.Medium, 2, false);

        var result = await _sut.AnalyzeAsync(input);

        result.SuggestedCategory.Should().Be(RequestCategory.Food);
        result.Confidence.Should().BeLessOrEqualTo(0.5);
        result.Rationale.Should().Contain("No location");
    }

    [Theory]
    [InlineData("trapped drowning bleeding unconscious buried", UrgencyLevel.Critical, 500)]
    [InlineData("hello", UrgencyLevel.Low, 1)]
    public async Task ScoreAndConfidence_AlwaysStayInValidRange(string text, UrgencyLevel urgency, int people)
    {
        var result = await _sut.AnalyzeAsync(new AnalysisInput(text, RequestCategory.Other, urgency, people, true));

        result.PriorityScore.Should().BeInRange(0, 100);
        result.Confidence.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task SameInput_GivesSameOutput()
    {
        var input = new AnalysisInput("Need food and drinking water", RequestCategory.Food, UrgencyLevel.Medium, 6, true);

        var a = await _sut.AnalyzeAsync(input);
        var b = await _sut.AnalyzeAsync(input);

        a.Should().Be(b);
    }
}
