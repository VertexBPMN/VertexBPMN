namespace VertexBPMN.Infrastructure.Notifications.Email;

public sealed class EmailNotificationOptions
{
    public bool Enabled { get; init; }
    public string From { get; init; } = "";
    public string SmtpHost { get; init; } = "";
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string[] DefaultRecipients { get; init; } = [];
}