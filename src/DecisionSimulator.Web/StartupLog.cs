using Microsoft.Extensions.Logging;

namespace DecisionSimulator.Web;

/// <summary>
/// Source-generated log messages for startup. Using a generated delegate keeps
/// the logging allocation-free and the message text in one place.
/// </summary>
internal static partial class StartupLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Could not install the built-in variable catalogue.")]
    public static partial void CatalogueSeedFailed(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Could not install the sample data.")]
    public static partial void DemoSeedFailed(ILogger logger, Exception exception);
}
