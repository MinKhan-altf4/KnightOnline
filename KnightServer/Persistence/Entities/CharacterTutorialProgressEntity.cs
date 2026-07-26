namespace KnightOnline.Server.Persistence.Entities;

public enum TutorialProgressState : byte
{
    NotStarted = 0,
    CoreTutorial = 1,
    ContinueOffered = 2,
    ExtendedTutorial = 3,
    Skipped = 4,
    Completed = 5,
}

public sealed class CharacterTutorialProgressEntity
{
    public int CharacterId { get; set; }
    public required string TutorialDefinitionId { get; set; }
    public required string CurrentStepDefinitionId { get; set; }
    public TutorialProgressState State { get; set; }
    public bool? ContinueChoice { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public CharacterEntity Character { get; set; } = null!;
}
