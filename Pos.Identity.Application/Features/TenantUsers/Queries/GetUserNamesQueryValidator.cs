using FluentValidation;

namespace Pos.Identity.Application.Features.TenantUsers.Queries;

public class GetUserNamesQueryValidator : AbstractValidator<GetUserNamesQuery>
{
    public GetUserNamesQueryValidator()
    {
        RuleFor(x => x.UserIds).NotNull().Must(ids => ids != null && ids.Count <= 500)
            .WithMessage("Request at most 500 user IDs.");
        RuleForEach(x => x.UserIds).NotEmpty();
    }
}