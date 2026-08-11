# Preview 2 final validation report

Validated on Windows x64, .NET SDK 10.0.302, 2026-08-10.

## Build and tests

- `dotnet build Aetheris.slnx -f net10.0 /m:1`: success, 0 warnings, 0 errors.
- `dotnet test Aetheris.slnx -f net10.0 --no-build`: 2,696 passed,
  0 failed, 0 skipped across 11 discovered test assemblies. The legacy
  `Aetheris.FrictionLab.Tests` assembly contains no discoverable tests.
- Focused KernelSDK/Host safety and enum regressions: 10 passed.
- Focused Concept Path/Compose diagnostics: 11 passed.
- Cadmata frontend: 78 tests passed; format, typecheck, build, and lint passed.
- Firmament VSIX: 13 extension/grammar tests passed; typecheck and bundle passed.
- Public documentation sibling checkout already presents Preview 2 as current;
  its production build passed without modifying that repository.

## Installed artifact smoke

- Self-contained bundle: `aetheris 2.0.0-preview.2`, help, validate, build,
  STEP inspect, and verify passed from a fresh extraction directory.
- NuGet package: installed into a fresh tool path from a local feed; reported
  `aetheris 2.0.0-preview.2`; canonical compile and STEP inspect passed.
- Assembly: `aetheris asm inspect fixtures/AssemblyM0/bearing-module.firmament
  --json` succeeded, including fit/tolerance results.
- Drawing: canonical fixture emitted DrawingIR, SVG, vector PDF, editable PPTX,
  review PPTX, and DFM review PPTX.
- Modules: packaged CLI returned the built-in module catalog.
- Forge sample/database/Continuum/FEA/SurfaceMeshIR/Piping/Surfacing/Review lanes
  are covered by the passing solution test matrix and persisted milestone evidence.

## Security and dependencies

- `dotnet list Aetheris.slnx package --vulnerable --include-transitive` reported
  no vulnerable packages in the publishable CLI graph. The `.esproj` is not a
  PackageReference project and is audited through tspack/npm instead.
- npm packaging audit reported 0 vulnerabilities.
- tspack retained two acknowledged advisories: 21 multi-version lock resolutions
  and 114 lifecycle scripts blocked by category policy. Neither executed nor
  produced a release failure.
- Targeted secret/path scan found no credentials. Historical evidence files do
  contain developer-machine absolute paths; none are included in release assets.

## Reproducibility

Two consecutive complete packaging runs produced byte-identical artifacts.
ZIP entry timestamps, NuGet package relationship IDs, and static-web-asset
Last-Modified metadata are canonicalized by `scripts/package-release.ps1`.

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| `Aetheris-2.0.0-preview.2-win-x64.zip` | 95,329,050 | `b0c8b9f94ee0e48ccbc4b782def925cd4691cb336423daef51ea0369b82ea065` |
| `aetheris-firmament-0.2.0-preview.2.vsix` | 12,143 | `badd7dbb9a045a6d6fa163bbbaeb03d22ef5b38e1aa60b4d986ce4bef5799762` |
| `Aetheris.CLI.2.0.0-preview.2.nupkg` | 3,529,189 | `0766dc525c7e89f775037ea75face25b26bc426f18527fed903c5606548ef219` |

The NuGet package contains the CLI tool payload and its runtime assemblies,
readme, nuspec, and tool settings; it contains no source tree, tests, secrets,
or evidence directory. The Windows bundle includes the self-contained CLI and
Cadmata by contract. The VSIX contains ten editor-extension entries only.

## Publication closeout

The final tag, GitHub Latest state, public release asset hashes, NuGet trusted
publication, and clean public-feed installation are recorded in
`publication-proof.md`. The release workflow completed successfully after this
local candidate validation, and the tagged release commit remains the immutable
source of the published artifacts.
