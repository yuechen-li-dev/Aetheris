# PERFORATION-CYLINDRICAL-X1

## Executive verdict: Accepted for the admitted cylindrical pattern family

One semantic Firmament `Perforation` now constructs a deterministic field of local circular holes through **one wall** of a `Cylinder<Hollow>`. Grid and staggered layouts build a closed, manifold, exact-surface BRep. The 133-hole ORDNING-inspired blank exports and reimports as one AP242 body with 133 cutter-cylinder wall faces. Its source contains one Perforation declaration. This milestone makes a perforated blank; it does not add the lower formed blend or finished top rim of the later product witness.

The acceptance is bounded: the host-cylinder parameter seam must fit between openings. The builder chooses the largest angular gap and diagnoses layouts with no seam-clear position. Splitting an opening across the host seam remains separate work. The default BRep display mesh is coarse around 133 trims; the high-quality shaded artifact used a tighter display tolerance. Neither limit affects the exact STEP BRep for the accepted fixtures.

## Authoring and placement

The [ORDNING-inspired fixture](../../fixtures/Canonical/Perforation/cylindrical-ordning-blank.firmament) is:

```firmament
Struct OrdningInspiredPerforatedBlank {
  Cylinder<Hollow> Body {
    Radius: 56mm
    Height: 142mm
    WallThickness: 0.8mm
    Openings: [Top]
  }
  Modify Body {
    Perforation SideVents {
      On: OuterWall
      Diameter: 9mm
      Layout: CylindricalGrid
      Pitch: 15mm
      CircumferentialPitch: 18mm
      MarginTop: 12mm
      MarginBottom: 20mm
      MinimumLigament: 2mm
      StartAngle: 9deg
    }
  }
}
```

`Pitch` is axial center spacing in mm. `CircumferentialPitch` is a nominal physical spacing at the **mid-wall radius**. The planner rounds `2π × mid-wall radius / nominal pitch` to an integer column count and distributes those columns evenly over `[0, 2π)`. In this fixture that gives 19 columns and 18.387 mm realized mid-wall arc spacing, or 0.330694 rad angular pitch. The planner checks the minimum adjacent circumferential chord at the inner wall, plus axial pitch, against the diameter and minimum ligament. The valid center band is `bottom + hole radius` through `height − top − hole radius`; axial rows are centered in it. Seven rows × 19 columns yield **133 holes**. Grid rows align; staggered rows shift alternating angular positions by half a column pitch and wrap without a special last column.

Instances are emitted row-major with stable IDs such as `Body.SideVents.Row3.Col7`. No hand-authored hole list, seam duplicate, partial top/bottom hole, generic Boolean, mesh Boolean, or voxel operation is involved. Invalid pitch, overlap, exhausted margins, unsupported host, and count over 1000 return controlled diagnostics. A failed intersection/curve realization reports the instance ID and fails the whole feature.

## Constructive BRep and provenance

`BrepHollowRadialCutPattern` uses the same exact cylinder-cylinder intersection, qualified finite curves, and host/tool pcurve realization authorities as the accepted one-hole radial cut. It constructs the outer and inner host faces once, with one trim loop on each per instance, and creates one bounded cutter-cylinder wall face for each pair. The end caps and rim remain unchanged. A shared host seam is placed in the largest clear angular gap. The hole cutter stays on the positive local radial side; representative outer and inner opening vertices have positive radial coordinates, so no opposite-wall opening is introduced by an instance.

Every wall, outer opening, and inner opening has a deterministic pattern-space topology key. Runtime correspondence emits `HoleWallFace`, `HoleEntryLoop`, and `HoleExitLoop` descendants per instance, linked to the single source Perforation span. Numeric BRep IDs are realization details, not pattern identities. Exact intersection curves remain the authority beneath finite cubic edge representations. For the 133-hole fixture the largest certified curve deviation is **9.89 × 10⁻⁸ mm** and the largest host/tool pcurve mismatch bound is **4.60 × 10⁻⁷ mm**.

The source body's face/edge/vertex counts follow `5 + N`, `6 + 5N`, and `4 + 4N`. Every edge has two coedges. The 133-hole fixture has **138 faces, 671 edges, 536 vertices**. Production STEP reimport verifies one manifold body, the same face and analytic cylinder counts, outer radius 56 mm, inner radius 55.2 mm, 133 cutter radii of 4.5 mm, and planes at 0, 0.8, and 142 mm. CLI STEP analysis confirms one enclosed-manifold body, bounds **112 × 112 × 142 mm**, 135 analytic cylinders, three planes, 532 B-spline trims, and no unsupported surface or edge families. The open top and closed base remain from Hollow authority. The STEP hash is `9A5D3D0BCDDF9BFAD4E3DC63FAFD371D999A53E470801B74E9B75A89A120C132`.

