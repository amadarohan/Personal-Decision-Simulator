using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Infrastructure.AI;

/// <summary>
/// Talks to a local Ollama instance. This is the preferred provider for personal
/// data because nothing leaves the machine.
/// </summary>
public sealed class OllamaProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly AIOptions _options;

    public OllamaProvider(HttpClient httpClient, AIOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string Name => "ollama";

    public AIProviderKind Kind => AIProviderKind.Ollama;

    /// <summary>
    /// Ollama runs on the user's own machine, so it is not treated as a remote
    /// provider and is not subject to the cloud sensitivity limit.
    /// </summary>
    public bool IsRemote => false;

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            return false;
        }

        try
        {
            using var response = await _httpClient
                .GetAsync(BuildUri("api/tags"), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
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
                "The Ollama provider is not configured. Set a base URL and a model name.");
        }

        var payload = new OllamaChatRequest(
            _options.Model,
            [.. request.Messages.Select(m => new OllamaChatMessage(ToWireRole(m.Role), m.Content))],
            Stream: false,
            new OllamaOptions(request.Temperature, request.MaxOutputTokens));

        try
        {
            using var response = await _httpClient
                .PostAsJsonAsync(BuildUri("api/chat"), payload, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return AIChatResponse.Unavailable(
                    $"Ollama returned HTTP {(int)response.StatusCode}. " +
                    "Check that the model is pulled and the endpoint is correct.");
            }

            var completion = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(cancellationToken)
                .ConfigureAwait(false);

            var content = completion?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
            {
                return AIChatResponse.Unavailable("Ollama returned an empty response.");
            }

            return AIChatResponse.FromProvider(
                content,
                Name,
                completion?.Model ?? _options.Model,
                completion?.PromptEvalCount is int prompt && completion.EvalCount is int eval
                    ? prompt + eval
                    : null);
        }
        catch (HttpRequestException)
        {
            return AIChatResponse.Unavailable(
                "Ollama could not be reached. Check that it is running and the base URL is correct.");
        }
        catch (TaskCanceledException)
        {
            return AIChatResponse.Unavailable(
                $"Ollama did not respond within {_options.TimeoutSeconds} seconds.");
        }
        catch (JsonException)
        {
            return AIChatResponse.Unavailable(
                "Ollama returned a response that is not valid chat JSON.");
        }
    }

    private Uri BuildUri(string relativePath)
    {
        var baseUrl = _options.BaseUrl!.TrimEnd('/');
        return new Uri($"{baseUrl}/{relativePath}", UriKind.Absolute);
    }

    private static string ToWireRole(AIMessageRole role) => role switch
    {
        AIMessageRole.System => "system",
        AIMessageRole.User => "user",
        AIMessageRole.Assistant => "assistant",
        AIMessageRole.Tool => "tool",
        _ => "user"
    };

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("options")] OllamaOptions Options);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaOptions(
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("model")] string? Model,
        [property: JsonPropertyName("message")] OllamaChatMessage? Message,
        [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int? EvalCount);
}
