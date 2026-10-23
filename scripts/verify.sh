#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
dotnet restore Ploteus.slnx -p:Platform='Any CPU'
dotnet build Ploteus.slnx -p:Platform='Any CPU' -c Release --no-restore
if [ -f "tests/Ploteus.ContractTests/bin/Release/net10.0/Ploteus.ContractTests.dll" ]; then
  dotnet "tests/Ploteus.ContractTests/bin/Release/net10.0/Ploteus.ContractTests.dll"
else
  dotnet "tests/Ploteus.ContractTests/bin/Any CPU/Release/net10.0/Ploteus.ContractTests.dll"
fi
