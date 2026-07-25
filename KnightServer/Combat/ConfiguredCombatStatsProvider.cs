namespace KnightOnline.Server.Combat;

/// <summary>
/// Bootstrap provider until character equipment is persisted. Replace this
/// implementation with a repository-backed provider without changing combat flow.
/// </summary>
public sealed class ConfiguredCombatStatsProvider(int baseAttackDamage)
    : ICombatStatsProvider
{
    public CombatStats GetFor(Networking.ClientConnection attacker) =>
        new(
            BaseAttack: attacker.PlayerSession?.BaseAttack ?? baseAttackDamage,
            EquipmentAttack: 0,
            BuffAttack: 0);
}
