# Firmament generic Sheet Metal product families (M5)

`Template` is a Firmament language feature for typed compile-time generic
engineering construction. It is not document templating, text substitution, a
CAD prefab, or a privileged Sheet Metal generator. The source of the standard
families is `Aetheris.SheetMetal/Firmament/SheetMetalProductFamilies.firmament`.
The core Firmament Template parser/binder specializes it before the Sheet Metal
domain compiler sees an ordinary concrete declaration.

## Generic source and specialization

```firmament
Record TraySpec {
    Width: Length
    Depth: Length
    WallHeight: Length
    Thickness: Length
    InsideRadius: Length
    KFactor: float
    ReliefPolicy: SheetMetalReliefPolicy
}

Template < Spec: TraySpec >
SheetMetal FourWallTray: Tray {
    Require Positive => Spec.Width > 0mm && Spec.WallHeight > 0mm
    Thickness: Spec.Thickness;
    KFactor: Spec.KFactor;
    Base Base { Profile: Rectangle { Width: Spec.Width; Height: Spec.Depth; }; }
    Flange Front { From: Base.Front; Height: Spec.WallHeight; Angle: 90deg; Radius: Spec.InsideRadius; Relief: Spec.ReliefPolicy; }
    // Right, Rear, and Left use the same ordinary Flange construct.
}

Static LabTray: TraySpec = TraySpec {
    Width: 160mm Depth: 110mm WallHeight: 36mm Thickness: 1.2mm
    InsideRadius: 1.5mm KFactor: 0.42 ReliefPolicy: Rectangular
}
SheetMetal Body = FourWallTray < Spec: LabTray >
```

Angle-bracket application creates a deterministic specialization identity. A
Record argument is checked field-by-field, `Require` is evaluated before
geometry lowering, and the generated `SheetMetal Body: Tray` claim is checked
against the Concept's required semantic members. Missing members and incompatible
member types are compile errors.

The shipped families are `LBracket`, `UChannel`, `FourWallTray`,
`RemovablePanLid`, and `ElectronicsEnclosure`. The latter includes a bounded body
and one parent-flange lip. A separate removable lid is specialized from the same
module because the current Sheet Metal compilation result is one manufactured
part. Cross-part nested Template specialization belongs in Firmament Assembly;
pretending that two blanks are one `SheetMetalPartIr` would corrupt manufacturing
semantics. This is the principal remaining product-family composition gap.

## Semantic blank composition

Authored exact flattening builds a `BlankCompositionPlan`:

```text
BaseContour
  + ordered connected material regions
  - topology-owned corner reliefs
  - nested through-cut loops
  = one exact material contour
```

Each operation states its semantic owner and expected topology. Corner reliefs
must replace an outer corner chain; through cuts must produce clockwise inner
loops. The shared analytic arrangement kernel retains line/arc provenance. M5
also normalizes signed zero in topology buckets, fixing the M4 failure where
`-0` and `0` incorrectly became different graph vertices in symmetric
multi-relief trays.

`SheetMetalFabricationIr` exposes the outer cut contour, inner cut loops,
bend-up/down lines, relief semantic IDs, millimetre units, and deterministic
hash. This is sufficient for a future bounded DXF serializer. M5 defers DXF
serialization rather than introducing an unreviewed writer; STEP and review SVG
are implemented now.

## Downstream authoring

A specialized part stays open to normal Sheet Metal operations:

```firmament
SheetMetal NetworkAppliance = ElectronicsEnclosure < Spec: NetworkSpec >

Extend SheetMetal NetworkAppliance {
    Cut RearEthernet { On: Rear; At: (80mm, 18mm); Profile: Rectangle; Width: 16mm; Length: 14mm; }
    Hole MountA { On: Body; Center: (25mm, 25mm); Diameter: 3.2mm; }
}
```

The public paths keep the same shape across specializations (`Body.Front`,
`Front.Outer`, `FrontLip.Bend`, `Flat.Body`, and owning-region cut paths such as
`Rear.RearEthernet`). DFM subjects retain the specialized instance, owning
region, and feature name.

## Forge host boundary

`SheetMetalProductFamilies.MakeEnclosure(spec)` passes a typed host Record to
the same Firmament Template binder. It returns the formed part IR, exact flat IR,
DFM report, stable semantic paths, fabrication IR, SVG, and specialization
identity. STEP emission remains an explicit artifact operation so applications
choose output locations and I/O policy.

The network-appliance fixtures demonstrate the body and lid paths. The awkward
parts today are repeated vent declarations and the separate body/lid Assembly
step. A generic bounded pattern Template is the next reusable language
abstraction; lid clearance and connector placement remain genuine engineering
choices rather than defaults the compiler should guess.
