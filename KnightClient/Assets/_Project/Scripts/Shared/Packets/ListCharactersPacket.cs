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

        public CharacterSummaryPacket(string characterName, int characterId, int level)
        {
            CharacterName = characterName;
            CharacterId = characterId;
            Level = level;
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