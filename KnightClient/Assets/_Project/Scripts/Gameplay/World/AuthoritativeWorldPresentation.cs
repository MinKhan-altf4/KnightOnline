using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Network;
using UnityEngine;
using KnightOnline.Client.Gameplay.Player;

namespace KnightOnline.Client.Gameplay.World
{
    public sealed class AuthoritativeWorldPresentation : MonoBehaviour
    {
        // Mirrors Shared PortalUseResult.OutOfRange; Gameplay does not reference
        // the protocol assembly (CORE-22), the event only carries the byte.
        private const byte PortalResultOutOfRange = 4;
        private const float PortalRetryDelaySeconds = 0.4f;

        private readonly List<GameObject> _npcObjects = new();
        private readonly List<GameObject> _portalObjects = new();
        private IEventBus _events;
        private NetworkClient _network;
        private IDisposable _npcSubscription, _portalSubscription;
        private IDisposable _transitionSubscription;
        private IDisposable _portalResultSubscription;
        private bool _transitionPending;
        private bool _awaitingPortalRefresh;
        private Sprite _portalDiscSprite;
        private Sprite _portalRingSprite;

        private void Awake()
        {
            _portalDiscSprite = CreateCircleSprite(false);
            _portalRingSprite = CreateCircleSprite(true);
        }

        public void Initialize(IEventBus events, NetworkClient network)
        {
            _events = events;
            _network = network;
            _npcSubscription = events.Subscribe<NpcListReceivedEvent>(OnNpcs);
            _portalSubscription = events.Subscribe<PortalListReceivedEvent>(OnPortals);
            _transitionSubscription = events.Subscribe<MapTransitionedEvent>(OnMapTransitioned);
            _portalResultSubscription = events.Subscribe<PortalUseResultEvent>(OnPortalUseResult);
        }

        private void OnNpcs(NpcListReceivedEvent value)
        {
            ClearObjects(_npcObjects);
            foreach (NpcSnapshotData npc in value.Npcs)
                CreateLabel($"NPC: {npc.DisplayName}", npc.PositionX,
                    npc.PositionY, new Color(0.9f, 0.45f, 0.8f), () =>
                        _network.SendInteractNpcRequestAsync(npc.DefinitionId).Forget(),
                    _npcObjects);
        }

        private void OnPortals(PortalListReceivedEvent value)
        {
            ClearObjects(_portalObjects);
            _awaitingPortalRefresh = false;
            foreach (PortalSnapshotData portal in value.Portals)
                CreatePortal(portal);
        }

        // No physics boundary: a collider at the edge stopped the player box
        // half a unit before the server's center clamp and caused wall jitter.
        // PlayerController applies the server rule (WalkableBounds) instead.

        private void CreatePortal(PortalSnapshotData portal)
        {
            var root = new GameObject($"Portal_{portal.DefinitionId}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(portal.X, portal.Y, 0);

            var glow = new GameObject("Glow");
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            var glowRenderer = glow.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = _portalDiscSprite;
            glowRenderer.color = new Color(0.1f, 0.75f, 1f, 0.25f);
            glowRenderer.sortingOrder = 5;

            var ring = new GameObject("PortalRing");
            ring.transform.SetParent(root.transform, false);
            var ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = _portalRingSprite;
            ringRenderer.color = new Color(0.25f, 0.9f, 1f, 0.95f);
            ringRenderer.sortingOrder = 6;

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0, 1.25f, 0);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = $"▶ {portal.DisplayName}";
            label.color = Color.white;
            label.fontSize = 40;
            label.characterSize = 0.09f;
            label.anchor = TextAnchor.MiddleCenter;
            MeshRenderer labelRenderer = label.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
                labelRenderer.sortingOrder = 7;
            if (labelObject.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D clickCollider =
                    labelObject.AddComponent<BoxCollider2D>();
                clickCollider.size = new Vector2(3f, 1.2f);
                clickCollider.isTrigger = true;
            }

            var entity = root;
            var collider = entity.AddComponent<CircleCollider2D>();
            collider.radius = Mathf.Max(0.35f,
                portal.InteractionRange > 0 ? portal.InteractionRange : 1.5f);
            collider.isTrigger = true;
            var relay = entity.AddComponent<PortalTriggerRelay>();
            relay.Initialize(() =>
            {
                if (_transitionPending || _awaitingPortalRefresh) return;
                _transitionPending = true;
                _network.SendUsePortalRequestAsync(portal.DefinitionId).Forget();
            });
            relay.SetEnabled(!_awaitingPortalRefresh);
            _portalObjects.Add(root);
        }

