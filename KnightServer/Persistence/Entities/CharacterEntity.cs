namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterEntity
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public int Level { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public AccountEntity Account { get; set; } = null!;
}
