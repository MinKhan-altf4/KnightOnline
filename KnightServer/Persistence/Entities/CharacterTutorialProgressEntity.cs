using KnightOnline.Server.Tutorials;

namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterTutorialProgressEntity
{
    public int CharacterId { get; set; }
    public required string TutorialDefinitionId { get; set; }
    public required string CurrentStepDefinitionId { get; set; }
    public TutorialState State { get; set; }
    public int ObjectiveProgress { get; set; }
    public bool? ContinueChoice { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public CharacterEntity Character { get; set; } = null!;
}
