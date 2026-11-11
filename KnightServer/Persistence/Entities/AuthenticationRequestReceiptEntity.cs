namespace KnightOnline.Server.Persistence.Entities;

public sealed class AuthenticationRequestReceiptEntity
{
    public Guid RequestId { get; set; }
    public required string KeyId { get; set; }
    public required string Operation { get; set; }
    public required string RequestFingerprint { get; set; }
    public required string ProtectedResult { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
