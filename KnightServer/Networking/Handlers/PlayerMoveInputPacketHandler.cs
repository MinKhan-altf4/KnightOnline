using System.Numerics;
using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Players;
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

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        PlayerMoveInputPacket? packet;
        try
        {
            packet = JsonSerializer.Deserialize<PlayerMoveInputPacket>(payload);
        }
        catch (JsonException)
        {
            return;
        }
        if (packet == null || connection.PlayerSession == null)
            return;

        if (!float.IsFinite(packet.DirectionX) ||
            !float.IsFinite(packet.DirectionY) ||
            packet.ClientSequence <= 0)
            return;

        DateTime utcNow = clock.UtcNow;
        bool accepted = connection.PlayerSession.TrySetMovement(
            new Vector2(packet.DirectionX, packet.DirectionY),
            packet.ClientSequence,
            utcNow,
            movementResolver);

        PlayerPositionState snapshot =
            connection.PlayerSession.CapturePositionSnapshot();
        await connection.SendAsync(
            PacketType.PlayerPositionSnapshot,
            new PlayerPositionSnapshotPacket(
                snapshot.ServerSequence,
                snapshot.AcknowledgedClientSequence,
                accepted
                    ? PlayerPositionSnapshotReason.MovementAccepted
                    : PlayerPositionSnapshotReason.MovementRejected,
                snapshot.Position.X,
                snapshot.Position.Y,
                utcNow),
            cancellationToken);
    }
}
