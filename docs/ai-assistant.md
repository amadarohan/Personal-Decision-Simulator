# AI assistant

## Default behaviour

The assistant is off by default. `AI:Provider` is `None`, and no outbound provider is registered.
The rest of the application works normally.

The `NoneAIProvider` is deterministic and offline. It makes it safe to install and open the app on a
machine that has no language model.

## Providers

| Provider | `AI:Provider` value | Requirement |
| --- | --- | --- |
| None | `None` | No endpoint or key |
| Ollama | `Ollama` | `AI:BaseUrl` |
| OpenAI-compatible | `OpenAICompatible` | `AI:BaseUrl` and usually `AI:ApiKey` |

OpenAI-compatible means an endpoint implementing the expected chat-completions contract, not
necessarily OpenAI itself.

## Remote calls are opt-in

Two settings must allow a remote call:

- `AI:AllowRemoteProviders=true`.
- The conversation or payload sensitivity does not exceed `AI:MaxRemoteSensitivity`.

The UI's settings page refuses secret keys. Supply `AI:ApiKey` through configuration or an
environment variable.

```powershell
$env:AI__ApiKey = "YOUR_API_KEY"
```

## Data sent to a provider

Only the context the assistant is given is sent. The application should not include unrelated
decisions or private settings. Sensitivity is enforced before the provider call.

The execution is recorded in `AIExecutionLog`, including provider, model, request status, latency,
tokens, and whether remote access was used.

## Consent boundary

The assistant proposes; it does not change data by itself.

An `AIProposal` stores the type of change and its proposed payload. It remains inactive until the
user confirms it. Confirmation creates or changes the relevant entity through the normal application
service, so domain invariants still hold.

## Memory

`AIMemory` stores facts the assistant may reuse. Memory is scoped and should be treated as data the
person can inspect or clear. It is not a hidden instruction channel.

## Numeric discipline

The assistant does not invent values. If it needs a number:

- It asks the person to provide it, or.
- It identifies a source and a required observation.

The assistant can draft a scenario structure, but the values remain assumptions or observations with
recorded basis and provenance.

## Failure behaviour

Provider failures are recorded and surfaced as a normal result. The assistant does not fall back to
making up a result. The rest of the application continues to work.
