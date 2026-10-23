$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet restore Ploteus.slnx '-p:Platform=Any CPU'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet build Ploteus.slnx -c Release '-p:Platform=Any CPU' --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$testDll = 'tests/Ploteus.ContractTests/bin/Release/net10.0/Ploteus.ContractTests.dll'
if (-not (Test-Path -LiteralPath $testDll)) { $testDll = 'tests/Ploteus.ContractTests/bin/Any CPU/Release/net10.0/Ploteus.ContractTests.dll' }
dotnet $testDll
exit $LASTEXITCODE
