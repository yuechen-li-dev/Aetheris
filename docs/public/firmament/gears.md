# Gear Standard Library

Firmament can build common gears from engineering parameters. Authors name the family and dimensions; the compiler owns tooth construction, radial ordering, closed BRep materialization, and deterministic AP242 export.

```firmament
Model Drive {
    Units: mm
    SpurGear DriveGear {
        Module: 2mm
        Teeth: 24
        PressureAngle: 20deg
        FaceWidth: 10mm
        BoreDiameter: 8mm
        Backlash: 0.15mm
        Phase: 0deg
    }
}
```

Use `SpurGear` for an external parallel-axis gear and `InternalSpurGear` for a ring. `BevelGear` is the bounded straight-bevel family; `MiterGear` is its equal-tooth 1:1 specialization. `RatchetGear` and `Pawl` describe one-way indexing geometry and are not involute gears. A pinion is a `BevelGear` or `SpurGear` in the smaller-tooth-count role; it is not a separate geometry type.

## Typed gear interfaces

`Interface<Gear>` binds two declared members without repeating a role schema:

```firmament
Model Pair {
    Units: mm
    SpurGear Driver { Module: 2mm Teeth: 24 PressureAngle: 20deg FaceWidth: 10mm BoreDiameter: 8mm }
    SpurGear Driven { Module: 2mm Teeth: 48 PressureAngle: 20deg FaceWidth: 10mm BoreDiameter: 12mm Phase: 3.75deg }
    Interface<Gear> Mesh { A: Driver B: Driven }
}
```

The interface checks family, module, pressure angle, and tooth-count constraints. Inspection reports compatibility, ideal ratio, rotation sign, and parallel-axis center distance. External meshes reverse direction; external/internal meshes retain direction. For bevel pairs, add `ShaftAngle`; pitch-cone angles must sum to it. A miter pair must contain equal tooth counts.

```firmament
Interface<Gear> RightAngle { A: Input B: Output ShaftAngle: 90deg }
```

Ratchet/pawl interfaces reuse the same typed form and add one-way metadata:

```firmament
Interface<Gear> Indexing {
    A: Wheel
    B: Stop
    AllowedDirection: Clockwise
    EngagementPhase: 2deg
}
```

This relationship is ideal geometry and kinematics. It is not a contact, dynamics, torque, wear, or stress solver.

## Gear ports in subassemblies

A gear does not stop being a Gear inside a Subassembly. Expose it without copying its parameters, then use ordinary `Interface<Gear>` in the parent:

```firmament
Expose { Gear Output = OutputGear; }

Interface<Gear> Transfer {
    A: RegisterA.Output;
    B: RegisterB.Input;
}
```

The endpoint holds a typed reference to the existing Gear AIR. Definition-level dimensions and phase remain Gear-owned; the Assembly occurrence supplies the world transform. Module, pressure angle, family, ratio, rotation sign, bevel closure, and ratchet rules still come from the one Gear evaluator. Assembly adds only occurrence-space axis and actual center-distance validation.

## Parameters and boundaries

| Family | Status | Required | Optional | Boundary |
|---|---|---|---|---|
| `SpurGear` | Supported | `Module`, `Teeth`, `PressureAngle`, `FaceWidth` | `BoreDiameter`, `Backlash`, `Phase` | Standard full-depth external involute; tooth counts below 17 rejected |
| `InternalSpurGear` | Supported | `Module`, `Teeth`, `PressureAngle`, `FaceWidth`, `OutsideDiameter` | `Backlash`, `Phase` | Reversed internal involute flanks and ring stock |
| `BevelGear` | Bounded | `Module`, `Teeth`, `PressureAngle`, `FaceWidth`, `PitchConeAngle` | `BoreDiameter`, `Phase` | Straight bevel only; pitch-cone ruled tooth loft |
| `MiterGear` | Bounded | `Module`, `Teeth`, `PressureAngle`, `FaceWidth` | `PitchConeAngle`, `BoreDiameter`, `Phase` | Equal-pair bevel specialization, normally 45-degree cones |
| `RatchetGear` | Bounded | `Teeth`, `OutsideDiameter`, `FaceWidth` | `BoreDiameter`, `DriveFaceAngle`, `Phase` | At least 8 asymmetric radial teeth |
| `Pawl` | Bounded | `Width`, `Length`, `Thickness`, `PivotDiameter`, `NoseLength` | `EngagementAngle` | Prismatic companion with axial pivot bore |

All lengths are millimetres. Pressure angles from 14 through 30 degrees are admitted. `BoreDiameter: 0mm` intentionally creates a solid member. A positive bore must leave root structure. `Backlash` is a linear reduction of circular tooth thickness at the pitch circle, divided symmetrically between the two flanks; it is not a scale factor.

Gear AIR always carries an explicit axis. Omitted source uses `[0,0,1]`; X0 also accepts `Axis: [0,0,1]` explicitly and rejects other frames until frame-aware gear placement is qualified.

`HubDiameter` and `HubLength` are reserved but fail with `firmament-gear-hub-geometry-not-qualified` in X0. This prevents product-specific hub intent from being discarded. Keyways, set screws, profile shift, custom root fillets, helical/worm/spiral-bevel/hypoid gears, contact, and manufacturing quality classes are deferred.

## Inspect and build

```powershell
aetheris validate fixtures/Canonical/Gears/spur-basic.firmament --json
aetheris inspect fixtures/Canonical/GearInterfaces/spur-pair.firmament --json
aetheris build fixtures/Canonical/Gears/spur-basic.firmament --out artifacts/local/spur.step --json
aetheris asm inspect fixtures/Canonical/AssemblyInterfaces/exposed-gear-port.firmament --json
```

`inspect` reports the authored and derived diameters, axis, phase, backlash, stable `Tooth0` through `ToothN-1` identities, and interface results. `build` adds topology and surface counts, involute approximation error, manifold STEP re-import evidence, and a SHA-256 digest. A build emits one gear body; use the typed interfaces as assembly/placement authority for trains rather than treating a multi-gear source as one fused solid.

See the compact executable examples in [`fixtures/Canonical/Gears`](../../../fixtures/Canonical/Gears) and [`fixtures/Canonical/GearInterfaces`](../../../fixtures/Canonical/GearInterfaces).
