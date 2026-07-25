namespace KnightOnline.Server.Persistence.Entities;

public sealed class RefreshSessionEntity
{
    public Guid Id { get; set; }
    public Guid FamilyId { get; set; }
    public Guid? ReplacedBySessionId { get; set; }
    public int AccountId { get; set; }
    public required string TokenHash { get; set; }
    public required string DeviceIdHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public AccountEntity Account { get; set; } = null!;
}
