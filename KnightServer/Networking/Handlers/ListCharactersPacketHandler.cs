using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Persistence;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class ListCharactersPacketHandler(
    CharacterRepository characterRepository,
    CharacterOptions options) : IPacketHandler
{
    public PacketType PacketType => PacketType.ListCharactersRequest;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        ListCharactersRequestPacket? request;
        try
        {
            request = JsonSerializer.Deserialize<ListCharactersRequestPacket>(
                payload);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request == null)
        {
            await SendFailure(
                connection,
                ListCharactersResult.MalformedRequest,
                "The character list request is malformed.",
                cancellationToken);
            return;
        }

        if (connection.AccountKey == null)
        {
            await SendFailure(
                connection,
                ListCharactersResult.Unauthorized,
                "Authentication is required.",
                cancellationToken);
            return;
        }

        string requestedServerId = string.IsNullOrWhiteSpace(request.ServerId)
            ? options.ServerId
            : request.ServerId;
        if (!string.Equals(
                requestedServerId,
                options.ServerId,
                StringComparison.OrdinalIgnoreCase))
        {
            await SendFailure(
                connection,
                ListCharactersResult.InvalidServer,
                "The selected server is not available.",
                cancellationToken);
            return;
        }

        IReadOnlyList<CharacterSummaryPacket> characters =
            await characterRepository.ListAsync(connection.AccountKey);

        await connection.SendAsync(
            PacketType.ListCharactersResponse,
            new ListCharactersResponsePacket(characters),
            cancellationToken);
    }

    private static Task SendFailure(
        ClientConnection connection,
        ListCharactersResult result,
        string message,
        CancellationToken cancellationToken) =>
        connection.SendAsync(
            PacketType.ListCharactersResponse,
            new ListCharactersResponsePacket([], result, message),
            cancellationToken);
}
