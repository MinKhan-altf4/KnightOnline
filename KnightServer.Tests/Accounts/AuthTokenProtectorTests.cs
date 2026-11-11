using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Tests.Accounts;

public sealed class AuthTokenProtectorTests
{
    [Fact]
    public void CreatesUniqueOpaqueTokens()
    {
        var protector = new AuthTokenProtector();

        string first = protector.CreateToken();
        string second = protector.CreateToken();

        Assert.NotEqual(first, second);
        Assert.True(
            AuthenticationInputPolicy.IsValidRefreshToken(first));
        Assert.True(
            AuthenticationInputPolicy.IsValidRefreshToken(second));
    }

    [Fact]
    public void HashIsDeterministicAndDoesNotContainRawToken()
    {
        var protector = new AuthTokenProtector();
        string token = protector.CreateToken();

        string firstHash = protector.Hash(token);
        string secondHash = protector.Hash(token);

        Assert.Equal(firstHash, secondHash);
        Assert.DoesNotContain(token, firstHash, StringComparison.Ordinal);
        Assert.Equal(64, firstHash.Length);
    }

    [Fact]
    public void ProtectedReceiptCanBeReadByRestartedProtectorWithSameKey()
    {
        string key = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var first = new AuthTokenProtector("test-v1",
            new Dictionary<string, string> { ["test-v1"] = key });
        var restarted = new AuthTokenProtector("test-v1",
            new Dictionary<string, string> { ["test-v1"] = key });
        string protectedValue = first.Protect("rotated-refresh-token");

        Assert.NotEqual("rotated-refresh-token", protectedValue);
        Assert.Equal("rotated-refresh-token", restarted.Unprotect(protectedValue));
        Assert.Equal(first.Fingerprint("request"),
            restarted.Fingerprint("request"));
    }

    [Fact]
    public void ProtectedReceiptCannotBeReadWithDifferentKey()
    {
        var first = new AuthTokenProtector("key-one",
            new Dictionary<string, string>
            {
                ["key-one"] = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            });
        var second = new AuthTokenProtector("key-two",
            new Dictionary<string, string>
            {
                ["key-two"] = Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            });
        string protectedValue = first.Protect("secret");

        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => second.Unprotect(protectedValue));
    }

    [Fact]
    public void RetiredKeyCanDecryptAndFingerprintReceiptAfterRotation()
    {
        string previousKey = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        string activeKey = Convert.ToBase64String(
            System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var beforeRotation = new AuthTokenProtector("v1",
            new Dictionary<string, string> { ["v1"] = previousKey });
        var afterRotation = new AuthTokenProtector("v2",
            new Dictionary<string, string>
            {
                ["v1"] = previousKey,
                ["v2"] = activeKey,
            });
        string requestFingerprint = beforeRotation.Fingerprint("resume-payload");
        string ciphertext = beforeRotation.Protect("rotated-token");

        Assert.Equal(requestFingerprint,
            afterRotation.Fingerprint("v1", "resume-payload"));
        Assert.Equal("rotated-token", afterRotation.Unprotect("v1", ciphertext));
        Assert.Equal("v2", afterRotation.ActiveKeyId);
    }
}
