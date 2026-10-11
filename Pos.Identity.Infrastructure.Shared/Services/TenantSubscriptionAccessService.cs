using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Pos.Identity.Application.Dtos;
using Pos.Identity.Application.Interfaces.Clients;
using Pos.Identity.Application.Interfaces.Services;
using System.Collections.Concurrent;

namespace Pos.Identity.Infrastructure.Shared.Services;

public class TenantSubscriptionAccessService : ITenantSubscriptionAccessService
{
    // One lookup per tenant at a time, so concurrent refreshes make a single call.
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    private readonly IMemoryCache _cache;
    private readonly ITenantBillingClient _client;
    private readonly ILogger<TenantSubscriptionAccessService> _logger;

    public TenantSubscriptionAccessService(IMemoryCache cache, ITenantBillingClient client,
        ILogger<TenantSubscriptionAccessService> logger)
    {
        _cache = cache;
        _client = client;
        _logger = logger;
    }

    public async Task<CachedTenantAccess> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var key = $"subscription-access:{tenantId}";

        // This key is used to get the last valid record for tenant if the lookup request failed
        var lastGoodKey = $"subscription-access-last-good:{tenantId}";

        if (_cache.TryGetValue<CachedTenantAccess>(key, out var cached) && cached != null && cached.ValidUntilUtc > DateTime.UtcNow)
            return cached;

        var gate = Gates.GetOrAdd(tenantId, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);

        try
        {
            // Another request may have filled the cache while this one waited.
            if (_cache.TryGetValue<CachedTenantAccess>(key, out cached) && cached != null && cached.ValidUntilUtc > DateTime.UtcNow)
                return cached;

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));

                var access = await _client.GetSubscriptionAccessAsync(tenantId, timeout.Token);

                var now = DateTime.UtcNow;

                var allowed = access.IsAllowed && access.SubscriptionEndsAt > now;

                // Denials are short-lived so a payment can restore access on refresh.
                var validUntil = allowed ? now.AddHours(24) : now.AddSeconds(30);
                if (allowed)
                {
                    var checkedUntil = access.CheckedAtUtc.AddHours(24);

                    if (checkedUntil < validUntil)
                        validUntil = checkedUntil;

                    if (access.SubscriptionEndsAt!.Value < validUntil)
                        validUntil = access.SubscriptionEndsAt.Value;
                }

                if (validUntil <= now)
                    throw new InvalidOperationException("TenantService returned stale access data.");

                var result = new CachedTenantAccess(allowed, access.SubscriptionEndsAt, validUntil);

                _cache.Set(key, result, new DateTimeOffset(validUntil, TimeSpan.Zero));

                //remember the last verified allowed answer until the subscription ends,
                // and drop it on a verified denial so an outage can never revive a blocked tenant.
                if (allowed)
                    _cache.Set(lastGoodKey, result, new DateTimeOffset(access.SubscriptionEndsAt!.Value, TimeSpan.Zero));
                else
                    _cache.Remove(lastGoodKey);

                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Subscription access lookup failed for tenant {TenantId}.", tenantId);

                // fail static. Reuse the last verified answer, never past its own end date.
                // Never grant access that was not verified before.
                var now = DateTime.UtcNow;

                if (_cache.TryGetValue<CachedTenantAccess>(lastGoodKey, out var lastGood) &&
                    lastGood != null && lastGood.IsAllowed &&
                    lastGood.SubscriptionEndsAt.HasValue && lastGood.SubscriptionEndsAt.Value > now)
                {
                    var until = now.AddSeconds(30);
                    if (lastGood.SubscriptionEndsAt.Value < until)
                        until = lastGood.SubscriptionEndsAt.Value;

                    var fallback = new CachedTenantAccess(true, lastGood.SubscriptionEndsAt, until);
                    _cache.Set(key, fallback, new DateTimeOffset(until, TimeSpan.Zero)); // queued requests reuse it
                    return fallback;
                }

                var denied = new CachedTenantAccess(false, null, now.AddSeconds(30));
                _cache.Set(key, denied, new DateTimeOffset(denied.ValidUntilUtc, TimeSpan.Zero)); // NEW
                return denied;
            }
        }
        finally
        {
            gate.Release();
        }
    }

    //Call from the SubscriptionRenewed / TenantSuspended consumers.
    //so the next token refresh asks TenantService again and sees the new state right away. Without it,
    //a renewal would wait for the normal entry to expire.

    public void Invalidate(Guid tenantId)
    {
        _cache.Remove($"subscription-access:{tenantId}");
        _cache.Remove($"subscription-access-last-good:{tenantId}");
    }
}