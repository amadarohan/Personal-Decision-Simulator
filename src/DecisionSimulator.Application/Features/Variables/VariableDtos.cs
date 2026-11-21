using DecisionSimulator.Domain.Enums;

namespace DecisionSimulator.Application.Features.Variables;

/// <summary>
/// A variable the user can bind an assumption to. The definition owns the key
/// the simulation models bind to, the unit shown in the UI, and the allowed range.
/// </summary>
public sealed record VariableDefinitionDto(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    VariableCategory Category,
    VariableDataType DataType,
    string Unit,
    decimal? DefaultValue,
    decimal? MinimumAllowedValue,
    decimal? MaximumAllowedValue,
    bool IsExternalDataCandidate,
    bool IsSensitive,
    bool IsBuiltIn);

public sealed record CreateVariableDefinitionRequest(
    string Key,
    string Name,
    VariableCategory Category,
    VariableDataType DataType,
    string Unit,
    decimal? DefaultValue = null,
    decimal? MinimumAllowedValue = null,
    decimal? MaximumAllowedValue = null,
    string? Description = null,
    bool IsExternalDataCandidate = false,
    bool IsSensitive = false);

public sealed record UpdateVariableDefinitionRequest(
    string Name,
    string? Description,
    VariableCategory Category,
    VariableDataType DataType,
    string Unit,
    decimal? DefaultValue,
    decimal? MinimumAllowedValue,
    decimal? MaximumAllowedValue,
    bool IsExternalDataCandidate,
    bool IsSensitive);
