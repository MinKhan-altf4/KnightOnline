using System.Text.Json;
using KnightOnline.Client.Shared.Packets;

namespace KnightServer.Tests.Accounts;

public sealed class AccountSessionPacketTests
{
    [Fact]
    public void AuthenticationResponse_RoundTripsLeaseContract()
    {
        Guid generation = Guid.NewGuid();
        DateTime expiresAtUtc =
            new(2026, 8, 2, 8, 0, 20, DateTimeKind.Utc);
        var source = new AuthenticationResponsePacket(
            AuthenticationResultCode.Success,
            "ok",
            "account-1",
            false,
            "refresh-token",
            expiresAtUtc.AddDays(30),
            "Player",
            generation,
            expiresAtUtc,
            5);

        AuthenticationResponsePacket? result =
            JsonSerializer.Deserialize<AuthenticationResponsePacket>(
                JsonSerializer.Serialize(source));

        Assert.NotNull(result);
        Assert.Equal(generation, result.SessionGeneration);
        Assert.Equal(expiresAtUtc, result.SessionLeaseExpiresAtUtc);
        Assert.Equal(5, result.HeartbeatIntervalSeconds);
    }

    [Fact]
    public void HeartbeatRequest_RoundTripsGeneration()
    {
        Guid generation = Guid.NewGuid();
        var source = new AccountSessionHeartbeatRequestPacket(generation);

        AccountSessionHeartbeatRequestPacket? result =
            JsonSerializer.Deserialize<AccountSessionHeartbeatRequestPacket>(
                JsonSerializer.Serialize(source));

        Assert.NotNull(result);
        Assert.Equal(generation, result.SessionGeneration);
    }
}
