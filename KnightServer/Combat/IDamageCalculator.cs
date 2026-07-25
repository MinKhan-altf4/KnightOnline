namespace KnightOnline.Server.Combat;

public interface IDamageCalculator
{
    int Calculate(CombatStats attacker);
}
