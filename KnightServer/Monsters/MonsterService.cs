namespace KnightOnline.Server.Monsters;

public sealed class MonsterService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<int, Monster> _monsters = [];
    private int _nextMonsterId = 1;

    public int Spawn(
        MonsterDefinition definition,
        WorldPosition spawnPosition)
    {
        lock (_syncRoot)
        {
            int monsterId = _nextMonsterId++;
            _monsters.Add(
                monsterId,
                new Monster(monsterId, definition, spawnPosition));
            return monsterId;
        }
    }

    public IReadOnlyList<MonsterSnapshot> GetSnapshots()
    {
        lock (_syncRoot)
        {
            return _monsters.Values
                .OrderBy(monster => monster.MonsterId)
                .Select(monster => monster.CreateSnapshot())
                .ToArray();
        }
    }

    public MonsterSnapshot? GetSnapshot(int monsterId)
    {
        lock (_syncRoot)
        {
            return _monsters.TryGetValue(monsterId, out Monster? monster)
                ? monster.CreateSnapshot()
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
