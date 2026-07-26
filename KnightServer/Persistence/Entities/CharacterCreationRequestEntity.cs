namespace KnightOnline.Server.Persistence.Entities;

public sealed class CharacterCreationRequestEntity
{
    public Guid RequestId { get; set; }
    public int AccountId { get; set; }
    public int? CharacterId { get; set; }
    public required string ResultCode { get; set; }
    public required string ResultMessage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public AccountEntity Account { get; set; } = null!;
    public CharacterEntity? Character { get; set; }
}
