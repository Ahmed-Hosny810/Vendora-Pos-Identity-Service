namespace Pos.Auth.WebApi.Authentication;

public class NotificationApiKeyOptions
{
    public const string SectionName = "NotificationServiceAuthentication";
    public string ApiKey { get; set; } = string.Empty;
}
