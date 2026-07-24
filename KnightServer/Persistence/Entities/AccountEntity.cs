namespace KnightOnline.Server.Persistence.Entities;

public sealed class AccountEntity
{
    public int Id { get; set; }
    public required string AccountKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<CharacterEntity> Characters { get; set; } = [];
}
