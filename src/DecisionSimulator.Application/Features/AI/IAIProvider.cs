using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// One turn in a chat exchange. Deliberately provider-neutral so the same
/// request can be served by the offline provider, a local model, or a cloud
/// endpoint without the caller knowing which.
/// </summary>
public sealed record AIChatMessage(AIMessageRole Role, string Content);

/// <summary>
/// A request to generate a completion.
/// <para>
/// <see cref="Sensitivity"/> is the highest sensitivity of any data included in
/// the prompt. The router uses it to decide whether a cloud provider is allowed
/// to see the request at all.
/// </para>
/// </summary>
public sealed record AIChatRequest(
    IReadOnlyList<AIChatMessage> Messages,
    AIRole Role,
    DataSensitivity Sensitivity = DataSensitivity.Internal,
    double Temperature = 0.2,
    int MaxOutputTokens = 1024);

/// <summary>
/// The outcome of a completion request.
/// <para>
/// <see cref="IsAvailable"/> is false when no provider could serve the request.
/// In that case <see cref="Content"/> explains why, and callers must surface
/// that explanation rather than inventing an answer. The application never
/// fabricates AI output.
/// </para>
/// </summary>
public sealed record AIChatResponse(
    bool IsAvailable,
    string Content,
    string? ProviderName,
    string? ModelName,
    int? TokenUsage,
    string? UnavailableReason)
{
    public static AIChatResponse Unavailable(string reason) =>
        new(false, string.Empty, null, null, null, reason);

    public static AIChatResponse FromProvider(
        string content,
        string providerName,
        string? modelName,
        int? tokenUsage) =>
        new(true, content, providerName, modelName, tokenUsage, null);
}

/// <summary>
/// A pluggable language-model backend.
/// </summary>
public interface IAIProvider
{
    /// <summary>Stable identifier used in logs and on stored messages.</summary>
    string Name { get; }

    AIProviderKind Kind { get; }

    /// <summary>
    /// Whether this provider sends data outside the machine. Cloud providers
    /// return true so the router can enforce the sensitivity policy.
    /// </summary>
    bool IsRemote { get; }

    /// <summary>
    /// Cheap readiness probe. Implementations must not throw; an unreachable
    /// endpoint is reported as unavailable, not as an exception.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    Task<AIChatResponse> CompleteAsync(
        AIChatRequest request,
        CancellationToken cancellationToken = default);
}
