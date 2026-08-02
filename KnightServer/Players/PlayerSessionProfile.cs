namespace KnightOnline.Server.Players;

public sealed record PlayerAppearanceSelection(
    string SlotDefinitionId,
    string OptionDefinitionId);

public sealed record PlayerSessionProfile(
    int CharacterId,
    string CharacterName,
    int Level,
    int SlotIndex,
    string ClassDefinitionId,
    string BodyTypeDefinitionId,
    string MapDefinitionId,
    string SpawnPointId,
    IReadOnlyList<PlayerAppearanceSelection> AppearanceSelections);
