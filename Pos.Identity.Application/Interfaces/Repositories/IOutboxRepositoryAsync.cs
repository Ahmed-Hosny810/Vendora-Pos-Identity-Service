using Pos.Identity.Domain.Models;
namespace Pos.Identity.Application.Interfaces.Repositories;

public interface IOutboxRepositoryAsync
{
    // Stages the message; the caller saves it in the same transaction as the identity operation.
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    // Returns due unpublished IDs across all tenants, including requests without a tenant.
    Task<IReadOnlyList<Guid>> GetPendingIdsAsync(
        DateTime nowUtc, int batchSize, int maxAttempts, CancellationToken cancellationToken = default);
    // Loads a tracked record so publisher progress can be saved by the caller.
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
