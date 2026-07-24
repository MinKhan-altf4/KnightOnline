using Microsoft.Extensions.Configuration;

namespace KnightOnline.Server.Persistence;

public static class DatabaseConfiguration
{
    public const string DevelopmentAccountKey = "local-dev";

    public static IConfiguration Build()
    {
        return new ConfigurationBuilder()
            .AddUserSecrets(
                typeof(DatabaseConfiguration).Assembly,
                optional: true)
            .Build();
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
