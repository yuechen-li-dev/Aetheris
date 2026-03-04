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

## Debug quickstart

### Visual Studio (canonical)

1. Open `Aetheris.slnx`.
2. Set `Aetheris.Server` as the startup project.
3. Press `F5`.

Visual Studio launches the ASP.NET host and the Vite dev server automatically via SpaProxy, then opens the app root page. API traffic remains under `/api` and proxies to the backend automatically in development.

### CLI (works everywhere, including VS Code terminals)

Run in two terminals:

```bash
# Terminal 1
cd Aetheris.Server
dotnet watch run
```

```bash
# Terminal 2
cd aetheris.client
npm run dev
```

Backend defaults to `https://localhost:7145` and the client dev server is pinned to `https://localhost:5173`.

