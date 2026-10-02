# Functional pickup construction

A pickup is one reusable exact part whose housing, coils and poles are features.
The guitar places two occurrences of its definition; poles are not assembly
occurrences. The authored recipe is [pickup.firmament](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/pickup.firmament).
The [standalone witness](../../../fixtures/Canonical/Feature/pickup-functional.firmament)
uses the same recipe without assembly port publication.

```firmament
Feature Pole(Center: Point2, Support: Plane) -> Boss {
    Circle2 Outline { Center: Center; Radius: 2.2mm }
    Profile Section { Loop Outer { Outline |> TraceLoop } }
    return Boss { On: Support; Profile: Section; Height: 1mm }
}

// Inside the pickup's Compose Body, after its housing and two coil features:
Feature PoleSeed = Pole(Center: Layout.FirstPole, Support: CoilTop)
Linear Pattern Poles {
    Source: PoleSeed
    Direction: Layout.Longitudinal.Direction
    Count: 6
    Spacing: Spec.PolePitch
}
```

Feature evaluation is pure semantic construction. Private local guides/profiles
receive invocation-qualified names, e.g. `UpperCoil__Section`. Named calls retain
`ResultIdentity` in Feature invocation evidence. Local declarations admit the
bounded Point2/Rect2/RoundedRect2/Circle2/Profile subset; arbitrary geometry,
mutation, runtime loops and recursion remain unsupported.

`Axis2` is a non-materialized planar datum with a Length-valued two-component
Origin and a finite nonzero dimensionless Direction. Direction is normalized;
inspection records the qualified owner, origin, direction and source span.
The pickup declares longitudinal and transverse axes under its XY Layout.

Linear Pattern is qualified for a circular-profile Boss in XY. It translates
semantic profile/feature intent before AIR, using one checked source and an
explicit direction. Count is 1..1024, includes the seed as instance zero, and
Spacing is a positive Length. Generated identities are `Poles_Instance0` through
`Poles_Instance5`. The seed is consumed rather than materialized a seventh time.
Inspection retains source, direction reference, normalized vector, spacing,
count and generated feature identities. Other profile families, Hole/Pocket
linear repetition, arbitrary 3D transforms and nested patterns are not qualified
by this milestone; rejected circular-Boss inputs have specific diagnostics.

Named Plane supports are bounded to origins `[0mm,0mm,z]`, normal `[0,0,1]` and optional Up `[0,1,0]`, in
an ordinary +Z prismatic Compose. A plane selects an actual connected material
support at its declared level; it does not permit detached material. Unsupported
planes, absent supports and ambiguous supports fail closed. Both coil features
use BaseTop; poles use CoilTop. No feature relies on mutable current-Top selection.

Templates specialize checked Record values before functional Features bind their
arguments. `Static Name: RecordType { ... }` is now a shorthand for the ordinary
`Static Name: RecordType = RecordType { ... }`. Immutable `with` derives a variant:

```firmament
Static BridgePickupSpec = StandardPickup with { PolePitch: 10.4mm }
```

The actual guitar currently places the same StandardPickup specialization twice,
so both occurrences share one geometry definition. Changing one occurrence to
`Humbucker<Spec: BridgePickupSpec>` selects a separate variant. Definition cache
identity remains the existing authored application identity; this milestone does
not add equality-based interning of differently named record aliases.

## Geometry and appearance

The pickup uses the existing exact prismatic section-stack compiler/emitter:
rounded housing, two rounded coil bosses, six circular pole bosses. There are
no imported mesh substitutes or pickup coordinate calculations in Python.
Published Mount frames let the assembly own only the two mounting placements.

The body is deliberately fused for the presentation witness. It does not claim
physical coil insulation, separate fasteners, magnets or manufacturing joinery.
The STEP assembly contains NeckPickup and BridgePickup, each with one body.
The guitar now has 91 visible parts and 53 shared geometry definitions, down from
107 and 55. Four assembly occurrence patterns remain for saddles/tuner hardware.

USD preview assigns one dark pickup material. Blender/Cycles restores cream,
black and nickel regions through default-recipe axial face-height bands, solely
as downstream appearance. That recipe-specific coloration is not authored
feature material assignment and does not change vertices or topology. The
renderer checks the imported geometry hash before and after shading. General
feature-owned appearance assignment remains a separate improvement.

## Reproduce

```powershell
python scripts/create-guitar-x0.py
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll build fixtures/Canonical/Feature/pickup-functional.firmament --out artifacts/local/pickup-features/pickup.step --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll asm inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm --json --profile
./scripts/qualify-guitar-x0.ps1 -OutDir artifacts/local/pickup-features/guitar
```

The generator preserves the authored pickup recipe and derives the standalone
witness from it. It emits only the two pickup occurrence placements.

## Qualification results

The Release solution build, fast core lane (1,004 tests), and full serial solution
lane (4,042 tests) passed. Regression coverage checks private construction hygiene,
named invocation evidence, immutable specification overrides, deterministic STEP,
exact fused volume, support selection, and rejected pattern/support inputs.
CLI inspection exposes both typed axes, the six-member linear pattern, and named
Feature results.

The standalone pickup STEP reimports as one closed, consistently oriented body.
The guitar STEP reimport contains 91 bodies sharing 53 definitions; NeckPickup and
BridgePickup reference one definition, with no coil/seat/pole category groups.
Pickup display tessellation has 7,436 triangles, no unmatched edges and no
nonpositive normal triangles.

Measured guitar compilation was 10.091 seconds cold and 4.389 seconds warm;
display preparation was 0.593 and 0.426 seconds respectively. Cold includes
first-use JIT/library initialization; warm recompiles without a persistent model
cache. Process startup and dotnet build are excluded. Camera interaction was not
remeasured in this change.

The updated 1800-by-2200 Cycles hero was visually inspected. Rendering took
43.659 seconds on an RTX 3070 at 64 samples. Both fused pickups received the
documented appearance bands; product topology hashes were unchanged by shading.
This preserves the presentation witness while correcting its part ownership.

Generated evidence is under ignored `artifacts/local/pickup-features/`:

- `pickup.step`, `part-inspection.json`: standalone exact part and typed evidence.
- `guitar/guitar.step`, `guitar/guitar.usda`, `guitar/guitar-studio.blend`: exports and scene.
- `guitar/hero.png`: refreshed hero image.
- `guitar/step-analysis.json`, `guitar/mesh-check.json`, `guitar/timings.json`,
  `guitar/render.json`: hierarchy, mesh, timing and shading evidence.
- `solution-build.log`, `fast.log`, `closeout-full.log`: build and test validation logs.

## Owl authoring update

The create-guitar-x0.py command is now a read-only CLI reproduction harness. It no longer generates or rewrites pickup, body, neck or assembly source. Appearance is assigned semantically through Material and with, while pickup axial feature shading remains an honest downstream placeholder.
