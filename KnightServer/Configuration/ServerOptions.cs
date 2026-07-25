using System.Text.Json;

namespace KnightOnline.Server.Configuration;

public sealed class ServerOptions
{
    public string Environment { get; set; } = "Development";
    public NetworkOptions Network { get; set; } = new();
    public AuthenticationOptions Authentication { get; set; } = new();
    public GuestOptions Guest { get; set; } = new();
    public CharacterOptions Characters { get; set; } = new();
    public CombatOptions Combat { get; set; } = new();
    public WorldOptions World { get; set; } = new();
    public List<MonsterDefinitionOptions> MonsterDefinitions { get; set; } = [];
    public List<MonsterSpawnOptions> MonsterSpawns { get; set; } = [];

    public static ServerOptions Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("Missing server settings file.", path);

        ServerOptions options =
            JsonSerializer.Deserialize<ServerOptions>(
                File.ReadAllText(path),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                })
            ?? throw new InvalidDataException("Server settings are empty.");

        options.Validate();
        return options;
    }

    private void Validate()
    {
        bool isDevelopment = string.Equals(
            Environment,
            "Development",
            StringComparison.OrdinalIgnoreCase);
        if (!isDevelopment)
        {
            throw new InvalidDataException(
                "Only the Development environment is currently allowed. " +
                "TLS transport and production secure storage must be " +
                "implemented before Staging or Production can start.");
        }

        if (Network.Port is <= 0 or > 65535)
            throw new InvalidDataException("Network.Port must be between 1 and 65535.");
        if (Network.MaximumPacketBytes <= 0)
            throw new InvalidDataException("Network.MaximumPacketBytes must be positive.");
        if (Authentication.RefreshTokenLifetimeDays <= 0)
            throw new InvalidDataException(
                "Authentication.RefreshTokenLifetimeDays must be positive.");
        if (Authentication.MaximumAttemptsPerWindow <= 0 ||
            Authentication.AttemptWindowSeconds <= 0)
            throw new InvalidDataException(
                "Authentication rate-limit values must be positive.");
        if (Guest.MaximumLevel <= 0)
            throw new InvalidDataException(
                "Guest.MaximumLevel must be positive.");
        if (Characters.MaximumPerAccount <= 0)
            throw new InvalidDataException("Characters.MaximumPerAccount must be positive.");
        if (Characters.InitialLevel <= 0)
            throw new InvalidDataException("Characters.InitialLevel must be positive.");
        if (Characters.InitialMaximumHealth <= 0 || Characters.MoveSpeed <= 0)
            throw new InvalidDataException(
                "Character health and move speed must be positive.");
        if (string.IsNullOrWhiteSpace(Characters.DevelopmentAccountKey))
            throw new InvalidDataException("Characters.DevelopmentAccountKey is required.");
        if (Combat.BaseAttackDamage <= 0)
            throw new InvalidDataException("Combat.BaseAttackDamage must be positive.");
        if (Combat.AttackRange <= 0 || Combat.AttackCooldownMilliseconds <= 0)
            throw new InvalidDataException(
                "Combat attack range and cooldown must be positive.");
        if (World.TickMilliseconds <= 0)
            throw new InvalidDataException("World.TickMilliseconds must be positive.");
        if (World.MaximumMovementDeltaMilliseconds <= 0)
            throw new InvalidDataException(
                "World.MaximumMovementDeltaMilliseconds must be positive.");
        if (MonsterDefinitions.Count == 0)
            throw new InvalidDataException("At least one MonsterDefinitions entry is required.");
        if (MonsterSpawns.Count == 0)
            throw new InvalidDataException("At least one MonsterSpawns entry is required.");

        foreach (MonsterDefinitionOptions definition in MonsterDefinitions)
            definition.Validate();

        if (MonsterDefinitions.Select(x => x.DefinitionId).Distinct().Count() !=
            MonsterDefinitions.Count)
            throw new InvalidDataException("Monster definition ids must be unique.");

        HashSet<int> definitionIds =
            MonsterDefinitions.Select(x => x.DefinitionId).ToHashSet();
        if (MonsterSpawns.Any(spawn => !definitionIds.Contains(spawn.DefinitionId)))
            throw new InvalidDataException(
                "Every monster spawn must reference an existing definition.");
    }
}

public sealed class NetworkOptions
{
    public int Port { get; set; }
    public int MaximumPacketBytes { get; set; }
}

public sealed class AuthenticationOptions
{
    public bool DevelopmentBypassEnabled { get; set; }
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public int MaximumAttemptsPerWindow { get; set; } = 10;
    public int AttemptWindowSeconds { get; set; } = 60;
}

public sealed class GuestOptions
{
    public int MaximumLevel { get; set; } = 10;
    public List<string> DisabledFeatures { get; set; } = [];
}

public sealed class CharacterOptions
{
    public string DevelopmentAccountKey { get; set; } = string.Empty;
    public int MaximumPerAccount { get; set; }
    public int InitialLevel { get; set; }
    public int InitialMaximumHealth { get; set; }
    public float MoveSpeed { get; set; }
    public float SpawnPositionX { get; set; }
    public float SpawnPositionY { get; set; }
}

public sealed class CombatOptions
{
    public int BaseAttackDamage { get; set; }
    public float AttackRange { get; set; }
    public int AttackCooldownMilliseconds { get; set; }
}

public sealed class WorldOptions
{
    public int TickMilliseconds { get; set; }
    public int MaximumMovementDeltaMilliseconds { get; set; }
}

public sealed class MonsterDefinitionOptions
{
    public int DefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int MaximumHealth { get; set; }
    public double RespawnSeconds { get; set; }

    internal void Validate()
    {
        if (DefinitionId <= 0 || Level <= 0 || MaximumHealth <= 0)
            throw new InvalidDataException(
                "Monster definition id, level and maximum health must be positive.");
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("Monster name is required.");
        if (RespawnSeconds < 0)
            throw new InvalidDataException("Monster respawn seconds cannot be negative.");
    }
}

public sealed class MonsterSpawnOptions
{
    public int DefinitionId { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }
}
