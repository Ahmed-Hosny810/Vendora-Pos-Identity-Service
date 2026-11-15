using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Pos.Identity.Application.Events;
using Pos.Identity.Application.Interfaces.Repositories;
using Pos.Identity.Application.Interfaces.Services;
using Pos.Identity.Domain.Models;
using Pos.Identity.Infrastructure.Shared.Settings;

namespace Pos.Identity.Infrastructure.Shared.Services;

/// <summary>
/// Validates an email request and stages it as an <see cref="EmailRequestedV1"/>
/// event in the outbox. Nothing is sent here: a background publisher forwards
/// the event to RabbitMQ and NotificationService does the actual sending.
/// </summary>
public class EmailRequestService : IEmailRequestService
{
    private const string DefaultLanguage = "en";
    private const int MaxEmailLength = 254;

    private const string ApplicationName = "Vendora";

    /// <summary>
    /// Template codes understood by NotificationService (upper-case by contract).
    /// </summary>
    private static class TemplateCodes
    {
        public const string EmailConfirmation = "EMAILCONFIRMATION";
        public const string PasswordReset = "PASSWORDRESET";
        public const string TenantUserInvitation = "TENANTUSERINVITATION";
        public const string PlatformAdminInvitation = "PLATFORMADMININVITATION";
        public const string UserInvitationResend = "USERINVITATIONRESEND";
    }

    /// <summary>
    /// Keys of the template values dictionary (the contract with NotificationService).
    /// </summary>
    private static class ValueKeys
    {
        public const string ConfirmationUrl = "ConfirmationUrl";
        public const string ResetPasswordUrl = "ResetPasswordUrl";
        public const string FullName = "FullName";
        public const string TemporaryPassword = "TemporaryPassword";
        public const string PortalName = "PortalName";
        public const string LoginUrl = "LoginUrl";
        public const string ExpiresAt = "ExpiresAt";
        public const string Email = "Email";
    }

    private readonly IOutboxRepositoryAsync _outbox;
    private readonly IdentityEmailLinkOptions _links;

    public EmailRequestService(IOutboxRepositoryAsync repository, IOptions<IdentityEmailLinkOptions> links)
    {
        _outbox = repository;
        _links = links.Value;
    }


    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    public Task<Guid> RequestConfirmationEmailAsync(
        Guid? tenantId, string userId, string email, string token, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            [ValueKeys.ConfirmationUrl] = BuildActionUrl(_links.ConfirmationUrl, userId, token)
        };

