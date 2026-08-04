namespace KnightOnline.Server.Persistence.Entities;

public sealed class TutorialKillCreditEntity
{
    public int CharacterId { get; set; }
    public required string TutorialDefinitionId { get; set; }
    public Guid MonsterLifeId { get; set; }
    public int MonsterDefinitionId { get; set; }
    public DateTime CreditedAtUtc { get; set; }
    public CharacterEntity Character { get; set; } = null!;
}
