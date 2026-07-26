using UnityEngine;
using TMPro;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;

namespace KnightOnline.Client.UI
{
    public class CharacterCreationView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField _nameInput;
        [SerializeField] private TextMeshProUGUI _resultText;

        private IEventBus _eventBus;
        private System.IDisposable _subscription;

        [Inject]
        public void Construct(IEventBus eventBus) => Initialize(eventBus);

        public void Initialize(IEventBus eventBus) =>
            _eventBus = eventBus ??
                throw new System.ArgumentNullException(nameof(eventBus));

        private void Start()
        {   
            _subscription = _eventBus.Subscribe<CharacterCreationResultEvent>(OnCharacterCreationResult);
        }

        // Gắn method này vào OnClick của nút "Tạo nhân vật" trong Inspector
        public void OnCreateCharacterClicked()
        {
            if (_eventBus == null)
            {
                Debug.LogError(
                    "[CharacterCreationView] EventBus was not injected.",
                    this);
                return;
            }

            string name = _nameInput?.text?.Trim() ?? string.Empty;
            _eventBus.Publish(new CharacterCreationRequestedEvent(name));
        }

        private void OnCharacterCreationResult(CharacterCreationResultEvent e)
        {
            if (_resultText == null)
                return;

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
