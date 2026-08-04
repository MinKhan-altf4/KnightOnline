namespace KnightOnline.Server.Persistence.Entities;

public sealed class GameplayAuditRecordEntity
{
    public Guid EventId { get; set; }
    public Guid RequestId { get; set; }
    public int CharacterId { get; set; }
    public required string Action { get; set; }
    public required string Reason { get; set; }
    public required string ResultJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
