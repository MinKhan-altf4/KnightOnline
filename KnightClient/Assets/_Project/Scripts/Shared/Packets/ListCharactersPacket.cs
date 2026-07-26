#nullable enable
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ListCharactersRequestPacket { }

    // Shared DTO keeps the server independent from Unity client models.
    public sealed class CharacterSummaryPacket
    {
        public string CharacterName { get; }
        public int CharacterId { get; }
        public int Level { get; }
        public int SlotIndex { get; }
        public string ClassDefinitionId { get; }
        public string BodyTypeDefinitionId { get; }
        public IReadOnlyList<AppearanceSelectionPacket> AppearanceSelections { get; }
        public string CurrentMapDefinitionId { get; }
        public string CurrentSpawnPointId { get; }
        public float PositionX { get; }
        public float PositionY { get; }

        public CharacterSummaryPacket(
            string characterName,
            int characterId,
            int level,
            int slotIndex = 0,
            string classDefinitionId = "",
            string bodyTypeDefinitionId = "",
            IReadOnlyList<AppearanceSelectionPacket>? appearanceSelections = null,
            string currentMapDefinitionId = "",
            string currentSpawnPointId = "",
            float positionX = 0,
            float positionY = 0)
        {
            CharacterName = characterName;
            CharacterId = characterId;
            Level = level;
            SlotIndex = slotIndex;
            ClassDefinitionId = classDefinitionId ?? string.Empty;
            BodyTypeDefinitionId = bodyTypeDefinitionId ?? string.Empty;
            AppearanceSelections =
                appearanceSelections ?? Array.Empty<AppearanceSelectionPacket>();
            CurrentMapDefinitionId = currentMapDefinitionId ?? string.Empty;
            CurrentSpawnPointId = currentSpawnPointId ?? string.Empty;
            PositionX = positionX;
            PositionY = positionY;
        }
    }

    public sealed class ListCharactersResponsePacket
    {
        public IReadOnlyList<CharacterSummaryPacket> Characters { get; }

        public ListCharactersResponsePacket(IReadOnlyList<CharacterSummaryPacket> characters)
        {
            Characters = characters ?? Array.Empty<CharacterSummaryPacket>();
        }
    }
}
