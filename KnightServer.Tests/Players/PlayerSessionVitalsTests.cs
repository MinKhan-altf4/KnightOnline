using System.Numerics;
using KnightOnline.Server.Players;

namespace KnightOnline.Server.Tests.Players;

public sealed class PlayerSessionVitalsTests
{
    [Fact]
    public void DamageAndHealing_AreClampedAndProduceOrderedSnapshots()
    {
        PlayerSession session = CreateSession();

        PlayerVitalsState damaged = session.ApplyDamage(150);
        PlayerVitalsState healed = session.ApplyHealing(500);

        Assert.Equal(PlayerVitalsChange.Damage, damaged.Reason);
        Assert.Equal(0, damaged.CurrentHealth);
        Assert.Equal(PlayerVitalsChange.Healing, healed.Reason);
        Assert.Equal(100, healed.CurrentHealth);
        Assert.True(healed.Sequence > damaged.Sequence);
    }

    [Fact]
    public void ManaMutation_RejectsInsufficientBalanceWithoutNewSequence()
    {
        PlayerSession session = CreateSession();

        Assert.False(session.TrySpendMana(31, out PlayerVitalsState rejected));
        Assert.Equal(30, rejected.CurrentMana);
        Assert.Equal(0, rejected.Sequence);

        Assert.True(session.TrySpendMana(20, out PlayerVitalsState spent));
        PlayerVitalsState restored = session.RestoreMana(100);

        Assert.Equal(10, spent.CurrentMana);
        Assert.Equal(30, restored.CurrentMana);
        Assert.True(restored.Sequence > spent.Sequence);
    }

    [Fact]
    public void NegativeMutations_DoNotIncreaseResources()
    {
        PlayerSession session = CreateSession();
        PlayerVitalsState damage = session.ApplyDamage(-10);
        PlayerVitalsState healing = session.ApplyHealing(-10);
        PlayerVitalsState mana = session.RestoreMana(-10);

        Assert.Equal(100, damage.CurrentHealth);
        Assert.Equal(100, healing.CurrentHealth);
        Assert.Equal(30, mana.CurrentMana);
    }

    [Fact]
    public void LargeHealingAndManaRestore_DoNotOverflow()
    {
        PlayerSession session = CreateSession();
        session.ApplyDamage(50);
        session.TrySpendMana(20, out _);

        PlayerVitalsState healed = session.ApplyHealing(int.MaxValue);
        PlayerVitalsState restored = session.RestoreMana(int.MaxValue);

        Assert.Equal(100, healed.CurrentHealth);
        Assert.Equal(30, restored.CurrentMana);
    }

    private static PlayerSession CreateSession() =>
        new(
            new PlayerSessionProfile(
                1,
                "Vitals Test",
                1,
                1,
                "warrior",
                "male",
                "tutorial-map",
                "spawn-1",
                []),
            currentHealth: 100,
            maximumHealth: 100,
            moveSpeed: 4f,
            spawnPosition: Vector2.Zero,
            baseAttack: 10,
            maximumMovementDelta: TimeSpan.FromSeconds(1),
            new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc),
            maximumMana: 30);
}
