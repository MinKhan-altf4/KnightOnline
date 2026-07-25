using System;
using KnightOnline.Client.Core.Events;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.Gameplay.Targeting
{
    public sealed class SelectionMarkerView : MonoBehaviour
    {
        [SerializeField] private GameObject _visual;
        [SerializeField] private Vector3 _offset = new(0f, 0.25f, 0f);
        [SerializeField] private float _bobDistance = 0.12f;
        [SerializeField] private float _bobSpeed = 5f;

        private ITargetable _target;
        private IDisposable _selectedSubscription;
        private IDisposable _clearedSubscription;

        [Inject]
        public void Construct(IEventBus eventBus)
        {
            _selectedSubscription = eventBus.Subscribe<TargetSelectedEvent>(OnTargetSelected);
            _clearedSubscription = eventBus.Subscribe<TargetClearedEvent>(_ => Hide());
        }

        private void Awake()
        {
            SetVisualActive(false);
        }

        private void LateUpdate()
        {
            if (_target?.MarkerAnchor == null)
            {
                if (_target != null)
                    Hide();
                return;
            }

            var anchorPosition = _target.MarkerAnchor.position;

            // A prefab created before MarkerAnchor existed falls back to its root.
            // In that case, place the marker above the collider instead of at its center.
            if (_target is Component targetComponent &&
                _target.MarkerAnchor == targetComponent.transform &&
                targetComponent.TryGetComponent<Collider2D>(out var targetCollider))
            {
                anchorPosition.y = targetCollider.bounds.max.y;
            }

            var bob = Mathf.Sin(Time.time * _bobSpeed) * _bobDistance;
            if (_visual != null)
                _visual.transform.position =
                    anchorPosition + _offset + Vector3.up * bob;
        }

        private void OnTargetSelected(TargetSelectedEvent gameEvent)
        {
            _target = gameEvent.Target;
            SetVisualActive(_target?.MarkerAnchor != null);
        }

        private void Hide()
        {
            _target = null;
            SetVisualActive(false);
        }

        private void SetVisualActive(bool active)
        {
            if (_visual != null)
                _visual.SetActive(active);
        }

        private void OnDestroy()
        {
            _selectedSubscription?.Dispose();
            _clearedSubscription?.Dispose();
        }
    }
}
