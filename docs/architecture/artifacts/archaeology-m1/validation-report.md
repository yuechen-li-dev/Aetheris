# M1 validation report

This report records the final M1 validation. The architecture work is documentation-first. Two narrow fixes discovered during validation were also made: canonical V1 formatting no longer mixes platform newlines, and legacy build tests now assert the intentional adjacent default output path.

## Ground-truth CLI inspection

| Command | Result |
|---|---|
| `dotnet run --project Aetheris.CLI -- --help` | passed; confirms current command surface |
| `dotnet run --project Aetheris.CLI -- asm --help` | passed; explicitly states current `.firmasm` is the V2 Assembly profile and only JSON syntax is legacy |
| `aetheris validate` on V1 TOON fixture | rejected by V2-only validation, proving CLI validation no longer provides V1 validation compatibility |
| `aetheris inspect` on V1 example | rejected by V2-only inspection |
| `aetheris build` on V1 `box_basic.firmament` | passed and emitted STEP, proving live V1 compiler/executor fallback |
| `aetheris asm inspect` on legacy OCCT AS1 JSON `.firmasm` | passed through migration; AssemblyIR part placements reported `LegacyExplicit` |
| `aetheris asm exec` on the same fixture | passed; 5 parts, 18 instances/bodies, deprecation warning |

The generated `.tmp/archaeology-v1-box.step` is an untracked temporary validation artifact ignored by Git; it is not part of this milestone.

## Build and tests

| Validation | Result |
|---|---|
| `dotnet restore Aetheris.slnx` | passed; all projects up to date |
| `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1` | passed; 0 warnings, 0 errors |
| active Core, Firmament, CLI, Semantics, Forge Host, Server, and Modules tests | 2,526 passed, 0 failed |
| full opt-in Firmament legacy suite (`AETHERIS_RUN_LEGACY_TESTS=1`) | 1,731 passed, 0 failed |
| focused Boolean/stepped-hole FrictionLab set | 40 passed, 0 failed |
| full opt-in FrictionLab suite | 394 passed, 5 failed |

The five full-FrictionsLab failures are all methods of `TriangleHexPrismProfileParityLabTests`. They share the same pre-existing failure: `ProfileExtrusionBRepPlanner.TryPlan` constructs a `ParameterInterval` with a non-finite end at `ProfileExtrusionBRepPlan.cs:128`. This is unrelated to V1 serialization or bounded Boolean archaeology and was not patched as part of M1. The focused Boolean audit set is green.

Before the narrow fixes, the full Firmament legacy suite reported 76 failures: platform-mixed formatter newlines and tests that still expected the retired `testdata/firmament/exports` default. After making LF output explicit and aligning those tests/docs with `ResolveDefaultOutputPath`, the same suite passes 1,731/1,731.

## Changed files

- architecture artifact set under this directory;
- `docs/firmament-v2/language-reference.md` (one contradictory profile statement corrected);
- `Aetheris.Kernel.Firmament/README.md` (stale pre-M0 scaffold description replaced with current ownership summary);
- `FirmamentCanonicalFormatter` (blank lines use explicit LF, matching the existing canonical contract);
- `FirmamentBuildAndExportTests` and `docs/cli-baseline.md` (default output expectations aligned with the implemented adjacent-source contract).

No schema, fixture, geometry, topology, parser, lowering, executor, API, or Boolean runtime behavior was changed.
