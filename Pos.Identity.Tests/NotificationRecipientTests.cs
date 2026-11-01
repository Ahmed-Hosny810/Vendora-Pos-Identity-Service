using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Auth.WebApi.Authentication;
using Pos.Identity.Application.Features.NotificationRecipients;
using Pos.Identity.Domain.Models;
using Pos.Identity.Infrastructure.Persistence.Context;
using Pos.Identity.Infrastructure.Persistence.Services;
using Xunit;

namespace Pos.Identity.Tests;

public class NotificationRecipientTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("incorrect", false)]
    [InlineData("test-notification-secret", true)]
    public async Task ApiKey_AuthenticatesOnlyMatchingCredential(string? key, bool expected)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<NotificationApiKeyOptions>(options => options.ApiKey = "test-notification-secret");
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions,
            NotificationApiKeyAuthenticationHandler>(NotificationApiKeyAuthenticationHandler.SchemeName, _ => { });
        using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        if (key != null) context.Request.Headers[NotificationApiKeyAuthenticationHandler.HeaderName] = key;
        var result = await context.AuthenticateAsync(NotificationApiKeyAuthenticationHandler.SchemeName);
        Assert.Equal(expected, result.Succeeded);
        if (expected) Assert.Equal("NotificationService", result.Principal!.FindFirst("service")!.Value);
    }

    [Fact]
    public async Task Recipients_AreActiveTenantUsers_WithAnyRole_WithoutDuplicates()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var tenant = Guid.NewGuid();
        var owner = new IdentityRole("TenantOwner") { Id = "owner" };
        var admin = new IdentityRole("Admin") { Id = "admin" };
        db.Roles.AddRange(owner, admin);
        var ownId = Guid.NewGuid().ToString();
        foreach (var entry in new[] {
            (ownId, tenant, true),
            (Guid.NewGuid().ToString(), tenant, false),
            (Guid.NewGuid().ToString(), Guid.NewGuid(), true) })
        {
            db.Users.Add(new ApplicationUser { Id = entry.Item1, TenantId = entry.Item2,
                IsActive = entry.Item3, UserType = "Tenant", FullName = "Recipient", Email = "unverified@example.test" });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = entry.Item1, RoleId = owner.Id });
        }
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = ownId, RoleId = admin.Id });
        await db.SaveChangesAsync();
        var result = await new NotificationRecipientService(db).GetRecipientsAsync(
            tenant, new[] { "TenantOwner", "Admin" }, default);
        var recipient = Assert.Single(result);
        Assert.Equal(Guid.Parse(ownId), recipient.UserId);
        Assert.Null(recipient.Email);
    }

    [Theory]
    [InlineData("TenantOwner", true)]
    [InlineData("Admin", true)]
    [InlineData("Cashier", false)]
    [InlineData("InventoryStaff", false)]
    [InlineData("SuperAdmin", false)]
    public void BranchRequests_RejectUnverifiableStaffAssignments(string role, bool expected)
    {
        var query = new GetNotificationRecipientsQuery
        { TenantId = Guid.NewGuid(), BranchId = Guid.NewGuid(), Roles = new() { role } };
        Assert.Equal(expected, new GetNotificationRecipientsQueryValidator().Validate(query).IsValid);
    }
}
