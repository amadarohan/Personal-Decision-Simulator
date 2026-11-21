using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling. The application itself resolves
/// the context from dependency injection, so this factory never runs at runtime.
/// <para>
/// The connection string here is a placeholder: migrations only need a provider
/// to generate SQL Server-specific DDL, and no connection is opened while
/// scaffolding a migration.
/// </para>
/// </summary>
public sealed class DecisionSimulatorDbContextFactory : IDesignTimeDbContextFactory<DecisionSimulatorDbContext>
{
    public DecisionSimulatorDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DECISIONSIMULATOR_CONNECTION")
            ?? "Server=localhost;Database=PersonalDecisionSimulator;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<DecisionSimulatorDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(DecisionSimulatorDbContext).Assembly.FullName))
            .Options;

        return new DecisionSimulatorDbContext(options);
    }
}
