using System.Globalization;
using DecisionSimulator.Application;
using DecisionSimulator.Application.Common;
using DecisionSimulator.Application.Features.AI;
using DecisionSimulator.Infrastructure;
using DecisionSimulator.Infrastructure.AI;
using DecisionSimulator.Infrastructure.Persistence;
using DecisionSimulator.Infrastructure.Storage;
using DecisionSimulator.Web;
using DecisionSimulator.Web.Components;
using DecisionSimulator.Web.Localization;
using DecisionSimulator.Web.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
var builder = WebApplication.CreateBuilder(args);

// Serilog writes to the console and to a rolling file so a user can send a log
// when something goes wrong without having to reproduce it under a debugger.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        Path.Combine(context.HostingEnvironment.ContentRootPath, "App_Data", "logs", "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        formatProvider: CultureInfo.InvariantCulture));

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "No 'Default' connection string is configured. Set ConnectionStrings__Default " +
        "or add it to appsettings.Development.json.");

builder.Services.AddPersistence(connectionString);

// The simulation engine lives in Infrastructure and is exposed to the
// application layer only through ISimulationEngine.
builder.Services.AddSimulationEngine();

// The application layer talks to the database through IApplicationDbContext.
// The concrete context is registered by AddPersistence, so this only exposes it
// under the narrower interface the services depend on.
builder.Services.AddScoped<IApplicationDbContext>(provider =>
    provider.GetRequiredService<DecisionSimulatorDbContext>());

builder.Services.AddApplication();

var aiOptions = builder.Configuration
    .GetSection(AIOptions.SectionName)
    .Get<AIOptions>() ?? new AIOptions();

builder.Services.AddAISubsystem(aiOptions);

var storageOptions = builder.Configuration
    .GetSection(DocumentStorageOptions.SectionName)
    .Get<DocumentStorageOptions>() ?? new DocumentStorageOptions();

builder.Services.AddSingleton(storageOptions);
builder.Services.AddSingleton<IDocumentStorage>(provider =>
    new LocalDocumentStorage(
        storageOptions,
        provider.GetRequiredService<IWebHostEnvironment>().ContentRootPath));

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<UserPreferencesAccessor>();
builder.Services.AddScoped<IUserPreferencesAccessor>(
    provider => provider.GetRequiredService<UserPreferencesAccessor>());
builder.Services.AddSingleton<DisplayFormatter>();

// The language choice is per-request state read from the profile, so the
// service is scoped. It caches the resolved tag for the life of the scope.
builder.Services.AddScoped<LanguageService>();

var app = builder.Build();

// The built-in simulation models bind to variable keys such as
// "monthly_income". Without a matching definition the user cannot enter that
// assumption, so the catalogue is installed before the first request. The
// seeder is additive and never overwrites a definition the user has edited.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var seeder = scope.ServiceProvider.GetRequiredService<BuiltInDataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // A missing catalogue degrades the app but does not make it unusable:
        // the user can still create definitions by hand. Failing to start would
        // be worse, so the error is logged and startup continues.
        StartupLog.CatalogueSeedFailed(logger, ex);
    }

    // Sample data is offered only on an empty database and only once. It runs
    // after the catalogue because the sample scenarios bind to catalogue keys.
    try
    {
        var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await demoSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Sample data is a convenience. A failure here must not stop the app.
        StartupLog.DemoSeedFailed(logger, ex);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// A health endpoint that reports whether the database is reachable. It is the
// quickest way for a user to tell "the app is broken" from "the database is down".
app.MapGet("/health", async (DecisionSimulatorDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new { status = "ok", database = "reachable" })
        : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
});

app.Run();
