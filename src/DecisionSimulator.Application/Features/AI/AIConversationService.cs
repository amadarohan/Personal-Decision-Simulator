using DecisionSimulator.Application.Common;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DecisionSimulator.Application.Features.AI;

/// <summary>
/// Manages AI conversations and turns.
/// <para>
/// The service stores every user turn before calling a provider, so a failed or
/// unavailable provider never loses what the user typed. Assistant turns are
/// stored only when a provider actually produced content.
/// </para>
/// </summary>
public sealed class AIConversationService
{
    private readonly IApplicationDbContext _db;
    private readonly AIProviderRouter _router;
    private readonly AIOptions _options;

    public AIConversationService(
        IApplicationDbContext db,
        AIProviderRouter router,
        AIOptions options)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<Result<IReadOnlyList<AIConversationDto>>> ListAsync(
        Guid? decisionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AIConversations.AsNoTracking();

        if (decisionId is not null)
        {
            query = query.Where(c => c.DecisionId == decisionId);
        }

        var conversations = await query
            .OrderByDescending(c => c.UpdatedAtUtc)
            .Select(c => new AIConversationDto(
                c.Id,
                c.Title,
                c.ContextType,
                c.DecisionId,
                c.Messages.Count,
                c.CreatedAtUtc,
                c.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<AIConversationDto>>.Success(conversations);
    }

    public async Task<Result<AIConversationDetailDto>> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _db.AIConversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return Result<AIConversationDetailDto>.Failure(
                ErrorCodes.NotFound,
                "The conversation could not be found.");
        }

        var messages = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new AIMessageDto(
                m.Id,
                m.Role,
                m.Content,
                m.Provider,
                m.Model,
                m.TokenUsage,
                m.CreatedAtUtc))
            .ToList();

        return Result<AIConversationDetailDto>.Success(new AIConversationDetailDto(
            conversation.Id,
            conversation.Title,
            conversation.ContextType,
            conversation.DecisionId,
            messages,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc));
    }

    public async Task<Result<Guid>> CreateAsync(
        CreateConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result<Guid>.Failure(
                ErrorCodes.ValidationFailed,
                "A conversation must have a title.");
        }

        if (request.DecisionId is not null)
        {
            var decisionExists = await _db.Decisions
                .AnyAsync(d => d.Id == request.DecisionId, cancellationToken);

            if (!decisionExists)
            {
                return Result<Guid>.Failure(
                    ErrorCodes.NotFound,
                    "The decision this conversation refers to could not be found.");
            }
        }

        var conversation = new AIConversation(
            request.Title.Trim(),
            request.ContextType,
            request.DecisionId);

        _db.AIConversations.Add(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(conversation.Id);
    }

    public async Task<Result> RenameAsync(
        Guid id,
        RenameConversationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Result.Failure(ErrorCodes.ValidationFailed, "A conversation must have a title.");
        }

        var conversation = await _db.AIConversations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The conversation could not be found.");
        }

        conversation.Rename(request.Title.Trim());
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var conversation = await _db.AIConversations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (conversation is null)
        {
            return Result.Failure(ErrorCodes.NotFound, "The conversation could not be found.");
        }

        _db.AIConversations.Remove(conversation);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>
    /// Records the user turn, asks the router for a completion, and records the
    /// assistant turn only if one was produced.
    /// </summary>
    public async Task<Result<SendMessageResult>> SendMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Result<SendMessageResult>.Failure(
                ErrorCodes.ValidationFailed,
                "A message cannot be empty.");
        }

        var conversation = await _db.AIConversations
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            return Result<SendMessageResult>.Failure(
                ErrorCodes.NotFound,
                "The conversation could not be found.");
        }

        var userMessage = conversation.AddMessage(
            AIMessageRole.User,
            request.Content.Trim());

        // Persist the user turn before the provider call. If the provider is slow,
        // fails, or is unavailable, the user's own words are already safe.
        await _db.SaveChangesAsync(cancellationToken);

        var history = conversation.Messages
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => new AIChatMessage(m.Role, m.Content))
            .ToList();

        var chatRequest = new AIChatRequest(
            AIPromptComposer.BuildConversation(request.Role, history, request.ContextSummary),
            request.Role,
            request.Sensitivity);

        var response = await _router.CompleteAsync(chatRequest, cancellationToken)
            .ConfigureAwait(false);

        var userDto = MapMessage(userMessage);

        if (!response.IsAvailable || string.IsNullOrWhiteSpace(response.Content))
        {
            return Result<SendMessageResult>.Success(new SendMessageResult(
                userDto,
                AssistantMessage: null,
                UnavailableReason: response.UnavailableReason
                    ?? "The assistant did not return a response."));
        }

        var assistantMessage = conversation.AddMessage(
            AIMessageRole.Assistant,
            response.Content,
            response.ProviderName,
            response.ModelName,
            response.TokenUsage);

        await _db.SaveChangesAsync(cancellationToken);

        return Result<SendMessageResult>.Success(new SendMessageResult(
            userDto,
            MapMessage(assistantMessage),
            UnavailableReason: null));
    }

    /// <summary>
    /// Reports whether the assistant can currently answer, and why not if it
    /// cannot. The UI uses this to disable the composer instead of letting the
    /// user type into a dead end.
    /// </summary>
    public async Task<AIStatusDto> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var (provider, refusal) = _router.Select(DataSensitivity.Internal);

        if (provider is null)
        {
            return new AIStatusDto(
                IsAvailable: false,
                ProviderName: _options.Provider.ToString(),
                ProviderKind: _options.Provider,
                IsRemote: false,
                MaxRemoteSensitivity: _options.MaxRemoteSensitivity,
                AllowRemoteProviders: _options.AllowRemoteProviders,
                UnavailableReason: refusal);
        }

        var reachable = await provider.IsAvailableAsync(cancellationToken).ConfigureAwait(false);

        return new AIStatusDto(
            IsAvailable: reachable && provider.Kind != AIProviderKind.None,
            ProviderName: provider.Name,
            ProviderKind: provider.Kind,
            IsRemote: provider.IsRemote,
            MaxRemoteSensitivity: _options.MaxRemoteSensitivity,
            AllowRemoteProviders: _options.AllowRemoteProviders,
            UnavailableReason: reachable
                ? (provider.Kind == AIProviderKind.None
                    ? "No language model is configured. The assistant is unavailable."
                    : null)
                : $"The '{provider.Name}' provider is not reachable.");
    }

    private static AIMessageDto MapMessage(AIMessage message) => new(
        message.Id,
        message.Role,
        message.Content,
        message.Provider,
        message.Model,
        message.TokenUsage,
        message.CreatedAtUtc);
}
