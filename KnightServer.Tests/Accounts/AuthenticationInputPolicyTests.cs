using KnightOnline.Server.Accounts;

namespace KnightOnline.Server.Tests.Accounts;

public sealed class AuthenticationInputPolicyTests
{
    [Fact]
    public void RejectsOversizedPasswordBeforeHashing()
    {
        string oversized = new(
            'x',
            AuthenticationInputPolicy.MaximumPasswordLength + 1);

        Assert.False(
            AuthenticationInputPolicy.IsValidLoginPassword(oversized));
        Assert.False(
            AuthenticationInputPolicy.IsValidRegistrationPassword(oversized));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("contains\ncontrol")]
    public void RejectsInvalidDeviceIds(string deviceId)
    {
        Assert.False(
            AuthenticationInputPolicy.IsValidDeviceId(deviceId));
    }

    [Fact]
    public void AcceptsExpectedAlphaClientValues()
    {
        Assert.True(AuthenticationInputPolicy.IsValidDeviceId(
            Guid.NewGuid().ToString("N")));
        Assert.True(AuthenticationInputPolicy.IsValidRefreshToken(
            new string('t', 43)));
        Assert.True(AuthenticationInputPolicy.IsValidLoginUsername(
            "player_one"));
        Assert.True(AuthenticationInputPolicy.IsValidRegistrationPassword(
            "correct-horse-battery-staple"));
    }
}
