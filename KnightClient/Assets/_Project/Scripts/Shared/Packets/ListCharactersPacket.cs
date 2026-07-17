using System;
using System.Collections.Generic;

namespace KnightOnline.Client.Shared.Packets
{
    public sealed class ListCharactersRequestPacket { }

    // Shared DTO keeps the server independent from Unity client models.
    public sealed class CharacterSummaryPacket
    {
        public string CharacterName { get; }

        public CharacterSummaryPacket(string characterName) => CharacterName = characterName;
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
