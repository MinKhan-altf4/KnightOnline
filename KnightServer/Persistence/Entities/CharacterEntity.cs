namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterEntity
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public required string ServerId { get; set; }
    public int SlotIndex { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public int Level { get; set; } = 1;
    public required string CurrentClassDefinitionId { get; set; }
    public required string BodyTypeDefinitionId { get; set; }
    public required string CurrentMapDefinitionId { get; set; }
    public required string CurrentSpawnPointId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public AccountEntity Account { get; set; } = null!;
    public ICollection<CharacterAppearanceEntity> Appearances { get; set; } = [];
    public ICollection<CharacterTutorialProgressEntity> TutorialProgress { get; set; } = [];
}