        private static Sprite CreateCircleSprite(bool ring)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y),
                    new Vector2(center, center)) / center;
                float alpha = ring
                    ? Mathf.Clamp01((0.92f - distance) * 18f) *
                      Mathf.Clamp01((distance - 0.72f) * 18f)
                    : Mathf.Clamp01((1f - distance) * 12f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 64f);
        }

        private void OnMapTransitioned(MapTransitionedEvent value)
        {
            ClearObjects(_portalObjects);
            _awaitingPortalRefresh = true;
            _transitionPending = false;
        }

        private void OnPortalUseResult(PortalUseResultEvent value)
        {
            if (value.Result == 0)
                return;

            _transitionPending = false;
            // Out of range: the player is probably still walking in, so retry
            // shortly while they stay inside. Other rejections (level, quest)
            // will not change by standing still: require leaving and
            // re-entering to avoid spamming the server.
            bool retryWhileInside = value.Result == PortalResultOutOfRange;
            foreach (GameObject portal in _portalObjects)
            {
                if (portal != null &&
                    portal.TryGetComponent(out PortalTriggerRelay relay))
                    relay.Rearm(!retryWhileInside, PortalRetryDelaySeconds);
            }
        }

        private GameObject CreateLabel(string label, float x, float y, Color color,
            Action clicked, List<GameObject> owner)
        {
            var entity = new GameObject(label);
            entity.transform.SetParent(transform, false);
            entity.transform.position = new Vector3(x, y, 0);
            var text = entity.AddComponent<TextMesh>();
            text.text = label;
            text.color = color;
            text.fontSize = 32;
            text.characterSize = 0.08f;
            text.anchor = TextAnchor.MiddleCenter;
            if (clicked != null)
            {
                var clickCollider = entity.AddComponent<BoxCollider2D>();
                clickCollider.size = new Vector2(3f, 1.2f);
                clickCollider.isTrigger = true;
                entity.AddComponent<WorldClickRelay>().Initialize(clicked);
            }
            owner.Add(entity);
            return entity;
        }

        private static void ClearObjects(List<GameObject> objects)
        {
            foreach (GameObject value in objects)
                if (value != null) Destroy(value);
            objects.Clear();
        }
        private void OnDestroy()
        {
            _npcSubscription?.Dispose();
            _portalSubscription?.Dispose();
            _transitionSubscription?.Dispose();
            _portalResultSubscription?.Dispose();
            ClearObjects(_npcObjects);
            ClearObjects(_portalObjects);
            if (_portalDiscSprite != null)
            {
                Destroy(_portalDiscSprite.texture);
                Destroy(_portalDiscSprite);
            }
            if (_portalRingSprite != null)
            {
                Destroy(_portalRingSprite.texture);
                Destroy(_portalRingSprite);
            }
        }
    }

    /// <summary>
    /// Sends one portal request when the player is inside the trigger, then
    /// waits for the server. On rejection the presentation re-arms it.
    /// </summary>
    public sealed class PortalTriggerRelay : MonoBehaviour
    {
        private Action _entered;
        private bool _armed = true;
        private bool _requireExit;
        private float _retryAt;

        public void Initialize(Action entered) => _entered = entered;

        public void SetEnabled(bool enabled)
        {
            _armed = enabled;
            _requireExit = false;
        }

        public void Rearm(bool requireExit, float delaySeconds)
        {
            _armed = true;
            _requireExit = requireExit;
            _retryAt = Time.unscaledTime + Mathf.Max(0f, delaySeconds);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!IsPlayer(other)) return;
            _requireExit = false;
            TryFire();
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsPlayer(other))
                TryFire();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (IsPlayer(other))
                _requireExit = false;
        }

        private void TryFire()
        {
            if (!_armed || _requireExit || Time.unscaledTime < _retryAt)
                return;
            _armed = false;
            _entered?.Invoke();
        }

        private static bool IsPlayer(Collider2D other) =>
            other.GetComponentInParent<PlayerController>() != null;
    }

    public sealed class WorldClickRelay : MonoBehaviour
    {
        private Action _clicked;
        public void Initialize(Action clicked) => _clicked = clicked;
        private void OnMouseDown() => _clicked?.Invoke();
    }
}
