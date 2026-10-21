$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet restore Plotarium.slnx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build Plotarium.slnx -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet run --project tests/Plotarium.ContractTests -c Release --no-build
exit $LASTEXITCODE
