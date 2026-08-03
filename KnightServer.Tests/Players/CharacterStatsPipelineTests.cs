using KnightOnline.Server.Configuration;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Tests.Players;

public sealed class CharacterStatsPipelineTests
{
    [Fact]
    public void Calculate_ComposesBaseGrowthEquipmentAndBuffModifiers()
    {
        var options = new CharacterOptions
        {
            Classes =
            [
                new CharacterClassOptions
                {
                    DefinitionId = "warrior",
                    BaseStats = new CharacterBaseStatsOptions
                    {
                        MaximumHealth = 100,
                        MaximumMana = 20,
                        Attack = 10,
                        Defense = 5,
                    },
                    PerLevelGrowth = new CharacterBaseStatsOptions
                    {
                        MaximumHealth = 10,
                        MaximumMana = 2,
                        Attack = 2,
                        Defense = 1,
                    },
                },
            ],
        };
        var pipeline = new CharacterStatsPipeline(options);

        CharacterStats stats = pipeline.Calculate(
            "warrior",
            level: 3,
            [
                new CharacterStatModifier(
                    "equipment:sword-1",
                    CharacterStatType.Attack,
                    CharacterStatModifierOperation.Add,
                    5),
                new CharacterStatModifier(
                    "buff:attack-10-percent",
                    CharacterStatType.Attack,
                    CharacterStatModifierOperation.Multiply,
                    1.1f),
            ]);

        Assert.Equal(120, stats.MaximumHealth);
        Assert.Equal(24, stats.MaximumMana);
        Assert.Equal(21, stats.Attack);
        Assert.Equal(7, stats.Defense);
    }
}
