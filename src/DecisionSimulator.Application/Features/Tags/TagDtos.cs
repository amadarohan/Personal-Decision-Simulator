namespace DecisionSimulator.Application.Features.Tags;

public sealed record TagDto(Guid Id, string Name, string? ColorHex, int UsageCount);

public sealed record CreateTagRequest(string Name, string? ColorHex = null);

public sealed record UpdateTagRequest(string Name, string? ColorHex);

public sealed record AttachTagRequest(string EntityType, Guid EntityId);

public sealed record TaggedEntityDto(string EntityType, Guid EntityId);
