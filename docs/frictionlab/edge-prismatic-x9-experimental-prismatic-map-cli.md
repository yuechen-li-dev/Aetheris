# EDGE-PRISMATIC-X9 — Experimental generated-source prismatic map CLI

## Purpose and scope

EDGE-PRISMATIC-X9 exposes the EDGE-PRISMATIC-X8 hybrid prismatic map proof through an explicit experimental CLI route:

```bash
aetheris experimental prismatic-map --case <case> --rows <n> --cols <n> --json
```

The route is intentionally narrow. It only creates controlled generated AIR/prismatic source cases, admits their CIR convex-polyhedron mirrors, and reports top-view map occupancy summaries. It is not a production analyzer path, not a stable public API, and not an arbitrary geometry route.

## References

- EDGE-PRISMATIC-X8 hybrid dispatch: `docs/edge-prismatic-x8-hybrid-map-dispatch-prismatic-mirror.md`
- CIR-PRISMATIC-X2 convex polyhedron mirror: `docs/cir-prismatic-x2-convex-polyhedron-mirror.md`
- CIR map/mirror dispatch background: `docs/cir-map-x1-primitive-map-prototype.md` and `docs/cir-map-x2-mirror-aware-primitive-map-dispatch.md`
- AIR/CIR authority and mirror contract: `docs/air-cir-a0-authority-and-mirror-contract.md`

## CLI command and examples

Supported command:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-map --case rectangle-inset --rows 16 --cols 16 --json
```

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-map --case top-edge-chamfer --rows 16 --cols 16 --json
```

The route requires `--json` so the output remains machine-checkable and avoids implying a stable human-readable API. The route also accepts an optional `--request` guard; only `map-occupancy` is admitted. `face-identity` and `topology-parity` reject as lossy.

## Supported generated cases

- `rectangle-inset`
- `top-edge-chamfer`

Optional corpus cases such as `box-primitive`, `hexagon-scaled`, `pentagon-scaled`, and `pentagon-asymmetric` are not exposed by X9 unless a future milestone adds a policy-backed expansion.

## STEP input is not accepted

`experimental prismatic-map` accepts no positional STEP path, no imported STEP prismatic body, and no arbitrary user-provided geometry. A positional `.step` argument rejects with the stable diagnostic:

```text
edge-prismatic-x9-step-input-rejected
experimental prismatic-map does not accept STEP input; use generated --case values only
```

This preserves the AIR/CIR authority model: generated source with an admitted mirror may use this experimental route; imported STEP cannot infer a CIR mirror.

## JSON schema

The command emits one JSON object with these fields:

- `success`: boolean.
- `milestone`: `EDGE-PRISMATIC-X9`.
- `commandRoute`: `experimental prismatic-map`.
- `caseName`: supported generated case name.
- `generatedSourceKind`: currently `generated-air-prismatic-source`.
- `backendSelected`: `cir-convex-polyhedron`, `cir-tape`, or `unsupported`.
- `mirrorStatus`: mirror admission status such as `mirror-admitted-exact`.
- `requestedUse`: currently `map-occupancy`.
- `view`: currently `top`.
- `rows`, `cols`: requested positive grid dimensions.
- `occupiedCount`, `emptyCount`: map occupancy sample counts.
- `thicknessMin`, `thicknessMax`, `thicknessAverage`: top-view vertical thickness summary values, or `null` on unsupported/failure results.
- `bounds`: generated source bounds with `min`, `max`, `sizeX`, `sizeY`, and `sizeZ`, or `null` on unsupported/failure results.
- `knownLosses`: explicit loss statements.
- `diagnostics`: deterministic machine-checkable diagnostics.
- `guarantees`: authority-preservation booleans.
- `error`: `null` on success or a stable failure reason.

## Expected 16x16 top-view summaries

For `rectangle-inset`:

- `backendSelected`: `cir-convex-polyhedron`
- `mirrorStatus`: `mirror-admitted-exact`
- `occupiedCount`: `256`
- `emptyCount`: `0`
- `thicknessMin`: approximately `0.75`
- `thicknessMax`: approximately `4.0`
- `thicknessAverage`: approximately `3.05`

