using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class PlayerMoveInputPacketHandler : IPacketHandler
{
    public PacketType PacketType => PacketType.PlayerMoveInput;

    public Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        var packet = JsonSerializer.Deserialize<PlayerMoveInputPacket>(payload);
        if (packet == null || connection.PlayerSession == null)
            return Task.CompletedTask;

        if (!float.IsFinite(packet.DirectionX) ||
            !float.IsFinite(packet.DirectionY))
            return Task.CompletedTask;

        connection.PlayerSession.SetMovement(
            new Vector2(packet.DirectionX, packet.DirectionY),
            DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
