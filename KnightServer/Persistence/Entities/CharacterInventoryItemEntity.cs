namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterInventoryItemEntity
{
    public Guid Id { get; set; }
    public int CharacterId { get; set; }
    public required string ItemDefinitionId { get; set; }
    public int Quantity { get; set; } = 1;
    public required string SourceType { get; set; }
    public required string SourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public CharacterEntity Character { get; set; } = null!;
}
