namespace Pos.Identity.Application.Interfaces.Services;

/// <summary>Stages email requests. The caller must save with account changes in one transaction.</summary>
public interface IEmailRequestService
{
    Task<Guid> RequestConfirmationEmailAsync(Guid? tenantId, string userId, string email,
        string token, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Guid> RequestPasswordResetEmailAsync(Guid? tenantId, string userId, string email,
        string token, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Guid> RequestTenantUserInvitationEmailAsync(Guid tenantId, string userId, string email,
        string fullName, string temporaryPassword, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Guid> RequestPlatformAdminInvitationEmailAsync(string userId, string email,
        string fullName, string temporaryPassword, DateTime expiresAt, CancellationToken cancellationToken = default);
    Task<Guid> RequestInvitationResendEmailAsync(Guid? tenantId, string userId, string email,
        string fullName, string temporaryPassword, DateTime expiresAt,
        CancellationToken cancellationToken = default);
}
