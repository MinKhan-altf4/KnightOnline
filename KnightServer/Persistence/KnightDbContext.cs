using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server.Persistence;

public sealed class KnightDbContext(DbContextOptions<KnightDbContext> options)
    : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();
    public DbSet<CharacterAppearanceEntity> CharacterAppearances =>
        Set<CharacterAppearanceEntity>();
    public DbSet<CharacterTutorialProgressEntity> CharacterTutorialProgress =>
        Set<CharacterTutorialProgressEntity>();
    public DbSet<CharacterCreationRequestEntity> CharacterCreationRequests =>
        Set<CharacterCreationRequestEntity>();
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
            entity.Property(character => character.ServerId)
                .HasColumnName("server_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(character => character.SlotIndex)
                .HasColumnName("slot_index")
                .IsRequired();
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
            entity.Property(character => character.CurrentClassDefinitionId)
                .HasColumnName("current_class_definition_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(character => character.BodyTypeDefinitionId)
                .HasColumnName("body_type_definition_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(character => character.CurrentMapDefinitionId)
                .HasColumnName("current_map_definition_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(character => character.CurrentSpawnPointId)
                .HasColumnName("current_spawn_point_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(character => character.PositionX)
                .HasColumnName("position_x");
            entity.Property(character => character.PositionY)
                .HasColumnName("position_y");
            entity.Property(character => character.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.Property(character => character.Version)
                .HasColumnName("version")
                .IsConcurrencyToken();

            entity.HasIndex(character => new
                {
                    character.ServerId,
                    character.NormalizedName,
                })
                .IsUnique();
            entity.HasIndex(character => new
                {
                    character.AccountId,
                    character.ServerId,
                    character.SlotIndex,
                })
                .IsUnique();
            entity.ToTable(
                table => table.HasCheckConstraint(
                    "ck_characters_slot_index",
                    "\"slot_index\" BETWEEN 1 AND 3"));

            entity.HasOne(character => character.Account)
                .WithMany(account => account.Characters)
                .HasForeignKey(character => character.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterAppearanceEntity>(entity =>
        {
            entity.ToTable("character_appearances");
            entity.HasKey(value => new
            {
                value.CharacterId,
                value.SlotDefinitionId,
            });
            entity.Property(value => value.CharacterId)
                .HasColumnName("character_id");
            entity.Property(value => value.SlotDefinitionId)
                .HasColumnName("slot_definition_id")
                .HasMaxLength(64);
            entity.Property(value => value.AppearanceDefinitionId)
                .HasColumnName("appearance_definition_id")
                .HasMaxLength(64)
                .IsRequired();
            entity.Property(value => value.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");
            entity.Property(value => value.Version)
                .HasColumnName("version")
                .IsConcurrencyToken();
            entity.HasOne(value => value.Character)
                .WithMany(character => character.Appearances)
                .HasForeignKey(value => value.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterTutorialProgressEntity>(entity =>
        {
            entity.ToTable("character_tutorial_progress");
            entity.HasKey(value => new
            {
                value.CharacterId,
                value.TutorialDefinitionId,
            });
            entity.Property(value => value.CharacterId)
                .HasColumnName("character_id");
            entity.Property(value => value.TutorialDefinitionId)
                .HasColumnName("tutorial_definition_id")
                .HasMaxLength(64);
            entity.Property(value => value.CurrentStepDefinitionId)
                .HasColumnName("current_step_definition_id")
                .HasMaxLength(64);
            entity.Property(value => value.State)
                .HasColumnName("state")
                .HasConversion<byte>();
            entity.Property(value => value.ContinueChoice)
                .HasColumnName("continue_choice");
            entity.Property(value => value.StartedAtUtc)
                .HasColumnName("started_at_utc");
            entity.Property(value => value.UpdatedAtUtc)
                .HasColumnName("updated_at_utc");
            entity.Property(value => value.CompletedAtUtc)
                .HasColumnName("completed_at_utc");
            entity.Property(value => value.Version)
                .HasColumnName("version")
                .IsConcurrencyToken();
            entity.HasOne(value => value.Character)
                .WithMany(character => character.TutorialProgress)
                .HasForeignKey(value => value.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CharacterCreationRequestEntity>(entity =>
        {
            entity.ToTable("character_creation_requests");
            entity.HasKey(value => value.RequestId);
            entity.Property(value => value.RequestId)
                .HasColumnName("request_id");
            entity.Property(value => value.AccountId)
                .HasColumnName("account_id");
            entity.Property(value => value.CharacterId)
                .HasColumnName("character_id");
            entity.Property(value => value.ResultCode)
                .HasColumnName("result_code")
                .HasMaxLength(64);
            entity.Property(value => value.ResultMessage)
                .HasColumnName("result_message")
                .HasMaxLength(256);
            entity.Property(value => value.CreatedAtUtc)
                .HasColumnName("created_at_utc");
            entity.HasOne(value => value.Account)
                .WithMany()
                .HasForeignKey(value => value.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(value => value.Character)
                .WithMany()
                .HasForeignKey(value => value.CharacterId)
                .OnDelete(DeleteBehavior.SetNull);
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
