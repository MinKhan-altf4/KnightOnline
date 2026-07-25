using System;
using System.Collections.Generic;
using KnightOnline.Client.Core.Events;
using KnightOnline.Client.Data.Events;
using KnightOnline.Client.Data.Models;
using KnightOnline.Client.Gameplay.Targeting;
using UnityEngine;
using VContainer;

namespace KnightOnline.Client.Gameplay.Monster
{
    /// <summary>
    /// Presenter for monster snapshots. It keeps one view per server monster id,
    /// updates existing views, and removes views no longer present or alive.
    /// </summary>
    public sealed class MonsterSpawner : MonoBehaviour
    {
        [Serializable]
        private struct PrefabMapping
        {
            public int DefinitionId;
            public MonsterView Prefab;
        }

        [Header("Prefab")]
        [SerializeField] private MonsterView _defaultPrefab;
        [SerializeField] private PrefabMapping[] _prefabsByDefinition;
        [SerializeField] private Transform _container;

        private readonly Dictionary<int, MonsterView> _views = new();
        private readonly Dictionary<int, MonsterView> _prefabs = new();
        private IDisposable _subscription;
        private IEventBus _eventBus;
        private TargetSelectionService _targetSelection;

        [Inject]
        public void Construct(IEventBus eventBus, TargetSelectionService targetSelection)
        {
            _eventBus = eventBus;
            _targetSelection = targetSelection;
            _subscription = eventBus.Subscribe<MonsterListReceivedEvent>(OnMonsterListReceived);
        }

        private void Awake()
        {
            if (_container == null)
                _container = transform;

            _prefabs.Clear();
            if (_prefabsByDefinition == null)
                return;

            foreach (var mapping in _prefabsByDefinition)
            {
                if (mapping.Prefab != null)
                    _prefabs[mapping.DefinitionId] = mapping.Prefab;
            }
        }

        private void OnMonsterListReceived(MonsterListReceivedEvent gameEvent)
        {
            var receivedIds = new HashSet<int>();

            if (gameEvent.Monsters != null)
            {
                foreach (var monster in gameEvent.Monsters)
                {
                    if (monster == null || !monster.IsAlive)
                        continue;

                    receivedIds.Add(monster.MonsterId);
                    Upsert(monster);
                }
            }

            var idsToRemove = new List<int>();
            foreach (var pair in _views)
            {
                if (!receivedIds.Contains(pair.Key))
                    idsToRemove.Add(pair.Key);
            }

            foreach (var monsterId in idsToRemove)
                Remove(monsterId);
        }

        private void Upsert(MonsterData monster)
        {
            if (!_views.TryGetValue(monster.MonsterId, out var view) || view == null)
            {
                var prefab = ResolvePrefab(monster.DefinitionId);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[Monster] No prefab configured for definition {monster.DefinitionId}; " +
                        $"cannot spawn monster {monster.MonsterId}.");
                    return;
                }

                view = Instantiate(prefab, _container);
                view.Clicked += OnMonsterClicked;
                _views[monster.MonsterId] = view;
            }

            view.Render(monster);
        }

        private MonsterView ResolvePrefab(int definitionId) =>
            _prefabs.TryGetValue(definitionId, out var prefab) ? prefab : _defaultPrefab;

        private void OnMonsterClicked(MonsterView view)
        {
            if (view == null || view.Data == null)
                return;

            _targetSelection.Select(view);
            _eventBus.Publish(new MonsterSelectedEvent(view.Data));
            Debug.Log($"[Monster] Selected {view.Data.MonsterName} (ID: {view.MonsterId}).");
        }

        private void Remove(int monsterId)
        {
            if (!_views.Remove(monsterId, out var view) || view == null)
                return;

            view.Clicked -= OnMonsterClicked;
            _targetSelection.ClearIfSelected(view);
            Destroy(view.gameObject);
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            _subscription = null;

            foreach (var view in _views.Values)
            {
                if (view != null)
                    view.Clicked -= OnMonsterClicked;
            }
        }
    }
}
