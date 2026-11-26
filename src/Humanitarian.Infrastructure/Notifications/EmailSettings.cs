namespace Humanitarian.Infrastructure.Notifications;

/// <summary>Bound from the "Email" section of appsettings. Keep the password in user-secrets, never in source control.</summary>
public class EmailSettings
{
    public const string Section = "Email";

    /// <summary>Empty = no SMTP server configured: messages are written to the log instead of being sent.</summary>
    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "no-reply@humanitarian.local";
    public string FromName { get; set; } = "Humanitarian Platform";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
