namespace Pos.Identity.Application.Dtos;

public record CachedTenantAccess(
    bool IsAllowed,
    DateTime? SubscriptionEndsAt,
    DateTime ValidUntilUtc);
