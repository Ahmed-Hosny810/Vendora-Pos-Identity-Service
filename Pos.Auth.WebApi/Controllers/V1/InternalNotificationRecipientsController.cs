using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pos.Identity.Application.Features.NotificationRecipients;
using Pos.Identity.Application.Wrappers;

namespace Pos.Auth.WebApi.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/notification-recipients")]
[Authorize(Policy = "NotificationRecipients.Read")]
public class InternalNotificationRecipientsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Response<IReadOnlyList<NotificationRecipientDto>>>> GetRecipients(
        [FromBody] GetNotificationRecipientsQuery query, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(query, cancellationToken));
    }
}
