using UnityEngine;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Entry point for the gameplay scene. Map and gameplay composition belongs here.</summary>
    public sealed class InGameSceneRoot : MonoBehaviour
    {
        private void Awake()
        {
            var character = GameSession.Current?.SelectedCharacter;
            if (character == null)
            {
                Debug.LogError("[InGame] No selected character.");
                return;
            }

            // Đã sửa dòng này để verify dữ liệu từ Server gửi về:
            Debug.Log($"[InGame] Loading gameplay for {character.CharacterName} | ID: {character.CharacterId} | Level: {character.Level}");
        }
    }
}