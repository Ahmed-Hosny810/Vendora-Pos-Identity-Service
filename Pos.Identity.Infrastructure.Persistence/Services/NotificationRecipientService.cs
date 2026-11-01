using Microsoft.EntityFrameworkCore;
using Pos.Identity.Application.Features.NotificationRecipients;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Domain.Constants;
using Pos.Identity.Infrastructure.Persistence.Context;

namespace Pos.Identity.Infrastructure.Persistence.Services;

public class NotificationRecipientService(ApplicationDbContext context) : INotificationRecipientService
{
    public async Task<IReadOnlyList<NotificationRecipientDto>> GetRecipientsAsync(
        Guid tenantId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var roleNames = roles.Distinct().ToArray();

        var users = await context.Users
            .AsNoTracking()
            .Where(user => user.TenantId == tenantId && user.IsActive && user.UserType == UserTypes.Tenant &&
                context.UserRoles.Any(membership => membership.UserId == user.Id &&
                    context.Roles.Any(role => role.Id == membership.RoleId && roleNames.Contains(role.Name!))))
            .OrderBy(user => user.Id)
            .Select(user => new { user.Id, user.FullName, user.Email, user.EmailConfirmed,
                user.PhoneNumber, user.PhoneNumberConfirmed })
            .ToListAsync(cancellationToken);

        return users.Select(user => new NotificationRecipientDto
        {
            UserId = Guid.Parse(user.Id),
            FullName = user.FullName,
            Email = user.EmailConfirmed ? user.Email : null,
            PhoneNumber = user.PhoneNumberConfirmed ? user.PhoneNumber : null
        }).ToList();
    }
}
