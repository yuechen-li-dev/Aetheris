# EDGE-X11 — AirChamfer STEP artifact regression corpus

## Purpose and scope

EDGE-X11 turns the EDGE-X10 single CLI-visible AirChamfer STEP artifact into a tiny deterministic regression corpus. The corpus is still experimental/lab-only: it exercises controlled AirChamfer shadow/prototype output, STEP smoke markers, topology summaries, and JSON diagnostics without replacing production chamfer behavior.

The corpus is intentionally narrow. It does not accept arbitrary model input and does not add arbitrary edge selection. It exists to keep the first AirChamfer candidate artifact repeatable as Aetheris evolves.

## References

- EDGE-X10: `docs/frictionlab/edge-x10-airchamfer-cube-step-artifact.md` introduced `aetheris experimental airchamfer-cube --out <path> [--json]` and the `edge-x10-airchamfer-cube-one-edge.step` trophy artifact.
- EDGE-V3: `docs/edge-v3-air-chamfer-shadow-route.md` defines the non-authoritative AirChamfer shadow route used by the CLI lane.
- EDGE-V2: `AirChamferRealBodyPrototype` remains the internal production-adjacent candidate body prototype behind the shadow route.
- EDGE-X9: feature-recognition parity remains analyzer/shadow evidence only and does not authorize route replacement.

## CLI commands

EDGE-X11 adds this experimental command:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-corpus --out-dir <dir> --json
```

The existing EDGE-X10 command remains supported:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-cube --out <path> --json
```

## Corpus cases

| Case | Expected status | STEP output | Notes |
| --- | --- | --- | --- |
| `canonical` | `succeeded` | yes | Canonical orthogonal one-edge cube candidate equivalent to EDGE-X10. |
| `nonorthogonal` | `succeeded` when current EDGE-V2/V3 support accepts it | yes when accepted | Controlled one-edge non-orthogonal planar pair. If support regresses, this should become deterministic JSON-only evidence rather than a production fallback. |
| `invalid-distance` | `rejected` | no | Uses an invalid zero chamfer distance and emits deterministic rejection diagnostics. |
| `triangle-legacy-dependent` | `deferred` | no | Represents a legacy-dependent triangle/chamfer fixture lane and preserves legacy authority. |

## Artifact filenames and path convention

The command writes successful STEP artifacts under the supplied `--out-dir`:

- `edge-x11-airchamfer-cube-canonical.step`
- `edge-x11-airchamfer-cube-nonorthogonal.step` when the controlled non-orthogonal candidate succeeds
- `edge-x11-airchamfer-corpus.json`

Rejected and deferred cases do not write STEP files. Their evidence lives in the corpus JSON summary.

## JSON summary schema

The corpus command writes `edge-x11-airchamfer-corpus.json` and, with `--json`, emits the same summary to stdout. Important fields include:

- `corpusVersion` / `milestone`: `EDGE-X11`
- `outputDirectory`
- `summaryPath`
- `candidatePath`: `AirChamferShadowRoute->AirChamferRealBodyPrototype`
- `route`: `experimental-cli-airchamfer-corpus`
- `legacyAuthorityPreserved`
- `productionOutputChanged`
- `noProductionRouteReplacement`
- `no3DBooleanUsed`
- `cases[]`
  - `caseName`
  - `status`: `succeeded`, `rejected`, `deferred`, or `failed`
  - `artifactPath` / `artifactFileName` when STEP output is written
  - `candidatePath`
  - `route`
  - `stepMarkerSummary` when a STEP body exists
  - `topologySummary` when a candidate body exists
  - `diagnostics`
  - `errors`
- top-level `diagnostics`
- top-level `errors`

## STEP marker expectations

Successful STEP artifacts must satisfy the same smoke markers as EDGE-X10:

Required present:

- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Forbidden absent:

- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

The JSON `stepMarkerSummary` records both the raw marker booleans and whether the required-present and forbidden-absent checks passed.

## Invalid and deferred behavior

Invalid or deferred cases must be deterministic JSON-only rows. They must not write STEP files. Expected diagnostics include patterns such as:

- `edge-x11-case-rejected:invalid-distance:reject-invalid-distance`
- `edge-x11-case-deferred:triangle-legacy-dependent:legacy-dependent-fallback`
- `edge-x11-legacy-authority-preserved`
- `edge-x11-no-production-route-replacement`
- `edge-x11-no-3d-boolean-used`

## Candidate path

The corpus uses the same experimental candidate path as EDGE-X10:

```text
AirChamferShadowRoute -> AirChamferRealBodyPrototype
```

This is reported in JSON as `AirChamferShadowRoute->AirChamferRealBodyPrototype`.

## Legacy authority and route boundary

Legacy `BrepBoundedChamfer` remains production-authoritative. EDGE-X11 does not route normal Firmament chamfer operations through AirChamfer, does not replace production output, and does not introduce a stable public API. The CLI hook is explicitly experimental/lab-only.

## No-3D-Boolean guarantee

The AirChamfer corpus candidate path uses no 3D Boolean fallback. STEP export is only performed for candidate bodies produced by the shadow/prototype path.

## Tests run

Recommended gates for this milestone:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-cube --out "$tmp/edge-x10-airchamfer-cube-one-edge.step" --json
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-corpus --out-dir "$tmp/edge-x11" --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "AirChamfer|Experimental|Lab|Step|CliBaseline|Export|Corpus"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirChamferShadow|AirChamferFeatureRecognition|AirChamferControlledBody|AirChamferTopologyGraft|AirChamferClosedWitness|AirChamferGeometryArtifact|AirChamferTopologyPlan|AirChamferJudgment|AirChamferPolicy|AirChamferPatch|EdgeSweep|Chamfer|Fillet|EdgeFinish|CIRLab"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|TriangularPrism|FirmamentPrimitive|FirmamentStepExporter|LineArcProfileExtrude|SemanticRecovery|FrepMaterializer|Rematerialize"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Judgment|Chamfer|Fillet|Corner|TriangularPrism|BrepPrimitives|Step242|Primitive|Extrude|Boolean|SafeComposition"
./scripts/test-all.sh
```

## Non-goals

EDGE-X11 does not add or change:

- production chamfer behavior
- production fillet behavior
- a stable public API
- arbitrary edge selection
- fillet geometry
- edge-chain or corner-chain support
- STEP exporter/importer behavior
- Boolean core behavior
- triangle migration
- sketch solver behavior
- clipping engine behavior
- NURBS/freeform support

## EDGE-X12 gated stability check

EDGE-X12 adds an explicitly gated/manual repeated-run stability check for this corpus. See `docs/frictionlab/edge-x12-airchamfer-corpus-stability.md` for the opt-in command, `Category=ArtifactCorpus` trait, `AETHERIS_RUN_ARTIFACT_CORPUS_TESTS=1` guard, and the raw STEP SHA256 plus normalized JSON/marker/topology comparisons. The check is intentionally not part of normal/default unit-test execution unless explicitly requested.

## Recommended next milestone

After EDGE-X12, any broader AirChamfer corpus shape expansion should remain analyzer/shadow evidence only until a separate production authorization milestone exists.

## EDGE-A1 matrix note

EDGE-A1 classifies CLI/artifact corpus coverage as one production-readiness gate in `docs/edge-a1-chamfer-fillet-support-compatibility-matrix.md`. The EDGE-X11 corpus supports controlled AirChamfer evidence rows, but corpus artifacts alone do not imply production authority, chain/corner support, fillet support, or feature-recognition parity for legacy-sensitive cases.
