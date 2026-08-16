# Aetheris 2.0.0-preview.3 + Firmament V2

> Aetheris is under feature freeze for the `2.0.0-preview.3` release candidate.
> This milestone accepts correctness, packaging, diagnostics, documentation,
> cross-platform, NativeAOT, determinism, and other release-hardening fixes to
> existing behavior; new feature families are deferred until after Preview 3.
>
> The public language-manual source is maintained in the sibling
> `yuechen-li-dev.github.io` repository at `src/aetheris` and published under
> `/aetheris/`. Documentation here includes architecture, implementation
> evidence, and historical milestone records. The Preview 2 support contract
> remains at `docs/preview2/feature-manifest.json`; Preview 3 release topology
> and qualification evidence are recorded in `docs/release/PREVIEW3-HARDEN-A1.md`.

Firmament is a deterministic DSL for generating CAD geometry (STEP AP242) via Aetheris.

Firmament V2 is the sole canonical authoring path. Firmament V1 TOON/JSON and JSON-shaped legacy `.firmasm` remain explicit compatibility/serialization inputs. See the [current authoring and kernel boundaries](docs/architecture/current-authoring-and-kernel-boundaries.md).

## Generic Sheet Metal product families

Firmament `Template` is typed compile-time generic programming—not a document
template, prefab model, macro, or C# geometry generator. The Sheet Metal module
ships its product families as readable Firmament source:

```firmament
Use SheetMetal.ProductFamilies;

Static Psu: EnclosureSpec = EnclosureSpec {
    Width: 240mm Depth: 180mm Height: 60mm LidLipHeight: 8mm
    Thickness: 1.2mm InsideRadius: 1.5mm KFactor: 0.42
    ReliefPolicy: Rectangular
}
SheetMetal Enclosure = ElectronicsEnclosure < Spec: Psu >
```

The specialization lowers through ordinary Base, Flange, Relief, Cut, exact-flat,
DFM, SVG, and STEP paths. Generic Assembly DatumFrames and tolerance-aware enclosure
fit are documented in the [M7 guide](docs/sheetmetal/aetheris-sheetmetal-m7.md); see
also the [M5 product-family guide](docs/firmament/sheetmetal-product-templates-m5.md).

## Static engineering data

Firmament `Record` values are immutable typed specifications. A finite standards
catalog can be authored directly as a columnar static Table, selected as a
Record, and derived without copying it:

```firmament
Static Table Sizes: BoltRow Key: Size {
    Size: [M8, M10]
    Diameter: [8mm, 10mm]
}
Static M8 = Sizes[M8]
Static M8Long = M8 with { Length: 80mm }
```

Tables and `with` values are compile-time-only: they feed typed Template Record
parameters and are erased before geometry AIR. A Table is not a runtime
dataframe, SQL surface, or spreadsheet import format. See
[the M1 language note](docs/preview2/firmament-tables-with-m1.md).

## Preview 3 release candidate

Aetheris `2.0.0-preview.3` is qualified for Windows x64 as a release bundle,
the `Aetheris.CLI` .NET global tool, public .NET integration packages, and a
standalone Forge Host Protocol v1 executable. After publication, install the
CLI with:

```bash
dotnet tool install --global Aetheris.CLI --prerelease
```