For `top-edge-chamfer`:

- `backendSelected`: `cir-convex-polyhedron`
- `mirrorStatus`: `mirror-admitted-exact`
- `occupiedCount`: `256`
- `emptyCount`: `0`
- `thicknessMin`: approximately `3.3125`
- `thicknessMax`: approximately `4.0`
- `thicknessAverage`: approximately `3.9531`

## Known losses

The JSON intentionally reports these losses and makes no topology identity claim:

- `face identity lost`
- `loop identity lost`
- `split-face lineage lost`
- `feature role labels lost`
- `topology parity unavailable`

## Diagnostics

Success diagnostics include:

- `edge-prismatic-x9-cli-route-started`
- `edge-prismatic-x9-generated-source-created:<case>`
- `edge-prismatic-x9-cir-mirror-admission-requested:<case>`
- `edge-prismatic-x9-cir-mirror-admitted-exact:<case>`
- `edge-prismatic-x9-backend-selected:cir-convex-polyhedron`
- `edge-prismatic-x9-map-summary-created:<case>`
- `edge-prismatic-x9-loss-face-identity`
- `edge-prismatic-x9-loss-split-face-lineage`
- `edge-prismatic-x9-loss-topology-parity`
- `edge-prismatic-x9-no-step-input`
- `edge-prismatic-x9-no-imported-step-mirror-inference`
- `edge-prismatic-x9-no-production-analyzer-behavior-changed`
- `edge-prismatic-x9-no-default-cli-behavior-changed`
- `edge-prismatic-x9-no-cir-to-brep-extraction`

Error diagnostics include:

- `edge-prismatic-x9-missing-case`
- `edge-prismatic-x9-unknown-case:<case>`
- `edge-prismatic-x9-invalid-grid`
- `edge-prismatic-x9-json-required`
- `edge-prismatic-x9-step-input-rejected`
- `edge-prismatic-x9-mirror-unavailable:<case>`
- `edge-prismatic-x9-lossy-request-rejected:<request>`

## Relationship to normal `aetheris analyze map`

Normal `aetheris analyze map <file.step> ... --json` remains unchanged. It continues to operate on STEP inputs through the existing analyzer behavior, including the established BRep-raycast/primitive-limited route for STEP map analysis. X9 does not route normal STEP analysis through CIR prismatic mirrors.

## Non-goals

EDGE-PRISMATIC-X9 does not change or add:

- production analyzer behavior;
- default CLI behavior;
- STEP exporter/importer behavior;
- Boolean core behavior;
- BRep topology behavior;
- AIR emitter behavior;
- production prismatic route behavior;
- CIR-to-BRep extraction;
- imported STEP mirror inference;
- topology identity, face identity, loop identity, split-face lineage, or topology parity claims;
- coplanar merge behavior.

## Tests run

Focused validation for this milestone should include:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- --help
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-map --case rectangle-inset --rows 16 --cols 16 --json
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental prismatic-map --case top-edge-chamfer --rows 16 --cols 16 --json
dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "PrismaticMap|Prismatic|Analyze|Map|CliBaseline|Step|AirChamfer|Experimental|Corpus"
dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "EDGE-PRISMATIC-X8|PrismaticHybridMap|CirPrismatic|CirMirror|CirMap|CIR|Cir|BrepPrimitives|BrepSpatialQueries|Raycast|Step242|Primitive|Boolean|SafeComposition"
dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "PrismaticSectionTransition|PrismaticTopEdgeChamfer|ProfileStackChamfer|ProfileChamfer|ProfileStack|LineArcProfileExtrude|Profile2D|AirChamfer|EdgeSweep|CIRLab"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize|Chamfer|Fillet|Corner|ProfileStack|LineArcProfileExtrude"
```

Do not enable gated artifact corpus stability tests by default.

## Next milestone

Recommended next work is one of:

- AIR-CIR-A1 mirror drift/parity policy;
- EDGE-A3 edge-finish selection taxonomy;
- a policy-backed optional experimental map expansion after mirror drift/parity policy is settled.
