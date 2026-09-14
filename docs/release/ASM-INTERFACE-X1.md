# ASM-INTERFACE-X1 — typed interface bundles and hierarchical subassemblies

## Executive verdict

**Meaningful progression.** Firmament can now author multi-file, recursively nested reusable Subassemblies with private internals, explicit public semantic ports, stable definition/occurrence identity, deterministic paths, and typed `Fixed`, `Axial`, `Revolute`, and `Custom` relationship bundles. The motivating hierarchy and V8 regression pass through the real Assembly compiler, and an executable multi-file Subassembly lowers once to shared AP242 structure. Full acceptance is withheld because Gear-profile `Interface<Gear>` has not yet been bridged into Assembly-profile semantic endpoints, so the Difference-Engine witness cannot yet prove a Gear mesh inside a Subassembly.

## Architecture audit

1. `Interface<Gear>` existed before X1, but in `FirmamentV2/GearAuthoring`: it is compiler-owned and checks module, pressure angle, family, center distance, axis relation, ratio/sign, bevel closure, and ratchet direction/phase. It was not an `AssemblyM0` Interface.
2. Atomic Assembly Mate semantics live in `InterfaceRequirementDefinition` and lower in `AssemblyM0Compiler.LowerConstraints` to `PlacementConstraintIr`.
3. Assembly capability, endpoint, placement, fit, and tolerance Require checks live in `AssemblyM0Compiler`; Gear compatibility checks live in `GearAuthoring.BindInterface`.
4. An untyped Interface already owned several `Lower` requirements and one fit policy. X1 makes the bundle boundary explicit and inspectable for typed families and `Custom`.
5. A semantic tree already existed: Template-produced Assemblies had locally solved `AssemblyDefinitionIr`, nested instances, exposed semantics, and private-boundary enforcement. X1 adds a first-class non-parameterized `Subassembly` spelling and makes definitions reusable across files and nesting levels.
6. Assembly lowering retains the tree in `AssemblyIr`. Geometry execution materializes shared leaf definitions, and AP242 lowering computes local occurrence transforms from the semantic tree; it does not Boolean-flatten before STEP.
7. One Template definition could already back several occurrences. X1 applies the same cache and stable definition identity to bare Subassembly definitions; the 100-occurrence stress test verifies it.
8. JSON `.firmasm` remains supported by `FirmamentAssemblyDocumentCompiler`/`FirmasmManifestLoader` with its deprecation diagnostic. It remains a compatibility/import format and does not define new Include or Subassembly semantics.

## Interface family matrix

| `Interface<T>` | Compiler-owned expansion | Atomic Mates | Requires | Qualified |
|---|---|---|---|---|
| Fixed | yes | FrameCoincident | exact DatumFrame capability | yes |
| Axial | yes | AxisCoincident | exact Axis capability | yes |
| Revolute | yes | AxisCoincident, PlaneCoincident | exact Axis and Plane capabilities | yes |
| Custom | authored bundle | admitted existing atomic kinds | bounded named numeric length predicates | yes |
| Gear | yes, Gear profile | Gear-specific compatibility/derived motion metadata | module, pressure angle, family, distance/cone/ratchet closure | not yet in Assembly profile |
| Slider / Tangent / Bearing | none | none | none | deferred; current placement authority cannot represent them faithfully without speculative semantics |

Inspection retains both levels. For `ShaftBearing`, the source Interface is `Revolute`; its local Mate reports two generated constraint IDs expanding to `AxisCoincident A.Axis -> B.Axis` and `PlaneCoincident A.Seat -> B.Seat`. `CarryLink` reports three authored atomic relations plus `PositiveClearance: 1mm > 0mm (passed)`.

## Hierarchy and encapsulation witness

The three-file fixture lowers to:

```text
DifferenceEngineReadiness
  RegisterA = Register
    Digit0 = DigitModule
      Shaft
      Bearing
    Digit1 = DigitModule
      Shaft
      Bearing
  RegisterB = Register
    Digit0 = DigitModule
      Shaft
      Bearing
    Digit1 = DigitModule
      Shaft
      Bearing
```

