namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterProgressionGrantEntity
{
    public Guid RequestId { get; set; }
    public int CharacterId { get; set; }
    public required string SourceType { get; set; }
    public required string SourceId { get; set; }
    public long RequestedExperience { get; set; }
    public long AppliedExperience { get; set; }
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }
    public long TotalExperienceAfter { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public CharacterEntity Character { get; set; } = null!;
}
