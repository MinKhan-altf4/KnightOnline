using KnightOnline.Server.Time;

namespace KnightOnline.Server.Monsters;

public sealed class MonsterService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<int, Monster> _monsters = [];
    private readonly IServerClock _clock;
    private readonly MonsterWanderPolicy? _wander;
    private readonly Random _random;
    private readonly Func<string, WorldPosition, WorldPosition> _clampToMap;
    private int _nextMonsterId = 1;

    /// <param name="clock">Time used for live monster positions in snapshots.</param>
    /// <param name="wander">Null keeps monsters standing on their spawn point.</param>
    /// <param name="random">Seedable for tests.</param>
    /// <param name="clampToMap">Keeps wander targets inside the map's walkable bounds.</param>
    public MonsterService(
        IServerClock? clock = null,
        MonsterWanderPolicy? wander = null,
        Random? random = null,
        Func<string, WorldPosition, WorldPosition>? clampToMap = null)
    {
        _clock = clock ?? new SystemServerClock();
        _wander = wander;
        _random = random ?? new Random();
        _clampToMap = clampToMap ?? ((_, position) => position);
    }

    public int Spawn(
        MonsterDefinition definition,
        string mapDefinitionId,
        WorldPosition spawnPosition)
    {
        lock (_syncRoot)
        {
            int monsterId = _nextMonsterId++;
            _monsters.Add(
                monsterId,
                new Monster(
                    monsterId,
                    definition,
                    mapDefinitionId,
                    spawnPosition));
            return monsterId;
        }
    }

    public IReadOnlyList<MonsterSnapshot> GetSnapshots()
    {
        DateTime utcNow = _clock.UtcNow;
        lock (_syncRoot)
        {
            return _monsters.Values
                .OrderBy(monster => monster.MonsterId)
                .Select(monster => monster.CreateSnapshot(utcNow))
                .ToArray();
        }
    }

    public MonsterSnapshot? GetSnapshot(int monsterId)
    {
        lock (_syncRoot)
        {
            return _monsters.TryGetValue(monsterId, out Monster? monster)
                ? monster.CreateSnapshot(_clock.UtcNow)
                : null;
        }
    }

    public MonsterDamageResult ApplyDamage(
        int monsterId,
        int damage,
        DateTime utcNow)
    {
        lock (_syncRoot)
        {
            if (!_monsters.TryGetValue(monsterId, out Monster? monster))
            {
                return new MonsterDamageResult(
                    MonsterDamageStatus.MonsterNotFound,
                    monsterId,
                    0,
                    0,
                    false);
            }

            return monster.ApplyDamage(damage, utcNow);
        }
    }

    /// <summary>
    /// Starts wander legs for alive monsters whose rest is over. Call it
    /// regularly (monster loop); each returned leg must be broadcast to the
    /// monster's map so clients can replay it.
    /// </summary>
    public IReadOnlyList<MonsterMovementLeg> AdvanceWander(DateTime utcNow)
    {
        if (_wander == null)
            return Array.Empty<MonsterMovementLeg>();

        lock (_syncRoot)
        {
            List<MonsterMovementLeg>? started = null;
            foreach (Monster monster in _monsters.Values)
            {
                MonsterMovementLeg? leg = monster.TryStartWanderLeg(
                    utcNow, _wander, _random, _clampToMap);
                if (leg != null)
                    (started ??= new List<MonsterMovementLeg>()).Add(leg.Value);
            }
            return started ?? (IReadOnlyList<MonsterMovementLeg>)Array.Empty<MonsterMovementLeg>();
        }
    }

    public IReadOnlyList<int> RespawnReadyMonsters(DateTime utcNow)
    {
        lock (_syncRoot)
        {
            return _monsters.Values
                .Where(monster => monster.TryRespawn(utcNow))
                .Select(monster => monster.MonsterId)
                .ToArray();
        }
    }
}
