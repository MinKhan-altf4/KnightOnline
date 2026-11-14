using Microsoft.Extensions.Configuration;

namespace KnightOnline.Server.Persistence;

public static class DatabaseConfiguration
{
    public static IConfiguration Build()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(DatabaseConfiguration).Assembly,
                optional: true)
            .Build();
    }

    /// <summary>
    /// Caps the Npgsql pool so bursts queue in the client instead of being
    /// rejected by PostgreSQL (SQLSTATE 53300). An explicit
    /// "Maximum Pool Size" in the connection string wins.
    /// </summary>
    public static string ApplyPoolLimit(string connectionString, int maximumPoolSize)
    {
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
        bool explicitLimit = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2)[0].Trim())
            .Any(key => key.Equals("Maximum Pool Size", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Max Pool Size", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("MaxPoolSize", StringComparison.OrdinalIgnoreCase));
        if (!explicitLimit)
            builder.MaxPoolSize = maximumPoolSize;
        return builder.ConnectionString;
    }

    public static string GetRequiredConnectionString(IConfiguration configuration)
    {
        return Environment.GetEnvironmentVariable(
                   "KNIGHTONLINE_ConnectionStrings__KnightOnline")
            ?? configuration.GetConnectionString("KnightOnline")
            ?? throw new InvalidOperationException(
                "Missing PostgreSQL connection string. Configure User Secrets key " +
                "'ConnectionStrings:KnightOnline' or environment variable " +
                "'KNIGHTONLINE_ConnectionStrings__KnightOnline'.");
    }
}
