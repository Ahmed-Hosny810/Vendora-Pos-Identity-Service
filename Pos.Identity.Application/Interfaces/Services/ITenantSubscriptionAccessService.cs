using Pos.Identity.Application.Dtos;

namespace Pos.Identity.Application.Interfaces.Services;

public interface ITenantSubscriptionAccessService
{
    Task<CachedTenantAccess> GetAsync(Guid tenantId, CancellationToken cancellationToken);
}
