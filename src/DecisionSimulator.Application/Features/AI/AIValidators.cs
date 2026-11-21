using FluentValidation;

namespace DecisionSimulator.Application.Features.AI;

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A conversation must have a title.")
            .MaximumLength(200);

        RuleFor(x => x.ContextType).IsInEnum();
    }
}

public sealed class RenameConversationRequestValidator : AbstractValidator<RenameConversationRequest>
{
    public RenameConversationRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A conversation must have a title.")
            .MaximumLength(200);
    }
}

public sealed class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("A message cannot be empty.")
            .MaximumLength(20_000);

        RuleFor(x => x.ContextSummary).MaximumLength(20_000);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Sensitivity).IsInEnum();
    }
}

public sealed class CreateProposalRequestValidator : AbstractValidator<CreateProposalRequest>
{
    public CreateProposalRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("A proposal must have a title.")
            .MaximumLength(300);

        RuleFor(x => x.Description).MaximumLength(4000);

        RuleFor(x => x.PayloadJson)
            .NotEmpty().WithMessage("A proposal must carry a payload describing the proposed change.")
            .MaximumLength(100_000);

        RuleFor(x => x.EvidenceJson).MaximumLength(100_000);
        RuleFor(x => x.ProposalType).IsInEnum();
        RuleFor(x => x.Confidence).IsInEnum();
    }
}

public sealed class RejectProposalRequestValidator : AbstractValidator<RejectProposalRequest>
{
    public RejectProposalRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(2000);
    }
}

public sealed class CreateMemoryRequestValidator : AbstractValidator<CreateMemoryRequest>
{
    public CreateMemoryRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("A memory cannot be empty.")
            .MaximumLength(4000);

        RuleFor(x => x.SourceReference).MaximumLength(1000);
        RuleFor(x => x.MemoryType).IsInEnum();
        RuleFor(x => x.Confidence).IsInEnum();
    }
}

public sealed class UpdateMemoryRequestValidator : AbstractValidator<UpdateMemoryRequest>
{
    public UpdateMemoryRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("A memory cannot be empty.")
            .MaximumLength(4000);

        RuleFor(x => x.SourceReference).MaximumLength(1000);
        RuleFor(x => x.Confidence).IsInEnum();
    }
}
