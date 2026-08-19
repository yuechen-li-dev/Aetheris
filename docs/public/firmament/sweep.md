# Circular Sweep

X0 adds a deliberately bounded wire-geometry primitive: a constant circular section transported along a semantic `Concept Path`.

```firmament
Concept Path Centerline {
    Start: Point2(0mm, 0mm)
    Heading: 90deg
    Line LeftLeg { Length: 25mm }
    Arc Return { Radius: 6mm; Turn: -180deg }
    Line RightLeg { Length: 25mm }
}
Sweep Wire {
    Path: Centerline
    Diameter: 1.2mm
    Material: Standard.Materials.StainlessSteel.304_Annealed
}
```

`Diameter` is canonical and must be a positive finite `Length`. The path is resolved through the same domain-neutral Concept Path frontend used by profiles; Sweep does not introduce a second path model.

## X0 boundary

Supported:

- one open planar XY path;
- ordered analytic `Line` and `Arc` segments;
- tangent, continuous joins;
- one constant circular section;
- analytic cylindrical and toroidal side faces;
- planar end caps and an enclosed solid;
- AP242 export and reimport.

Not supported:

- arbitrary 3D guide curves or non-XY planes;
- sharp corners or implicit fillets;
- closed paths;
- variable/multiple profiles, rails, lofts, twist laws, or user-controlled moving frames.

Arc bend radius must exceed the section radius. Nonadjacent path regions must remain at least one wire diameter apart, plus `MinimumGap` when supplied. Line/line clearance is exact. Arc-involving clearance uses a deterministic conservative chord witness and is intentionally obvious-overlap coverage, not a complete general curve-distance proof.

Invalid paths fail before topology construction with segment-specific diagnostics. See the canonical [Sweep fixtures](../../../fixtures/Canonical/Features/Sweep/) and focused [invalid fixtures](../../../fixtures/Invalid/Sweep/).
