# Notification recipient integration

POST /api/v1/internal/notification-recipients

Authentication: X-Internal-Api-Key. Only the dedicated NotificationServiceApiKey authentication scheme is accepted. User login/token defaults are unchanged.
Configure NotificationServiceAuthentication:ApiKey in Identity and IdentityRecipientClient:ApiKey in NotificationService with the same dedicated random secret.
An empty Identity key rejects every request. Do not reuse the Identity-to-Tenant API key.

PowerShell environment variables (set in each respective service's process environment):
Identity: NotificationServiceAuthentication__ApiKey
Notification: IdentityRecipientClient__ApiKey

Alternatively use dotnet user-secrets with the respective startup project.
Use HTTPS and restrict deployment access to trusted services. No credential values are logged.

Request example:
{
  "tenantId": "7922a41d-6b79-46b8-81ac-88a5a1d9ffd4",
  "branchId": null,
  "roles": ["TenantOwner", "Admin"]
}

The API returns the standard Response wrapper with a data array containing userId, fullName, email, phoneNumber.
Only active Tenant users with ANY requested role and the specified TenantId are eligible.
Users are deduplicated. Email/phone are returned only if confirmed; userId still enables in-app delivery.
An empty successful result is valid.

BranchId can be supplied for tenant-wide TenantOwner/Admin recipients. Cashier/InventoryStaff with BranchId is rejected because branch assignments are not available in Identity yet.
TenantId and roles are trusted service input, not public user input. Unsupported roles and empty tenant IDs are rejected.
Lookup remains available when a subscription expires.

No database migrations are required. Recipient delivery, RabbitMQ consumption and live two-service tests are outside this change.
