using DecisionSimulator.Domain.Entities;
using DecisionSimulator.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// Installs the variable definitions that the built-in simulation models bind to.
/// <para>
/// A model declares its required inputs by key (for example
/// <c>monthly_income</c>). Without a matching definition the user has no way to
/// enter that assumption, so every run would report a missing input. Seeding the
/// catalogue is therefore what makes the engine reachable from the UI at all.
/// </para>
/// <para>
/// The seeder is idempotent and additive: it inserts only keys that are absent
/// and never overwrites a definition the user has edited. A definition the user
/// deleted is restored on the next start, because the models still require it.
/// </para>
/// </summary>
public sealed class BuiltInDataSeeder
{
    private readonly DecisionSimulatorDbContext _db;
    private readonly ILogger<BuiltInDataSeeder> _logger;

    public BuiltInDataSeeder(DecisionSimulatorDbContext db, ILogger<BuiltInDataSeeder> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The catalogue of built-in definitions. Keys must match the input keys
    /// declared by the simulation models; the unit and bounds are what the UI
    /// uses to render and validate an assumption.
    /// </summary>
    private static IReadOnlyList<VariableDefinition> BuildCatalogue() =>
    [
        // --- Cash flow projection -------------------------------------------------
        new VariableDefinition(
            "monthly_income",
            "Monthly income",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Take-home income received each month, after tax.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "monthly_expenses",
            "Monthly expenses",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Total recurring spending each month, including rent and debt payments.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "starting_cash",
            "Starting cash",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "total",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Liquid savings available on the first day of the projection.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "monthly_income_growth",
            "Monthly income growth",
            VariableCategory.Financial,
            VariableDataType.Percentage,
            "% per month",
            defaultValue: 0m,
            minimumAllowedValue: -100m,
            maximumAllowedValue: 100m,
            description: "Expected month-on-month change in income, as a percentage.",
            isExternalDataCandidate: false,
            isSensitive: false),

        new VariableDefinition(
            "monthly_expense_growth",
            "Monthly expense growth",
            VariableCategory.Financial,
            VariableDataType.Percentage,
            "% per month",
            defaultValue: 0m,
            minimumAllowedValue: -100m,
            maximumAllowedValue: 100m,
            description: "Expected month-on-month change in expenses, excluding inflation.",
            isExternalDataCandidate: false,
            isSensitive: false),

        new VariableDefinition(
            "monthly_inflation",
            "Monthly inflation",
            VariableCategory.Economic,
            VariableDataType.Percentage,
            "% per month",
            defaultValue: 0m,
            minimumAllowedValue: -100m,
            maximumAllowedValue: 100m,
            description: "Inflation applied to expenses only. Income is not indexed automatically.",
            isExternalDataCandidate: true,
            isSensitive: false),

        new VariableDefinition(
            "one_off_initial_cost",
            "One-off initial cost",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "total",
            defaultValue: 0m,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "A single cost paid at the start, such as a deposit or moving fee.",
            isExternalDataCandidate: false,
            isSensitive: true),

        // --- Debt amortization ----------------------------------------------------
        new VariableDefinition(
            "loan_principal",
            "Loan principal",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "total",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Amount borrowed before interest.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "annual_interest_rate",
            "Annual interest rate",
            VariableCategory.Financial,
            VariableDataType.Percentage,
            "% per year",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: 1000m,
            description: "Nominal annual rate. The model divides it by twelve to get the monthly rate.",
            isExternalDataCandidate: true,
            isSensitive: false),

        new VariableDefinition(
            "loan_term_months",
            "Loan term",
            VariableCategory.Financial,
            VariableDataType.Integer,
            "months",
            defaultValue: null,
            minimumAllowedValue: 1m,
            maximumAllowedValue: 600m,
            description: "Number of monthly instalments.",
            isExternalDataCandidate: false,
            isSensitive: false),

        // --- Business viability ---------------------------------------------------
        new VariableDefinition(
            "business_startup_cost",
            "Startup cost",
            VariableCategory.Business,
            VariableDataType.Currency,
            "total",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Cash spent before the first month of trading.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "business_monthly_revenue",
            "Monthly revenue",
            VariableCategory.Business,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Revenue in the first month of trading.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "business_monthly_cost",
            "Monthly operating cost",
            VariableCategory.Business,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Fixed and variable costs of running the business each month.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "business_revenue_growth",
            "Monthly revenue growth",
            VariableCategory.Business,
            VariableDataType.Percentage,
            "% per month",
            defaultValue: 0m,
            minimumAllowedValue: -100m,
            maximumAllowedValue: 100m,
            description: "Expected month-on-month revenue change.",
            isExternalDataCandidate: false,
            isSensitive: false),

        // --- Relocation comparison ------------------------------------------------
        new VariableDefinition(
            "destination_monthly_income",
            "Destination monthly income",
            VariableCategory.Career,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Take-home income expected after moving.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "destination_monthly_expenses",
            "Destination monthly expenses",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "per month",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Total recurring spending expected after moving.",
            isExternalDataCandidate: false,
            isSensitive: true),

        new VariableDefinition(
            "relocation_one_off_cost",
            "Relocation cost",
            VariableCategory.Financial,
            VariableDataType.Currency,
            "total",
            defaultValue: null,
            minimumAllowedValue: 0m,
            maximumAllowedValue: null,
            description: "Total cost of the move: transport, deposits, fees and lost income.",
            isExternalDataCandidate: false,
            isSensitive: true)
    ];

    /// <summary>
    /// Inserts any missing built-in definition. Returns the number inserted.
    /// </summary>
    public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
    {
        var existingKeys = await _db.VariableDefinitions
            .AsNoTracking()
            .Select(v => v.Key)
            .ToListAsync(cancellationToken);

        var known = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);
        var inserted = 0;

        foreach (var definition in BuildCatalogue())
        {
            if (!known.Add(definition.Key))
            {
                continue;
            }

            definition.MarkAsBuiltIn();
            _db.VariableDefinitions.Add(definition);
            inserted++;
        }

        if (inserted == 0)
        {
            return 0;
        }

        await _db.SaveChangesAsync(cancellationToken);

        SeederLog.CatalogueSeeded(_logger, inserted);

        return inserted;
    }
}
