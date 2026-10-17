# Sheet Metal

Authored Sheet Metal uses a specialized semantic model: a base region, named flanges and bend strips, and cuts on named planar regions. It can emit formed STEP, thickness-bearing flat STEP, and flat SVG; it also carries material, K-factor, DFM, semantic layout, and manufacturing definitions on qualified routes.

A closed boundary has the same `Profile` meaning in mechanical and sheet-metal
construction. Use canonical 2D shapes for a compact boundary, or the ordinary
`Loop`/`Segment`/pipeline forms for an authored boundary. The consuming operation
then checks its own capabilities.

**Complete file — a bracket with a reusable blank and rounded opening:**

```firmament
Profile blank = Rect2 { center: [40mm, 25mm]; size: [80mm, 50mm] }
Profile ventilation = Slot2 { center: [0mm, 0mm]; length: 12mm; width: 6mm }

SheetMetal MountingBracket {
    thickness: 1.5mm
    material: "5052-H32 Aluminum"
    kFactor: 0.42
    Base deck { profile: blank }
    Flange upright {
        from: deck.Rear
        length: 30mm
        angle: 90deg
        insideRadius: 2mm
        direction: Up
    }
    Cut vent { on: deck; profile: ventilation; at: Point2(40mm, 20mm) }
}
```

`Profile blank = Rect2 { ... }` is also admitted in a mechanical Model and consumed
by `Extrude stock { profile: blank; from: 0mm; to: 1.5mm }`. The shape's existing
field schema, closure checks and exact Profile lowerer remain authoritative.
`Profile copy = blank` names an immutable derived boundary. Detailed boundary
editing continues to use the ordinary `Profile { from: ... }` and `Replace` forms.
There is no separate sheet-metal sketch language for new named boundaries.

Use PascalCase for constructs/types and domain symbols (`Base`, `Rect2`, `Up`,
`Rear`), camelCase for fields and authored value names. PascalCase field spellings
remain compatible. Semicolons are optional in Base, Flange, Hole and Cut fields;
newlines can separate fields. Older manufacturing/semantic-layout recipes retain
their documented statement forms and terminators.

### Coordinates and capability admission

A Base currently admits one axis-aligned rectangular Profile in local XY. Its
minimum corner becomes the physical base origin; it is not silently recentered.
An optional Base `origin` adds a physical translation. A circle or arbitrary
polygon cannot serve as this rectangular base merely because it has bounds.
Non-axis-aligned or non-XY bases reject explicitly.

A Cut admits one closed line/arc Profile or a circle. Its coordinates are local to
its named sheet region. Optional `at: Point2(x, y)` adds a translation; without
`at`, the authored Profile coordinates are used directly. Curves remain exact in
the formed and flat contours. Inner islands, ellipses, splines and non-XY Profile
frames are not admitted by this sheet cut owner. Crossing a region boundary or
bend zone still rejects. Structural parsing alone does not admit a geometry
capability.

`Slot2.length` is the **overall** rounded-slot length, including its semicircular
ends, exactly as in mechanical profiles. `Circle2` uses `radius`; `Rect2` uses
`size`. This avoids the old sheet-specific dimension vocabulary for new Profiles.

The sheet region's `Rear`/`Front` members are attachment semantics; a Profile's
`Top`/`Bottom` members name its planar boundary. They are not global-axis aliases.
Named regions, attachment paths, ProfileDelta programs and formed/flat identity
remain useful sheet-metal concepts; ordinary Profile boundaries and edits now
feed them through the shared boundary authority.

### Compatibility

Existing `Base: Rectangle(w, d)` and named
`Base deck { Profile: Rectangle { Width: 80mm; Height: 50mm; }; }` retain their
geometry. Legacy cuts using `Profile: Rectangle`, `Circle` or `Slot`, including
inline dimensions and tuple placement, remain admitted. **Legacy `Slot` cuts are
rectangular** in this route; they are not silently converted into capsules. Use a
named `Profile` built with `Slot2` when a rounded slot is intended.

Sheet Metal does not use a Model `units` declaration in this authored route;
dimensional fields carry explicit units. `thickness`, `material`, `kFactor`, and
the named Base belong directly in the `SheetMetal` body.

[`modern-bracket.firmament`](../../../fixtures/Regression/SheetMetalSyntax/modern-bracket.firmament)
and its legacy reference qualify formed STEP parity. Build and flatten it:

```powershell
aetheris build fixtures/Regression/SheetMetalSyntax/modern-bracket.firmament --output artifacts/local/bracket-formed.step --json
aetheris sheetmetal flatten fixtures/Regression/SheetMetalSyntax/modern-bracket.firmament --step artifacts/local/bracket-flat.step --svg artifacts/local/bracket-flat.svg --json
```

[`profile-delta-tab-family.firmament`](../../../fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament)
is the data-driven edge-edit example: a finite Table selects `ProfileTabSpec`,
`with` derives the shop variant, and the standard-library `ProfileDelta Tab`
modifies the wall edge while preserving formed/flat semantic identity.

Flat inspection distinguishes `MaterialArea` from `BoundingArea`. `MaterialArea` is exact line/arc integration of the final flat material contour after additive ProfileDelta regions, notches, holes, cutouts, and overlap clipping. `BoundingArea` is only the rectangular nesting envelope. If no validated exact blank contour exists, material area is reported as unavailable rather than reconstructed from feature bookkeeping.

Sheet Metal holes use `Hole Mount { On: Base; Center: (40mm, 20mm); Diameter: 8mm; }`. Model-domain `Hole<Shaft>` is deliberately rejected here with `sheetmetal-hole-domain-syntax`. `On` targets a named planar semantic region (`Base`, a named flange, or a verified exposed region/path), not `face(+Z)`. Manufacturing PMI likewise uses `DatumFeature A { Target: Base; }` inside `Pmi`, together with a named `Manufacturing` block; Model-domain `Datum`/`face(-Z)` syntax is rejected with `sheetmetal-pmi-domain-syntax`.

The JSON `part.features` count is the number of lowered cuts/openings in `SheetMetalPartIr`; bends are reported separately as `part.bends`. Authoring and imported reconstruction are separate workflows. Commands such as `sheetmetal recognize`, `recover-flat`, and `recover` operate on imported STEP and may be partial; they do not turn arbitrary geometry into authoritative authored intent.


### Domain schemas before assembly binding

`Use SheetMetal.ProductFamilies` supplies the family's Record schemas as well as
its Templates. `EnclosureProductFamilies.Compile` and `SheetMetalFirmament.Compile`
resolve those imports through the same owner before shared value admission. API
clients using the low-level Assembly parser directly can call
`SheetMetalFirmament.ResolveImports(source)` first; the Core parser does not
invent or duplicate SheetMetal schemas. This preparation does not materialize
geometry or grant build success.
