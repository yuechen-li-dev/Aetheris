# AIR-V3 — Production Box-as-AirExtrude migration

## Context and prior evidence

AIR-V3 consumes prior AIR evidence directly:

- AIR-X3 audit (`docs/air-x3-primitive-as-air-normalization-audit.md`) concluded primitive-as-AIR normalization is viable and recommended box as first migration candidate.
- AIR-X4 lab (`docs/frictionlab/air-x4-box-as-air-extrude-lab.md`) validated rectangle-profile linear extrusion parity against pre-migration `CreateBox` behavior.

## What changed in production

`BrepPrimitives.CreateBox(width, height, depth)` is now production-routed through the same centered rectangle-profile linear extrusion path proven in AIR-X4:

- build a centered rectangle profile in XY using `width` and `height`;
- extrude along +Z from `z = -depth/2` with extent `depth`;
- return the resulting validated BRep body.

Public API signature and semantics remain unchanged.

## Topology and STEP parity contract

For valid dimensions, box output is required to preserve these observable parity contracts:

- closed manifold BRep;
- deterministic accepted box topology counts: 8 vertices, 12 edges, 6 faces;
- all six faces planar;
- STEP/AP242 smoke markers remain present: `ISO-10303-21`, `MANIFOLD_SOLID_BREP`, `ADVANCED_FACE`, `PLANE`;
- STEP/AP242 must not contain `BREP_WITH_VOIDS` for simple boxes.

AIR-V3 does not modify STEP exporter/importer behavior to achieve this parity.

## Why only box migrated

Box was selected as the lowest-risk first primitive because it is bounded, analytic, and already validated in AIR-X4 with deterministic parity expectations. AIR-V3 intentionally keeps migration narrow to avoid conflating primitive-specific risks.

## Non-goals

- No migration for cylinder, cone, sphere, or torus.
- No generalized production AIR framework expansion beyond box routing.
- No Boolean core behavior changes.
- No STEP exporter/importer behavior changes.
- No fillet/chamfer/shell or freeform surfacing work.

## Tests run

- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj --filter "BrepPrimitives|CreateBox|Step242|Primitive|BrepBoolean|SafeComposition"`
- `dotnet test Aetheris.FrictionLab.Tests/Aetheris.FrictionLab.Tests.csproj -c Release -f net10.0 --filter "AirBoxExtrude|AirProfileStack|ProfileStackExtrude|RecoveryPolicy|CIRLab"`
- `dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FirmamentPrimitive|FirmamentStepExporter|SemanticRecovery|FrepMaterializer|Rematerialize"`
- `./scripts/test-all.sh`

## Known limitations

- AIR-V3 validates only centered canonical box construction path for production routing.
- Broader primitive-as-AIR normalization still requires primitive-specific parity lanes.

## Next recommended milestone

Proceed to the next bounded primitive candidate only after introducing an equivalent pre-migration evidence lane and parity contract (topology + STEP smoke) for that primitive.
