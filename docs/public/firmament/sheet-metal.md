# Sheet Metal

Authored Sheet Metal uses a specialized semantic model: a base region, named flanges and bend strips, and cuts on named planar regions. It can emit formed STEP, thickness-bearing flat STEP, and flat SVG; it also carries material, K-factor, DFM, semantic layout, and manufacturing definitions on qualified routes.

```firmament
SheetMetal MountingBracket {
    Thickness: 1.5mm;
    Material: "5052-H32 Aluminum";
    KFactor: 0.42;
    Base: Rectangle(80mm, 50mm);

    Flange Upright {
        From: Base.Rear;
        Length: 30mm;
        Angle: 90deg;
        InsideRadius: 2mm;
        Direction: Up;
    }

    Hole Mount {
        On: Base;
        Center: (40mm, 20mm);
        Diameter: 8mm;
    }
}
```

Sheet Metal does not use a Model `Units` declaration in this authored route; dimensional fields carry explicit units. `Thickness`, `Material`, `KFactor`, and `Base` belong directly in the `SheetMetal` body, alongside `Flange` and `Hole` declarations.

[`l-bracket-with-hole.firmament`](../../../fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament) is the practical first example. Build the formed part and then flatten the same source:

```powershell
aetheris build fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament --output artifacts/bracket-formed.step --json
aetheris sheetmetal flatten fixtures/Canonical/SheetMetal/l-bracket-with-hole.firmament --step artifacts/bracket-flat.step --svg artifacts/bracket-flat.svg --json
```

[`profile-delta-tab-family.firmament`](../../../fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament) is the modern data-driven profile example: a finite Table selects `ProfileTabSpec`, `with` derives the shop variant, and the standard-library `ProfileDelta Tab` modifies the wall edge while preserving formed/flat semantic identity.

Flat inspection distinguishes `MaterialArea` from `BoundingArea`. `MaterialArea` is exact line/arc integration of the final flat material contour after additive ProfileDelta regions, notches, holes, cutouts, and overlap clipping. `BoundingArea` is only the rectangular nesting envelope. If no validated exact blank contour exists, material area is reported as unavailable rather than reconstructed from feature bookkeeping.

Sheet Metal holes use `Hole Mount { On: Base; Center: (40mm, 20mm); Diameter: 8mm; }`. Model-domain `Hole<Shaft>` is deliberately rejected here with `sheetmetal-hole-domain-syntax`. `On` targets a named planar semantic region (`Base`, a named flange, or a verified exposed region/path), not `face(+Z)`. Manufacturing PMI likewise uses `DatumFeature A { Target: Base; }` inside `Pmi`, together with a named `Manufacturing` block; Model-domain `Datum`/`face(-Z)` syntax is rejected with `sheetmetal-pmi-domain-syntax`.

The JSON `part.features` count is the number of lowered cuts/openings in `SheetMetalPartIr`; bends are reported separately as `part.bends`. Authoring and imported reconstruction are separate workflows. Commands such as `sheetmetal recognize`, `recover-flat`, and `recover` operate on imported STEP and may be partial; they do not turn arbitrary geometry into authoritative authored intent.
