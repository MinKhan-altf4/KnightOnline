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
}
