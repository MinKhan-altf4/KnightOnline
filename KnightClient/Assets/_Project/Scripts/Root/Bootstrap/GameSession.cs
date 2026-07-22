using KnightOnline.Client.Data.Models;
using UnityEngine;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Stores the character selected before entering the gameplay scene.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Current { get; private set; }
        public CharacterData SelectedCharacter { get; private set; }

        public void SetSelectedCharacter(CharacterData character) => SelectedCharacter = character;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }

            Current = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }
    }
}
