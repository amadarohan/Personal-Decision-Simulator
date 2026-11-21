$ErrorActionPreference = 'Stop'
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
Set-Location "C:\Users\m.soori\Downloads\Books to read\sample projects\PersonalDecisionSimulator"

Remove-Item -Recurse -Force ".probe" -ErrorAction SilentlyContinue

dotnet sln PersonalDecisionSimulator.sln add `
  src\DecisionSimulator.Domain\DecisionSimulator.Domain.csproj `
  src\DecisionSimulator.Application\DecisionSimulator.Application.csproj `
  src\DecisionSimulator.Infrastructure\DecisionSimulator.Infrastructure.csproj `
  src\DecisionSimulator.Web\DecisionSimulator.Web.csproj `
  tests\DecisionSimulator.UnitTests\DecisionSimulator.UnitTests.csproj `
  tests\DecisionSimulator.SimulationTests\DecisionSimulator.SimulationTests.csproj `
  tests\DecisionSimulator.IntegrationTests\DecisionSimulator.IntegrationTests.csproj

Write-Output "=== RESTORE ==="
dotnet restore PersonalDecisionSimulator.sln
Write-Output "=== EXIT: $LASTEXITCODE ==="
