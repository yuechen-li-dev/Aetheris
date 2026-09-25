# Circular perforation on a flat box face

`Perforation` describes one field of circular through openings. The compiler places and clips its instances; source does not list individual holes.

```firmament
Model FlatHexPanel {
  Units: mm
  Box Panel { Size: [90mm, 70mm, 3mm] }
  Modify Panel {
    Perforation Vent {
      On: +Z
      Diameter: 6mm
      Layout: Hex
      Pitch: 9mm
      Margin: 8mm
      MinimumLigament: 2mm
    }
  }
}
```

`On: +Z` binds the semantic top face of the Box. Its 2D frame uses world X and Y, with the box center as origin. `Layout: Grid` gives square rows; `Layout: Hex` shifts alternate rows by half the pitch and uses row spacing `Pitch * sqrt(3) / 2`. The default lattice is centered in the admissible region. `OffsetX` and `OffsetY` shift it in millimeters.

`Margin` is material clearance between every hole rim and a box edge. A center outside that inset region is omitted; partial openings are never emitted. `Pitch` must exceed `Diameter` and leave at least `MinimumLigament` between adjacent rims. At most 1000 openings are admitted. The compiler reports the count and pattern-space identities such as `Panel.Vent.Row3.Col7`; every generated wall has source correspondence to the Perforation and its instance.

Production STEP support includes one Perforation on a Box `+Z` face or on the `OuterWall` of a `Cylinder<Hollow>`. Cylindrical layout accepts `CylindricalGrid` or `CylindricalStaggered`, axial `Pitch`, optional `CircumferentialPitch`, `MarginTop`, `MarginBottom`, `MinimumLigament`, and `StartAngle` in degrees. The compiler resolves an integer number of columns around the full circumference, centers axial rows in the allowed band, and assigns stable `RowN.ColM` identities. It checks spacing at the inner wall. The constructive BRep has one outer and one inner opening loop plus a local cutter-cylinder wall per instance; the opposite wall is not cut by an instance.

A top-open `Cylinder<Hollow>` may specify `BottomBlendRadius: 8mm` in its body. The lower outer and inner quarter-torus transitions share one major radius; their minor radii differ by `WallThickness`. This keeps the formed bottom transition at constant normal thickness. The radius must exceed wall thickness and remain below the cylinder radius and height. Perforation holes must clear the cylindrical wall's blend junction. The [blended utensil-holder fixture](../../../fixtures/Canonical/Presentation/ordning-utensil-holder-blended.firmament) keeps the 133-hole layout of the sharp-bottom blank.

```firmament
Struct PerforatedSleeve {
  Cylinder<Hollow> Body {
    Radius: 56mm
    Height: 142mm
    WallThickness: 0.8mm
    Openings: [Top]
  }
  Modify Body {
    Perforation Vents {
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

The full-wall pattern currently requires a clear angular location for the host's periodic parameter seam. Dense patterns whose openings cover every possible seam angle fail with `Brep.HollowRadialCutPattern.SeamClearance`; periodic split topology for that case is not yet available. SurfaceMeshIR does not yet support the trimmed cylindrical faces; use the BRep display tessellator or STEP wireframe route for display.

Reproduce the flat witness with:

```text
dotnet run --project Aetheris.CLI -- build fixtures/Canonical/Perforation/flat-hex-panel.firmament --output artifacts/local/perforation/flat-hex-panel.step --json
```
