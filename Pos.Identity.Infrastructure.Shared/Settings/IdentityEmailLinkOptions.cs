namespace Pos.Identity.Infrastructure.Shared.Settings;

public class IdentityEmailLinkOptions
{
    public string ConfirmationUrl { get; set; } = string.Empty;
    public string ResetPasswordUrl { get; set; } = string.Empty;
    public string LoginUrl { get; set; } = string.Empty;
}
