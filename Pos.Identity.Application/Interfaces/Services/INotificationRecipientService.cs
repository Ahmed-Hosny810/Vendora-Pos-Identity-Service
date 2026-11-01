using Pos.Identity.Application.Features.NotificationRecipients;

namespace Pos.Identity.Application.Interfaces.Services;

public interface INotificationRecipientService
{
    Task<IReadOnlyList<NotificationRecipientDto>> GetRecipientsAsync(
        Guid tenantId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);
}
