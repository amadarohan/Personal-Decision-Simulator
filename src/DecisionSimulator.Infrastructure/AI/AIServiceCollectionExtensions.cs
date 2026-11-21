using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace DecisionSimulator.Infrastructure.AI;

public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI providers and the router.
    /// <para>
    /// The offline provider is always registered so the router always has
    /// something to select. Remote providers are registered only when they are
    /// configured, which keeps an unconfigured application from making network
    /// calls it was never asked to make.
    /// </para>
    /// </summary>
    public static IServiceCollection AddAISubsystem(
        this IServiceCollection services,
        AIOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);

        services.AddSingleton(options);

        services.AddSingleton<IAIProvider, NoneAIProvider>();

        if (options.Provider == AIProviderKind.Ollama && !string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddHttpClient<IAIProvider, OllamaProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }

        if (options.Provider == AIProviderKind.OpenAICompatible
            && !string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddHttpClient<IAIProvider, OpenAICompatibleProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });
        }

        // Scoped, not singleton: typed HttpClient registrations are transient, and
        // a singleton router would capture them for the lifetime of the process.
        services.AddScoped<AIProviderRouter>();

        return services;
    }
}
