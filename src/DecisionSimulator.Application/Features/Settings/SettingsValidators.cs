using DecisionSimulator.Domain.Enums;
using FluentValidation;

namespace DecisionSimulator.Application.Features.Settings;

public sealed class SetSettingRequestValidator : AbstractValidator<SetSettingRequest>
{
    public SetSettingRequestValidator()
    {
        RuleFor(x => x.Value).MaximumLength(4000);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public sealed class UpdateUserProfileRequestValidator : AbstractValidator<UpdateUserProfileRequest>
{
    public UpdateUserProfileRequestValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("A display name is required.")
            .MaximumLength(200);

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("A country code is required.")
            .Length(2, 3).WithMessage("A country code must be two or three letters.");

        RuleFor(x => x.CountryName).MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(200);

        RuleFor(x => x.TimeZoneId)
            .MaximumLength(100)
            .Must(BeAKnownTimeZone)
            .When(x => !string.IsNullOrWhiteSpace(x.TimeZoneId))
            .WithMessage("'{PropertyName}' is not a time zone this machine recognises.");

        RuleFor(x => x.PreferredLanguage).MaximumLength(20);

        RuleFor(x => x.DefaultCurrencyCode)
            .NotEmpty().WithMessage("A default currency is required.")
            .Length(3).WithMessage("A currency code must be three letters.");

        RuleFor(x => x.CalendarSystem).IsInEnum();
        RuleFor(x => x.CurrencyUnitConvention).IsInEnum();
    }

    private static bool BeAKnownTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}
