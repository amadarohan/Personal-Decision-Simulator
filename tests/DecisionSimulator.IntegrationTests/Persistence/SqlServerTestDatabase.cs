using DecisionSimulator.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.IntegrationTests.Persistence;

/// <summary>
/// Creates a throwaway SQL Server database per test class and applies the real
/// migrations to it.
/// <para>
/// SQLite is deliberately not used here. The schema depends on SQL Server
/// features that SQLite silently ignores or rejects: <c>rowversion</c> columns,
/// filtered unique indexes, and <c>decimal(18,6)</c> precision. Testing against
/// SQLite would prove nothing about the schema that actually ships.
/// </para>
/// </summary>
public sealed class SqlServerTestDatabase : IAsyncLifetime
{
    private const string MasterConnection =
        "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True";

    public string DatabaseName { get; } = $"PDS_Test_{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=localhost;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    public async Task InitializeAsync()
    {
        var masterOptions = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseSqlServer(MasterConnection)
            .Options;

        await using (var context = new DecisionSimulatorDbContext(masterOptions))
        {
            await context.Database.ExecuteSqlRawAsync($"CREATE DATABASE [{DatabaseName}];");
        }

        var options = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        await using (var db = new DecisionSimulatorDbContext(options))
        {
            await db.Database.MigrateAsync();
        }
    }

    public async Task DisposeAsync()
    {
        var masterOptions = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseSqlServer(MasterConnection)
            .Options;

        await using var context = new DecisionSimulatorDbContext(masterOptions);

        // Force single-user so pooled connections cannot block the drop.
        await context.Database.ExecuteSqlRawAsync(
            $"IF DB_ID('{DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{DatabaseName}]; END");
    }

    public DecisionSimulatorDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new DecisionSimulatorDbContext(options);
    }
}
