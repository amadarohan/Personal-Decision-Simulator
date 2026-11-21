using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionSimulator.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database context. The connection string is supplied by the
    /// caller so that no credential is ever compiled into the assembly.
    /// </summary>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<DecisionSimulatorDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(DecisionSimulatorDbContext).Assembly.FullName);

                // Transient failures on a local instance are usually a service
                // restart; retrying avoids surfacing a spurious error to the user.
                sql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);

                // Several aggregates load more than one collection in a single
                // query (a decision with its alternatives, scenarios, scenario
                // variables and reviews). A single query joins those collections
                // into a cartesian product, so the row count multiplies with each
                // collection. Split queries issue one statement per collection
                // instead, which keeps the cost linear.
                sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            }));

        services.AddScoped<BuiltInDataSeeder>();
        services.AddScoped<DemoDataSeeder>();

        return services;
    }
}
