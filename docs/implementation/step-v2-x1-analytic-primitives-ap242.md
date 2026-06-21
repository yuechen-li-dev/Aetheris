# STEP-V2-X1 Analytic Primitives AP242 Wiring

STEP-V2-X1 extends the Firmament V2 real build/export path from the STEP-V2-A1 Box fixture to Cylinder, Cone/frustum, Sphere, and Torus fixtures. The syntax remains the V2 typed record-literal style:

```firmament
solid cyl: Cylinder { radius: 2mm height: 10mm }
solid cone: Cone { bottomRadius: 3mm topRadius: 1mm height: 10mm }
solid sphere: Sphere { radius: 5mm }
solid torus: Torus { majorRadius: 8mm minorRadius: 2mm }
```

## Bridge organization

The V2 parser and AST own only V2 syntax and parsed primitive records. `FirmamentV2BuildLowering.LowerPrimitiveBridge` is the isolated compatibility bridge from V2 records to existing `FirmamentLowered*Parameters` records. The bridge deliberately reuses `FirmamentPrimitiveExecutor`, `BrepBody`, and `Step242Exporter`; it is not a V2 exporter and does not route through trace-only artifacts.

This keeps the current seam clear:

- V2 parser / AST: `Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2Parser.cs`, `FirmamentV2Ast.cs`
- V2-to-lowered compatibility bridge: `Aetheris.Kernel.Firmament/FirmamentV2/FirmamentV2BuildLowering.cs`
- legacy/current lowered records: `Aetheris.Kernel.Firmament/Lowering/FirmamentPrimitiveLoweringPlan.cs`
- production executor: `Aetheris.Kernel.Firmament/Execution/FirmamentPrimitiveExecutor.cs`
- BRep/STEP back half: `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`

## Fixtures and formulas

- `fixtures/FirmamentV2/Primitive/valid/primitive-v2-cylinder-step-verified.valid.firmfixture`: volume `πr²h`, with `r=2`, `h=10`.
- `fixtures/FirmamentV2/Primitive/valid/primitive-v2-cone-step-verified.valid.firmfixture`: frustum volume `(πh/3)(r1² + r1r2 + r2²)`, with `r1=3`, `r2=1`, `h=10`.
- `fixtures/FirmamentV2/Primitive/valid/primitive-v2-sphere-step-verified.valid.firmfixture`: volume `(4/3)πr³`, with `r=5`.
- `fixtures/FirmamentV2/Primitive/valid/primitive-v2-torus-step-verified.valid.firmfixture`: volume `2π²Rr²`, with `R=8`, `r=2`.

Canonical topology follows the current Aetheris exporter/importer representation: cylinder and frustum use three faces; sphere and torus use one analytic face. Vertex markers are emitted by the exporter for vertexless whole analytic bodies so STEP smoke checks can consistently prove topology marker presence without using hardcoded STEP templates.

## Deferred

This milestone does not add modify, Boolean, hole, side-hole, pattern, PMI, DFM, chamfer, fillet, or V2-only exporter/executor support. Apex cone syntax is deferred; STEP-V2-X1 verifies a frustum.

## Validation commands

```bash
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1
dotnet test Aetheris.slnx -f net10.0 --no-build --filter "STEP-V2|primitive-v2|FirmamentV2|AP242|Build"
dotnet run --project Aetheris.CLI -- --help
git diff --check
```

Direct fixture build/analyze commands were run for Cylinder, Cone, Sphere, and Torus through `dotnet run --project Aetheris.CLI -- build ... --json` and `dotnet run --project Aetheris.CLI -- analyze volume ... --json`.
