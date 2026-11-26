using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Notifications;

/// <summary>
/// Message text per notification type and language (English, Sinhala, Tamil). A person always gets the text in their
/// preferred language; any other language falls back to English. Placeholders look like {ref} and are filled in by
/// <see cref="Render"/>. Announcements are written by a coordinator and are not templated.
/// </summary>
public static class NotificationTemplates
{
    public static readonly IReadOnlyList<string> SupportedLanguages = new[] { "en", "si", "ta" };

    private static readonly Dictionary<(NotificationType, string), (string Title, string Body)> Catalog = new()
    {
        [(NotificationType.RequestReceived, "en")] = ("We received your request",
            "Your request (ref {ref}) was received. A case worker will review it. You can follow its status in the app."),
        [(NotificationType.RequestTriaged, "en")] = ("Your request was reviewed",
            "A case worker reviewed your request (ref {ref}) and help is being arranged."),
        [(NotificationType.RequestEscalated, "en")] = ("Your request needs senior attention",
            "Your request (ref {ref}) was passed to a senior coordinator. We will update you."),
        [(NotificationType.RequestMerged, "en")] = ("Your request was combined with another",
            "Your request (ref {ref}) covers the same need as another request, so they were combined. You do not need to do anything."),
        [(NotificationType.RequestClosed, "en")] = ("Your request was closed",
            "Your request (ref {ref}) is closed. If you still need help, please submit a new request."),
        [(NotificationType.ShelterPlacement, "en")] = ("A shelter place was arranged",
            "{people} people were placed at {shelter}, {address}. Please go there as soon as it is safe."),
        [(NotificationType.VolunteerAssignment, "en")] = ("New volunteer task",
            "You were assigned the task \"{task}\" from {start} to {end}. Please accept or decline it in the app."),

        [(NotificationType.RequestReceived, "si")] = ("ඔබගේ ඉල්ලීම ලැබුණා",
            "ඔබගේ ඉල්ලීම (යොමුව {ref}) අපට ලැබුණා. කළමනාකරුවෙකු එය පරීක්ෂා කරනු ඇත. යෙදුමෙන් එහි තත්ත්වය බලාගත හැකිය."),
        [(NotificationType.RequestTriaged, "si")] = ("ඔබගේ ඉල්ලීම පරීක්ෂා කළා",
            "ඔබගේ ඉල්ලීම (යොමුව {ref}) කළමනාකරුවෙකු පරීක්ෂා කර අනුමත කළා. උදව් සැලසුම් කරමින් පවතී."),
        [(NotificationType.RequestEscalated, "si")] = ("ඔබගේ ඉල්ලීම ජ්‍යෙෂ්ඨ අවධානයට යොමු කළා",
            "ඔබගේ ඉල්ලීම (යොමුව {ref}) ජ්‍යෙෂ්ඨ සම්බන්ධීකාරකවරයෙකුට යොමු කළා. අපි ඔබට දැනුම් දෙන්නෙමු."),
        [(NotificationType.RequestMerged, "si")] = ("ඔබගේ ඉල්ලීම වෙනත් ඉල්ලීමක් සමඟ ඒකාබද්ධ කළා",
            "ඔබගේ ඉල්ලීම (යොමුව {ref}) එම අවශ්‍යතාවයටම අදාළ වෙනත් ඉල්ලීමක් සමඟ ඒකාබද්ධ කළා. ඔබට කිසිවක් කිරීමට අවශ්‍ය නැත."),
        [(NotificationType.RequestClosed, "si")] = ("ඔබගේ ඉල්ලීම වසා දැමුවා",
            "ඔබගේ ඉල්ලීම (යොමුව {ref}) වසා දැමුවා. ඔබට තවමත් උදව් අවශ්‍ය නම් කරුණාකර අලුත් ඉල්ලීමක් ඉදිරිපත් කරන්න."),
        [(NotificationType.ShelterPlacement, "si")] = ("නවාතැනක් වෙන් කළා",
            "පුද්ගලයින් {people} දෙනෙකු {shelter} ({address}) වෙත යොමු කළා. ආරක්ෂිත වූ විගස එහි යන්න."),
        [(NotificationType.VolunteerAssignment, "si")] = ("නව ස්වේච්ඡා කාර්යයක්",
            "\"{task}\" කාර්යය ඔබට පවරා ඇත ({start} සිට {end} දක්වා). කරුණාකර යෙදුමෙන් පිළිගන්න හෝ ප්‍රතික්ෂේප කරන්න."),

        [(NotificationType.RequestReceived, "ta")] = ("உங்கள் கோரிக்கை பெறப்பட்டது",
            "உங்கள் கோரிக்கை (குறிப்பு {ref}) எங்களுக்குக் கிடைத்தது. ஒரு பணியாளர் அதைப் பரிசீலிப்பார். செயலியில் அதன் நிலையைப் பார்க்கலாம்."),
        [(NotificationType.RequestTriaged, "ta")] = ("உங்கள் கோரிக்கை பரிசீலிக்கப்பட்டது",
            "உங்கள் கோரிக்கை (குறிப்பு {ref}) பரிசீலிக்கப்பட்டு ஏற்றுக்கொள்ளப்பட்டது. உதவி ஏற்பாடு செய்யப்படுகிறது."),
        [(NotificationType.RequestEscalated, "ta")] = ("உங்கள் கோரிக்கை மூத்த கவனத்திற்கு அனுப்பப்பட்டது",
            "உங்கள் கோரிக்கை (குறிப்பு {ref}) மூத்த ஒருங்கிணைப்பாளருக்கு அனுப்பப்பட்டது. நாங்கள் உங்களுக்குத் தெரிவிப்போம்."),
        [(NotificationType.RequestMerged, "ta")] = ("உங்கள் கோரிக்கை மற்றொன்றுடன் இணைக்கப்பட்டது",
            "உங்கள் கோரிக்கை (குறிப்பு {ref}) அதே தேவைக்கான மற்றொரு கோரிக்கையுடன் இணைக்கப்பட்டது. நீங்கள் எதுவும் செய்ய வேண்டியதில்லை."),
        [(NotificationType.RequestClosed, "ta")] = ("உங்கள் கோரிக்கை மூடப்பட்டது",
            "உங்கள் கோரிக்கை (குறிப்பு {ref}) மூடப்பட்டது. இன்னும் உதவி தேவைப்பட்டால் புதிய கோரிக்கையைச் சமர்ப்பிக்கவும்."),
        [(NotificationType.ShelterPlacement, "ta")] = ("தங்குமிடம் ஏற்பாடு செய்யப்பட்டது",
            "{people} பேர் {shelter} ({address}) இடத்தில் தங்க ஏற்பாடு செய்யப்பட்டுள்ளது. பாதுகாப்பாக இருக்கும்போது அங்கு செல்லவும்."),
        [(NotificationType.VolunteerAssignment, "ta")] = ("புதிய தன்னார்வப் பணி",
            "\"{task}\" பணி உங்களுக்கு ஒதுக்கப்பட்டுள்ளது ({start} முதல் {end} வரை). செயலியில் ஏற்கவும் அல்லது நிராகரிக்கவும்.")
    };

    /// <summary>"si-LK" and "SI" both become "si"; anything unsupported becomes "en".</summary>
    public static string NormalizeLanguage(string? language)
    {
        var code = (language ?? "").Trim().ToLowerInvariant();
        if (code.Length > 2) code = code[..2];
        return SupportedLanguages.Contains(code) ? code : "en";
    }

    public static (string Title, string Body) Render(
        NotificationType type, string? language, IReadOnlyDictionary<string, string> args)
    {
        var lang = NormalizeLanguage(language);
        if (!Catalog.TryGetValue((type, lang), out var template) && !Catalog.TryGetValue((type, "en"), out template))
            throw new InvalidOperationException($"No template for {type}.");

        return (Fill(template.Title, args), Fill(template.Body, args));
    }

    public static bool HasTemplate(NotificationType type) => Catalog.ContainsKey((type, "en"));

    private static string Fill(string text, IReadOnlyDictionary<string, string> args)
    {
        foreach (var (key, value) in args) text = text.Replace("{" + key + "}", value);
        return text;
    }
}
