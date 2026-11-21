using System.Text;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// The offline provider. It never contacts a network and never generates prose
/// that looks like a model answer.
/// <para>
/// Its job is to keep the application honest when no model is configured: it
/// returns a structured, deterministic response that tells the user the
/// assistant is unavailable and points at the parts of the application that work
/// without one. It deliberately does not attempt to imitate an LLM, because a
/// convincing imitation would be indistinguishable from fabricated analysis.
/// </para>
/// </summary>
public sealed class NoneAIProvider : IAIProvider
{
    public string Name => "offline";

    public AIProviderKind Kind => AIProviderKind.None;

    public bool IsRemote => false;

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<AIChatResponse> CompleteAsync(
        AIChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = BuildOfflineNotice(request);

        // IsAvailable is false: the assistant itself is not available. The
        // content explains what the user can do instead. Callers must render
        // this as a notice, not as an assistant reply.
        return Task.FromResult(new AIChatResponse(
            IsAvailable: false,
            Content: content,
            ProviderName: Name,
            ModelName: null,
            TokenUsage: null,
            UnavailableReason: "No language model is configured."));
    }

    private static string BuildOfflineNotice(AIChatRequest request)
    {
        var builder = new StringBuilder();

        builder.AppendLine("No language model is configured, so the assistant cannot answer this.");
        builder.AppendLine();
        builder.AppendLine("Everything that does not need a model still works:");
        builder.AppendLine("- Define alternatives and scenarios for your decision.");
        builder.AppendLine("- Set assumptions and run deterministic or Monte Carlo simulations.");
        builder.AppendLine("- Compare scenarios, run sensitivity analysis, and apply stress tests.");
        builder.AppendLine("- Record data sources and observations with their provenance.");
        builder.AppendLine();
        builder.AppendLine("To enable the assistant, configure a provider in settings:");
        builder.AppendLine("- A local Ollama instance, which keeps all data on this machine.");
        builder.AppendLine("- Any OpenAI-compatible endpoint, which sends data off the machine.");

        var lastUserMessage = request.Messages
            .LastOrDefault(m => m.Role == AIMessageRole.User);

        if (lastUserMessage is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Your question has been saved with this conversation so it is not lost:");
            builder.Append("> ").AppendLine(Truncate(lastUserMessage.Content, 500));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}
