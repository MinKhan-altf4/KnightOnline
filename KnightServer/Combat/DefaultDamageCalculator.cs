namespace KnightOnline.Server.Combat;

public sealed class DefaultDamageCalculator : IDamageCalculator
{
    public int Calculate(CombatStats attacker) => attacker.TotalAttack;
}
