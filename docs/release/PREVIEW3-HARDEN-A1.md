# PREVIEW3-HARDEN-A1 — release topology and qualification

Status: qualified on 2026-08-16 for `2.0.0-preview.3` on Windows x64.

This is a feature-freeze milestone. Only corrections, packaging, diagnostics,
documentation, portability, NativeAOT, determinism, and tests for existing
behavior are in scope. New geometry, Sheet Metal, FEA, Firmament, Forge,
Cadmata, physics, and interop feature families remain deferred.

## Release topology

```text
Public .NET packages
  Aetheris.Kernel.Core            Aetheris.Modules
  Aetheris.Geometry               Aetheris.Semantics
  Aetheris.Forge                  Aetheris.Collaboration
  Aetheris.Kernel.StandardLibrary Aetheris.Continuum
  Aetheris.Surfacing              Aetheris.Piping
  Aetheris.SheetMetal             Aetheris.Modules.BuiltIn
  Aetheris.Kernel.Firmament       Aetheris.FEA
  Aetheris.Forge.Host             Aetheris.Forge.KernelSDK

Executables
  Aetheris.CLI global-tool package and self-contained win-x64 executable
  Aetheris.Forge.Host NativeAOT win-x64 executable (Protocol v1)
  Aetheris.Server/Cadmata self-contained win-x64 host

Client/editor
  aetheris.client Cadmata production bundle, built from manifest.tsx + ts-lock.toml
  aetheris-firmament VS Code VSIX, built from manifest.tsx + ts-lock.toml

Runtime assets
  Aetheris.Kernel.StandardLibrary/Materials/Database/aetheris-materials-x1.sqlite
  SQLite native runtime supplied through SQLitePCLRaw
  Cadmata static web assets under the published server wwwroot

Samples/reference data
  testdata/firmament/examples and testdata/firmament/exports
  samples/forge-interop-x1 foreign-language clients
  samples/Aetheris.Samples.DatabaseDrivenCad
  testdata/step242/nist reference models
  docs/geometry/artifacts/bunny-m4 derived Stanford Bunny evidence

Licensing/provenance
  LICENSE (Aetheris code: AGPL-3.0)
  THIRD_PARTY_NOTICES.md (bundled/reference assets retain their provenance)
```

`Aetheris.Reconstruction`, `Aetheris.Firmament.FrictionLab`, server projects,
demos, samples, tests, and development tools are not independent public library
packages. Reconstruction and FrictionLab may be implementation dependencies of
the distributed executables.

Build prerequisites are the .NET 10 SDK, the Windows x64 C++ toolchain required
by .NET NativeAOT, and TSPack. Cadmata and the VS Code extension are TSPack
workspaces. Their committed `ts-lock.toml` files are authoritative; release
qualification does not invoke npm directly or introduce `package-lock.json`.

## Package inventory

| Package/artifact | Version | Role | Qualified |
| --- | --- | --- | --- |
| `Aetheris.Kernel.Core` | 2.0.0-preview.3 | exact kernel, BRep, STEP | Yes |
| `Aetheris.Modules` | 2.0.0-preview.3 | module contracts | Yes |
| `Aetheris.Geometry` | 2.0.0-preview.3 | geometry API | Yes |
| `Aetheris.Semantics` | 2.0.0-preview.3 | semantic model | Yes |
| `Aetheris.Forge` | 2.0.0-preview.3 | Forge abstractions | Yes |
| `Aetheris.Collaboration` | 2.0.0-preview.3 | collaboration model | Yes |
| `Aetheris.Kernel.StandardLibrary` | 2.0.0-preview.3 | standard library and material catalog | Yes |
| `Aetheris.Continuum` | 2.0.0-preview.3 | bounded continuum infrastructure | Yes |
| `Aetheris.Surfacing` | 2.0.0-preview.3 | surfacing | Yes |
| `Aetheris.Piping` | 2.0.0-preview.3 | piping | Yes |
| `Aetheris.SheetMetal` | 2.0.0-preview.3 | Sheet Metal | Yes |
| `Aetheris.Modules.BuiltIn` | 2.0.0-preview.3 | built-in modules | Yes |
| `Aetheris.Kernel.Firmament` | 2.0.0-preview.3 | Firmament compiler/runtime | Yes |
| `Aetheris.FEA` | 2.0.0-preview.3 | existing FEA analysis | Yes |
| `Aetheris.Forge.Host` | 2.0.0-preview.3 | host SDK/package | Yes |
| `Aetheris.Forge.KernelSDK` | 2.0.0-preview.3 | Forge kernel SDK | Yes |
| `Aetheris.CLI` | 2.0.0-preview.3 | .NET tool and bundled CLI | Yes |
| `Aetheris-2.0.0-preview.3-win-x64.zip` | 2.0.0-preview.3 | Windows product bundle | Yes |
| `aetheris-firmament` VSIX | 0.3.0-preview.3 | editor integration | Yes |

