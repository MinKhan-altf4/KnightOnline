using System;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Gameplay.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace KnightOnline.Client.UI
{
    /// <summary>
    /// B15: the authentication entry panel lives in the Bootstrap scene, which
    /// is unloaded in game, so a lost connection used to leave the player in a
    /// dead world. This view blocks input, explains what happened and returns to
    /// Bootstrap, where AuthenticationFlowService has already published the
    /// (sticky) entry state with the Continue option.
    ///
    /// Automatic resume + re-select of the same character is a later step.
    /// </summary>
    public sealed class InGameConnectionLostView : MonoBehaviour
    {
        private const string VisibleClass = "connection-lost-overlay--visible";
        private const string DefaultMessage =
            "Bạn bị mất kết nối máy chủ. Hãy đăng nhập lại để tiếp tục.";

        [SerializeField] private string _entrySceneName = "Bootstrap";

        private VisualElement _overlay;
        private Label _message;
        private Button _returnButton;
        private PlayerController _player;
        private IDisposable _disconnectSubscription;
        private bool _shown;
        private bool _returning;

        public void Initialize(VisualElement root, IEventBus events,
            PlayerController player)
        {
            _player = player;
            _overlay = root?.Q("connection-lost-overlay");
            _message = root?.Q<Label>("connection-lost-message");
            _returnButton = root?.Q<Button>("connection-lost-return");
            if (_overlay == null || _message == null || _returnButton == null)
            {
                Debug.LogError(
                    "[InGame] VillageHud.uxml is missing the connection-lost " +
                    "overlay nodes; disconnects will not be shown.", this);
            }
            else
            {
                _returnButton.clicked += ReturnToEntry;
            }

            _disconnectSubscription =
                events?.Subscribe<ServerDisconnectedEvent>(OnDisconnected);
        }

        private void OnDisconnected(ServerDisconnectedEvent value)
        {
            // A forced disconnect arrives first with its reason, followed by
            // the socket close; keep the first (most specific) message.
            if (_shown || _returning)
                return;
            _shown = true;

            _player?.SetMovementEnabled(false);
            if (_overlay == null)
            {
                ReturnToEntry();
                return;
            }

            _message.text = string.IsNullOrWhiteSpace(value.Message)
                ? DefaultMessage
                : value.Message;
            _overlay.AddToClassList(VisibleClass);
            _returnButton.Focus();
        }

        private void ReturnToEntry()
        {
            if (_returning)
                return;
            _returning = true;
            _returnButton?.SetEnabled(false);
            SceneManager.LoadSceneAsync(_entrySceneName, LoadSceneMode.Single);
        }

        private void OnDestroy()
        {
            _disconnectSubscription?.Dispose();
            if (_returnButton != null)
                _returnButton.clicked -= ReturnToEntry;
        }
    }
}
