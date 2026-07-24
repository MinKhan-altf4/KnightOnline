using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KnightOnline.Server.Persistence;

public sealed class KnightDbContextFactory : IDesignTimeDbContextFactory<KnightDbContext>
{
    public KnightDbContext CreateDbContext(string[] args)
    {
        var configuration = DatabaseConfiguration.Build();
        var connectionString =
            DatabaseConfiguration.GetRequiredConnectionString(configuration);

        var options = new DbContextOptionsBuilder<KnightDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new KnightDbContext(options);
    }
}
