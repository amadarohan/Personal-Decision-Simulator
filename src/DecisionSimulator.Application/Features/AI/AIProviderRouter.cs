using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// Configuration for the AI subsystem. Bound from the "AI" configuration
/// section. Every value has a safe default: with no configuration at all the
/// application runs with the offline provider and never contacts a network.
/// </summary>
public sealed class AIOptions
{
    public const string SectionName = "AI";

    /// <summary>
    /// Which provider to prefer. <see cref="AIProviderKind.None"/> keeps the
    /// application fully offline and deterministic.
    /// </summary>
    public AIProviderKind Provider { get; set; } = AIProviderKind.None;

    /// <summary>Base URL for the OpenAI-compatible endpoint.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// API key for the remote endpoint. Supplied through configuration or an
    /// environment variable; never committed to source control.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Model identifier passed to the provider.</summary>
    public string? Model { get; set; }

    /// <summary>Request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// The highest data sensitivity the user permits to leave the machine.
    /// Defaults to <see cref="DataSensitivity.Internal"/>, which blocks personal
    /// and financial data from reaching a cloud provider until the user opts in.
    /// </summary>
    public DataSensitivity MaxRemoteSensitivity { get; set; } = DataSensitivity.Internal;

    /// <summary>
    /// When false, the router refuses every remote provider regardless of
    /// sensitivity. This is the hard offline switch.
    /// </summary>
    public bool AllowRemoteProviders { get; set; }
}

/// <summary>
/// Chooses which provider serves a request, enforcing the data-sensitivity
/// policy. This is the single place where "may this data leave the machine?" is
/// answered, so the rule cannot be bypassed by a caller.
/// </summary>
public sealed class AIProviderRouter
{
    private readonly IReadOnlyList<IAIProvider> _providers;
    private readonly AIOptions _options;

    public AIProviderRouter(IEnumerable<IAIProvider> providers, AIOptions options)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _providers = [.. providers];
        _options = options;
    }

    /// <summary>
    /// Returns the provider that should serve the request, or a reason why none
    /// can. Never throws for a policy refusal: an unavailable provider is a
    /// normal state the UI must be able to display.
    /// </summary>
    public (IAIProvider? Provider, string? RefusalReason) Select(DataSensitivity sensitivity)
    {
        var preferred = _providers.FirstOrDefault(p => p.Kind == _options.Provider);

        if (preferred is null)
        {
            return (null, $"No AI provider is configured for '{_options.Provider}'. " +
                          "The assistant is unavailable until a provider is set up.");
        }

        if (preferred.Kind == AIProviderKind.None)
        {
            // The offline provider is always allowed: nothing leaves the machine.
            return (preferred, null);
        }

        // A local provider (Ollama) never leaves the machine, so neither the
        // remote switch nor the sensitivity limit applies to it. Checking the
        // remote switch first would wrongly disable a local model.
        if (!preferred.IsRemote)
        {
            return (preferred, null);
        }

        if (!_options.AllowRemoteProviders)
        {
            return (null, "Remote AI providers are disabled in settings. " +
                          "Enable them to use this feature, or continue without the assistant.");
        }

        if (sensitivity > _options.MaxRemoteSensitivity)
        {
            return (null,
                $"This request contains {sensitivity} data, which exceeds the configured limit of " +
                $"{_options.MaxRemoteSensitivity} for remote providers. " +
                "Raise the limit in settings or remove the sensitive data from the request.");
        }

        return (preferred, null);
    }

    /// <summary>
    /// Selects a provider and performs the completion, converting an
    /// unreachable provider into an unavailable response rather than an error.
    /// </summary>
    public async Task<AIChatResponse> CompleteAsync(
        AIChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (provider, refusal) = Select(request.Sensitivity);

        if (provider is null)
        {
            return AIChatResponse.Unavailable(refusal ?? "No AI provider is available.");
        }

        if (!await provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false))
        {
            return AIChatResponse.Unavailable(
                $"The '{provider.Name}' provider is not reachable. " +
                "Check that the endpoint is running and the configuration is correct.");
        }

        return await provider.CompleteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
