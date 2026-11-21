using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Infrastructure.Simulation.Adapters;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionSimulator.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the simulation engine behind the application layer's
    /// <see cref="ISimulationEngine"/> contract.
    /// <para>
    /// The adapter is stateless apart from its model catalogue, which is built
    /// once in the constructor, so a singleton is safe and avoids rebuilding the
    /// catalogue on every request.
    /// </para>
    /// </summary>
    public static IServiceCollection AddSimulationEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISimulationEngine, SimulationEngineAdapter>();

        return services;
    }
}
