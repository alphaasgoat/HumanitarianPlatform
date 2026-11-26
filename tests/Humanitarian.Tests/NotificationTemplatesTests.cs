using FluentAssertions;
using Humanitarian.Application.Notifications;
using Humanitarian.Domain.Enums;
using Xunit;

namespace Humanitarian.Tests;

public class NotificationTemplatesTests
{
    private static readonly Dictionary<string, string> Args = new()
    {
        ["ref"] = "ab12cd34", ["people"] = "4", ["shelter"] = "Temple hall", ["address"] = "Kelaniya",
        ["task"] = "Deliver food", ["start"] = "2026-11-25 09:00 UTC", ["end"] = "2026-11-25 13:00 UTC"
    };

    [Theory]
    [InlineData("en")]
    [InlineData("si")]
    [InlineData("ta")]
    public void EveryTypeHasATemplate_InEveryLanguage_WithAllPlaceholdersFilled(string language)
    {
        foreach (var type in Enum.GetValues<NotificationType>().Where(NotificationTemplates.HasTemplate))
        {
            var (title, body) = NotificationTemplates.Render(type, language, Args);

            title.Should().NotBeNullOrWhiteSpace();
            body.Should().NotBeNullOrWhiteSpace();
            (title + body).Should().NotContain("{", $"{type}/{language} has an unfilled placeholder");
        }
    }

    [Fact]
    public void ARequestMessage_ContainsTheReference()
    {
        var (_, body) = NotificationTemplates.Render(NotificationType.RequestTriaged, "en", Args);
        body.Should().Contain("ab12cd34");
    }

    [Fact]
    public void ShelterMessage_ContainsPeopleShelterAndAddress()
    {
        var (_, body) = NotificationTemplates.Render(NotificationType.ShelterPlacement, "en", Args);
        body.Should().Contain("4").And.Contain("Temple hall").And.Contain("Kelaniya");
    }

    [Theory]
    [InlineData("si-LK", "si")]
    [InlineData("TA", "ta")]
    [InlineData("fr", "en")]
    [InlineData("", "en")]
    [InlineData(null, "en")]
    public void LanguageIsNormalised_AndUnknownLanguagesFallBackToEnglish(string? input, string expected) =>
        NotificationTemplates.NormalizeLanguage(input).Should().Be(expected);

    [Fact]
    public void Sinhala_DiffersFromEnglish()
    {
        var en = NotificationTemplates.Render(NotificationType.RequestReceived, "en", Args);
        var si = NotificationTemplates.Render(NotificationType.RequestReceived, "si", Args);
        si.Title.Should().NotBe(en.Title);
    }

    [Fact]
    public void AnnouncementsAreNotTemplated() =>
        NotificationTemplates.HasTemplate(NotificationType.Announcement).Should().BeFalse();
}