Package inspection found one expected `lib/net10.0` assembly and the package
README in every public library package, no PDBs, and no absolute developer path
in package contents or metadata. Dependencies use the same Preview 3 version.
The Standard Library package additionally contains the catalog and a
`buildTransitive` target. `Aetheris.CLI` is packaged separately as a .NET tool.

## Runtime assets

The 49,152-byte SQLite catalog is checked in at
`Aetheris.Kernel.StandardLibrary/Materials/Database/aetheris-materials-x1.sqlite`.
The Standard Library NuGet copies it into `contentFiles` and uses
`buildTransitive/Aetheris.Kernel.StandardLibrary.targets` to copy it to a
consumer's output. Published applications copy it beside their executable.
`MaterialCatalog` checks the application base directory, so neither execution
mode requires a checkout or source-relative fallback. The compiled EF model and
parameter-free generated query make the read path viable under NativeAOT; the
SQLite data is not replaced by hardcoded records.

The external NuGet consumer and the external NativeAOT material consumer both
resolved `Standard.Materials.Aluminum.5052_H32` from that packaged database.
Catalog regeneration is byte-for-byte deterministic.

## License state

- Aetheris code: GNU Affero General Public License v3.0 (`AGPL-3.0`), recorded in `LICENSE`, the root README, NuGet metadata, Cadmata metadata, and VSIX metadata.
- Alternative licensing: available on request.
- Third-party assets: retain their respective licenses, terms, attribution, and provenance; see `THIRD_PARTY_NOTICES.md` and the asset-local records it links.

The Stanford Bunny entry preserves the existing Stanford provenance statement
and identifies the repository content as generated evidence rather than the
original model archive.

## NIST acknowledgment

The root README visibly thanks NIST for making the STEP AP242 test models
available and explains their role in STEP, PMI, Sheet Metal, and reconstruction
validation. `THIRD_PARTY_NOTICES.md` points to `testdata/step242/nist` and the
PMI demo copy, and explicitly avoids any implication of endorsement, Aetheris
authorship, inclusion of NIST code, or AGPL relicensing of the models.

## NativeAOT matrix

| RID | Publish | Runtime smoke | Notes |
| --- | --- | --- | --- |
| `win-x64` | Pass | Pass | Forge Host NativeAOT; 8,821,760-byte executable |

No other RID is claimed for Preview 3. The published NativeAOT host ran outside
the repository and completed `info`, `list`, `describe`, and `invoke`. It
reported Aetheris `2.0.0-preview.3` (plus Git commit build metadata) and Forge
Host Protocol `1`, discovered five
real templates, and invoked the release-style enclosure witness. STEP, flat
STEP, SVG, structured diagnostics, and manifest output were produced without
source fixtures or development configuration.

## CLI and Cadmata evidence

The self-contained CLI was copied outside the repository and exercised for
version reporting, Firmament validation/build, STEP inspection, a Sheet Metal
material coupon, an FEA solve using 5052-H32, artifact validation, and missing
file behavior. The packaged .NET tool was also installed into an isolated tool
directory. Missing input returned exit code 1 with a diagnostic.

Cadmata was qualified only through TSPack: sync, lock/policy check, typecheck,
81 tests, production build, and lint all passed. The production bundle contains
seven files (1,496,068 bytes). Scans found no source maps, secrets, localhost
URLs, or developer absolute paths. Existing model loading, picking, semantic
PMI filters/callouts, and result infrastructure are covered by the passing
client tests; this pass added no client features.

The VS Code workspace was likewise qualified through TSPack: typecheck, 13
tests, production bundle, and VSIX package all passed.

## Clean consumer evidence

A clean source snapshot was assembled outside the checkout from only Git-visible
source files, with no `bin`, `obj`, `node_modules`, generated package directory,
or npm lockfile. It restored and built Release, ran the full serial .NET suite,
packed all public libraries, published the CLI and NativeAOT host, and ran the
TSPack client checks from the committed `ts-lock.toml`.

