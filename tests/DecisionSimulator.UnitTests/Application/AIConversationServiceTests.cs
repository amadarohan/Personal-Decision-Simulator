using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// Conversation behaviour, with emphasis on what happens when the assistant is
/// unavailable: the user's own words must survive, and no assistant turn may be
/// invented.
/// </summary>
public sealed class AIConversationServiceTests : IDisposable
{
    private readonly InMemoryDatabase _database = new();

    public void Dispose() => _database.Dispose();

    private sealed class StubProvider(
        bool available = true,
        string? content = "A considered answer.",
        string? unavailableReason = null,
        AIProviderKind kind = AIProviderKind.None,
        bool responseAvailable = true) : IAIProvider
    {
        public string Name => "stub";

        public AIProviderKind Kind => kind;

        public bool IsRemote => false;

        public int CallCount { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(available);

        public Task<AIChatResponse> CompleteAsync(
            AIChatRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            if (!responseAvailable)
            {
                return Task.FromResult(
                    AIChatResponse.Unavailable(unavailableReason ?? "unavailable"));
            }

            return Task.FromResult(
                AIChatResponse.FromProvider(content!, Name, "stub-model", 42));
        }
    }

    private AIConversationService CreateService(IAIProvider provider)
    {
        var options = new AIOptions { Provider = AIProviderKind.None };
        var router = new AIProviderRouter([provider], options);
        return new AIConversationService(_database, router, options);
    }

    private async Task<Guid> CreateConversationAsync(AIConversationService service)
    {
        var result = await service.CreateAsync(
            new CreateConversationRequest("Career move", AIContextType.Decision, null));

        result.Succeeded.Should().BeTrue();
        return result.Value;
    }

    [Fact]
    public async Task A_conversation_requires_a_title()
    {
        var service = CreateService(new StubProvider());

        var result = await service.CreateAsync(
            new CreateConversationRequest("  ", AIContextType.Global, null));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task A_conversation_for_an_unknown_decision_is_rejected()
    {
        var service = CreateService(new StubProvider());

        var result = await service.CreateAsync(
            new CreateConversationRequest("Orphan", AIContextType.Decision, Guid.NewGuid()));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task A_successful_turn_stores_both_messages()
    {
        var service = CreateService(new StubProvider());
        var conversationId = await CreateConversationAsync(service);

        var result = await service.SendMessageAsync(
            conversationId,
            new SendMessageRequest("What should I compare?"));

        result.Succeeded.Should().BeTrue();
        result.Value.UserMessage.Content.Should().Be("What should I compare?");
        result.Value.AssistantMessage.Should().NotBeNull();
        result.Value.AssistantMessage!.Content.Should().Be("A considered answer.");
        result.Value.AssistantMessage.Provider.Should().Be("stub");
        result.Value.AssistantMessage.TokenUsage.Should().Be(42);
    }

    [Fact]
    public async Task An_unavailable_provider_still_persists_the_user_turn()
    {
        // The provider is reachable but declines to answer, which is the case the
        // service must handle: the user's words are kept, no reply is invented.
        var service = CreateService(new StubProvider(
            responseAvailable: false,
            unavailableReason: "No model."));

        var conversationId = await CreateConversationAsync(service);

        var result = await service.SendMessageAsync(
            conversationId,
            new SendMessageRequest("Is this a good idea?"));

        result.Succeeded.Should().BeTrue();
        result.Value.AssistantMessage.Should().BeNull();
        result.Value.UnavailableReason.Should().Be("No model.");

        var detail = await service.GetAsync(conversationId);
        detail.Value.Messages.Should().ContainSingle();
        detail.Value.Messages[0].Role.Should().Be(AIMessageRole.User);
        detail.Value.Messages[0].Content.Should().Be("Is this a good idea?");
    }

    [Fact]
    public async Task An_unreachable_provider_still_persists_the_user_turn()
    {
        var service = CreateService(new StubProvider(available: false));
        var conversationId = await CreateConversationAsync(service);

        var result = await service.SendMessageAsync(
            conversationId,
            new SendMessageRequest("Is this a good idea?"));

        result.Succeeded.Should().BeTrue();
        result.Value.AssistantMessage.Should().BeNull();
        result.Value.UnavailableReason.Should().Contain("not reachable");

        var detail = await service.GetAsync(conversationId);
        detail.Value.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task No_assistant_message_is_stored_when_the_provider_returns_nothing()
    {
        var service = CreateService(new StubProvider(content: "   "));
        var conversationId = await CreateConversationAsync(service);

        var result = await service.SendMessageAsync(
            conversationId,
            new SendMessageRequest("Hello"));

        result.Value.AssistantMessage.Should().BeNull();

        var detail = await service.GetAsync(conversationId);
        detail.Value.Messages.Should().ContainSingle();
        detail.Value.Messages.Should().NotContain(m => m.Role == AIMessageRole.Assistant);
    }

    [Fact]
    public async Task An_empty_message_is_rejected_without_calling_the_provider()
    {
        var provider = new StubProvider();
        var service = CreateService(provider);
        var conversationId = await CreateConversationAsync(service);

        var result = await service.SendMessageAsync(
            conversationId,
            new SendMessageRequest("   "));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
        provider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Messages_are_returned_in_the_order_they_were_sent()
    {
        var service = CreateService(new StubProvider());
        var conversationId = await CreateConversationAsync(service);

        await service.SendMessageAsync(conversationId, new SendMessageRequest("first"));
        await service.SendMessageAsync(conversationId, new SendMessageRequest("second"));

        var detail = await service.GetAsync(conversationId);

        detail.Value.Messages.Should().HaveCount(4);
        detail.Value.Messages[0].Content.Should().Be("first");
        detail.Value.Messages[2].Content.Should().Be("second");
    }

    [Fact]
    public async Task The_status_reports_the_assistant_as_unavailable_without_a_model()
    {
        var service = CreateService(new NoneAIProvider());

        var status = await service.GetStatusAsync();

        status.IsAvailable.Should().BeFalse();
        status.ProviderKind.Should().Be(AIProviderKind.None);
        status.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_status_reports_a_working_provider_as_available()
    {
        var options = new AIOptions { Provider = AIProviderKind.Ollama };
        var router = new AIProviderRouter([new StubProvider(kind: AIProviderKind.Ollama)], options);
        var service = new AIConversationService(_database, router, options);

        var status = await service.GetStatusAsync();

        status.IsAvailable.Should().BeTrue();
        status.UnavailableReason.Should().BeNull();
    }

    [Fact]
    public async Task Conversations_can_be_renamed()
    {
        var service = CreateService(new StubProvider());
        var conversationId = await CreateConversationAsync(service);

        await service.RenameAsync(conversationId, new RenameConversationRequest("Renamed"));

        var detail = await service.GetAsync(conversationId);
        detail.Value.Title.Should().Be("Renamed");
    }

    [Fact]
    public async Task Conversations_can_be_filtered_by_decision()
    {
        var service = CreateService(new StubProvider());

        var decision = new Decision("Relocate", DecisionCategory.Relocation);
        _database.Context.Decisions.Add(decision);
        await _database.Context.SaveChangesAsync();

        await service.CreateAsync(new CreateConversationRequest("About it", AIContextType.Decision, decision.Id));
        await service.CreateAsync(new CreateConversationRequest("Unrelated", AIContextType.Global, null));

        var result = await service.ListAsync(decisionId: decision.Id);

        result.Value.Should().ContainSingle();
        result.Value[0].Title.Should().Be("About it");
    }

    [Fact]
    public async Task Deleting_a_conversation_removes_it()
    {
        var service = CreateService(new StubProvider());
        var conversationId = await CreateConversationAsync(service);

        var deleted = await service.DeleteAsync(conversationId);

        deleted.Succeeded.Should().BeTrue();
        var detail = await service.GetAsync(conversationId);
        detail.Succeeded.Should().BeFalse();
        detail.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }
}

