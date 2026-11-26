using Cysharp.Threading.Tasks;
using KnightOnline.Client.Network;
using UnityEngine;
using VContainer;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.World;
using KnightOnline.Client.Gameplay.World.Environment;
using System;
using KnightOnline.Client.Gameplay.NPC;
using KnightOnline.Client.Gameplay.Player;
using KnightOnline.Client.UI;

namespace KnightOnline.Client.Core.Bootstrap
{
    /// <summary>Entry point for the gameplay scene. Map and gameplay composition belongs here.</summary>
    public sealed class InGameSceneRoot : MonoBehaviour
    {
        private NetworkClient _networkClient;
        private IEventBus _events;
        private CharacterData _character;
        private PlayerController _player;
        private InGameMenuView _menuView;
        private RemotePlayerDirectory _remotePlayers;
        private IDisposable _mapSubscription;
        private MapEnvironmentView _environment;
        private string _shownMapId;

        [Inject]
        public void Construct(NetworkClient networkClient, IEventBus events,
            CharacterData character, PlayerController player,
            InGameMenuView menuView, RemotePlayerDirectory remotePlayers)
        {
            _remotePlayers = remotePlayers;
            _networkClient = networkClient;
            _events = events;
            _character = character;
            _player = player;
            _menuView = menuView;
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
            ConfigurePlayerSprite();
            _environment = new GameObject("MapEnvironment").AddComponent<MapEnvironmentView>();
            _mapSubscription = _events.Subscribe<MapTransitionedEvent>(OnMapTransitioned);
            var presentation = new GameObject("AuthoritativeWorldPresentation")
                .AddComponent<AuthoritativeWorldPresentation>();
            presentation.Initialize(_events, _networkClient);
            new GameObject("RemotePlayers")
                .AddComponent<RemotePlayersPresenter>()
                .Initialize(_remotePlayers);
            var villageHud = new GameObject("VillageUIToolkitHud")
                .AddComponent<VillageUIToolkitHud>();
            villageHud.Initialize(_events, _character, _menuView);
            villageHud.gameObject.AddComponent<InGameConnectionLostView>()
                .Initialize(villageHud.Root, _events, _player);
            InGameHUD playerHud = FindAnyObjectByType<InGameHUD>();
            playerHud?.SetQuestCardVisible(false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            new GameObject("HudDebugPanel").AddComponent<HudDebugPanel>()
                .Initialize(villageHud, playerHud);
#endif
            ApplyMapPresentation(_character.CurrentMapDefinitionId);
            RequestMapPresentation();
        }

        /// <summary>Shows the PixelDoll hero on the local player, facing its movement.</summary>
        private void ConfigurePlayerSprite()
        {
            if (_player == null) return;
            SpriteRenderer renderer = _player.GetComponent<SpriteRenderer>();
            DirectionalCharacterSprites sprites = DirectionalCharacterSprites.Load("Hero");
            if (renderer == null || sprites == null) return;
            // The legacy knight Animator rewrites the sprite every frame and
            // PlayerAnimationController flips it horizontally; the 8-direction
            // set already contains mirrored directions, so both are disabled.
            if (_player.TryGetComponent(out Animator animator))
                animator.enabled = false;
            if (_player.TryGetComponent(out PlayerAnimationController legacyAnimation))
                legacyAnimation.enabled = false;
            // Draw the hero on a child object: the view moves it for the step
            // bob, which must not move the Rigidbody/collider on the root.
            renderer.enabled = false;
            var visual = new GameObject("HeroVisual");
            visual.transform.SetParent(_player.transform, false);
            SpriteRenderer heroRenderer = visual.AddComponent<SpriteRenderer>();
            heroRenderer.sortingLayerID = renderer.sortingLayerID;
            heroRenderer.sortingOrder = renderer.sortingOrder;
            // Y-sorted against trees and bushes from the feet, not the sprite centre.
            heroRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            heroRenderer.sharedMaterial = renderer.sharedMaterial;
            _player.gameObject.AddComponent<DirectionalSpriteView>()
                .Initialize(heroRenderer, sprites, () => _player.MoveDirection);
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
            bool mapChanged = !string.Equals(_character.CurrentMapDefinitionId,
                value.MapId, StringComparison.OrdinalIgnoreCase);
            _character.CurrentMapDefinitionId = value.MapId;
            _character.CurrentSpawnPointId = value.SpawnId;
            _character.SpawnPosition = new Vector2(value.X, value.Y);
            ApplyMapPresentation(value.MapId);
            if (mapChanged)
                RequestMapPresentation();
        }

        private void ApplyMapPresentation(string mapId)
        {
            // The flat legacy floor is replaced by the tiled environment.
            GameObject floor = GameObject.Find("Floor");
            if (floor != null)
                floor.SetActive(false);
            if (_environment == null ||
                string.Equals(_shownMapId, mapId, StringComparison.OrdinalIgnoreCase))
                return;
            _shownMapId = mapId;
            _environment.Show(mapId);
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
