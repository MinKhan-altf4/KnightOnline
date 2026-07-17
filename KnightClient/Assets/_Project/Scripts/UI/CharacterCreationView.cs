using UnityEngine;
using TMPro;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Services;

namespace KnightOnline.Client.UI
{
    public class CharacterCreationView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TextMeshProUGUI _resultText;

        private IEventBus _eventBus;
        private CharacterService _characterService;
        private System.IDisposable _subscription;

        [Inject]
        public void Construct(IEventBus eventBus, CharacterService characterService)
        {
            _eventBus = eventBus;
            _characterService = characterService;
        }

        private void Start()
        {
            _subscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
        }

        // Gắn method này vào OnClick của nút "Tạo nhân vật" trong Inspector
        public void OnCreateCharacterClicked()
        {
            string name = _nameInput.text;
            _ = _characterService.RequestCreateCharacter(name);
        }

        private void OnCharacterCreationResult(CharacterCreationResultEvent e)
        {
            _resultText.text = e.Success
                ? $"Tạo thành công: {e.Character.CharacterName}"
                : $"Thất bại: {e.Message}";
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }
    }
}