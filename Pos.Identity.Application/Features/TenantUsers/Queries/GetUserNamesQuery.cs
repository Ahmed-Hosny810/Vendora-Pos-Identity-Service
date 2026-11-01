using MediatR;
using FluentValidation;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Application.Wrappers;

namespace Pos.Identity.Application.Features.TenantUsers.Queries;

public class GetUserNamesQuery : IRequest<Response<IReadOnlyDictionary<Guid, string>>>
{
    public List<Guid> UserIds { get; set; } = new();
}

public class GetUserNamesQueryHandler(IUserNameLookupService service)
    : IRequestHandler<GetUserNamesQuery, Response<IReadOnlyDictionary<Guid, string>>>
{
    public async Task<Response<IReadOnlyDictionary<Guid, string>>> Handle(
        GetUserNamesQuery request, CancellationToken cancellationToken)
    {
        return new Response<IReadOnlyDictionary<Guid, string>>(
            await service.GetNamesAsync(request.UserIds, cancellationToken));
    }
}