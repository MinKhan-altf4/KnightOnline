namespace KnightOnline.Server.Persistence.Entities;

public enum AccountKind : byte
{
    Guest = 0,
    Registered = 1,
    Development = 2,
}

public sealed class AccountEntity
{
    public int Id { get; set; }
    public required string AccountKey { get; set; }
    public AccountKind Kind { get; set; }
    public string? Username { get; set; }
    public string? NormalizedUsername { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public List<CharacterEntity> Characters { get; set; } = [];
    public List<RefreshSessionEntity> RefreshSessions { get; set; } = [];
}
