# Aetheris

Aetheris is a server/web-first solid modeling kernel project with an ASP.NET backend and React TypeScript frontend.

## Current status

Milestone **M00** is complete scaffolding work:

- Kernel project/test scaffolding exists.
- Core vision, non-goals, architecture, numerics, and milestone docs are defined.
- Baseline .NET CI is configured for restore/build/test.

No CAD/kernel feature implementation has started yet.

## Repository structure

- `Aetheris.Server/` — ASP.NET host/API project.
- `aetheris.client/` — React + Vite TypeScript client.
- `Aetheris.Kernel.Core/` — placeholder kernel core class library (no modeling logic yet).
- `Aetheris.Kernel.Core.Tests/` — xUnit baseline tests for kernel core scaffolding.
- `docs/` — project charter, scope, and policy documentation.

## Run .NET checks

```bash
dotnet restore Aetheris.Server/Aetheris.Server.csproj
dotnet restore Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj
dotnet build Aetheris.Server/Aetheris.Server.csproj --no-restore
dotnet build Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --no-restore
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --no-build
```

Kernel implementation milestones begin after M00.