        return StageAsync(tenantId, userId, email, TemplateCodes.EmailConfirmation,
            values, expiresAt, cancellationToken);
    }

    public Task<Guid> RequestPasswordResetEmailAsync(
        Guid? tenantId, string userId, string email, string token, DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            [ValueKeys.ResetPasswordUrl] = BuildActionUrl(_links.ResetPasswordUrl, userId, token)
        };

        return StageAsync(tenantId, userId, email, TemplateCodes.PasswordReset,
            values, expiresAt, cancellationToken);
    }

    public Task<Guid> RequestTenantUserInvitationEmailAsync(
        Guid tenantId, string userId, string email, string fullName, string temporaryPassword,
        DateTime expiresAt, CancellationToken cancellationToken = default)
        => StageInvitationAsync(tenantId, userId, email, TemplateCodes.TenantUserInvitation,
            fullName, temporaryPassword, expiresAt, cancellationToken);

    public Task<Guid> RequestPlatformAdminInvitationEmailAsync(
        string userId, string email, string fullName, string temporaryPassword,
        DateTime expiresAt, CancellationToken cancellationToken = default)
        => StageInvitationAsync(tenantId: null, userId, email, TemplateCodes.PlatformAdminInvitation,
            fullName, temporaryPassword, expiresAt, cancellationToken);

    public Task<Guid> RequestInvitationResendEmailAsync(
        Guid? tenantId, string userId, string email, string fullName, string temporaryPassword,
        DateTime expiresAt, CancellationToken cancellationToken = default)
    {
        return StageInvitationAsync(tenantId, userId, email, TemplateCodes.UserInvitationResend,
            fullName, temporaryPassword, expiresAt, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Staging
    // ------------------------------------------------------------------

    private Task<Guid> StageInvitationAsync(
        Guid? tenantId, string userId, string email, string templateCode,
        string fullName, string temporaryPassword, DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPassword);
        EnsureSecureUrl(_links.LoginUrl);

        var values = new Dictionary<string, string>
        {
            [ValueKeys.FullName] = fullName,
            [ValueKeys.TemporaryPassword] = temporaryPassword,
            [ValueKeys.PortalName] = ApplicationName,
            [ValueKeys.LoginUrl] = _links.LoginUrl
        };

        return StageAsync(tenantId, userId, email, templateCode, values, expiresAt, cancellationToken);
    }

    private async Task<Guid> StageAsync(
        Guid? tenantId, string userId, string email, string templateCode,
        Dictionary<string, string> values, DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = DateTime.UtcNow;
        ValidateRequest(tenantId, userId, email, expiresAt, now);

        // Values every template receives, regardless of its own specific values.
        values[ValueKeys.ExpiresAt] = expiresAt.ToString("O", CultureInfo.InvariantCulture);
        values[ValueKeys.Email] = email;

        var eventId = Guid.NewGuid();
        var request = new EmailRequestedV1(
            eventId, tenantId, userId, email, templateCode, DefaultLanguage, values, now, expiresAt);

        await _outbox.AddAsync(new OutboxMessage
        {
            Id = eventId,
            TenantId = tenantId,
            EventType = EmailRequestedV1Contract.EventType,
            Payload = JsonSerializer.Serialize(request),
            OccurredAt = now
        }, cancellationToken);

        return eventId;
    }

    // ------------------------------------------------------------------
    // Validation
    // ------------------------------------------------------------------

    private static void ValidateRequest(
        Guid? tenantId, string userId, string email, DateTime expiresAt, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant ID must be null or a non-empty ID.", nameof(tenantId));

        if (!IsPlainEmailAddress(email))
            throw new ArgumentException("A valid recipient email is required.", nameof(email));

        if (expiresAt.Kind != DateTimeKind.Utc || expiresAt <= now)
            throw new ArgumentException("Expiry must be a future UTC timestamp.", nameof(expiresAt));
    }

    /// <summary>
    /// True for a bare address such as "alice@example.com". Rejects display-name
    /// forms ("Alice &lt;alice@example.com&gt;") and anything over 254 characters.
    /// </summary>
    private static bool IsPlainEmailAddress(string? email)
        => !string.IsNullOrWhiteSpace(email)
           && email.Length <= MaxEmailLength
           && MailAddress.TryCreate(email, out var parsed)
           && parsed.Address == email;

    /// <summary>Requires HTTPS; plain HTTP is allowed only for loopback (local development).</summary>
    private static void EnsureSecureUrl(string url)
    {
        var isValid = Uri.TryCreate(url, UriKind.Absolute, out var uri)
                      && (uri.Scheme == Uri.UriSchemeHttps
                          || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));

        if (!isValid)
            throw new InvalidOperationException(
                "Configure an HTTPS Identity email link (HTTP is allowed only for loopback development).");
    }

    // ------------------------------------------------------------------
    // Link building
    // ------------------------------------------------------------------

    /// <summary>
    /// Builds "{url}?UserId=...&amp;Token=..." where the token is Base64Url-encoded
    /// so it survives URLs and email clients unchanged.
    /// </summary>
    private static string BuildActionUrl(string url, string userId, string token)
    {
        EnsureSecureUrl(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return QueryHelpers.AddQueryString(url, new Dictionary<string, string?>
        {
            ["UserId"] = userId,
            ["Token"] = encodedToken
        });
    }
}