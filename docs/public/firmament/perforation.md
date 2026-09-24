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

Current production support is a single Perforation on a Box `+Z` face. Cylindrical supports, including constant-thickness piping sleeves, currently produce `perforation-cylindrical-topology-unavailable`: the BRep Boolean route does not construct radial cylinder-to-cylinder intersections on an annular wall. This source form does not export an unperforated sleeve as if it were complete.

Reproduce the flat witness with:

```text
dotnet run --project Aetheris.CLI -- build fixtures/Canonical/Perforation/flat-hex-panel.firmament --output artifacts/local/perforation/flat-hex-panel.step --json
```