An independent temporary `net10.0` console project consumed the generated
NuGet files through a directory feed (no project references), invoked a real
Firmament template, loaded the standard material, and inspected the generated
artifact. A Python client under a different temporary directory spoke JSONL to
the published NativeAOT Forge Host and completed all four Protocol v1 verbs.

## Determinism evidence

Representative SHA-256 values established for Preview 3 are:

| Output | SHA-256 |
| --- | --- |
| Standard material SQLite catalog | `10180ac314b35c9b0f6b7b5fd415779cc05d1ab96ed3e71ab4da05d94e6d048e` |
| Enclosure STEP | `114cd7c0c6a8a364b2943cc955a12d8a96b576a187dfc1957ea9f769296872be` |
| Enclosure flat STEP | `88c437373fe4fdf91e8f0a5b5e0e5c135b290f0dfe18449ec3ee7c0970c1d075` |
| Enclosure SVG | `1657e3bbc3ef418617b45c5d9ab76a96d70b0ba6356c0e88a7ba07edc18b6519` |
| Windows release ZIP | `8e90475dd4189eb4f00c83c45ef0b9687d757e04a613ddf2ee1518c95506203d` |
| Firmament VSIX | `3f188020cdfc4908d49c0eb810baed4ec4b78e1eb8bd66d39553f4c4558587a4` |
| CLI NuGet | `6f4b3a2724c382e99f84fd6f155538f2c44ff99a614f2c4deb5ca104bd09c0df` |

Two clean invocation runs produced identical STEP/flat STEP/SVG hashes. Two
release-script runs produced identical normalized NuGet, VSIX, and Windows ZIP
bytes. The release script canonicalizes ZIP metadata, NuGet-generated relation
IDs, Cadmata last-modified metadata, and the NativeAOT PE/debug timestamps.

## Validation summary

- Release build: pass.
- Full serial .NET suite: 16 projects, 2,971 passed, 0 failed.
- Cadmata TSPack: typecheck pass; 81/81 tests; build pass; lint pass.
- VS Code extension TSPack: typecheck pass; 13/13 tests; build/package pass.
- Public NuGet pack and package-content audit: 16/16 pass.
- NativeAOT publish and outside-repository Forge smoke: pass for `win-x64`.
- Outside-repository CLI and external package consumers: pass.
- Deterministic artifact rerun: pass.
- `git diff --check`: pass.

## Findings

### ReleaseBlocker — fixed

- Added the omitted `Aetheris.Geometry` public package.
- Made the SQLite catalog flow transitively to package consumers.
- Removed the NativeAOT material-read dependency on EF runtime model building.
- Included the NativeAOT Forge Host in the product bundle and qualified it.
- Fixed a solution restore/build mismatch in `Aetheris.PmiInjectionDemo`.
- Replaced an ignored source-tree STEP dependency with the tracked canonical fixture.
- Made release scripting stop on native-command failures instead of emitting a partial artifact set.

### MustFix — fixed

- Reconciled public product/package/client metadata to Preview 3 while preserving Protocol v1 as an independent version.
- Clarified AGPL and third-party provenance; added the NIST acknowledgment.
- Removed the Bunny tool's hardcoded developer path and made the archive path explicit.
- Removed Cadmata's remote Google-font dependency.
- Restored TSPack as the only JavaScript package/build entrypoint and removed the accidental npm lockfile/fallback.
- Removed nondeterministic package and executable timestamps from release artifacts.

### DocumentForPreview

- Only `win-x64` is qualified. The CLI/Cadmata bundle is self-contained JIT; Forge Host is the NativeAOT executable.
- NativeAOT analysis still reports known warnings in code paths not exercised by the bounded Host protocol surface. The qualified Host paths pass runtime smoke.
- TSPack reports acknowledged dependency-version multiplicity and blocked dependency lifecycle scripts. These are policy diagnostics, not direct npm execution.
- Forge invocation timing fields may vary; produced artifacts, content hashes, and semantic manifest identity are deterministic.

### DeferredPostPreview3

- Additional RIDs and a fully NativeAOT CLI remain future qualification work.
- No new geometry/kernel, Sheet Metal, FEA, Firmament, Forge, Cadmata, physics, or interop capability was introduced.

## Remaining limitations

Preview 3 is qualified only for Windows x64. External consumers require .NET 10
for library use; the distributed Windows applications are self-contained. A
maintainer creating release artifacts also needs TSPack and the NativeAOT C++
toolchain. The NIST models and Stanford-derived evidence remain reference/test
data under their recorded provenance, not Aetheris-authored AGPL assets.
