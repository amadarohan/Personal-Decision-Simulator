using System.Text;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// Builds the system prompt for each AI role.
/// <para>
/// The guardrails live here rather than in each provider so that every backend —
/// offline, local, or cloud — receives the same constraints. A provider cannot
/// opt out of them.
/// </para>
/// </summary>
public static class AIPromptComposer
{
    /// <summary>
    /// The non-negotiable rules. Kept short and imperative because long prompts
    /// dilute instruction-following.
    /// </summary>
    private const string Guardrails = """
        You are an assistant inside a personal decision simulator.

        Absolute rules:
        1. You do not know the future. Never state what will happen. Describe what
           could happen, under which assumptions, and what would have to be true.
        2. Never invent numbers. Every quantity you mention must come from the
           simulation results or data supplied in this conversation. If a number is
           not supplied, say it is not available.
        3. Express uncertainty qualitatively (low, medium, high). Never attach a
           percentage probability to a real-world event unless it was computed by
           the simulation engine and supplied to you.
        4. Never invent sources, statistics, prices, interest rates, or regulations.
           If you do not have a source, say so and suggest how the user could find one.
        5. Stay neutral on politics, religion, and ideology. Describe trade-offs
           without recommending a political or ideological position.
        6. The user decides. Present options and consequences; do not issue verdicts
           or tell the user what they should value.
        7. If the user asks you to predict the future, explain what the simulator can
           and cannot do, then offer a scenario comparison instead.
        """;

    /// <summary>
    /// Role-specific instructions layered on top of the guardrails.
    /// </summary>
    private static string RoleInstruction(AIRole role) => role switch
    {
        AIRole.DecisionAnalyst =>
            "Your focus is framing the decision: the real choice, the alternatives, " +
            "the criteria that matter, and the information that is missing.",

        AIRole.ScenarioArchitect =>
            "Your focus is constructing scenarios: plausible combinations of " +
            "assumptions, each internally consistent, each labelled with what it assumes.",

        AIRole.RiskAnalyst =>
            "Your focus is downside exposure: what could go wrong, how the user would " +
            "notice early, and which risks are reversible.",

        AIRole.ResearchAnalyst =>
            "Your focus is evidence gaps: which facts are missing, where they could be " +
            "found, and how reliable each source type tends to be. Never supply the " +
            "facts yourself.",

        AIRole.SimulationInterpreter =>
            "Your focus is explaining simulation output: what the distribution shows, " +
            "what it does not show, and which assumptions drive the result most.",

        AIRole.StrategicAdvisor =>
            "Your focus is sequencing: which decisions must precede others, what can be " +
            "reversed cheaply, and what commits the user irreversibly.",

        _ => "Your focus is helping the user think clearly about their decision."
    };

    /// <summary>
    /// Composes the system prompt for a role, optionally including a summary of
    /// the decision context the caller has already assembled.
    /// </summary>
    public static string ComposeSystemPrompt(AIRole role, string? contextSummary = null)
    {
        var builder = new StringBuilder();

        builder.AppendLine(Guardrails);
        builder.AppendLine();
        builder.AppendLine(RoleInstruction(role));

        if (!string.IsNullOrWhiteSpace(contextSummary))
        {
            builder.AppendLine();
            builder.AppendLine("Context supplied by the application:");
            builder.AppendLine(contextSummary);
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// Wraps a user turn with the system prompt so a provider that only accepts a
    /// flat message list still receives the guardrails.
    /// </summary>
    public static IReadOnlyList<AIChatMessage> BuildConversation(
        AIRole role,
        IEnumerable<AIChatMessage> history,
        string? contextSummary = null)
    {
        ArgumentNullException.ThrowIfNull(history);

        var messages = new List<AIChatMessage>
        {
            new(AIMessageRole.System, ComposeSystemPrompt(role, contextSummary))
        };

        messages.AddRange(history);
        return messages;
    }
}
