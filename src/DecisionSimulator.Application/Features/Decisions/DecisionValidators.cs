using FluentValidation;

namespace DecisionSimulator.Application.Features.Decisions;

/// <summary>
/// Validates the shape of a create request before it reaches the domain. The
/// domain still enforces its own invariants; this exists so the user gets a
/// field-level message instead of a single exception.
/// </summary>
public sealed class CreateDecisionRequestValidator : AbstractValidator<CreateDecisionRequest>
{
    public CreateDecisionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A decision needs a title.")
            .MaximumLength(300).WithMessage("A decision title cannot exceed 300 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(4_000).WithMessage("A description cannot exceed 4000 characters.");

        RuleFor(x => x.Category)
            .IsInEnum().WithMessage("Choose a valid decision category.");

        RuleFor(x => x.Importance)
            .IsInEnum().WithMessage("Choose a valid importance level.");

        RuleFor(x => x.Horizon)
            .IsInEnum().WithMessage("Choose a valid time horizon.");

        RuleFor(x => x.RiskTolerance)
            .IsInEnum().WithMessage("Choose a valid risk tolerance.");
    }
}

public sealed class UpdateDecisionRequestValidator : AbstractValidator<UpdateDecisionRequest>
{
    public UpdateDecisionRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A decision needs a title.")
            .MaximumLength(300).WithMessage("A decision title cannot exceed 300 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(4_000).WithMessage("A description cannot exceed 4000 characters.");

        RuleFor(x => x.Context)
            .MaximumLength(8_000).WithMessage("Context cannot exceed 8000 characters.");

        RuleFor(x => x.DesiredOutcome)
            .MaximumLength(4_000).WithMessage("The desired outcome cannot exceed 4000 characters.");

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Importance).IsInEnum();
        RuleFor(x => x.Horizon).IsInEnum();
        RuleFor(x => x.RiskTolerance).IsInEnum();
    }
}

public sealed class CreateAlternativeRequestValidator : AbstractValidator<CreateAlternativeRequest>
{
    public CreateAlternativeRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("An alternative needs a name.")
            .MaximumLength(200).WithMessage("An alternative name cannot exceed 200 characters.");

        RuleFor(x => x.EstimatedInitialCost)
            .GreaterThanOrEqualTo(0m).When(x => x.EstimatedInitialCost is not null)
            .WithMessage("An initial cost cannot be negative.");

        RuleFor(x => x.EstimatedRecurringCost)
            .GreaterThanOrEqualTo(0m).When(x => x.EstimatedRecurringCost is not null)
            .WithMessage("A recurring cost cannot be negative.");
    }
}

public sealed class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.ActualOutcome)
            .NotEmpty().WithMessage("A review must record what actually happened.")
            .MaximumLength(4_000).WithMessage("The actual outcome cannot exceed 4000 characters.");

        RuleFor(x => x.ReviewDateUtc)
            .NotEqual(default(DateTime)).WithMessage("A review needs a date.");
    }
}
