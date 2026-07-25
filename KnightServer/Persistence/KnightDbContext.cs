using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server.Persistence;

public sealed class KnightDbContext(DbContextOptions<KnightDbContext> options)
    : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<RefreshSessionEntity> RefreshSessions => Set<RefreshSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(account => account.Id);
            entity.Property(account => account.Id)
                .HasColumnName("id");
            entity.Property(account => account.AccountKey)
                .HasColumnName("account_key")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(account => account.Kind)
                .HasColumnName("kind")
                .HasConversion<byte>()
                .IsRequired();
            entity.Property(account => account.Username)
                .HasColumnName("username")
                .HasMaxLength(32);
            entity.Property(account => account.NormalizedUsername)
                .HasColumnName("normalized_username")
                .HasMaxLength(32);
            entity.Property(account => account.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(512);
            entity.Property(account => account.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(account => account.RegisteredAtUtc)
                .HasColumnName("registered_at_utc");
            entity.HasIndex(account => account.AccountKey)
                .IsUnique();
            entity.HasIndex(account => account.NormalizedUsername)
                .IsUnique()
                .HasFilter("\"normalized_username\" IS NOT NULL");
        });

        modelBuilder.Entity<CharacterEntity>(entity =>
        {
            entity.ToTable("characters");
            entity.HasKey(character => character.Id);
            entity.Property(character => character.Id)
                .HasColumnName("id");
            entity.Property(character => character.AccountId)
                .HasColumnName("account_id");
            entity.Property(character => character.Name)
                .HasColumnName("name")
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(character => character.NormalizedName)
                .HasColumnName("normalized_name")
                .HasMaxLength(20)
                .IsRequired();
            entity.Property(character => character.Level)
                .HasColumnName("level")
                .HasDefaultValue(1);
            entity.Property(character => character.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();

            entity.HasIndex(character => character.NormalizedName)
                .IsUnique();

            entity.HasOne(character => character.Account)
                .WithMany(account => account.Characters)
                .HasForeignKey(character => character.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshSessionEntity>(entity =>
        {
            entity.ToTable("refresh_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Id)
                .HasColumnName("id");
            entity.Property(session => session.FamilyId)
                .HasColumnName("family_id");
            entity.Property(session => session.ReplacedBySessionId)
                .HasColumnName("replaced_by_session_id");
            entity.Property(session => session.AccountId)
                .HasColumnName("account_id");
            entity.Property(session => session.TokenHash)
                .HasColumnName("token_hash")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(session => session.DeviceIdHash)
                .HasColumnName("device_id_hash")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(session => session.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(session => session.ExpiresAtUtc)
                .HasColumnName("expires_at_utc")
                .IsRequired();
            entity.Property(session => session.RevokedAtUtc)
                .HasColumnName("revoked_at_utc");

            entity.HasIndex(session => session.TokenHash)
                .IsUnique();
            entity.HasIndex(session => session.FamilyId);
            entity.HasIndex(session => new
                {
                    session.AccountId,
                    session.DeviceIdHash,
                });

            entity.HasOne(session => session.Account)
                .WithMany(account => account.RefreshSessions)
                .HasForeignKey(session => session.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
