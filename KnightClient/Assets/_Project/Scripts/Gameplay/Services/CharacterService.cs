using Cysharp.Threading.Tasks;
using KnightOnline.Client.Network;
using KnightOnline.Client.Data.Models;

namespace KnightOnline.Client.Gameplay.Services
{
    /// <summary>
    /// Trạm trung chuyển giữa UI và Network cho các thao tác liên quan nhân vật.
    /// UI gọi qua đây thay vì biết trực tiếp về NetworkClient/packet - giữ đúng
    /// dependency graph (UI không reference Network).
    /// </summary>
    public sealed class CharacterService
    {
        private readonly NetworkClient _networkClient;

        public CharacterService(NetworkClient networkClient)
        {
            _networkClient = networkClient;
        }

        public UniTask RequestCreateCharacter(CharacterCreationDraftData draft)
        {
            return _networkClient.SendCreateCharacterRequestAsync(draft);
        }

        public UniTask RequestListCharacters(string serverId)
        {
            return _networkClient.SendListCharactersRequestAsync(serverId);
        }

        public UniTask RequestCreationCatalog(string serverId) =>
            _networkClient.SendCharacterCreationCatalogRequestAsync(serverId);

        public UniTask CheckName(string serverId, string characterName) =>
            _networkClient.SendCheckCharacterNameRequestAsync(
                serverId,
                characterName);
        
    }
}
