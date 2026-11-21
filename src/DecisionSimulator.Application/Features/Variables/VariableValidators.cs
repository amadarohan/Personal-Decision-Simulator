using FluentValidation;

namespace DecisionSimulator.Application.Features.Variables;

public sealed class CreateVariableDefinitionRequestValidator : AbstractValidator<CreateVariableDefinitionRequest>
{
    public CreateVariableDefinitionRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithMessage("A variable key is required.")
            .MaximumLength(100)
            .Matches("^[A-Za-z_][A-Za-z0-9_]*$")
            .WithMessage("The key may only contain letters, digits and underscores, and cannot start with a digit.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A variable name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("A unit is required so the value is interpretable.")
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Category)
            .IsInEnum();

        RuleFor(x => x.DataType)
            .IsInEnum();

        RuleFor(x => x.MaximumAllowedValue)
            .GreaterThanOrEqualTo(x => x.MinimumAllowedValue!.Value)
            .When(x => x.MinimumAllowedValue is not null && x.MaximumAllowedValue is not null)
            .WithMessage("The maximum allowed value cannot be less than the minimum.");

        RuleFor(x => x.DefaultValue)
            .Must((request, value) => value is null
                || (request.MinimumAllowedValue is null || value >= request.MinimumAllowedValue)
                && (request.MaximumAllowedValue is null || value <= request.MaximumAllowedValue))
            .WithMessage("The default value must lie within the allowed range.");
    }
}

public sealed class UpdateVariableDefinitionRequestValidator : AbstractValidator<UpdateVariableDefinitionRequest>
{
    public UpdateVariableDefinitionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A variable name is required.")
            .MaximumLength(200);

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("A unit is required so the value is interpretable.")
            .MaximumLength(50);

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.Category)
            .IsInEnum();

        RuleFor(x => x.DataType)
            .IsInEnum();

        RuleFor(x => x.MaximumAllowedValue)
            .GreaterThanOrEqualTo(x => x.MinimumAllowedValue!.Value)
            .When(x => x.MinimumAllowedValue is not null && x.MaximumAllowedValue is not null)
            .WithMessage("The maximum allowed value cannot be less than the minimum.");

        RuleFor(x => x.DefaultValue)
            .Must((request, value) => value is null
                || (request.MinimumAllowedValue is null || value >= request.MinimumAllowedValue)
                && (request.MaximumAllowedValue is null || value <= request.MaximumAllowedValue))
            .WithMessage("The default value must lie within the allowed range.");
    }
}
