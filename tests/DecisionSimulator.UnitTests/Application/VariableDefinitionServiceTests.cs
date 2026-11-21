using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.Variables;
using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using FluentAssertions;

namespace DecisionSimulator.UnitTests.Application;

public sealed class VariableDefinitionServiceTests : IDisposable
{
    private readonly InMemoryDatabase _db = new();
    private readonly VariableDefinitionService _service;

    public VariableDefinitionServiceTests()
    {
        _service = new VariableDefinitionService(_db);
    }

    public void Dispose() => _db.Dispose();

    private static CreateVariableDefinitionRequest ValidRequest(string key = "monthly_income") =>
        new(
            key,
            "Monthly income",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "IRR/month",
            DefaultValue: 50_000_000m,
            MinimumAllowedValue: 0m,
            MaximumAllowedValue: 1_000_000_000m);

    [Fact]
    public async Task Creates_a_definition_and_normalises_the_key()
    {
        var result = await _service.CreateAsync(ValidRequest("  Monthly_Income  "));

        result.Succeeded.Should().BeTrue();

        var stored = await _db.VariableDefinitions.FindAsync(result.Value);
        stored!.Key.Should().Be("monthly_income");
        stored.Name.Should().Be("Monthly income");
        stored.Unit.Should().Be("IRR/month");
    }

    [Fact]
    public async Task Rejects_a_duplicate_key()
    {
        await _service.CreateAsync(ValidRequest("monthly_income"));

        var result = await _service.CreateAsync(ValidRequest("MONTHLY_INCOME"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
        result.ErrorMessage.Should().Contain("monthly_income");
    }

    [Fact]
    public async Task Rejects_a_key_with_an_invalid_character()
    {
        var result = await _service.CreateAsync(ValidRequest("monthly income"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Rejects_a_key_starting_with_a_digit()
    {
        var result = await _service.CreateAsync(ValidRequest("1st_income"));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Rejects_a_default_value_outside_the_allowed_range()
    {
        var request = ValidRequest() with { DefaultValue = 2_000_000_000m };

        var result = await _service.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Rejects_a_maximum_below_the_minimum()
    {
        var request = ValidRequest() with
        {
            MinimumAllowedValue = 100m,
            MaximumAllowedValue = 10m,
            DefaultValue = null
        };

        var result = await _service.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Lists_definitions_ordered_by_category_then_name()
    {
        await _service.CreateAsync(ValidRequest("z_income"));
        await _service.CreateAsync(new CreateVariableDefinitionRequest(
            "a_rent",
            "Rent",
            VariableCategory.Geographic,
            VariableDataType.Currency,
            "IRR/month"));

        var result = await _service.ListAsync();

        result.Succeeded.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        // Financial sorts before Geographic, so the financial variable comes
        // first regardless of its name.
        result.Value[0].Key.Should().Be("z_income");
        result.Value[1].Key.Should().Be("a_rent");
    }

    [Fact]
    public async Task Filters_the_list_by_category()
    {
        await _service.CreateAsync(ValidRequest("z_income"));
        await _service.CreateAsync(new CreateVariableDefinitionRequest(
            "a_rent",
            "Rent",
            VariableCategory.Geographic,
            VariableDataType.Currency,
            "IRR/month"));

        var result = await _service.ListAsync(VariableCategory.Geographic);

        result.Value.Should().ContainSingle();
        result.Value[0].Key.Should().Be("a_rent");
    }

    [Fact]
    public async Task Updates_a_definition_without_changing_its_key()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var result = await _service.UpdateAsync(
            created.Value,
            new UpdateVariableDefinitionRequest(
                "Net monthly income",
                "After tax",
                VariableCategory.Financial,
                VariableDataType.Currency,
                "IRR/month",
                45_000_000m,
                0m,
                1_000_000_000m,
                false,
                true));

        result.Succeeded.Should().BeTrue();

        var stored = await _db.VariableDefinitions.FindAsync(created.Value);
        stored!.Name.Should().Be("Net monthly income");
        stored.Key.Should().Be("monthly_income");
        stored.IsSensitive.Should().BeTrue();
    }

    [Fact]
    public async Task Returns_not_found_when_updating_an_unknown_definition()
    {
        var result = await _service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateVariableDefinitionRequest(
                "Name",
                null,
                VariableCategory.Custom,
                VariableDataType.Decimal,
                "unit",
                null,
                null,
                null,
                false,
                false));

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Deletes_an_unused_definition()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var result = await _service.DeleteAsync(created.Value);

        result.Succeeded.Should().BeTrue();
        (await _db.VariableDefinitions.FindAsync(created.Value)).Should().BeNull();
    }

    [Fact]
    public async Task Refuses_to_delete_a_definition_that_an_assumption_uses()
    {
        var created = await _service.CreateAsync(ValidRequest());

        var decision = new Decision("Delete guard", DecisionCategory.Finance);
        var scenario = decision.AddScenario("Base", ScenarioType.Baseline, 12);

        _db.Decisions.Add(decision);
        await _db.SaveChangesAsync(CancellationToken.None);

        scenario.SetVariable(
            created.Value,
            1_000m,
            DistributionType.Fixed,
            null,
            null,
            null,
            ConfidenceLevel.Medium,
            SourceType.UserInput,
            null,
            DistributionBasis.UserDefined,
            false);

        await _db.SaveChangesAsync(CancellationToken.None);

        var result = await _service.DeleteAsync(created.Value);

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.Conflict);
        result.ErrorMessage.Should().Contain("1 scenario assumption");
        (await _db.VariableDefinitions.FindAsync(created.Value)).Should().NotBeNull();
    }

    [Fact]
    public async Task Returns_not_found_for_an_unknown_definition()
    {
        var result = await _service.GetAsync(Guid.NewGuid());

        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.NotFound);
    }
}
