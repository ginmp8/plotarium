#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
dotnet restore Plotarium.slnx
dotnet build Plotarium.slnx -c Release --no-restore
dotnet run --project tests/Plotarium.ContractTests -c Release --no-build
