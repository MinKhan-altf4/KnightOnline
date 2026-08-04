namespace KnightOnline.Server.Persistence.Entities;

public sealed class DomainOutboxMessageEntity
{
    public Guid EventId { get; set; }
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
    public required string EventType { get; set; }
    public int EventVersion { get; set; }
    public required string AggregateType { get; set; }
    public required string AggregateId { get; set; }
    public required string PayloadJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}
