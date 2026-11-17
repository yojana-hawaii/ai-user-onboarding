#!/bin/sh
set -eu
dotnet build DaHub.slnx --configuration Release
dotnet tool restore
dotnet ef migrations list --no-connect --project src/DaHub.Infrastructure --configuration Release --no-build
dotnet ef migrations has-pending-model-changes --project src/DaHub.Infrastructure --configuration Release --no-build
mkdir -p /artifacts/migrations
dotnet ef migrations script --idempotent --project src/DaHub.Infrastructure --configuration Release --no-build --output /artifacts/migrations/idempotent.sql
# Stale hits must never satisfy a later run's gate.
mkdir -p /artifacts/coverage
rm -f /artifacts/coverage/coverage.json /artifacts/coverage/coverage.cobertura.xml
dotnet test tests/DaHub.Tests.Unit --configuration Release --no-build \
  -p:CollectCoverage=true -p:CoverletOutput=/artifacts/coverage/coverage
dotnet test tests/DaHub.Tests.Integration --configuration Release --no-build \
  -p:CollectCoverage=true -p:CoverletOutput=/artifacts/coverage/coverage \
  -p:MergeWith=/artifacts/coverage/coverage.json -p:Threshold=80
dotnet format DaHub.slnx --verify-no-changes --no-restore