The NuGet tool does not include Cadmata; use the Windows bundle for `aetheris view`.
The public manual and installation guidance are at
[yuechen-li-dev.github.io/aetheris](https://yuechen-li-dev.github.io/aetheris/).

Maintainer release builds require the .NET 10 SDK, the Windows NativeAOT C++
toolchain, and TSPack. Cadmata and the Firmament VS Code extension are restored
and built from their committed `manifest.tsx` and `ts-lock.toml` files. Do not
substitute npm install/ci or generate a `package-lock.json` for either workspace.

## What works today

- canonical V2 semantic construction, including bounded exact through-hole Recipes
- historical V1 primitive/Boolean execution retained as explicit compatibility
- placement with `place.on` anchors and `offset[3]`
- validation ops: `expect_exists`, `expect_selectable`, `expect_manifold`
- schema-aware CNC minimum tool radius validation
- deterministic canonical formatting for supported `.firmament` source
- STEP AP242 export for the current single-body golden path
- typed relational assembly inspection (`asm inspect`) plus legacy `.firmasm` execution/export compatibility

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

The canonical solution format is `Aetheris.slnx` for both local and automation workflows. Primary targeting is .NET 10 (`net10.0`) only; legacy .NET 8 targeting has been removed.

### Canonical repo-level test path

Use the shell script below as the official automation-friendly entrypoint. It runs the canonical solution (`Aetheris.slnx`) on the sole active framework (`net10.0`), excludes `SlowCorpus` by default, and leaves legacy Firmament V1/FrictionLab tests opt-in.

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
dotnet test Aetheris.Continuum.Tests/Aetheris.Continuum.Tests.csproj --logger "console;verbosity=minimal"
```

If you want the same script with a narrower or broader explicit project list, pass the test projects as arguments:

```bash
export PATH="$HOME/.dotnet:$PATH"
DOTNET_BIN=dotnet ./scripts/test-all.sh Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj Aetheris.Server.Tests/Aetheris.Server.Tests.csproj
```

For editor/IDE compatibility or solution-wide restore/build flows, use:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.slnx -f net10.0 --filter "Category!=SlowCorpus"
```

Use the `SlowCorpus` category to keep the heavyweight STEP242 NIST audit out of default solution runs. Invoke that corpus explicitly when needed:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.slnx -f net10.0 --filter "Category!=SlowCorpus"
dotnet test Aetheris.Kernel.Core.Tests --filter "Category=SlowCorpus"
```

Use `Aetheris.slnx` as the only solution entrypoint. For legacy Firmament V1/FrictionLab test validation, run `AETHERIS_RUN_LEGACY_TESTS=1 dotnet test Aetheris.slnx -f net10.0` or `./scripts/test-legacy.sh`. See `docs/build-test-policy-net10-and-legacy-v1.md`.

## Repo map

- exact kernel math, geometry, and BRep: `Aetheris.Kernel.Core/`
- occupied-region, SDF backend, and lattice experiments: `Aetheris.Continuum/`
- Continuum architecture and M0 evidence: `docs/continuum/`
- language/compiler/runtime: `Aetheris.Kernel.Firmament/`
- examples: `testdata/firmament/examples/`
- exported STEP artifacts: `testdata/firmament/exports/`

## Docs

- assembly model and contracts (canonical): `docs/assembly/architecture.md`
- overview: `docs/firmament-overview.md`
- build/export workflow: `docs/firmament-build-workflow.md`
- selector contracts: `docs/firmament-selectors.md`
- demo script: `docs/firmament-demo.md`

## Assembly model snapshot

- Firmament Assembly source lowers to normalized AssemblyIR; `.firmasm` is the current single-root Assembly document profile. Only the historical JSON-shaped `.firmasm` syntax is a deprecated transform-first compatibility lane.
- STEP is treated as foreign interop input/output.
- Multi-root STEP is assembly-like input and must route through assembly extraction/import, not single-part import.
- Current roundtrip export is intentionally bounded to per-instance STEP + `roundtrip.package.json`.

## Notes

- The repository still contains broader Aetheris server/client scaffolding; the Firmament + CLI paths above are the primary deterministic kernel entry points.

## License and acknowledgments

Aetheris source code is licensed under the GNU Affero General Public License
v3.0 (`AGPL-3.0`), except for third-party assets distributed with the
repository, which retain their respective licenses and attribution.
Alternative licensing is available on request. See [LICENSE](LICENSE) and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Special thanks to the National Institute of Standards and Technology (NIST)
for making available the STEP AP242 test models used during development and
validation. The project's STEP, PMI, Sheet Metal, and reconstruction work
would not have been possible in its present form without those models. The
files retain their original NIST project descriptions and filenames; this
acknowledgment does not imply NIST endorsement, authorship of Aetheris, or
application of the Aetheris license to the models.
