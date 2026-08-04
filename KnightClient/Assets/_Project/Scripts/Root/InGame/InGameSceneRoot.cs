using Cysharp.Threading.Tasks;
using KnightOnline.Client.Network;
using UnityEngine;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.World;
using System;
using KnightOnline.Client.Gameplay.NPC;
using KnightOnline.Client.Gameplay.Player;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Entry point for the gameplay scene. Map and gameplay composition belongs here.</summary>
    public sealed class InGameSceneRoot : MonoBehaviour
    {
        private NetworkClient _networkClient;
        private IEventBus _events;
        private CharacterData _character;
        private PlayerController _player;
        private IDisposable _mapSubscription;

        [Inject]
        public void Construct(NetworkClient networkClient, IEventBus events,
            CharacterData character, PlayerController player)
        {
            _networkClient = networkClient;
            _events = events;
            _character = character;
            _player = player;
        }

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

        private void Start()
        {
            DisableLegacyNpcs();
            DisableLegacyWalls();
            ConfigureCameraFollow();
            _mapSubscription = _events.Subscribe<MapTransitionedEvent>(OnMapTransitioned);
            var presentation = new GameObject("AuthoritativeWorldPresentation")
                .AddComponent<AuthoritativeWorldPresentation>();
            presentation.Initialize(_events, _networkClient);
            ApplyMapPresentation(_character.CurrentMapDefinitionId);
            RequestMapPresentation();
        }

        private static void DisableLegacyWalls()
        {
            GameObject walls = GameObject.Find("Walls");
            if (walls != null)
                walls.SetActive(false);
        }

        private void ConfigureCameraFollow()
        {
            Camera camera = Camera.main;
            if (camera == null || _player == null) return;
            CameraFollow2D follow = camera.GetComponent<CameraFollow2D>() ??
                camera.gameObject.AddComponent<CameraFollow2D>();
            follow.Initialize(_player.transform);
        }

        private static void DisableLegacyNpcs()
        {
            foreach (InteractableNPC npc in
                     FindObjectsByType<InteractableNPC>(
                         FindObjectsInactive.Include))
            {
                if (npc != null)
                    npc.gameObject.SetActive(false);
            }
        }

        private void OnMapTransitioned(MapTransitionedEvent value)
        {
            _character.CurrentMapDefinitionId = value.MapId;
            _character.CurrentSpawnPointId = value.SpawnId;
            _character.SpawnPosition = new Vector2(value.X, value.Y);
            ApplyMapPresentation(value.MapId);
            RequestMapPresentation();
        }

        private static void ApplyMapPresentation(string mapId)
        {
            Color mapColor = mapId switch
            {
                "tutorial_map_01" => new Color(0.28f, 0.48f, 0.27f),
                "wolf_field_01" => new Color(0.24f, 0.31f, 0.18f),
                "safe_zone_01" => new Color(0.25f, 0.38f, 0.50f),
                _ => new Color(0.25f, 0.35f, 0.25f),
            };
            GameObject floor = GameObject.Find("Floor");
            SpriteRenderer renderer = floor != null
                ? floor.GetComponent<SpriteRenderer>()
                : null;
            if (renderer != null)
                renderer.color = mapColor;
            Camera camera = Camera.main;
            if (camera != null)
                camera.backgroundColor = mapColor * 0.55f;
        }

        private void RequestMapPresentation()
        {
            _networkClient.SendListMonstersRequestAsync().Forget();
            _networkClient.SendListNpcsRequestAsync().Forget();
            _networkClient.SendListPortalsRequestAsync().Forget();
        }
        private void OnDestroy() => _mapSubscription?.Dispose();
    }
}
