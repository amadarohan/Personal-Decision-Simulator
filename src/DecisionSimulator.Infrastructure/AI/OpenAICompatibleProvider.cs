using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.AI;

/// <summary>
/// Talks to an OpenAI-compatible chat-completions endpoint. This covers OpenAI
/// itself, Azure OpenAI gateways, and self-hosted servers such as LM Studio or
/// vLLM that expose the same shape.
/// <para>
/// The adapter is deliberately thin: it forwards the composed prompt and returns
/// whatever the endpoint produced. It never post-processes the text into
/// numbers, and it never retries with a different prompt, because either would
/// make the output harder to attribute.
/// </para>
/// </summary>
public sealed class OpenAICompatibleProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIOptions _options;

    public OpenAICompatibleProvider(HttpClient httpClient, AIOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "openai-compatible";

    public AIProviderKind Kind => AIProviderKind.OpenAICompatible;

    public bool IsRemote => true;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.Model))
        {
            return false;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildUri("models"));
            AddAuthorization(request);

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
    }

    public async Task<AIChatResponse> CompleteAsync(
        AIChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.Model))
        {
            return AIChatResponse.Unavailable(
                "The OpenAI-compatible provider is not configured. Set a base URL and a model name.");
        }

        var payload = new ChatCompletionRequest(
            _options.Model,
            [.. request.Messages.Select(m => new ChatCompletionMessage(ToWireRole(m.Role), m.Content))],
            request.Temperature,
            request.MaxOutputTokens);

        try
        {
            using var httpRequest = new HttpRequestMessage(
                HttpMethod.Post,
                BuildUri("chat/completions"))
            {
                Content = JsonContent.Create(payload)
            };

            AddAuthorization(httpRequest);

            using var response = await _httpClient
                .SendAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                // The status code is reported, the body is not echoed: it can
                // contain account details or the prompt itself.
                return AIChatResponse.Unavailable(
                    $"The provider returned HTTP {(int)response.StatusCode}. " +
                    "Check the endpoint, the model name, and the API key.");
            }

            var completion = await response.Content
                .ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken)
                .ConfigureAwait(false);

            var choices = completion?.Choices;
            var content = choices is { Count: > 0 } ? choices[0].Message?.Content : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                return AIChatResponse.Unavailable("The provider returned an empty response.");
            }

            return AIChatResponse.FromProvider(
                content,
                Name,
                completion?.Model ?? _options.Model,
                completion?.Usage?.TotalTokens);
        }
        catch (HttpRequestException)
        {
            return AIChatResponse.Unavailable(
                "The provider could not be reached. Check the base URL and network access.");
        }
        catch (TaskCanceledException)
        {
            return AIChatResponse.Unavailable(
                $"The provider did not respond within {_options.TimeoutSeconds} seconds.");
        }
        catch (JsonException)
        {
            return AIChatResponse.Unavailable(
                "The provider returned a response that is not valid chat-completion JSON.");
        }
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        return new Uri($"{baseUrl}/{relativePath}", UriKind.Absolute);
    }

    private void AddAuthorization(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    private static string ToWireRole(AIMessageRole role) => role switch
    {
        AIMessageRole.System => "system",
        AIMessageRole.User => "user",
        AIMessageRole.Assistant => "assistant",
        AIMessageRole.Tool => "tool",
        _ => "user"
    };

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatCompletionMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private sealed record ChatCompletionMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("choices")] IReadOnlyList<ChatCompletionChoice>? Choices,
        [property: JsonPropertyName("usage")] ChatCompletionUsage? Usage);

    private sealed record ChatCompletionChoice(
        [property: JsonPropertyName("message")] ChatCompletionMessage? Message);

    private sealed record ChatCompletionUsage(
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
