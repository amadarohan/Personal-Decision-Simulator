namespace DecisionSimulator.Application.Features.Documents;

public sealed record DecisionDocumentDto(
    Guid Id,
    Guid DecisionId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string Checksum,
    DateTime CreatedAtUtc);

public sealed record UploadDocumentRequest(
    string FileName,
    string ContentType,
    Stream Content);
