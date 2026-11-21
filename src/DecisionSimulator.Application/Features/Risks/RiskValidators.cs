using FluentValidation;

namespace DecisionSimulator.Application.Features.Risks;

public sealed class CreateRiskFactorRequestValidator : AbstractValidator<CreateRiskFactorRequest>
{
    public CreateRiskFactorRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A risk factor must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(4000);

        RuleFor(x => x.ImpactMagnitude)
            .MaximumLength(1000);

        RuleFor(x => x.SourceReference)
            .MaximumLength(1000);

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.GeographicScope).IsInEnum();
        RuleFor(x => x.ImpactDirection).IsInEnum();
        RuleFor(x => x.LikelihoodAssessment).IsInEnum();
        RuleFor(x => x.Confidence).IsInEnum();
    }
}

public sealed class UpdateRiskFactorRequestValidator : AbstractValidator<UpdateRiskFactorRequest>
{
    public UpdateRiskFactorRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A risk factor must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(4000);

        RuleFor(x => x.ImpactMagnitude)
            .MaximumLength(1000);

        RuleFor(x => x.SourceReference)
            .MaximumLength(1000);

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.GeographicScope).IsInEnum();
        RuleFor(x => x.ImpactDirection).IsInEnum();
        RuleFor(x => x.LikelihoodAssessment).IsInEnum();
        RuleFor(x => x.Confidence).IsInEnum();
    }
}
