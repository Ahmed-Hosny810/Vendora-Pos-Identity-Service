using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Pos.Identity.Domain.Models;
using Pos.Identity.Infrastructure.Persistence.Context;
using Pos.Identity.Infrastructure.Persistence.Services;
using Xunit;

namespace Pos.Identity.Tests;

public class UserNameLookupTests
{
    [Fact]
    public async Task Lookup_UsesClaimTenant_IncludesInactive_AndRejectsMissingTenant()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using var db = new ApplicationDbContext(options);
        var tenant = Guid.NewGuid();
        var own = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        db.Users.AddRange(
            new ApplicationUser { Id = own.ToString(), TenantId = tenant, FullName = "Inactive cashier", IsActive = false, UserType = "Tenant" },
            new ApplicationUser { Id = foreign.ToString(), TenantId = Guid.NewGuid(), FullName = "Foreign cashier", UserType = "Tenant" });
        await db.SaveChangesAsync();
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("tenant_id", tenant.ToString()) }, "test"));
        var service = new UserNameLookupService(db, http);
        var result = await service.GetNamesAsync(new[] { own, own, foreign }, default);
        Assert.Equal("Inactive cashier", Assert.Single(result).Value);
        http.HttpContext.User = new ClaimsPrincipal();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetNamesAsync(new[] { own }, default));
    }
}
