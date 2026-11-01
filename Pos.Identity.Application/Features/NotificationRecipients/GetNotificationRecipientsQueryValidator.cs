using FluentValidation;
using Pos.Identity.Domain.Constants;

namespace Pos.Identity.Application.Features.NotificationRecipients;

public class GetNotificationRecipientsQueryValidator : AbstractValidator<GetNotificationRecipientsQuery>
{
    public GetNotificationRecipientsQueryValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.BranchId).Must(id => !id.HasValue || id.Value != Guid.Empty);
        RuleFor(x => x.Roles).NotEmpty();
        RuleForEach(x => x.Roles).Must(role =>
            role is TenantRoles.TenantOwner or TenantRoles.Admin or TenantRoles.Cashier or TenantRoles.InventoryStaff)
            .WithMessage("Unsupported tenant role.");
        RuleFor(x => x).Must(request => !request.BranchId.HasValue ||
            request.Roles == null ||
            request.Roles.All(role => role is TenantRoles.TenantOwner or TenantRoles.Admin))
            .WithMessage("Branch-scoped staff lookup is not supported until branch assignments are available.");
    }
}
