using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// The guardrails are the product's core promise: the assistant must not predict
/// the future, invent numbers, or fabricate sources. These tests assert the
/// constraints are actually present in every composed prompt.
/// </summary>
public sealed class AIPromptComposerTests
{
    [Theory]
    [InlineData(AIRole.DecisionAnalyst)]
    [InlineData(AIRole.ScenarioArchitect)]
    [InlineData(AIRole.RiskAnalyst)]
    [InlineData(AIRole.ResearchAnalyst)]
    [InlineData(AIRole.SimulationInterpreter)]
    [InlineData(AIRole.StrategicAdvisor)]
    public void Every_role_receives_the_no_prediction_rule(AIRole role)
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(role);

        prompt.Should().Contain("You do not know the future");
        prompt.Should().Contain("Never state what will happen");
    }

    [Theory]
    [InlineData(AIRole.DecisionAnalyst)]
    [InlineData(AIRole.ScenarioArchitect)]
    [InlineData(AIRole.RiskAnalyst)]
    [InlineData(AIRole.ResearchAnalyst)]
    [InlineData(AIRole.SimulationInterpreter)]
    [InlineData(AIRole.StrategicAdvisor)]
    public void Every_role_is_forbidden_from_inventing_numbers_and_sources(AIRole role)
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(role);

        prompt.Should().Contain("Never invent numbers");
        prompt.Should().Contain("Never invent sources");
    }

    [Theory]
    [InlineData(AIRole.DecisionAnalyst)]
    [InlineData(AIRole.ScenarioArchitect)]
    [InlineData(AIRole.RiskAnalyst)]
    [InlineData(AIRole.ResearchAnalyst)]
    [InlineData(AIRole.SimulationInterpreter)]
    [InlineData(AIRole.StrategicAdvisor)]
    public void Every_role_is_required_to_stay_politically_neutral(AIRole role)
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(role);

        prompt.Should().Contain("Stay neutral on politics");
    }

    [Fact]
    public void Confidence_must_be_qualitative_not_a_percentage()
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(AIRole.RiskAnalyst);

        // Asserted as separate fragments because the guardrail text wraps.
        prompt.Should().Contain("Express uncertainty qualitatively");
        prompt.Should().Contain("percentage probability");
    }

    [Fact]
    public void The_user_keeps_decision_authority()
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(AIRole.StrategicAdvisor);

        prompt.Should().Contain("The user decides");
        prompt.Should().Contain("do not issue verdicts");
    }

    [Fact]
    public void Each_role_gets_a_distinct_focus()
    {
        var analyst = AIPromptComposer.ComposeSystemPrompt(AIRole.DecisionAnalyst);
        var risk = AIPromptComposer.ComposeSystemPrompt(AIRole.RiskAnalyst);

        analyst.Should().Contain("framing the decision");
        risk.Should().Contain("downside exposure");
        analyst.Should().NotBe(risk);
    }

    [Fact]
    public void Context_is_included_when_supplied()
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(
            AIRole.DecisionAnalyst,
            "Decision: move to Isfahan. Two alternatives.");

        prompt.Should().Contain("Context supplied by the application");
        prompt.Should().Contain("move to Isfahan");
    }

    [Fact]
    public void Context_is_omitted_when_blank()
    {
        var prompt = AIPromptComposer.ComposeSystemPrompt(AIRole.DecisionAnalyst, "   ");

        prompt.Should().NotContain("Context supplied by the application");
    }

    [Fact]
    public void The_system_prompt_is_always_the_first_message()
    {
        var history = new List<AIChatMessage>
        {
            new(AIMessageRole.User, "first"),
            new(AIMessageRole.Assistant, "second")
        };

        var conversation = AIPromptComposer.BuildConversation(AIRole.DecisionAnalyst, history);

        conversation.Should().HaveCount(3);
        conversation[0].Role.Should().Be(AIMessageRole.System);
        conversation[0].Content.Should().Contain("You do not know the future");
        conversation[1].Content.Should().Be("first");
        conversation[2].Content.Should().Be("second");
    }
}
