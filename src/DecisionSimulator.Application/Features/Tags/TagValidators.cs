using FluentValidation;

namespace DecisionSimulator.Application.Features.Tags;

public sealed class CreateTagRequestValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A tag must have a name.")
            .MaximumLength(100);

        RuleFor(x => x.ColorHex)
            .Matches("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.ColorHex))
            .WithMessage("A colour must be a hex value such as #1a2b3c.");
    }
}

public sealed class UpdateTagRequestValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A tag must have a name.")
            .MaximumLength(100);

        RuleFor(x => x.ColorHex)
            .Matches("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")
            .When(x => !string.IsNullOrWhiteSpace(x.ColorHex))
            .WithMessage("A colour must be a hex value such as #1a2b3c.");
    }
}

public sealed class AttachTagRequestValidator : AbstractValidator<AttachTagRequest>
{
    public AttachTagRequestValidator()
    {
        RuleFor(x => x.EntityType)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.EntityId)
            .NotEmpty().WithMessage("An entity id is required.");
    }
}
