using System;

namespace KnightOnline.Client.Data.Models
{
    /// <summary>
    /// Client fallbacks used only until these stats are included in server snapshots.
    /// Kept in scene configuration so presentation prototypes do not hard-code them.
    /// </summary>
    public sealed class ClientGameplaySettings
    {
        public ClientGameplaySettings(
            int initialLevel,
            int initialMaximumHealth,
            float defaultMoveSpeed)
        {
            if (initialLevel <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialLevel));
            if (initialMaximumHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(initialMaximumHealth));
            if (defaultMoveSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(defaultMoveSpeed));

            InitialLevel = initialLevel;
            InitialMaximumHealth = initialMaximumHealth;
            DefaultMoveSpeed = defaultMoveSpeed;
        }

        public int InitialLevel { get; }
        public int InitialMaximumHealth { get; }
        public float DefaultMoveSpeed { get; }
    }
}
