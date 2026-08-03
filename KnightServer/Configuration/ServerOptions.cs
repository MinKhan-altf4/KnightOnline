using System.Text.Json;

namespace KnightOnline.Server.Configuration;

public sealed class ServerOptions
{
    public string Environment { get; set; } = "Development";
    public NetworkOptions Network { get; set; } = new();
    public AuthenticationOptions Authentication { get; set; } = new();
    public CapacityOptions Capacity { get; set; } = new();
    public RegistrationOptions Registration { get; set; } = new();
    public GuestOptions Guest { get; set; } = new();
    public CharacterOptions Characters { get; set; } = new();
    public CombatOptions Combat { get; set; } = new();
    public ProgressionOptions Progression { get; set; } = new();
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
        if (Capacity.MaximumActiveAccounts <= 0 ||
            Capacity.MaximumTransportConnections <
                Capacity.MaximumActiveAccounts)
        {
            throw new InvalidDataException(
                "Capacity requires a positive active-account limit and a " +
                "transport limit greater than or equal to it.");
        }
        if (Authentication.RefreshTokenLifetimeDays <= 0)
            throw new InvalidDataException(
                "Authentication.RefreshTokenLifetimeDays must be positive.");
        if (Authentication.MaximumAttemptsPerWindow <= 0 ||
            Authentication.AttemptWindowSeconds <= 0)
            throw new InvalidDataException(
                "Authentication rate-limit values must be positive.");
        if (Authentication.HeartbeatIntervalSeconds <= 0 ||
            Authentication.SessionLeaseTtlSeconds <=
                Authentication.HeartbeatIntervalSeconds ||
            Authentication.DisconnectGraceSeconds < 0 ||
            Authentication.DisconnectGraceSeconds >=
                Authentication.SessionLeaseTtlSeconds)
        {
            throw new InvalidDataException(
                "Authentication session lease requires a positive heartbeat, " +
                "TTL greater than heartbeat, and grace in [0, TTL).");
        }
        if (Registration.TransactionLifetimeMinutes <= 0)
            throw new InvalidDataException(
                "Registration.TransactionLifetimeMinutes must be positive.");
        if (string.IsNullOrWhiteSpace(Registration.PortalBaseUrl))
            throw new InvalidDataException(
                "Registration.PortalBaseUrl is required.");
        if (!isDevelopment &&
            Registration.DevelopmentCompletionEnabled)
            throw new InvalidDataException(
                "Development registration completion must be disabled " +
                "outside Development.");
        if (Guest.MaximumLevel <= 0)
            throw new InvalidDataException(
                "Guest.MaximumLevel must be positive.");
        if (Characters.MaximumPerAccount <= 0)
            throw new InvalidDataException("Characters.MaximumPerAccount must be positive.");
        if (Characters.MaximumPerAccount != 3)
            throw new InvalidDataException(
                "Character flow currently requires exactly three slots.");
        if (Characters.InitialLevel <= 0)
            throw new InvalidDataException("Characters.InitialLevel must be positive.");
        if (Characters.InitialMaximumHealth <= 0 || Characters.MoveSpeed <= 0)
            throw new InvalidDataException(
                "Character health and move speed must be positive.");
        if (string.IsNullOrWhiteSpace(Characters.DevelopmentAccountKey))
            throw new InvalidDataException("Characters.DevelopmentAccountKey is required.");
        if (string.IsNullOrWhiteSpace(Characters.ServerId) ||
            Characters.CatalogVersion <= 0 ||
            string.IsNullOrWhiteSpace(Characters.StartingMapDefinitionId) ||
            string.IsNullOrWhiteSpace(Characters.StartingSpawnPointId) ||
            string.IsNullOrWhiteSpace(
                Characters.StartingTutorialDefinitionId) ||
            string.IsNullOrWhiteSpace(
                Characters.StartingTutorialStepDefinitionId))
        {
            throw new InvalidDataException(
                "Character server, catalog, starter spawn and tutorial " +
                "settings are required.");
        }
        ValidateCharacterCatalog(Characters);
        if (Combat.BaseAttackDamage <= 0)
            throw new InvalidDataException("Combat.BaseAttackDamage must be positive.");
        if (Combat.AttackRange <= 0 || Combat.AttackCooldownMilliseconds <= 0)
            throw new InvalidDataException(
                "Combat attack range and cooldown must be positive.");
        Progression.Validate();
        if (Characters.InitialLevel > Progression.MaximumLevel)
        {
            throw new InvalidDataException(
                "Characters.InitialLevel cannot exceed " +
                "Progression.MaximumLevel.");
        }
        if (Guest.MaximumLevel > Progression.MaximumLevel)
        {
            throw new InvalidDataException(
                "Guest.MaximumLevel cannot exceed Progression.MaximumLevel.");
        }
        if (World.TickMilliseconds <= 0)
            throw new InvalidDataException("World.TickMilliseconds must be positive.");
        if (World.MaximumMovementDeltaMilliseconds <= 0)
            throw new InvalidDataException(
                "World.MaximumMovementDeltaMilliseconds must be positive.");
        if (World.PlayerCollisionRadius <= 0 ||
            World.MonsterCollisionRadius <= 0)
        {
            throw new InvalidDataException(
                "World collision radii must be positive.");
        }
        if (World.RespawnDisplacementAngularSamples < 8)
            throw new InvalidDataException(
                "World.RespawnDisplacementAngularSamples must be at least 8.");
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
        if (MonsterSpawns.Any(spawn =>
                string.IsNullOrWhiteSpace(spawn.MapDefinitionId)))
            throw new InvalidDataException(
                "Every monster spawn must reference a map definition.");
    }

    private static void ValidateCharacterCatalog(CharacterOptions characters)
    {
        if (characters.Classes.Count == 0 ||
            characters.BodyTypes.Count == 0 ||
            characters.AppearanceOptions.Count == 0)
        {
            throw new InvalidDataException(
                "Character creation catalog cannot be empty.");
        }

        EnsureUnique(
            characters.Classes.Select(value => value.DefinitionId),
            "character class");
        EnsureUnique(
            characters.BodyTypes.Select(value => value.DefinitionId),
            "body type");
        EnsureUnique(
            characters.AppearanceOptions.Select(value => value.DefinitionId),
            "appearance");

        var bodyIds = characters.BodyTypes
            .Select(value => value.DefinitionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (CharacterClassOptions classDefinition in characters.Classes)
        {
            if (string.IsNullOrWhiteSpace(classDefinition.DefinitionId) ||
                string.IsNullOrWhiteSpace(classDefinition.DisplayName) ||
                classDefinition.AllowedBodyTypeIds.Count == 0 ||
                classDefinition.AllowedBodyTypeIds.Any(id => !bodyIds.Contains(id)) ||
                classDefinition.BaseStats.MaximumHealth <= 0 ||
                classDefinition.BaseStats.MaximumMana < 0 ||
                classDefinition.BaseStats.Attack <= 0 ||
                classDefinition.BaseStats.Defense < 0 ||
                classDefinition.PerLevelGrowth.MaximumHealth < 0 ||
                classDefinition.PerLevelGrowth.MaximumMana < 0 ||
                classDefinition.PerLevelGrowth.Attack < 0 ||
                classDefinition.PerLevelGrowth.Defense < 0)
            {
                throw new InvalidDataException(
                    $"Invalid class definition '{classDefinition.DefinitionId}'.");
            }
        }

        if (characters.RequiredStarterAppearanceSlotIds.Count == 0)
        {
            throw new InvalidDataException(
                "At least one starter appearance slot is required.");
        }
        EnsureUnique(
            characters.RequiredStarterAppearanceSlotIds,
            "required starter appearance slot");
        foreach (string slot in
                 characters.RequiredStarterAppearanceSlotIds)
        {
            if (!characters.AppearanceOptions.Any(
                    value =>
                        value.IsStarterOption &&
                        string.Equals(
                            value.SlotDefinitionId,
                            slot,
                            StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException(
                    $"Missing starter appearance for slot '{slot}'.");
            }
        }
    }

    private static void EnsureUnique(
        IEnumerable<string> ids,
        string label)
    {
        string[] values = ids.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            values.Length)
        {
            throw new InvalidDataException(
                $"{label} definition ids must be non-empty and unique.");
        }
    }
}

public sealed class NetworkOptions
{
    public int Port { get; set; }
    public int MaximumPacketBytes { get; set; }
}

public sealed class CapacityOptions
{
    public int MaximumActiveAccounts { get; set; } = 500;
    public int MaximumTransportConnections { get; set; } = 750;
}

public sealed class AuthenticationOptions
{
    public bool DevelopmentBypassEnabled { get; set; }
    public int RefreshTokenLifetimeDays { get; set; } = 30;
    public int MaximumAttemptsPerWindow { get; set; } = 10;
    public int AttemptWindowSeconds { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int SessionLeaseTtlSeconds { get; set; } = 20;
    public int DisconnectGraceSeconds { get; set; } = 10;
}

public sealed class GuestOptions
{
    public int MaximumLevel { get; set; } = 10;
    public List<string> DisabledFeatures { get; set; } = [];
}

public sealed class RegistrationOptions
{
    public string PortalBaseUrl { get; set; } =
        "https://account.example.com/register";
    public int TransactionLifetimeMinutes { get; set; } = 15;
    public bool DevelopmentCompletionEnabled { get; set; } = true;
}

public sealed class CharacterOptions
{
    public string ServerId { get; set; } = "server-1";
    public string DevelopmentAccountKey { get; set; } = string.Empty;
    public int MaximumPerAccount { get; set; }
    public int InitialLevel { get; set; }
    public int InitialMaximumHealth { get; set; }
    public float MoveSpeed { get; set; }
    public float SpawnPositionX { get; set; }
    public float SpawnPositionY { get; set; }
    public int CatalogVersion { get; set; } = 1;
    public string StartingMapDefinitionId { get; set; } = "tutorial_map_01";
    public string StartingSpawnPointId { get; set; } =
        "tutorial_spawn_default";
    public string StartingTutorialDefinitionId { get; set; } =
        "starter_tutorial_v1";
    public string StartingTutorialStepDefinitionId { get; set; } =
        "welcome";
    public List<string> RequiredStarterAppearanceSlotIds { get; set; } =
        ["base_body", "hair", "bottom", "expression"];
    public List<CharacterClassOptions> Classes { get; set; } = [];
    public List<BodyTypeOptions> BodyTypes { get; set; } = [];
    public List<AppearanceDefinitionOptions> AppearanceOptions { get; set; } = [];
}

public sealed class CharacterClassOptions
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AllowedBodyTypeIds { get; set; } = [];
    public string PreviewAssetAddress { get; set; } = string.Empty;
    public CharacterBaseStatsOptions BaseStats { get; set; } = new();
    public CharacterBaseStatsOptions PerLevelGrowth { get; set; } = new();
}

public sealed class CharacterBaseStatsOptions
{
    public int MaximumHealth { get; set; }
    public int MaximumMana { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
}

public sealed class ProgressionOptions
{
    public int MaximumLevel { get; set; } = 40;
    public long BaseExperienceToNextLevel { get; set; } = 100;
    public long LinearExperienceGrowth { get; set; } = 25;
    public long QuadraticExperienceGrowth { get; set; } = 5;

    internal void Validate()
    {
        if (MaximumLevel < 2 ||
            BaseExperienceToNextLevel <= 0 ||
            LinearExperienceGrowth < 0 ||
            QuadraticExperienceGrowth < 0)
        {
            throw new InvalidDataException(
                "Progression configuration is invalid.");
        }
    }
}

public sealed class BodyTypeOptions
{
    public string DefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class AppearanceDefinitionOptions
{
    public string DefinitionId { get; set; } = string.Empty;
    public string SlotDefinitionId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> AllowedBodyTypeIds { get; set; } = [];
    public List<string> AllowedClassDefinitionIds { get; set; } = [];
    public string AssetAddress { get; set; } = string.Empty;
    public bool IsStarterOption { get; set; }
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
    public float PlayerCollisionRadius { get; set; } = 0.35f;
    public float MonsterCollisionRadius { get; set; } = 0.5f;
    public int RespawnDisplacementAngularSamples { get; set; } = 32;
}

public sealed class MonsterDefinitionOptions
{
    public int DefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int MaximumHealth { get; set; }
    public double RespawnSeconds { get; set; }
    public int ExperienceReward { get; set; }

    internal void Validate()
    {
        if (DefinitionId <= 0 || Level <= 0 || MaximumHealth <= 0 ||
            ExperienceReward <= 0)
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
    public string MapDefinitionId { get; set; } = string.Empty;
    public float PositionX { get; set; }
    public float PositionY { get; set; }
}
