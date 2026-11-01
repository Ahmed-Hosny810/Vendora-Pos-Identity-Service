using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pos.Identity.Application.Common.Security;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Infrastructure.Persistence.Context;

namespace Pos.Identity.Infrastructure.Persistence.Services;

public class UserNameLookupService(
    ApplicationDbContext context, IHttpContextAccessor httpContext) : IUserNameLookupService
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var tenantClaim = httpContext.HttpContext?.User.FindFirst(CustomClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty)
            throw new UnauthorizedAccessException("A valid tenant is required.");

        var ids = userIds.Distinct().Select(id => id.ToString()).ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, string>();

        // Inactive employees remain visible in historical reports.
        var users = await context.Users.AsNoTracking()
            .Where(user => user.TenantId == tenantId && ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => Guid.Parse(user.Id), user => user.FullName);
    }
}