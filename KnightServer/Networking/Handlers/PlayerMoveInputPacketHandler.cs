using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Time;
using KnightOnline.Server.World;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class PlayerMoveInputPacketHandler(
    IServerClock clock,
    IWorldMovementResolver movementResolver) : IPacketHandler
{
    public PacketType PacketType => PacketType.PlayerMoveInput;
    public PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.CharacterSelected;

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
            clock.UtcNow,
            movementResolver);

        return Task.CompletedTask;
    }
}
