#!/bin/sh
set -eu
dotnet build DaHub.slnx --configuration Release
# Stale hits must never satisfy a later run's gate.
mkdir -p /artifacts/coverage
rm -f /artifacts/coverage/coverage.json /artifacts/coverage/coverage.cobertura.xml
dotnet test tests/DaHub.Tests.Unit --configuration Release --no-build \
  -p:CollectCoverage=true -p:CoverletOutput=/artifacts/coverage/coverage
dotnet test tests/DaHub.Tests.Integration --configuration Release --no-build \
  -p:CollectCoverage=true -p:CoverletOutput=/artifacts/coverage/coverage \
  -p:MergeWith=/artifacts/coverage/coverage.json -p:Threshold=80
dotnet format DaHub.slnx --verify-no-changes --no-restore