## Witnesses and visual result

| Fixture | Layout | Rows × columns | Holes | STEP roundtrip |
| --- | --- | ---: | ---: | --- |
| `cylindrical-grid-8` | Grid | 1 × 8 | 8 | Manifold |
| `cylindrical-grid-24` | Grid | 3 × 8 | 24 | Manifold |
| `cylindrical-staggered-32` | Staggered | 4 × 8 | 32 | Manifold |
| `cylindrical-ordning-blank` | Grid | 7 × 19 | 133 | Manifold |

The [shaded blank](../../artifacts/local/perforation-cylindrical-x1/cylindrical-ordning-blank-shaded.png) comes from the supported BRep display tessellator at 0.01 mm chord tolerance and 1024 maximum segments. The [isometric wireframe](../../artifacts/local/perforation-cylindrical-x1/cylindrical-ordning-blank.wireframe.svg) comes from `aetheris wireframe` on the production STEP. The silhouette reads as an open, thin-wall utensil-holder blank with clean top and bottom material bands and regular cylindrical spacing. The rim and bottom corner remain primitive in this X1 blank by design.

## Performance and size

One warm-process Release/RyuJIT run on this machine, including in-process STEP export/reimport and default BRep display tessellation, measured:

| Holes | Pattern build | STEP export | STEP reimport | Default tessellation | Faces / edges / vertices | STEP size |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 (accepted one-hole builder) | 47.8 ms | 38.3 ms | 931.4 ms | 417.4 ms | 6 / 11 / 8 | 0.41 MB |
| 8 | 33.8 ms | 38.1 ms | 603.2 ms | 457.6 ms | 13 / 46 / 36 | 3.21 MB |
| 32 | 36.9 ms | 322.6 ms | 1081.9 ms | 645.3 ms | 37 / 166 / 132 | 13.62 MB |
| 133 | 84.6 ms | 559.9 ms | 2748.8 ms | 1221.0 ms | 138 / 671 / 536 | 57.36 MB |

The timings are observations, not throughput guarantees. Reimport and STEP size dominate the 133-hole path. The high-quality 133-hole display tessellation took **14.44 s**; that expense is kept out of the normal test lane and used only for the gallery artifact. A separate CLI run, which includes process startup, build, export and reimport, took 2.4 s for 8 holes, 2.8 s for 32, and 5.9 s for 133. No high-count optimizer was added. WASM AOT was not measured.

## Qualification and remaining boundaries

The Core fast lane passed **972/972**. Focused tests cover seam-equivalent 0/2π placement and byte-stable STEP, two-hole and 24-hole manifold topology, 133-hole Firmament/STEP/display, staggered placement, thickness and diameter edits, overlap, exhausted margins, schema/language service, and source provenance. In the serial solution gate Core passed **1105/1105** and Firmament passed **1647/1647**. The gate had seven failures: two CLI tests (`MeshObj_HexBolt_ExportsStructuredPolygonsDirectlyFromSurfaceMeshIr`, `ValidateRoutesModuleShapedSheetMetalThroughDomainCompiler`) and five CTC03 SheetMetal tests. The same seven were reproduced at parent HEAD during the preceding radial-cut milestone. A Hollow-policy test initially exposed an overly broad parser admission in this change; the parser was corrected and the subsequent gate passed that test. The full run log is under `artifacts/local/perforation-cylindrical-x1/full-test-final.log`.

SurfaceMeshIR still rejects trimmed cylindrical faces (`SurfaceMeshIR does not support trim topology on cylindrical face 134`); the supported BRep display tessellator and STEP wireframe paths succeed. Dense layouts with no angular seam gap return `Brep.HollowRadialCutPattern.SeamClearance`. The exact geometry remains available for admitted layouts, including the ORDNING-scale witness; the kernel does not fabricate a partial or opposite-wall cut. This X1 does not add ORDNING's formed lower blend or safe upper edge.

The CLI analysis currently labels imported length units as assumed mm because unit metadata is not preserved by the STEP importer. The source and reimported coordinates and analytic radii match the authored millimeter dimensions; unit-metadata retention is outside this milestone.

Reproduce the production artifact with:

```text
dotnet run --project Aetheris.CLI -c Release -- build fixtures/Canonical/Perforation/cylindrical-ordning-blank.firmament --output artifacts/local/perforation-cylindrical-x1/cylindrical-ordning-blank.step --json
dotnet run --project Aetheris.CLI -c Release -- wireframe artifacts/local/perforation-cylindrical-x1/cylindrical-ordning-blank.step --out artifacts/local/perforation-cylindrical-x1/cylindrical-ordning-blank.wireframe.svg --view iso --density 4 --json
```
