using System.Collections.Generic;
using KnightOnline.Client.Data.Models;
using UnityEngine;

namespace KnightOnline.Client.Gameplay.World
{
    /// <summary>
    /// Shows other players from <see cref="RemotePlayerDirectory"/>: a simple
    /// marker plus name label, positioned by interpolation a fixed delay behind
    /// real time (World_Simulation_P1_Design §7). Purely visual: no colliders,
    /// no input, no authority.
    /// </summary>
    public sealed class RemotePlayersPresenter : MonoBehaviour
    {
        /// <summary>Two 10 Hz snapshots of buffer to absorb jitter.</summary>
        public const double InterpolationDelaySeconds = 0.1;

        private readonly Dictionary<int, Transform> _views = new();
        private readonly List<int> _removeBuffer = new();
        private RemotePlayerDirectory _directory;
        private Sprite _markerSprite;
        private int _seenMembershipVersion = -1;

        public void Initialize(RemotePlayerDirectory directory)
        {
            _directory = directory;
            _markerSprite = CreateMarkerSprite();
        }

        private void Update()
        {
            if (_directory == null)
                return;

            if (_seenMembershipVersion != _directory.MembershipVersion)
            {
                SyncViews();
                _seenMembershipVersion = _directory.MembershipVersion;
            }

            double renderTime =
                Time.realtimeSinceStartupAsDouble - InterpolationDelaySeconds;
            foreach (RemotePlayerState player in _directory.Players)
            {
                if (_views.TryGetValue(player.CharacterId, out Transform view) &&
                    player.Positions.TrySample(renderTime, out Vector2 position))
                {
                    view.position = new Vector3(position.x, position.y, 0f);
                }
            }
        }

        private void SyncViews()
        {
            _removeBuffer.Clear();
            foreach (int characterId in _views.Keys)
            {
                if (!_directory.TryGet(characterId, out _))
                    _removeBuffer.Add(characterId);
            }
            foreach (int characterId in _removeBuffer)
            {
                if (_views.Remove(characterId, out Transform view) && view != null)
                    Destroy(view.gameObject);
            }

            foreach (RemotePlayerState player in _directory.Players)
            {
                if (!_views.ContainsKey(player.CharacterId))
                    _views.Add(player.CharacterId, CreateView(player));
            }
        }

        private Transform CreateView(RemotePlayerState player)
        {
            var root = new GameObject($"RemotePlayer_{player.CharacterId}");
            root.transform.SetParent(transform, false);
            if (player.Positions.TrySample(double.MaxValue, out Vector2 start))
                root.transform.position = new Vector3(start.x, start.y, 0f);

            var marker = new GameObject("Marker");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = _markerSprite;
            renderer.color = new Color(0.45f, 0.8f, 1f, 0.95f);
            renderer.sortingOrder = 8;

            var label = new GameObject("Name");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = $"{player.Name}  Lv.{player.Level}";
            text.fontSize = 32;
            text.characterSize = 0.07f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
            MeshRenderer textRenderer = label.GetComponent<MeshRenderer>();
            if (textRenderer != null)
                textRenderer.sortingOrder = 9;
            return root.transform;
        }

        private static Sprite CreateMarkerSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y),
                    new Vector2(center, center)) / center;
                texture.SetPixel(x, y,
                    new Color(1f, 1f, 1f, Mathf.Clamp01((1f - distance) * 10f)));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            foreach (Transform view in _views.Values)
            {
                if (view != null)
                    Destroy(view.gameObject);
            }
            _views.Clear();
            if (_markerSprite != null)
            {
                Destroy(_markerSprite.texture);
                Destroy(_markerSprite);
            }
        }
    }
}
