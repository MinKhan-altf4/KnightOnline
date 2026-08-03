using System.Text.Json;
using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Combat;
using KnightOnline.Server.Monsters;
using KnightOnline.Server.Players;
using KnightOnline.Server.Progression;
using KnightOnline.Server.Configuration;
using KnightOnline.Server.Time;

namespace KnightOnline.Server.Networking.Handlers;

public sealed class AttackMonsterPacketHandler(
    MonsterCombatService combatService,
    MonsterService monsterService,
    ConnectionRegistry connections,
    CharacterProgressionService progression,
    CharacterStatsPipeline statsPipeline,
    ProgressionOptions progressionOptions,
    GuestOptions guestOptions,
    IServerClock clock) : IPacketHandler
{
    public PacketType PacketType => PacketType.AttackMonsterRequest;
    public PacketAccessLevel RequiredAccess =>
        PacketAccessLevel.CharacterSelected;

    public async Task HandleAsync(
        ClientConnection connection,
        string payload,
        CancellationToken cancellationToken)
    {
        AttackMonsterRequestPacket? request =
            JsonSerializer.Deserialize<AttackMonsterRequestPacket>(payload);

        if (request == null)
            return;

        MonsterAttackResolution resolution = combatService.Attack(
            connection,
            request.MonsterId,
            clock.UtcNow);

        MonsterDamageResult? result = resolution.DamageResult;
        await connection.SendAsync(
            PacketType.AttackResult,
            new AttackResultPacket(
                resolution.Status,
                resolution.MonsterId,
                result?.AppliedDamage ?? 0,
                (int)Math.Ceiling(
                    resolution.CooldownRemaining?.TotalMilliseconds ?? 0)),
            cancellationToken);

        if (resolution.Status != AttackResultStatus.Success ||
            result == null)
            return;

        MonsterSnapshot? snapshot =
            monsterService.GetSnapshot(result.MonsterId);

        if (snapshot == null)
            return;

        await connections.BroadcastToMapAsync(
            snapshot.MapDefinitionId,
            PacketType.MonsterHealthChanged,
            new MonsterHealthChangedPacket(
                result.MonsterId,
                result.RemainingHealth,
                snapshot.MaximumHealth));

        if (result.WasKilled)
        {
            await connections.BroadcastToMapAsync(
                snapshot.MapDefinitionId,
                PacketType.MonsterDied,
                new MonsterDiedPacket(result.MonsterId));

            PlayerSession session = connection.PlayerSession!;
            int maximumLevel = connection.IsGuest
                ? Math.Min(
                    guestOptions.MaximumLevel,
                    progressionOptions.MaximumLevel)
                : progressionOptions.MaximumLevel;
            ProgressionGrantResult grant =
                await progression.GrantExperienceAsync(
                    result.MonsterLifeId,
                    session.CharacterId,
                    snapshot.ExperienceReward,
                    maximumLevel,
                    "monster_kill",
                    $"monster:{result.MonsterId}:life:{result.MonsterLifeId:N}",
                    cancellationToken);
            if (grant.Status is ProgressionGrantStatus.Applied or
                ProgressionGrantStatus.AlreadyApplied)
            {
                CharacterStats stats = statsPipeline.Calculate(
                    session.Profile.ClassDefinitionId,
                    grant.LevelAfter);
                session.ApplyProgression(
                    grant.LevelAfter,
                    grant.TotalExperience,
                    grant.ExperienceIntoLevel,
                    grant.ExperienceToNextLevel,
                    stats);
                await connection.SendAsync(
                    PacketType.CharacterProgressionChanged,
                    new CharacterProgressionChangedPacket(
                        grant.RequestId,
                        grant.AppliedExperience,
                        grant.TotalExperience,
                        grant.LevelAfter,
                        grant.ExperienceIntoLevel,
                        grant.ExperienceToNextLevel,
                        session.CurrentHealth,
                        session.MaximumHealth,
                        session.CurrentMana,
                        session.MaximumMana,
                        session.BaseAttack,
                        session.Defense),
                    cancellationToken);
            }
        }
    }
}
