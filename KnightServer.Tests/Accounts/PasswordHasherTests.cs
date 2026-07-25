using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Tests.Accounts;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashesWithUniqueSaltAndVerifiesCorrectPassword()
    {
        var hasher = new PasswordHasher();
        const string password = "correct-horse-battery-staple";

        string first = hasher.Hash(password);
        string second = hasher.Hash(password);

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify(password, first));
        Assert.True(hasher.Verify(password, second));
        Assert.False(hasher.Verify("wrong-password", first));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-supported-format")]
    [InlineData("pbkdf2-sha256-v1$invalid$salt$hash")]
    public void RejectsMalformedHashes(string encodedHash)
    {
        var hasher = new PasswordHasher();

        Assert.False(hasher.Verify("password", encodedHash));
    }
}
