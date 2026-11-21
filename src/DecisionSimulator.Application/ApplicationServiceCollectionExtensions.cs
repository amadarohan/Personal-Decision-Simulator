using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Application.Features.Audit;
using DecisionSimulator.Application.Features.Data;
using DecisionSimulator.Application.Features.Decisions;
using DecisionSimulator.Application.Features.Documents;
using DecisionSimulator.Application.Features.Risks;
using DecisionSimulator.Application.Features.Scenarios;
using DecisionSimulator.Application.Features.Settings;
using DecisionSimulator.Application.Features.Simulations;
using DecisionSimulator.Application.Features.Tags;
using DecisionSimulator.Application.Features.Variables;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionSimulator.Application;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the application services. The persistence context, the
    /// simulation engine, and the user-preferences accessor are supplied by the
    /// outer layers, so this method never decides which database or provider is used.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<DecisionService>();
        services.AddScoped<ScenarioService>();
        services.AddScoped<SimulationService>();
        services.AddScoped<VariableDefinitionService>();
        services.AddScoped<RiskFactorService>();
        services.AddScoped<DataProvenanceService>();
        services.AddScoped<SettingsService>();
        services.AddScoped<TagService>();
        services.AddScoped<AuditService>();

        // Document storage is supplied by the outer layer, which is the only
        // place that knows where files live on disk.
        services.AddScoped<DecisionDocumentService>();

        // The AI subsystem. AIOptions and the provider list are supplied by the
        // outer layer, which is the only place that knows about configuration and
        // HTTP clients. With no provider registered the router reports the
        // assistant as unavailable rather than failing.
        services.AddScoped<AIConversationService>();
        services.AddScoped<AIProposalService>();
        services.AddScoped<AIMemoryService>();

        services.AddValidatorsFromAssemblyContaining<CreateDecisionRequestValidator>(
            includeInternalTypes: false);

        return services;
    }
}
