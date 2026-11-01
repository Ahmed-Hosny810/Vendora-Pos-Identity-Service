using MediatR;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Application.Wrappers;

namespace Pos.Identity.Application.Features.NotificationRecipients;

public class GetNotificationRecipientsQuery : IRequest<Response<IReadOnlyList<NotificationRecipientDto>>>
{
    public Guid TenantId { get; set; }
    public Guid? BranchId { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class GetNotificationRecipientsQueryHandler(INotificationRecipientService service)
    : IRequestHandler<GetNotificationRecipientsQuery, Response<IReadOnlyList<NotificationRecipientDto>>>
{
    public async Task<Response<IReadOnlyList<NotificationRecipientDto>>> Handle(
        GetNotificationRecipientsQuery request, CancellationToken cancellationToken)
    {
        return new Response<IReadOnlyList<NotificationRecipientDto>>(
            await service.GetRecipientsAsync(request.TenantId,
                request.Roles.Distinct(StringComparer.Ordinal).ToArray(), cancellationToken));
    }
}
