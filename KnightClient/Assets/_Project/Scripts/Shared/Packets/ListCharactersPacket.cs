#nullable enable
using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public enum ListCharactersResult : byte
    {
        Success = 0,
        Unauthorized = 1,
        InvalidServer = 2,
        MalformedRequest = 3,
    }

    public sealed class ListCharactersRequestPacket
    {
        public string ServerId { get; }

        public ListCharactersRequestPacket(string serverId = "") =>
            ServerId = serverId ?? string.Empty;
    }

    // Shared DTO keeps the server independent from Unity client models.
    public sealed class CharacterSummaryPacket
    {
        public string CharacterName { get; }
        public int CharacterId { get; }
        public int Level { get; }
        public long TotalExperience { get; }
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
            float positionY = 0,
            long totalExperience = 0)
        {
            CharacterName = characterName;
            CharacterId = characterId;
            Level = level;
            TotalExperience = totalExperience;
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
        public ListCharactersResult Result { get; }
        public string Message { get; }

        public ListCharactersResponsePacket(
            IReadOnlyList<CharacterSummaryPacket> characters,
            ListCharactersResult result = ListCharactersResult.Success,
            string message = "")
        {
            Characters = characters ?? Array.Empty<CharacterSummaryPacket>();
            Result = result;
            Message = message ?? string.Empty;
        }
    }
}
