using System.Collections.Generic;
using System.Text.Json;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Shared.Packets;
using UnityEngine;

namespace KnightOnline.Client.Network.Handlers
{
    public sealed class ConnectResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public ConnectResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.ConnectResponse;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<ConnectResponsePacket>(payload);
            if (packet != null)
                _events.Publish(new ServerConnectionResultEvent(
                    packet.Result switch
                    {
                        ConnectResult.Success => ConnectionOutcome.Success,
                        ConnectResult.VersionMismatch =>
                            ConnectionOutcome.VersionMismatch,
                        ConnectResult.ServerFull => ConnectionOutcome.ServerFull,
                        _ => ConnectionOutcome.NetworkError,
                    },
                    packet.Message));
        }
    }

    public sealed class CreateCharacterResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        private readonly ClientGameplaySettings _settings;

        public CreateCharacterResponseHandler(
            IEventBus events,
            ClientGameplaySettings settings)
        {
            _events = events;
            _settings = settings;
        }

        public PacketType PacketType => PacketType.CreateCharacterResponse;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<CreateCharacterResponsePacket>(payload);
            if (packet == null)
                return;

            bool success = packet.Result == CreateCharacterResult.Success;
            _events.Publish(new CharacterCreationResultEvent(
                success,
                packet.Message,
                success
                    ? CharacterPacketMapper.Create(
                        packet.Message,
                        _settings.InitialLevel,
                        _settings)
                    : null));
        }
    }

    public sealed class ListCharactersResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        private readonly ClientGameplaySettings _settings;

        public ListCharactersResponseHandler(
            IEventBus events,
            ClientGameplaySettings settings)
        {
            _events = events;
            _settings = settings;
        }

        public PacketType PacketType => PacketType.ListCharactersResponse;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<ListCharactersResponsePacket>(payload);
            if (packet == null)
                return;

            var characters = new List<CharacterData>(packet.Characters.Count);
            foreach (CharacterSummaryPacket entry in packet.Characters)
            {
                if (string.IsNullOrWhiteSpace(entry.CharacterName))
                    continue;

                CharacterData character = CharacterPacketMapper.Create(
                    entry.CharacterName,
                    entry.Level,
                    _settings);
                character.CharacterId = entry.CharacterId;
                characters.Add(character);
            }

            _events.Publish(new CharacterListReceivedEvent(characters));
        }
    }

    public sealed class ListMonstersResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public ListMonstersResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.ListMonstersResponse;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<ListMonstersResponsePacket>(payload);
            if (packet == null)
                return;

            var monsters = new List<MonsterData>(packet.Monsters.Count);
            foreach (MonsterSnapshotPacket entry in packet.Monsters)
                monsters.Add(MonsterPacketMapper.ToModel(entry));

            _events.Publish(new MonsterListReceivedEvent(monsters));
            Debug.Log($"[Monster] Received {monsters.Count} monster snapshot(s).");
        }
    }

    public sealed class MonsterHealthChangedHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public MonsterHealthChangedHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.MonsterHealthChanged;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<MonsterHealthChangedPacket>(payload);
            if (packet != null)
                _events.Publish(new MonsterHealthChangedEvent(
                    packet.MonsterId,
                    packet.CurrentHealth,
                    packet.MaximumHealth));
        }
    }

    public sealed class MonsterDiedHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public MonsterDiedHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.MonsterDied;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<MonsterDiedPacket>(payload);
            if (packet != null)
                _events.Publish(new MonsterDiedEvent(packet.MonsterId));
        }
    }

    public sealed class MonsterRespawnedHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public MonsterRespawnedHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.MonsterRespawned;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<MonsterRespawnedPacket>(payload);
            if (packet?.Monster != null)
                _events.Publish(new MonsterRespawnedEvent(
                    MonsterPacketMapper.ToModel(packet.Monster)));
        }
    }

    public sealed class SelectCharacterResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public SelectCharacterResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.SelectCharacterResponse;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<SelectCharacterResponsePacket>(payload);
            if (packet?.Result != SelectCharacterResult.Success ||
                packet.Character == null)
            {
                _events.Publish(new CharacterSelectionFailedEvent(
                    packet?.Message ?? "Character selection failed."));
                return;
            }

            SelectedCharacterPacket selected = packet.Character;
            var character = new CharacterData(
                selected.CharacterName,
                selected.Level,
                selected.MaximumHealth,
                selected.CurrentHealth,
                selected.MoveSpeed)
            {
                CharacterId = selected.CharacterId,
                SpawnPosition = new Vector2(
                    selected.PositionX,
                    selected.PositionY)
            };

            _events.Publish(new CharacterSelectedEvent(character));
        }
    }

    public sealed class AttackResultHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public AttackResultHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.AttackResult;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<AttackResultPacket>(payload);
            if (packet == null)
                return;

            _events.Publish(new AttackResultEvent(
                ToOutcome(packet.Status),
                packet.MonsterId,
                packet.AppliedDamage,
                packet.CooldownRemainingMilliseconds));
        }

        private static AttackOutcome ToOutcome(AttackResultStatus status) =>
            status switch
            {
                AttackResultStatus.Success => AttackOutcome.Success,
                AttackResultStatus.NoSelectedCharacter =>
                    AttackOutcome.NoSelectedCharacter,
                AttackResultStatus.MonsterNotFound =>
                    AttackOutcome.MonsterNotFound,
                AttackResultStatus.MonsterDead => AttackOutcome.MonsterDead,
                AttackResultStatus.OutOfRange => AttackOutcome.OutOfRange,
                AttackResultStatus.Cooldown => AttackOutcome.Cooldown,
                AttackResultStatus.PlayerDead => AttackOutcome.PlayerDead,
                _ => AttackOutcome.MonsterNotFound,
            };
    }

    public sealed class ForcedDisconnectHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public ForcedDisconnectHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.ForcedDisconnect;

        public void Handle(string payload)
        {
            var packet = JsonSerializer.Deserialize<ForcedDisconnectPacket>(payload);
            if (packet != null)
                _events.Publish(new ServerDisconnectedEvent(
                    packet.Message,
                    isForced: true));
        }
    }

    public sealed class CreateGuestResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public CreateGuestResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.CreateGuestResponse;
        public void Handle(string payload) =>
            AuthenticationPacketMapper.Publish(payload, _events);
    }

    public sealed class ResumeAccountResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public ResumeAccountResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.ResumeAccountResponse;
        public void Handle(string payload) =>
            AuthenticationPacketMapper.Publish(payload, _events);
    }

    public sealed class LoginResponseHandler : IClientPacketHandler
    {
        private readonly IEventBus _events;
        public LoginResponseHandler(IEventBus events) => _events = events;
        public PacketType PacketType => PacketType.LoginResponse;
        public void Handle(string payload) =>
            AuthenticationPacketMapper.Publish(payload, _events);
    }

    internal static class AuthenticationPacketMapper
    {
        public static void Publish(string payload, IEventBus events)
        {
            var packet =
                JsonSerializer.Deserialize<AuthenticationResponsePacket>(payload);
            if (packet == null)
                return;

            events.Publish(new AuthenticationResultEvent(
                MapOutcome(packet.Result),
                packet.Message,
                packet.AccountKey,
                packet.IsGuest,
                packet.RefreshToken,
                packet.RefreshTokenExpiresAtUtc));
        }

        private static AuthenticationOutcome MapOutcome(
            AuthenticationResultCode result) =>
            result switch
            {
                AuthenticationResultCode.Success =>
                    AuthenticationOutcome.Success,
                AuthenticationResultCode.InvalidCredentials =>
                    AuthenticationOutcome.InvalidCredentials,
                AuthenticationResultCode.InvalidOrExpiredToken =>
                    AuthenticationOutcome.InvalidOrExpiredToken,
                AuthenticationResultCode.UsernameUnavailable =>
                    AuthenticationOutcome.UsernameUnavailable,
                AuthenticationResultCode.GuestNotFound =>
                    AuthenticationOutcome.GuestNotFound,
                AuthenticationResultCode.AlreadyAuthenticated =>
                    AuthenticationOutcome.AlreadyAuthenticated,
                AuthenticationResultCode.SessionConflict =>
                    AuthenticationOutcome.SessionConflict,
                _ => AuthenticationOutcome.InvalidRequest,
            };
    }

    internal static class CharacterPacketMapper
    {
        public static CharacterData Create(
            string name,
            int level,
            ClientGameplaySettings settings) =>
            new(
                name,
                level,
                settings.InitialMaximumHealth,
                settings.InitialMaximumHealth,
                settings.DefaultMoveSpeed);
    }

    internal static class MonsterPacketMapper
    {
        public static MonsterData ToModel(MonsterSnapshotPacket packet) =>
            new()
            {
                MonsterId = packet.MonsterId,
                DefinitionId = packet.DefinitionId,
                MonsterName = packet.MonsterName,
                Level = packet.Level,
                CurrentHealth = packet.CurrentHealth,
                MaximumHealth = packet.MaximumHealth,
                IsAlive = packet.IsAlive,
                Position = new Vector2(packet.PositionX, packet.PositionY)
            };
    }
}
