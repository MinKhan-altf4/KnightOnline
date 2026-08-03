using KnightOnline.Server.Configuration;

namespace KnightOnline.Server.Players;

public enum CharacterStatType : byte
{
    MaximumHealth = 0,
    MaximumMana = 1,
    Attack = 2,
    Defense = 3,
}

public enum CharacterStatModifierOperation : byte
{
    Add = 0,
    Multiply = 1,
}

public readonly record struct CharacterStatModifier(
    string SourceId,
    CharacterStatType Stat,
    CharacterStatModifierOperation Operation,
    float Value);

public readonly record struct CharacterStats(
    int MaximumHealth,
    int MaximumMana,
    int Attack,
    int Defense);

public sealed class CharacterStatsPipeline(CharacterOptions options)
{
    private readonly IReadOnlyDictionary<string, CharacterClassOptions>
        _classes = options.Classes.ToDictionary(
            value => value.DefinitionId,
            StringComparer.Ordinal);

    public CharacterStats Calculate(
        string classDefinitionId,
        int level,
        IEnumerable<CharacterStatModifier>? modifiers = null)
    {
        if (!_classes.TryGetValue(classDefinitionId, out var definition))
            throw new InvalidOperationException(
                $"Unknown character class '{classDefinitionId}'.");
        if (level <= 0)
            throw new ArgumentOutOfRangeException(nameof(level));

        int growthLevels = level - 1;
        var values = new Dictionary<CharacterStatType, float>
        {
            [CharacterStatType.MaximumHealth] =
                definition.BaseStats.MaximumHealth +
                definition.PerLevelGrowth.MaximumHealth * growthLevels,
            [CharacterStatType.MaximumMana] =
                definition.BaseStats.MaximumMana +
                definition.PerLevelGrowth.MaximumMana * growthLevels,
            [CharacterStatType.Attack] =
                definition.BaseStats.Attack +
                definition.PerLevelGrowth.Attack * growthLevels,
            [CharacterStatType.Defense] =
                definition.BaseStats.Defense +
                definition.PerLevelGrowth.Defense * growthLevels,
        };

        foreach (CharacterStatModifier modifier in
                 modifiers ?? Array.Empty<CharacterStatModifier>())
        {
            values[modifier.Stat] = modifier.Operation switch
            {
                CharacterStatModifierOperation.Add =>
                    values[modifier.Stat] + modifier.Value,
                CharacterStatModifierOperation.Multiply =>
                    values[modifier.Stat] * modifier.Value,
                _ => throw new InvalidOperationException(
                    $"Unknown stat modifier operation {modifier.Operation}."),
            };
        }

        return new CharacterStats(
            Math.Max(1, (int)MathF.Round(values[CharacterStatType.MaximumHealth])),
            Math.Max(0, (int)MathF.Round(values[CharacterStatType.MaximumMana])),
            Math.Max(1, (int)MathF.Round(values[CharacterStatType.Attack])),
            Math.Max(0, (int)MathF.Round(values[CharacterStatType.Defense])));
    }
}
