# EDGE-X10 AirChamfer cube STEP artifact

## Purpose and scope
EDGE-X10 answers whether existing Aetheris CLI/export infrastructure can produce an explicit experimental AirChamfer candidate STEP file. The supported artifact is intentionally narrow: a controlled cube/box-like body, one explicit convex planar edge, the existing EDGE-V3/EDGE-V2 AirChamfer candidate path, and a deterministic STEP file written through a lab/experimental CLI lane.

This is the first AirChamfer candidate STEP artifact exposed through CLI/export plumbing. It is not a production AirChamfer feature.

## References
- EDGE-V2: `docs/edge-v2-real-body-air-chamfer-prototype.md`
- EDGE-X9: `docs/frictionlab/edge-x9-air-chamfer-feature-recognition-parity-lab.md`
- EDGE-V3: `docs/edge-v3-air-chamfer-shadow-route.md`
- V2 sweep-first architecture: `docs/aetheris-v2-sweep-first-architecture.md`
- Legacy topology / parallel emitter lanes: `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`
- AirEdgeSweep audit: `docs/edge-a0-air-edge-sweep-fillet-chamfer-audit.md`

## Artifact route and exact path convention
CLI integration is implemented as an explicitly experimental command:

```bash
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-cube --out <path> --json
```

Recommended deterministic filename:

```text
edge-x10-airchamfer-cube-one-edge.step
```

The route identifier emitted by the command is:

```text
experimental-cli-airchamfer-cube
```

The candidate path identifier emitted by the command is:

```text
AirChamferShadowRoute->AirChamferRealBodyPrototype
```

## How to generate manually
From the repository root:

```bash
mkdir -p artifacts/edge-x10
dotnet run --project Aetheris.CLI --framework net10.0 -- experimental airchamfer-cube --out artifacts/edge-x10/edge-x10-airchamfer-cube-one-edge.step --json
```

The command writes the requested STEP path and emits JSON diagnostics. The CLI command is lab/experimental-only and does not imply normal Firmament chamfer support through AirChamfer.

## STEP marker result
EDGE-X10 validates the written candidate STEP with deterministic marker checks.

Required present:
- `ISO-10303-21`
- `MANIFOLD_SOLID_BREP`
- `ADVANCED_FACE`
- `PLANE`

Required absent:
- `CYLINDRICAL_SURFACE`
- `BREP_WITH_VOIDS`

The successful diagnostic is:

```text
edge-x10-step-smoke-succeeded
```

## AirChamfer candidate path used
The artifact writer invokes the non-authoritative EDGE-V3 shadow route, which invokes the EDGE-V2 real-body prototype. The controlled fixture uses a 10 x 8 x 6 box-like source body and a single explicit convex planar edge from `(5, 4, -3)` to `(5, 4, 3)` with adjacent planar normals `(1, 0, 0)` and `(0, 1, 0)`. The chamfer distance is fixed at `1`.

Key diagnostics include:
- `edge-x10-airchamfer-step-artifact-started`
- `edge-x10-cli-export-path-used`
- `edge-x10-air-chamfer-shadow-route-invoked`
- `edge-x10-candidate-body-created`
- `edge-x10-step-artifact-written`
- `edge-x10-step-smoke-succeeded`
- `edge-x10-legacy-authority-preserved`
- `edge-x10-no-production-route-replacement`
- `edge-x10-no-3d-boolean-used`

EDGE-V3 diagnostics remain present as additional evidence, including `edge-v3-no-production-route-replacement` and `edge-v3-no-3d-boolean-used`.

## Explicit production-safety statement
EDGE-X10 does not route normal Firmament chamfer operations through AirChamfer. Legacy `BrepBoundedChamfer` remains production-authoritative. EDGE-X10 does not replace production chamfer behavior, does not change production chamfer/fillet behavior, does not change STEP exporter/importer behavior, does not change Boolean core behavior, does not introduce fillet geometry, does not add arbitrary edge selection, and does not add edge-chain/corner-chain support.

No 3D Boolean is used in the AirChamfer candidate path.

## CLI integration status
Implemented: `aetheris experimental airchamfer-cube --out <path> [--json]`.

The command is deliberately under `experimental` and is documented as lab-only. It exports exactly the controlled one-edge AirChamfer candidate body and preserves the existing production route boundary.

## Tests run
Intended EDGE-X10 gates:
- `dotnet run --project Aetheris.CLI --framework net10.0 -- --help`
- `dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj --filter "AirChamfer|Experimental|Lab|Step|CliBaseline|Export"`
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirChamferShadow|AirChamferFeatureRecognition|AirChamferControlledBody|AirChamferTopologyGraft|AirChamferClosedWitness|AirChamferGeometryArtifact|AirChamferTopologyPlan|AirChamferJudgment|AirChamferPolicy|AirChamferPatch|EdgeSweep|Chamfer|Fillet|EdgeFinish|CIRLab"`
- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "Chamfer|Fillet|Corner|TriangularPrism|FirmamentPrimitive|FirmamentStepExporter|LineArcProfileExtrude|SemanticRecovery|FrepMaterializer|Rematerialize"`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "Judgment|Chamfer|Fillet|Corner|TriangularPrism|BrepPrimitives|Step242|Primitive|Extrude|Boolean|SafeComposition"`
- `./scripts/test-all.sh` after focused CLI/shared gates pass.

## Next recommended milestone
Recommended next milestone: EDGE-X11 should compare the exported AirChamfer candidate artifact against legacy chamfer output through analyzer-only evidence, while preserving legacy authority and avoiding production route replacement. If candidate recognition diverges first, run a narrow EDGE-X9.1 recognition hardening milestone before broader export comparisons.
