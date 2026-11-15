namespace Pos.Identity.Infrastructure.Shared.Options;

public class OutboxPublisherOptions
{
    public bool Enabled { get; set; }
    public int PollingIntervalSeconds { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int PublishTimeoutSeconds { get; set; } = 10;
    public int MaxAttempts { get; set; } = 10;
    public int InitialRetryDelaySeconds { get; set; } = 10;
    public int MaxRetryDelaySeconds { get; set; } = 300;
}
