using Microsoft.Extensions.Logging;

namespace DecisionSimulator.Infrastructure.Persistence;

/// <summary>
/// Source-generated log messages for the built-in data seeder.
/// </summary>
internal static partial class SeederLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Seeded {Count} built-in variable definitions.")]
    public static partial void CatalogueSeeded(ILogger logger, int count);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Seeded the sample decisions, scenarios, risks and indicators.")]
    public static partial void DemoSeedCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Warning,
        Message = "Sample data was not seeded because the variable catalogue is empty.")]
    public static partial void DemoSeedSkippedNoCatalogue(ILogger logger);
}
