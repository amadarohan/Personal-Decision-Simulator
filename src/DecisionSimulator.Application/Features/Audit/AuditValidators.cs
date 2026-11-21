using FluentValidation;

namespace DecisionSimulator.Application.Features.Audit;

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(x => x.EntityType).MaximumLength(100);
        RuleFor(x => x.EntityId).MaximumLength(100);
        RuleFor(x => x.Action).MaximumLength(200);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, 500);

        RuleFor(x => x.ToUtc)
            .GreaterThanOrEqualTo(x => x.FromUtc!.Value)
            .When(x => x.FromUtc.HasValue && x.ToUtc.HasValue)
            .WithMessage("The end of the range cannot be before its start.");
    }
}

public sealed class RecordBackupRequestValidator : AbstractValidator<RecordBackupRequest>
{
    public RecordBackupRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .MaximumLength(260);

        RuleFor(x => x.RelativePath)
            .NotEmpty().WithMessage("A relative path is required.")
            .MaximumLength(1000);

        RuleFor(x => x.SizeBytes).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Checksum).MaximumLength(200);
        RuleFor(x => x.Message).MaximumLength(2000);
    }
}
