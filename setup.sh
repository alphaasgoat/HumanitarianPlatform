#!/usr/bin/env bash
# Creates the .sln and wires up the projects. Run once from the repo root.
set -e
dotnet new sln -n HumanitarianPlatform
dotnet sln add src/Humanitarian.Domain src/Humanitarian.Application src/Humanitarian.Infrastructure src/Humanitarian.Api tests/Humanitarian.Tests
dotnet restore
dotnet build
