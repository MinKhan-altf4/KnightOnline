namespace KnightOnline.Server.Persistence.Entities;

public sealed class TutorialCommandEntity
{
    public Guid RequestId { get; set; }
    public int CharacterId { get; set; }
    public required string CommandType { get; set; }
    public required string ResultCode { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public CharacterEntity Character { get; set; } = null!;
}
