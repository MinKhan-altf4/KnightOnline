namespace KnightOnline.Server.Monsters;

public sealed record MonsterDefinition
{
    public MonsterDefinition(
        int definitionId,
        string name,
        int level,
        int maximumHealth,
        TimeSpan respawnDelay)
    {
        if (definitionId <= 0)
            throw new ArgumentOutOfRangeException(nameof(definitionId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Monster name is required.", nameof(name));
        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level));
        if (maximumHealth <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumHealth));
        if (respawnDelay < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(respawnDelay));

        DefinitionId = definitionId;
        Name = name.Trim();
        Level = level;
        MaximumHealth = maximumHealth;
        RespawnDelay = respawnDelay;
    }

    public int DefinitionId { get; }
    public string Name { get; }
    public int Level { get; }
    public int MaximumHealth { get; }
    public TimeSpan RespawnDelay { get; }
}
