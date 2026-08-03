using KnightOnline.Client.Shared.Packets;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Networking;

public static class GameplaySessionPacketMapper
{
    public static SelectedCharacterPacket ToCharacterPacket(
        PlayerSession session) =>
        new(
            session.CharacterId,
            session.CharacterName,
            session.Level,
            session.CurrentHealth,
            session.MaximumHealth,
            session.MoveSpeed,
            session.Position.X,
            session.Position.Y,
            session.Profile.SlotIndex,
            session.Profile.ClassDefinitionId,
            session.Profile.BodyTypeDefinitionId,
            session.Profile.MapDefinitionId,
            session.Profile.SpawnPointId,
            session.Profile.AppearanceSelections.Select(value =>
                new AppearanceSelectionPacket(
                    value.SlotDefinitionId,
                    value.OptionDefinitionId)).ToArray(),
            session.TotalExperience,
            session.ExperienceIntoLevel,
            session.ExperienceToNextLevel,
            session.CurrentMana,
            session.MaximumMana,
            session.BaseAttack,
            session.Defense);
}
