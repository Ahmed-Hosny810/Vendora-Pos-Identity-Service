using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pos.Identity.Application.Dtos;
using Pos.Identity.Application.Interfaces.Clients;
using Pos.Identity.Infrastructure.Shared.Services;
using Xunit;

namespace Pos.Identity.Tests;

public class SubscriptionAccessCacheTests
{
    [Fact]
    public async Task AllowedAccess_IsCachedPerTenant_AndLimitedToOriginalCheckTime()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new Mock<ITenantBillingClient>();
        var checkedAt = DateTime.UtcNow.AddHours(-1);
        client.Setup(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => new TenantSubscriptionAccessDto
            { TenantId = id, IsAllowed = true, CheckedAtUtc = checkedAt, SubscriptionEndsAt = checkedAt.AddDays(10) });
        var service = new TenantSubscriptionAccessService(cache, client.Object, NullLogger<TenantSubscriptionAccessService>.Instance);
        var tenantId = Guid.NewGuid();
        var first = await service.GetAsync(tenantId, default);
        var second = await service.GetAsync(tenantId, default);
        Assert.True(first.IsAllowed);
        Assert.Equal(checkedAt.AddHours(24), first.ValidUntilUtc);
        Assert.Equal(first, second);
        client.Verify(x => x.GetSubscriptionAccessAsync(tenantId, It.IsAny<CancellationToken>()), Times.Once);
        await service.GetAsync(Guid.NewGuid(), default);
        client.Verify(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CacheNeverOutlivesSubscription()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new Mock<ITenantBillingClient>();
        var end = DateTime.UtcNow.AddMinutes(2);
        client.Setup(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscriptionAccessDto { IsAllowed = true, CheckedAtUtc = DateTime.UtcNow, SubscriptionEndsAt = end });
        var service = new TenantSubscriptionAccessService(cache, client.Object, NullLogger<TenantSubscriptionAccessService>.Instance);
        Assert.Equal(end, (await service.GetAsync(Guid.NewGuid(), default)).ValidUntilUtc);
    }

    [Fact]
    public async Task FailureDoesNotGrantAccessOrPopulateCache_AndNextCallCanRecover()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new Mock<ITenantBillingClient>();
        client.SetupSequence(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Unavailable"))
            .ReturnsAsync(new TenantSubscriptionAccessDto { IsAllowed = true, CheckedAtUtc = DateTime.UtcNow, SubscriptionEndsAt = DateTime.UtcNow.AddDays(1) });
        var service = new TenantSubscriptionAccessService(cache, client.Object, NullLogger<TenantSubscriptionAccessService>.Instance);
        var id = Guid.NewGuid();
        Assert.False((await service.GetAsync(id, default)).IsAllowed);
        Assert.True((await service.GetAsync(id, default)).IsAllowed);
    }

    [Fact]
    public async Task DeniedAccess_IsCachedBriefly()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new Mock<ITenantBillingClient>();
        client.Setup(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSubscriptionAccessDto { IsAllowed = false, CheckedAtUtc = DateTime.UtcNow });
        var service = new TenantSubscriptionAccessService(cache, client.Object, NullLogger<TenantSubscriptionAccessService>.Instance);
        var id = Guid.NewGuid();
        var before = DateTime.UtcNow;
        var result = await service.GetAsync(id, default);
        Assert.False(result.IsAllowed);
        Assert.InRange(result.ValidUntilUtc, before.AddSeconds(30), DateTime.UtcNow.AddSeconds(30));
        await service.GetAsync(id, default);
        client.Verify(x => x.GetSubscriptionAccessAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallerCancellation_IsNotConvertedToDenial()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var client = new Mock<ITenantBillingClient>();
        client.Setup(x => x.GetSubscriptionAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        var service = new TenantSubscriptionAccessService(cache, client.Object, NullLogger<TenantSubscriptionAccessService>.Instance);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(Guid.NewGuid(), cancellation.Token));
    }
}
