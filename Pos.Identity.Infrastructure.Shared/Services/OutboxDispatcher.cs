using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pos.Identity.Application.Interfaces.Publisher;
using Pos.Identity.Application.Interfaces.Repositories;
using Pos.Identity.Infrastructure.Persistence.Context;
using Pos.Identity.Infrastructure.Shared.Options;

namespace Pos.Identity.Infrastructure.Shared.Services;

public class OutboxDispatcher
{
    private readonly IOutboxRepositoryAsync _outboxRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ApplicationDbContext _context;
    private readonly OutboxPublisherOptions _options;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(IOutboxRepositoryAsync outboxRepository,
        IEventPublisher eventPublisher, ApplicationDbContext context,
        IOptions<OutboxPublisherOptions> options, ILogger<OutboxDispatcher> logger)
    {
        _outboxRepository = outboxRepository;
        _eventPublisher = eventPublisher;
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    public async Task DispatchAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var message = await _outboxRepository.GetByIdAsync(messageId, cancellationToken);
        if (message == null || message.PublishedAt.HasValue ||
            message.AttemptCount >= _options.MaxAttempts ||
            message.NextAttemptAt > DateTime.UtcNow)
            return;

        try
        {
            // Publish the persisted payload with its original event ID.
            // Confirmation means broker acceptance, not email delivery.
            await _eventPublisher.PublishAsync(message.Id, message.EventType,
                message.Payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            message.AttemptCount++;
            // Store only the exception type: payloads contain credentials and action links.
            message.LastError = exception.GetType().Name;
            var delay = Math.Min(_options.MaxRetryDelaySeconds,
                _options.InitialRetryDelaySeconds * Math.Pow(2, Math.Min(message.AttemptCount - 1, 20)));
            message.NextAttemptAt = DateTime.UtcNow.AddSeconds(delay);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Outbox message {MessageId} failed with {ErrorType}. Attempt {Attempt}/{MaxAttempts}.",
                message.Id, message.LastError, message.AttemptCount, _options.MaxAttempts);
            return;
        }

        message.AttemptCount++;
        message.PublishedAt = DateTime.UtcNow;
        message.NextAttemptAt = null;
        message.LastError = null;
        // A failed save leaves the message pending. A retry can publish it again:
        // NotificationService must deduplicate by EventId.
        await _context.SaveChangesAsync(cancellationToken);
    }
}
