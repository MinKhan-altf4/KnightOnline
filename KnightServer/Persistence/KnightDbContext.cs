using KnightOnline.Server.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace KnightOnline.Server.Persistence;

public sealed class KnightDbContext(DbContextOptions<KnightDbContext> options)
    : DbContext(options)
{
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<CharacterEntity> Characters => Set<CharacterEntity>();

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
            entity.Property(account => account.CreatedAtUtc)
                .HasColumnName("created_at_utc")
                .IsRequired();
            entity.HasIndex(account => account.AccountKey)
                .IsUnique();
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
    }
}
