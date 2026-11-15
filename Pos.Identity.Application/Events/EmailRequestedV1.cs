namespace Pos.Identity.Application.Events;

/// <summary>A durable request to NotificationService. Retries retain EventId; a deliberate resend gets a new ID.</summary>
public record EmailRequestedV1(
    Guid EventId,
    Guid? TenantId,
    string? RecipientUserId,
    string RecipientEmail,
    string TemplateCode,
    string Language,
    Dictionary<string, string> TemplateValues,
    DateTime OccurredAt,
    DateTime? ExpiresAt);

public static class EmailRequestedV1Contract
{
    public const string EventType = nameof(EmailRequestedV1);
    public const string RoutingKey = "identity.email.requested.v1";
}
