namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterAppearanceEntity
{
    public int CharacterId { get; set; }
    public required string SlotDefinitionId { get; set; }
    public required string AppearanceDefinitionId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public CharacterEntity Character { get; set; } = null!;
}
