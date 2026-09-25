# HOLLOW-BLEND-X0 — coupled bottom transition

**Verdict: Accepted.** Aetheris builds the ORDNING-inspired perforated Hollow blank with a generous, constant-thickness lower transition and roundtrips its production STEP as one enclosed manifold.

## Construction

The authored `BottomBlendRadius: 8mm` is part of `Cylinder<Hollow>` stock. With 56 mm outer radius and 0.8 mm wall, the outer and inner quarter-tori have the same 48 mm major radius and respective 8 mm and 7.2 mm minor radii. They meet the wall at z=8 mm and the bottom planes at z=0 mm and z=0.8 mm. This paired normal offset preserves 0.8 mm through the bend. Invalid radii or holes intersecting the blend junction are rejected. The construction uses analytic cylinders, planes, and tori; it does not shell or independently fillet a finished solid.

The [canonical source](../../fixtures/Canonical/Presentation/ordning-utensil-holder-blended.firmament) retains 112 mm OD, 142 mm height, 0.8 mm wall, and the existing 7 × 19 local perforation grid. The top rim is intentionally outside this milestone.

## Qualification

- CLI build: 133 openings; 140 faces, 673 edges, 538 vertices; enclosed manifold before and after AP242 export/reimport.
- Reimported analytic supports: 135 cylinders, 3 planes, 2 tori. Bounding box: x/y ±56 mm, z 0–142 mm.
- BRep display tessellation produces all 140 face patches. The shaded render under `artifacts/local/hollow-blend-x0/` shows the lower 8 mm transition around the full circumference.
- On the recorded CLI run, pattern/BRep construction took 466 ms, STEP export 828 ms, and STEP reimport 3,960 ms. The STEP file is 57,361,059 bytes.
- `SurfaceMeshIR` still rejects trimmed cylindrical faces; this does not affect BRep display tessellation or STEP.

The Release solution build completed with zero errors. The fast Core lane passed 977 tests. The serial full solution lane passed Core 1110/1110 and Firmament 1649/1649; its seven CLI/SheetMetal failures are the same tests reproduced at parent HEAD in the preceding radial-cut milestone. See `artifacts/local/hollow-blend-x0/full-test-qualified.log` for the final run.

Reproduce with `dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Presentation/ordning-utensil-holder-blended.firmament --output artifacts/local/hollow-blend-x0/ordning-utensil-holder-blended.step --json`. The ignored `artifacts/local/hollow-blend-x0/` directory contains the STEP, OBJ, and shaded PNG from this run.
