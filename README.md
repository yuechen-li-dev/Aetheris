# Aetheris + Firmament v1

Firmament is a deterministic DSL for generating CAD geometry (STEP AP242) via Aetheris.

## What works today

- primitives and booleans: `box`, `cylinder`, `sphere`, `add`, `subtract`, `intersect`
- placement with `place.on` anchors and `offset[3]`
- validation ops: `expect_exists`, `expect_selectable`, `expect_manifold`
- schema-aware CNC minimum tool radius validation
- deterministic canonical formatting for supported `.firmament` source
- STEP AP242 export for the current single-body golden path
- assembly runtime via `.firmasm` (`asm exec`) and bounded per-instance roundtrip export (`asm export`)

## 30-second demo

Open the example at `testdata/firmament/examples/box_with_hole.firmament`.

Then run the demo-oriented helper test:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FullyQualifiedName~FirmamentBuildAndExportTests.Run_BoxWithHoleExample_Writes_Default_Export_Artifact"
```

That flow compiles the example, exports STEP using the current last-successful-body policy, and writes the artifact to:

- `testdata/firmament/exports/box_with_hole.step`

For `box_with_hole.firmament`, the exported body is currently the deterministic fallback body selected by the existing export contract, so the demo stays behaviorally accurate without changing runtime semantics.

If you want the API entry point instead of the test wrapper, use `FirmamentBuildAndExport.Run(string sourcePath)` from `Aetheris.Kernel.Firmament`.

## Automation-friendly test entrypoints

The repository now has a classic `Aetheris.sln` for compatibility with .NET 8 automation and tools that do not handle `Aetheris.slnx` reliably.

### Canonical repo-level test path

Use the shell script below as the official automation-friendly entrypoint. It runs the repository test projects in a deterministic order, prints each `dotnet test` command before running it, and fails fast on the first failing suite. In the current .NET 8 automation environment it runs the full Firmament and Server test projects plus the Core suite with `Category!=SlowCorpus`.

```bash
export PATH="$HOME/.dotnet:$PATH"
./scripts/test-all.sh
```

### Narrow fallback test paths

If you need a single project or a smaller local repro, use the project-level commands directly:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --logger "console;verbosity=minimal"
dotnet test Aetheris.Server.Tests/Aetheris.Server.Tests.csproj --logger "console;verbosity=minimal"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --logger "console;verbosity=minimal"
```

If you want the same script with a narrower or broader explicit project list, pass the test projects as arguments:

```bash
export PATH="$HOME/.dotnet:$PATH"
DOTNET_BIN=dotnet ./scripts/test-all.sh Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj Aetheris.Server.Tests/Aetheris.Server.Tests.csproj
```

For editor/IDE compatibility or solution-wide restore/build flows, use:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.sln --filter "Category!=SlowCorpus"
```

Use the `SlowCorpus` category to keep the heavyweight STEP242 NIST audit out of default solution runs. Invoke that corpus explicitly when needed:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.sln --filter "Category!=SlowCorpus"
dotnet test Aetheris.Kernel.Core.Tests --filter "Category=SlowCorpus"
```

`Aetheris.slnx` is retained for newer tooling convenience, but routine automation should use `./scripts/test-all.sh` and `Aetheris.sln`.

## Repo map

- language/compiler/runtime: `Aetheris.Kernel.Firmament/`
- examples: `testdata/firmament/examples/`
- exported STEP artifacts: `testdata/firmament/exports/`

## Docs

- assembly model and contracts (canonical): `docs/assembly.md`
- overview: `docs/firmament-overview.md`
- build/export workflow: `docs/firmament-build-workflow.md`
- selector contracts: `docs/firmament-selectors.md`
- demo script: `docs/firmament-demo.md`

## Assembly model snapshot

- `.firmasm` is authoritative for assembly semantics in Aetheris.
- STEP is treated as foreign interop input/output.
- Multi-root STEP is assembly-like input and must route through assembly extraction/import, not single-part import.
- Current roundtrip export is intentionally bounded to per-instance STEP + `roundtrip.package.json`.

## Notes

- The repository still contains broader Aetheris server/client scaffolding; the Firmament + CLI paths above are the primary deterministic kernel entry points.
