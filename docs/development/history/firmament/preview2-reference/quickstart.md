# Firmament V2 quickstart

Firmament is Aetheris's statically evaluated, typed compiler metaprogramming language for engineering intent. The examples below use canonical source and real CLI commands. They are not a second tutorial-only dialect.

## 1. Compile one exact part

```firmament
Model Plate {
    Units: mm
    Box Body { Size: [40mm, 20mm, 8mm] }
}
```

Save the source as `plate.firmament`, then validate and build deterministic STEP AP242:

```powershell
aetheris validate plate.firmament
aetheris build plate.firmament --output artifacts/plate.step --json
```

`Model` is the ordinary root and `Units: mm` establishes the document length unit. Exact BRep/STEP is the geometry result; the compiler also retains semantic and source evidence.

## 2. Name engineering semantics

Concepts and `Expose` publish stable semantic members instead of asking downstream tools to rediscover anonymous topology. Canonical Concept IR expressions include `Bounds.Center.Axis(+Z)` and `Bounds.Face(+Z)`. Assembly-local semantics use explicit datums:

```firmament
Semantic Joint {
    Point Origin = [0,0,0];
    Axis Axis = [0,0,0] -> [0,0,1];
    Plane Seat = [0,0,10] normal [0,0,1];
    Dimension Diameter = 20mm tol +0.01mm -0.008mm;
}
```

Point, Axis, Plane, and Dimension carry capabilities plus exact bindings. Part occurrences compose those local bindings with their instance transforms.

## 3. Move parameters into typed values and Records

`let` is immutable compile-time data. `tol` is a symbolic interval; it never randomly perturbs nominal geometry.

```firmament
Record BlockSpec {
    Width: Length
    Depth: Length
    Height: Length
}

Static Standard: BlockSpec = BlockSpec {
    Width: 20mm
    Depth: 20mm
    Height: 10mm
}

Static Tall = Standard with { Height: 15mm }
```

`with` creates a new value and rechecks the field names and types. The derivation remains in specialization provenance.

## 4. Put finite standards data in a Static Table

```firmament
Static Table Blocks: BlockSpec Key: Width {
    Width: [20mm, 40mm]
    Depth: [20mm, 40mm]
    Height: [10mm, 15mm]
}

Static Small = Blocks[20mm]
```

Columns must have the same length and keyed lookup is typed. The complete Table/Template/Profile example is `fixtures/Canonical/valid/table-template-concept-path-compose.firmament`.

## 5. Specialize a Template

```firmament
Template < Spec: BlockSpec >
Struct AssemblyBlock: AssemblyBlockConcept {
    Require Positive => Spec.Width > 0mm && Spec.Depth > 0mm && Spec.Height > 0mm
    Concept Struct Design: AssemblyBlockConcept {
        Bounds: Box3 { Size: [Spec.Width, Spec.Depth, Spec.Height] }
        Axis: Bounds.Center.Axis(+Z)
        Seat: Bounds.Face(+Z)
        Height: Spec.Height
    }
    Box Body { Bounds: Design.Bounds }
    Expose {
        Bounds: Design.Bounds
        Axis: Design.Axis
        Seat: Design.Seat
        Height: Design.Height
    }
}
```

Template binding checks fields, types, defaults, `Require`, and recursion before feature AIR. The compiler records arguments, defaults, Match arms, source spans, and Record/Table provenance.

## 6. Build a Profile and Compose it

Use `Concept Path` for named ordered planar motion, convert a valid closed Path to a Profile, then use the ordinary Compose/Modify lanes. The canonical complete source is `fixtures/Canonical/valid/table-template-concept-path-compose.firmament`.

This separation is useful: Path captures construction intent, Profile is admitted exact planar geometry, Compose builds a body, and Modify applies a supported semantic feature route.

## 7. Bring in existing STEP

The complete `fixtures/Canonical/valid/inline-step-recognize-replace.firmament` workflow performs:

```text
InlineStep -> Recognize -> SemanticValue -> bounded Replace
```

Recognition binds verified imported face IDs to named semantic regions with evidence. It does not promise arbitrary feature-history recovery. A recognized value can enter Selection, PMI, Modify, or FEA only when its capabilities and exact binding meet that consumer's contract.

## 8. Add a linear-elastic Analysis

`docs/development/milestones/fea/artifacts/m5/plate-with-hole.firmament` is the canonical native plate-with-hole benchmark. Its `Analysis` declares one isotropic material, semantic fixed/load regions, and requested results.

```powershell
aetheris fea docs/development/milestones/fea/artifacts/m5/plate-with-hole.firmament --out-dir artifacts/plate-fea --json
```

The command writes AnalysisIR, native displacement/stress results, diagnostics/metrics, and an Abaqus `.inp` verification deck. No commercial Abaqus execution is claimed; running Abaqus requires the user's installation.

## 9. Assemble Template-produced parts

`fixtures/AssemblyM1/template-block-pair.firmament` turns two Static Records into Template-produced Part definitions, instantiates them in one product tree, and relates their exposed semantics through an Interface and Mate.

```powershell
aetheris asm inspect fixtures/AssemblyM1/template-block-pair.firmament --json
```

The report distinguishes reusable definitions from occurrences and includes transforms, world bounds, Mate residuals, dimensional transitions, and Template/Record provenance.

## 10. Assert a worst-case tolerance path

`fixtures/AssemblyM0/bearing-module.firmament` combines Shaft/Bore and seated-axis Interfaces with explicit dimensional Relations:

```firmament
Assert ToleranceStackup AxialReach {
    Between: [BearingModule.FixedSupport.Housing.Datum, BearingModule.Rotor.Shaft.Shoulder];
    Require: Clearance >= 44.90mm;
}
```

The compiler finds one deterministic path, propagates signed nominal/lower/upper intervals, retains every contribution and provenance string, and fails compilation when the minimum clearance is not met. `bearing-module-failing.firmament` is the intentional negative proof.

Continue with the [definitive language reference](language-reference.md) for exact syntax/status, then use `language-features.json` as the machine-readable feature contract.
