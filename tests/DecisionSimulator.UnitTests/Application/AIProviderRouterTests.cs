using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

/// <summary>
/// The router is the only place that decides whether data may leave the machine.
/// These tests pin that policy down, because a regression here would silently
/// send personal financial data to a cloud provider.
/// </summary>
public sealed class AIProviderRouterTests
{
    private sealed class StubProvider(
        AIProviderKind kind,
        bool isRemote,
        bool available = true,
        string? content = "stub answer") : IAIProvider
    {
        public string Name => kind.ToString();

        public AIProviderKind Kind => kind;

        public bool IsRemote => isRemote;

        public int CallCount { get; private set; }

        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(available);

        public Task<AIChatResponse> CompleteAsync(
            AIChatRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(AIChatResponse.FromProvider(content!, Name, "stub-model", 10));
        }
    }

    private static AIChatRequest Request(DataSensitivity sensitivity = DataSensitivity.Internal) =>
        new([new AIChatMessage(AIMessageRole.User, "hello")], AIRole.DecisionAnalyst, sensitivity);

    [Fact]
    public void The_offline_provider_is_always_selectable()
    {
        var offline = new StubProvider(AIProviderKind.None, isRemote: false);
        var router = new AIProviderRouter([offline], new AIOptions { Provider = AIProviderKind.None });

        var (provider, refusal) = router.Select(DataSensitivity.HighlySensitive);

        provider.Should().BeSameAs(offline);
        refusal.Should().BeNull();
    }

    [Fact]
    public void A_remote_provider_is_refused_when_remote_access_is_disabled()
    {
        var remote = new StubProvider(AIProviderKind.OpenAICompatible, isRemote: true);
        var router = new AIProviderRouter([remote], new AIOptions
        {
            Provider = AIProviderKind.OpenAICompatible,
            AllowRemoteProviders = false
        });

        var (provider, refusal) = router.Select(DataSensitivity.Public);

        provider.Should().BeNull();
        refusal.Should().Contain("disabled");
    }

    [Fact]
    public void Personal_data_is_refused_when_it_exceeds_the_remote_limit()
    {
        var remote = new StubProvider(AIProviderKind.OpenAICompatible, isRemote: true);
        var router = new AIProviderRouter([remote], new AIOptions
        {
            Provider = AIProviderKind.OpenAICompatible,
            AllowRemoteProviders = true,
            MaxRemoteSensitivity = DataSensitivity.Internal
        });

        var (provider, refusal) = router.Select(DataSensitivity.Financial);

        provider.Should().BeNull();
        refusal.Should().Contain("Financial");
        refusal.Should().Contain("Internal");
    }

    [Fact]
    public void Data_within_the_remote_limit_is_allowed()
    {
        var remote = new StubProvider(AIProviderKind.OpenAICompatible, isRemote: true);
        var router = new AIProviderRouter([remote], new AIOptions
        {
            Provider = AIProviderKind.OpenAICompatible,
            AllowRemoteProviders = true,
            MaxRemoteSensitivity = DataSensitivity.Financial
        });

        var (provider, refusal) = router.Select(DataSensitivity.Financial);

        provider.Should().BeSameAs(remote);
        refusal.Should().BeNull();
    }

    [Fact]
    public void A_local_provider_is_not_subject_to_the_remote_limit()
    {
        // Ollama runs on the user's machine, so highly sensitive data is fine.
        var local = new StubProvider(AIProviderKind.Ollama, isRemote: false);
        var router = new AIProviderRouter([local], new AIOptions
        {
            Provider = AIProviderKind.Ollama,
            AllowRemoteProviders = false,
            MaxRemoteSensitivity = DataSensitivity.Public
        });

        var (provider, refusal) = router.Select(DataSensitivity.HighlySensitive);

        provider.Should().BeSameAs(local);
        refusal.Should().BeNull();
    }

    [Fact]
    public void A_missing_provider_is_reported_rather_than_thrown()
    {
        var router = new AIProviderRouter([], new AIOptions { Provider = AIProviderKind.Ollama });

        var (provider, refusal) = router.Select(DataSensitivity.Public);

        provider.Should().BeNull();
        refusal.Should().Contain("No AI provider is configured");
    }

    [Fact]
    public async Task An_unreachable_provider_yields_an_unavailable_response()
    {
        var remote = new StubProvider(AIProviderKind.Ollama, isRemote: false, available: false);
        var router = new AIProviderRouter([remote], new AIOptions { Provider = AIProviderKind.Ollama });

        var response = await router.CompleteAsync(Request());

        response.IsAvailable.Should().BeFalse();
        response.UnavailableReason.Should().Contain("not reachable");
        remote.CallCount.Should().Be(0, "an unreachable provider must not be called");
    }

    [Fact]
    public async Task A_refused_request_never_reaches_the_provider()
    {
        var remote = new StubProvider(AIProviderKind.OpenAICompatible, isRemote: true);
        var router = new AIProviderRouter([remote], new AIOptions
        {
            Provider = AIProviderKind.OpenAICompatible,
            AllowRemoteProviders = true,
            MaxRemoteSensitivity = DataSensitivity.Internal
        });

        var response = await router.CompleteAsync(Request(DataSensitivity.HighlySensitive));

        response.IsAvailable.Should().BeFalse();
        remote.CallCount.Should().Be(0, "sensitive data must not be sent to a remote provider");
    }

    [Fact]
    public async Task An_allowed_request_is_forwarded_and_attributed()
    {
        var remote = new StubProvider(AIProviderKind.OpenAICompatible, isRemote: true);
        var router = new AIProviderRouter([remote], new AIOptions
        {
            Provider = AIProviderKind.OpenAICompatible,
            AllowRemoteProviders = true,
            MaxRemoteSensitivity = DataSensitivity.Personal
        });

        var response = await router.CompleteAsync(Request(DataSensitivity.Personal));

        response.IsAvailable.Should().BeTrue();
        response.Content.Should().Be("stub answer");
        response.ProviderName.Should().Be("OpenAICompatible");
        response.ModelName.Should().Be("stub-model");
        remote.CallCount.Should().Be(1);
    }
}
