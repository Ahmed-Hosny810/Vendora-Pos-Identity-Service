using Microsoft.EntityFrameworkCore;
using Pos.Identity.Application.Interfaces.Repositories;
using Pos.Identity.Domain.Models;
using Pos.Identity.Infrastructure.Persistence.Context;

namespace Pos.Identity.Infrastructure.Persistence.Repositories;

public class OutboxRepositoryAsync : IOutboxRepositoryAsync
{
    private readonly ApplicationDbContext _context;

    public OutboxRepositoryAsync(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        DateTime nowUtc, int batchSize, int maxAttempts, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        return await _context.OutboxMessages.AsNoTracking()
            .Where(x => x.PublishedAt == null && x.AttemptCount < maxAttempts &&
                (x.NextAttemptAt == null || x.NextAttemptAt <= nowUtc))
            .OrderBy(x => x.OccurredAt).ThenBy(x => x.Id)
            .Take(batchSize).Select(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.OutboxMessages.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
