using Cysharp.Threading.Tasks;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Network;

namespace KnightOnline.Client.Gameplay.Services
{
    public sealed class CharacterSelectionService
    {
        private readonly NetworkClient _networkClient;

        public CharacterSelectionService(NetworkClient networkClient) =>
            _networkClient = networkClient;

        public void SelectCharacter(CharacterData character)
        {
            if (character == null || character.CharacterId <= 0)
                return;

            _networkClient
                .SendSelectCharacterRequestAsync(character.CharacterId)
                .Forget();
        }
    }
}
