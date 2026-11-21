using FluentValidation;

namespace DecisionSimulator.Application.Features.Data;

public sealed class CreateDataSourceRequestValidator : AbstractValidator<CreateDataSourceRequest>
{
    public CreateDataSourceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A data source must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.UpdateFrequency).MaximumLength(100);

        RuleFor(x => x.BaseUrl)
            .MaximumLength(2000)
            .Must(BeAnAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.BaseUrl))
            .WithMessage("The base URL must be an absolute http or https URL.");

        RuleFor(x => x.ProviderType).IsInEnum();
        RuleFor(x => x.ReliabilityLevel).IsInEnum();
    }

    private static bool BeAnAbsoluteHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
}

public sealed class UpdateDataSourceRequestValidator : AbstractValidator<UpdateDataSourceRequest>
{
    public UpdateDataSourceRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("A data source must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.UpdateFrequency).MaximumLength(100);

        RuleFor(x => x.BaseUrl)
            .MaximumLength(2000)
            .Must(BeAnAbsoluteHttpUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.BaseUrl))
            .WithMessage("The base URL must be an absolute http or https URL.");

        RuleFor(x => x.ReliabilityLevel).IsInEnum();
    }

    private static bool BeAnAbsoluteHttpUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed)
        && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
}

public sealed class CreateEconomicIndicatorRequestValidator : AbstractValidator<CreateEconomicIndicatorRequest>
{
    public CreateEconomicIndicatorRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("An indicator must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.IndicatorCode)
            .NotEmpty().WithMessage("An indicator must have a code.")
            .MaximumLength(100);

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("An indicator must have a country code.")
            .Length(2, 3)
            .WithMessage("A country code must be two or three characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("An indicator must have a unit so its value is interpretable.")
            .MaximumLength(50);

        RuleFor(x => x.City).MaximumLength(200);

        RuleFor(x => x.FreshnessWindow)
            .Must(window => window is null || window.Value > TimeSpan.Zero)
            .WithMessage("A freshness window must be positive.");

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.GeographicScope).IsInEnum();
    }
}

public sealed class UpdateEconomicIndicatorRequestValidator : AbstractValidator<UpdateEconomicIndicatorRequest>
{
    public UpdateEconomicIndicatorRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("An indicator must have a name.")
            .MaximumLength(200);

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("An indicator must have a country code.")
            .Length(2, 3)
            .WithMessage("A country code must be two or three characters.");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("An indicator must have a unit so its value is interpretable.")
            .MaximumLength(50);

        RuleFor(x => x.City).MaximumLength(200);

        RuleFor(x => x.FreshnessWindow)
            .Must(window => window is null || window.Value > TimeSpan.Zero)
            .WithMessage("A freshness window must be positive.");

        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.GeographicScope).IsInEnum();
    }
}

public sealed class RecordObservationRequestValidator : AbstractValidator<RecordObservationRequest>
{
    public RecordObservationRequestValidator()
    {
        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("An observation must have a unit so its value is interpretable.")
            .MaximumLength(50);

        RuleFor(x => x)
            .Must(x => x.IndicatorId is not null || x.VariableDefinitionId is not null)
            .WithMessage("An observation must reference either an indicator or a variable definition.");

        RuleFor(x => x.ObservationDate)
            .Must(date => date != default)
            .WithMessage("An observation must have the date the value describes.");

        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.SourceReference).MaximumLength(2000);

        RuleFor(x => x.VerificationStatus).IsInEnum();
        RuleFor(x => x.Confidence).IsInEnum();
    }
}
