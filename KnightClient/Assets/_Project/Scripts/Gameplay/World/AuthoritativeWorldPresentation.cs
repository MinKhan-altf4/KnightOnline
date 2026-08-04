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
        private readonly List<GameObject> _objects = new();
        private IEventBus _events;
        private NetworkClient _network;
        private IDisposable _npcSubscription, _portalSubscription;

        public void Initialize(IEventBus events, NetworkClient network)
        {
            _events = events;
            _network = network;
            _npcSubscription = events.Subscribe<NpcListReceivedEvent>(OnNpcs);
            _portalSubscription = events.Subscribe<PortalListReceivedEvent>(OnPortals);
        }

        private void OnNpcs(NpcListReceivedEvent value)
        {
            Clear();
            foreach (NpcSnapshotData npc in value.Npcs)
                CreateLabel($"NPC: {npc.DisplayName}", npc.PositionX,
                    npc.PositionY, new Color(0.9f, 0.45f, 0.8f), () =>
                        _network.SendInteractNpcRequestAsync(npc.DefinitionId).Forget());
        }

        private void OnPortals(PortalListReceivedEvent value)
        {
            CreateMapBoundary(value.MinimumX, value.MaximumX,
                value.MinimumY, value.MaximumY);
            foreach (PortalSnapshotData portal in value.Portals)
                CreatePortal(portal);
        }

        private void CreateMapBoundary(float minimumX, float maximumX,
            float minimumY, float maximumY)
        {
            if (minimumX >= maximumX || minimumY >= maximumY) return;
            var boundary = new GameObject("AuthoritativeMapBoundary");
            boundary.transform.SetParent(transform, false);
            var edge = boundary.AddComponent<EdgeCollider2D>();
            edge.points = new[]
            {
                new Vector2(minimumX, minimumY),
                new Vector2(maximumX, minimumY),
                new Vector2(maximumX, maximumY),
                new Vector2(minimumX, maximumY),
                new Vector2(minimumX, minimumY),
            };
            _objects.Add(boundary);
        }

        private void CreatePortal(PortalSnapshotData portal)
        {
            GameObject entity = CreateLabel(portal.DisplayName, portal.X,
                portal.Y, new Color(0.2f, 0.8f, 1f), null);
            BoxCollider2D collider = entity.GetComponent<BoxCollider2D>();
            collider.isTrigger = true;
            WorldClickRelay click = entity.GetComponent<WorldClickRelay>();
            if (click != null) Destroy(click);
            entity.AddComponent<PortalTriggerRelay>().Initialize(() =>
                _network.SendUsePortalRequestAsync(
                    portal.DefinitionId).Forget());
        }

        private GameObject CreateLabel(string label, float x, float y, Color color,
            Action clicked)
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
            var collider = entity.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3f, 1.2f);
            if (clicked != null)
                entity.AddComponent<WorldClickRelay>().Initialize(clicked);
            _objects.Add(entity);
            return entity;
        }

        private void Clear()
        {
            foreach (GameObject value in _objects)
                if (value != null) Destroy(value);
            _objects.Clear();
        }
        private void OnDestroy()
        { _npcSubscription?.Dispose(); _portalSubscription?.Dispose(); }
    }

    public sealed class PortalTriggerRelay : MonoBehaviour
    {
        private Action _entered;
        public void Initialize(Action entered) => _entered = entered;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
                _entered?.Invoke();
        }
    }

    public sealed class WorldClickRelay : MonoBehaviour
    {
        private Action _clicked;
        public void Initialize(Action clicked) => _clicked = clicked;
        private void OnMouseDown() => _clicked?.Invoke();
    }
}
