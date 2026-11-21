using DecisionSimulator.Application.Features.Scenarios;
using FluentValidation;

namespace DecisionSimulator.Application.Features.Scenarios;

public sealed class CreateScenarioRequestValidator : AbstractValidator<CreateScenarioRequest>
{
    public CreateScenarioRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A scenario needs a name.")
            .MaximumLength(200).WithMessage("A scenario name cannot exceed 200 characters.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Choose a valid scenario type.");

        RuleFor(x => x.TimeHorizonMonths)
            .InclusiveBetween(1, 600)
            .WithMessage("A scenario horizon must be between 1 and 600 months.");

        RuleFor(x => x.Description)
            .MaximumLength(4_000).WithMessage("A description cannot exceed 4000 characters.");
    }
}

public sealed class UpdateScenarioRequestValidator : AbstractValidator<UpdateScenarioRequest>
{
    public UpdateScenarioRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A scenario needs a name.")
            .MaximumLength(200).WithMessage("A scenario name cannot exceed 200 characters.");

        RuleFor(x => x.Type).IsInEnum();

        RuleFor(x => x.TimeHorizonMonths)
            .InclusiveBetween(1, 600)
            .WithMessage("A scenario horizon must be between 1 and 600 months.");
    }
}

/// <summary>
/// Validates an assumption before it reaches the domain. The distribution
/// parameter rules mirror the domain's own checks so the user sees a field-level
/// message rather than a thrown exception.
/// </summary>
public sealed class SetScenarioVariableRequestValidator : AbstractValidator<SetScenarioVariableRequest>
{
    public SetScenarioVariableRequestValidator()
    {
        RuleFor(x => x.VariableDefinitionId)
            .NotEmpty().WithMessage("Choose the variable this value belongs to.");

        RuleFor(x => x.DistributionType)
            .IsInEnum().WithMessage("Choose a valid distribution type.");

        RuleFor(x => x.StandardDeviation)
            .GreaterThan(0m)
            .When(x => x.DistributionType is Domain.Enums.DistributionType.Normal
                or Domain.Enums.DistributionType.LogNormal)
            .WithMessage("A normal or log-normal distribution needs a positive standard deviation.");

        RuleFor(x => x.MinimumValue)
            .NotNull()
            .When(x => x.DistributionType is Domain.Enums.DistributionType.Uniform
                or Domain.Enums.DistributionType.Triangular)
            .WithMessage("This distribution needs a minimum value.");

        RuleFor(x => x.MaximumValue)
            .NotNull()
            .When(x => x.DistributionType is Domain.Enums.DistributionType.Uniform
                or Domain.Enums.DistributionType.Triangular)
            .WithMessage("This distribution needs a maximum value.");

        RuleFor(x => x.MaximumValue)
            .GreaterThanOrEqualTo(x => x.MinimumValue!.Value)
            .When(x => x.MinimumValue is not null && x.MaximumValue is not null)
            .WithMessage("The maximum must be greater than or equal to the minimum.");

        RuleFor(x => x.DiscreteOutcomesJson)
            .NotEmpty()
            .When(x => x.DistributionType is Domain.Enums.DistributionType.Discrete
                or Domain.Enums.DistributionType.Custom)
            .WithMessage("This distribution needs at least one outcome.");

        RuleFor(x => x.SourceReference)
            .MaximumLength(1_000).WithMessage("A source reference cannot exceed 1000 characters.");
    }
}
