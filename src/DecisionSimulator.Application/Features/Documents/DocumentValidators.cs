using FluentValidation;

namespace DecisionSimulator.Application.Features.Documents;

public sealed class UploadDocumentRequestValidator : AbstractValidator<UploadDocumentRequest>
{
    public UploadDocumentRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("A file name is required.")
            .MaximumLength(260);

        RuleFor(x => x.ContentType).MaximumLength(200);

        RuleFor(x => x.Content)
            .NotNull().WithMessage("The file content is required.");
    }
}
