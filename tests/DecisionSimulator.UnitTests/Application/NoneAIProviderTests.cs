using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// The offline provider must never look like a model answer. If it did, a user
/// could mistake a canned notice for analysis.
/// </summary>
public sealed class NoneAIProviderTests
{
    private static AIChatRequest Request(string content = "Should I take the job?") =>
        new([new AIChatMessage(AIMessageRole.User, content)], AIRole.DecisionAnalyst);

    [Fact]
    public async Task It_reports_itself_as_unavailable()
    {
        var provider = new NoneAIProvider();

        var response = await provider.CompleteAsync(Request());

        response.IsAvailable.Should().BeFalse();
        response.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task It_never_claims_to_be_a_remote_provider()
    {
        var provider = new NoneAIProvider();

        provider.IsRemote.Should().BeFalse();
        provider.Kind.Should().Be(AIProviderKind.None);
        (await provider.IsAvailableAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task It_explains_what_still_works_without_a_model()
    {
        var provider = new NoneAIProvider();

        var response = await provider.CompleteAsync(Request());

        response.Content.Should().Contain("No language model is configured");
        response.Content.Should().Contain("Monte Carlo");
        response.Content.Should().Contain("Ollama");
    }

    [Fact]
    public async Task It_preserves_the_users_question()
    {
        var provider = new NoneAIProvider();

        var response = await provider.CompleteAsync(Request("Should I relocate to Shiraz?"));

        response.Content.Should().Contain("Should I relocate to Shiraz?");
    }

    [Fact]
    public async Task It_does_not_answer_the_question()
    {
        var provider = new NoneAIProvider();

        var response = await provider.CompleteAsync(Request("Should I take the job?"));

        // A canned notice must not contain advice, or it becomes indistinguishable
        // from fabricated analysis.
        response.Content.Should().NotContain("you should");
        response.Content.Should().NotContain("I recommend");
        response.Content.Should().NotContain("I suggest");
    }

    [Fact]
    public async Task It_truncates_a_very_long_question()
    {
        var provider = new NoneAIProvider();
        var longQuestion = new string('x', 2000);

        var response = await provider.CompleteAsync(Request(longQuestion));

        response.Content.Should().Contain("...");
        response.Content.Length.Should().BeLessThan(longQuestion.Length);
    }
}
