using System.Security.Cryptography;
using System.Text;

namespace KnightOnline.Server.Accounts;

public sealed class AuthTokenProtector
{
    private readonly IReadOnlyDictionary<string, byte[]> _receiptKeys;

    public AuthTokenProtector()
        : this("test-ephemeral", new Dictionary<string, string>
        {
            ["test-ephemeral"] = Convert.ToBase64String(
                RandomNumberGenerator.GetBytes(32)),
        })
    {
    }

    public AuthTokenProtector(
        string activeKeyId,
        IReadOnlyDictionary<string, string> base64Keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activeKeyId);
        ArgumentNullException.ThrowIfNull(base64Keys);
        ActiveKeyId = activeKeyId;
        _receiptKeys = base64Keys.ToDictionary(
            pair => pair.Key,
            pair => Convert.FromBase64String(pair.Value),
            StringComparer.Ordinal);
        if (!_receiptKeys.TryGetValue(activeKeyId, out byte[]? activeKey))
            throw new InvalidOperationException(
                $"Active receipt key id '{activeKeyId}' is not configured.");
        if (_receiptKeys.Any(pair => pair.Value.Length != 32))
            throw new InvalidOperationException(
                "Every authentication receipt key must decode to 32 bytes.");
    }

    public string ActiveKeyId { get; }

    public string CreateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string Hash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    public string Fingerprint(string value)
        => Fingerprint(ActiveKeyId, value);

    public string Fingerprint(string keyId, string value)
    {
        byte[] mac = HMACSHA256.HashData(GetKey(keyId),
            Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(mac);
    }

    public string Protect(string value)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] plaintext = Encoding.UTF8.GetBytes(value);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(GetKey(ActiveKeyId), tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
    }

    public string Unprotect(string protectedValue)
        => Unprotect(ActiveKeyId, protectedValue);

    public string Unprotect(string keyId, string protectedValue)
    {
        byte[] payload = Convert.FromBase64String(protectedValue);
        if (payload.Length < 28)
            throw new CryptographicException("Invalid auth receipt payload.");
        ReadOnlySpan<byte> nonce = payload.AsSpan(0, 12);
        ReadOnlySpan<byte> tag = payload.AsSpan(12, 16);
        byte[] plaintext = new byte[payload.Length - 28];
        using var aes = new AesGcm(GetKey(keyId), tag.Length);
        aes.Decrypt(nonce, payload.AsSpan(28), tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    private byte[] GetKey(string keyId) =>
        _receiptKeys.TryGetValue(keyId, out byte[]? key)
            ? key
            : throw new CryptographicException(
                $"Authentication receipt key '{keyId}' is not available.");
}