Evidence: 2 shared Subassembly definitions, 15 total tree instances, 2 Register occurrences, 4 DigitModule occurrences, four tree levels including the root, and 2 exposed ports per definition. The parent `RegisterCarry` Interface references only `RegisterA.Output` and `RegisterB.Input`. A fixture that reaches through `RegisterB.Digit0.Input` fails with `assembly-internal-member-hidden`.

The source manifest records the root plus both included files and their SHA-256 identities. Includes are recursively expanded as immutable compiler input before the ordinary parser/compiler path; no child STEP file is generated or consumed. Missing files, cycles, paths outside the source root, duplicate Interfaces, and duplicate Subassembly definitions have typed diagnostics. Include aliasing is intentionally deferred.

The separate executable two-file witness materializes one `CellBlock` leaf definition, instantiates one shared `ExecutableCell` Subassembly definition twice, exports five non-root AP242 occurrences with two definitions, and reimports the product structure successfully. Repository, repeat, and freshly packaged CLI exports all have SHA-256 `E02BEFE33E30BD57BB87AA8C1F38451AFA3CD52E647C24FF944F5BE3DFB903B4`. Neither source file names or consumes a child STEP artifact.

## V8 before / after

The bounded ENGINE-X0 burn-in replaces the generic fixed-frame definition:

```firmament
Interface RegisteredSeat {
    Role Moving requires DatumFrameCapable;
    Role Fixed requires DatumFrameCapable;
    Lower FrameCoincident Moving Fixed SameDirection;
}
```

with:

```firmament
Interface<Fixed> RegisteredSeat { }
```

This removes two repeated role declarations and the atomic lowering declaration from the generated source while preserving 170 Mates, 171 physical occurrences, 32 fit results, and all materialized residual checks in the focused real-path regression. Other bespoke V8 Interfaces remain explicit because their fit direction and winding semantics are not equivalent to `Fixed`, `Axial`, or `Revolute`.

## Difference Engine readiness

The upcoming machine can be split into source-level Register and Digit definitions, instantiated repeatedly without cloning definition IR, and connected through exposed ports. It no longer requires one monolithic Assembly file or child STEP round-trips.

Remaining acceptance blockers are concrete:

- bridge Gear-authored semantic endpoints and `Interface<Gear>` expansion into the Assembly IR without duplicating `GearAuthoring`;
- bridge nested Subassembly definition identity into display-mesh definition records if future consumers need selectable assembly-level (not leaf-part) mesh nodes; current occurrence paths and parent IDs already survive;
- add an explicit Include alias policy if same-named library definitions become a real consumer need;
- preserve per-included-file declaration spans rather than only the dependency manifest plus root compilation identity.

No general mate solver, dynamics/contact engine, runtime loader, arbitrary generic Interface family, or second Assembly executor was introduced.

## Validation

- Release solution build: passed with zero warnings and zero errors.
- Focused Firmament Assembly/Gear suite: 100 passed; focused CLI Assembly/Gear suite: 16 passed.
- ENGINE-X0 materialized assembly regression: passed with 170 Mates, 171 physical occurrences, 32 fits, and all exported geometry residuals passing.
- X1 hierarchy, typed-family expansion, custom Require failure, private-boundary rejection, missing Include, cycle chain, whole-tree AP242 lowering/reimport, deterministic repeat, and 100-occurrence reuse: 7 passed.
- Repository and freshly packed `Aetheris.CLI` inspection parity: both report success, 15 instances, 2 shared definitions, 3 source dependencies, and 3 Interfaces. Repository/repeat/package AP242 hashes match; packaged reimport reports 5 occurrences and 1 geometric leaf definition.
- Full solution lane: 3,497 tests passed. Three pre-existing timing-budget tests failed while projects ran concurrently (one recipe microbenchmark and two display tessellation budgets); all six relevant theory/case reruns passed immediately in isolated serial project runs. The legacy-gated FrictionLab project exposed no tests.
- Canonical fixture operations all executed successfully, but the policy scan still reports the pre-existing `Assembly/annular-pair.firmament` use of `LegacyExplicit`; X1 does not alter that compatibility fixture.
- Repository layout guard and `git diff --check`: passed. Remote CI was not run.
