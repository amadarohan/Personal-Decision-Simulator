# Development

## Prerequisites

- .NET 9 SDK.
- SQL Server 2019 or later.
- SQL Server authentication reachable from the machine.
- Optional: `dotnet-ef` CLI for migrations.

Install the EF tool if needed:

```powershell
dotnet tool install --global dotnet-ef
```

## Restore and build

```powershell
dotnet restore PersonalDecisionSimulator.sln
dotnet build PersonalDecisionSimulator.sln
```

`Directory.Build.props` enables the .NET analyzers at `latest-recommended`, nullable reference types,
and latest C# language features.

## Run

```powershell
dotnet run --project src/DecisionSimulator.Web
```

Stop a previous web process before rebuilding on Windows if its DLLs are locked:

```powershell
Stop-Process -Name DecisionSimulator.Web -Force -ErrorAction SilentlyContinue
```

## Database setup

Set the connection string in `appsettings.json` or with an environment variable:

```powershell
$env:ConnectionStrings__Default = "Server=localhost;Database=PersonalDecisionSimulator;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```

Apply migrations:

```powershell
dotnet ef database update \
  --project src/DecisionSimulator.Infrastructure \
  --startup-project src/DecisionSimulator.Web
```

List migrations:

```powershell
dotnet ef migrations list \
  --project src/DecisionSimulator.Infrastructure \
  --startup-project src/DecisionSimulator.Web
```

Create a migration:

```powershell
dotnet ef migrations add DescribeTheChange \
  --project src/DecisionSimulator.Infrastructure \
  --startup-project src/DecisionSimulator.Web
```

## SQL Server verification

Use SQL Server authentication or Windows authentication according to the instance. For Windows
authentication:

```powershell
sqlcmd -S localhost -E -d PersonalDecisionSimulator -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Decisions;"
```

To verify Persian text is stored correctly, use `LEN` and `UNICODE` rather than visually inspecting
SQL output, whose console code page may show question marks.

## Tests

Run all tests:

```powershell
dotnet test PersonalDecisionSimulator.sln
```

Run one project:

```powershell
dotnet test tests/DecisionSimulator.UnitTests/DecisionSimulator.UnitTests.csproj
dotnet test tests/DecisionSimulator.SimulationTests/DecisionSimulator.SimulationTests.csproj
dotnet test tests/DecisionSimulator.IntegrationTests/DecisionSimulator.IntegrationTests.csproj
```

## Seed behaviour

- `BuiltInDataSeeder` installs or updates the variable catalogue additively.
- `DemoDataSeeder` runs only if no decisions exist and writes `demo.data.seeded` after success.
- Both catch startup failures and log them. The application remains usable even if seeding fails.

## Health check

Open `/health` or call:

```powershell
Invoke-RestMethod http://localhost:5000/health
```

A successful response is:

```json
{ "status": "ok", "database": "reachable" }
```

## Common problems

### The build says DLL is locked

Stop `DecisionSimulator.Web` before rebuilding:

```powershell
Stop-Process -Name DecisionSimulator.Web -Force -ErrorAction SilentlyContinue
```

### No `Default` connection string

Set `ConnectionStrings__Default` or add `ConnectionStrings:Default` to
`src/DecisionSimulator.Web/appsettings.Development.json`.

### Ollama is unavailable

Leave `AI:Provider=None` or start Ollama at the configured `AI:BaseUrl`. The rest of the application
does not depend on it.

### Persian text appears as question marks

Check storage with `UNICODE(SUBSTRING(...,1,1))`, not only the SQL console rendering. `nvarchar`
and the correct culture are used by the application.

### Language switch does not appear to work

Confirm the browser is running the interactive server render mode and that the profile contains
`PreferredLanguage`. The switch forces a full reload after persisting the choice.
