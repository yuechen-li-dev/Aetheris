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
remain compatible. Semicolons and newlines separate fields in the sheet,
semantic-layout, ProfileDelta, manufacturing and PMI owners. Unknown fields,
unknown statements, duplicate aliases and unsupported header placements reject.

### Layout, requirements and release data

A layout Datum is a named region-local **construction point**. It is not a PMI
DatumFeature or a 3D DatumFrame. Declare `Datum center { on: deck; at: deck.Center }`
in a Concept Struct, then refer to `layout.center`. `at` admits `Point2(...)`,
legacy tuples, region centers and scoped Datums, optionally plus a `Vector2`
offset. Short names resolve in their layout; forward references work and cycles
reject. Layout `on XZ` is unsupported and rejects rather than producing XY.

Keep reusable construction data in Concept Structs; put material operations in
the consuming SheetMetal body. A single legacy Concept Struct containing sheet
effects remains supported with its existing identities. Multiple implicit effect
bundles reject as ambiguous. `intent:` does not select between several effect
bundles; move those operations into the sheet body.

Sheet fields consume the same checked scalar Static/Record values as mechanical
models. `Require name => predicate` uses the common Boolean/Concept evaluator,
and `SheetMetal name: Contract` checks every structural member regardless of
semicolons. Legacy brace Require records still support RequiredMembers,
EqualSize, EqualPitch and Mirror. Spatial checks require one owning region;
Mirror accepts `axis: X` or `axis: Y` when its `about` names a Datum. Arbitrary
geometric constraint solving is not part of these checks.

Named `Set<Point2>` values and ordinary `Pattern name over points { point =>
Recipe(point) }` use the existing Feature/Pattern expansion. Sheet recipes may
return `Hole` or `Cut`; mechanical `Hole<Shaft>` is a different domain operation.
Generated identities use Pattern/Set keys, such as `mounting.left`, independently
of entry order. See the complete
[keyed recipe example](../../../fixtures/Regression/SheetMetalSemantics/keyed-features.firmament).
The legacy centered Count/Pitch Pattern retains its placement and index IDs.
Its `Slot` and `Rectangle` feature spellings both retain the historical capsule
with a straight-run Length; use shared Rect2/Slot2 Profiles for new boundaries.

`Manufacturing release { ... }` describes material/process data and general
allowance policies. A sheet selects it with `manufacturing: release`. A single
legacy release remains implicitly selectable. Multiple unselected releases
reject; reusable unselected releases are still checked for invalid fields.
An ordinary quoted process string may contain `//` or `#`.

Use one root-contained `Pmi { ... }` for explicit projection. One named top-level
Pmi remains compatible; multiple Pmi blocks reject. PMI does not require a
Manufacturing block. `Dimension width { kind: Linear; target: deck;
dimension: plate.width }` projects a bound toleranced Length; geometry consumes
its nominal. Record/Static origins are retained on the bound dimension. Literal
`value` and `tolerance: PlusMinus(...)` remain compatible but cannot be combined
with `dimension:`. Allowances must be nonnegative, quantities positive integers,
and kinds one of Thickness, Linear or Diameter.

Sheet PMI targets named regions, bends, parts, authored cuts and generated
features. `DatumFeature`, `Position` with `datumRefs`, and `Note` (legacy
`Annotation`) retain their existing AP242 roles and face associations. General
GD&T support is not implied. Check the exported result with `analyze --json`.
The complete [typed layout/release example](../../../fixtures/Regression/SheetMetalSemantics/modern-layout-release.firmament)
uses a Record requirement for both construction and PMI.

ProfileDelta fields use the same AST consumption and dimensional binding.
Prefer `anchor: FromStart(0mm)` / `FromEnd(...)` / `CenteredAt(...)`; the historic
`Anchor: FromStart 0mm` spelling remains a local compatibility production.

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
geometry. To modernize an implicit Base while preserving its established region
and bend identities, use `base: blank` with a named rectangular Profile. This
feeds the same Profile capability binder as `Base deck { profile: blank }`.
Choosing a named Base changes the public identity family; it is not a cosmetic
rewrite of the implicit Base field. The
[compatibility witnesses](../../../fixtures/Compatibility/FirmamentV2/SheetMetal/README.md)
check exact formed STEP, region/bend/feature IDs and flat hashes.

Legacy cuts using `Profile: Rectangle`, `Circle` or `Slot`, including
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

Sheet Metal holes use `Hole Mount { On: Base; Center: (40mm, 20mm); Diameter: 8mm; }`. Model-domain `Hole<Shaft>` is deliberately rejected here with `sheetmetal-hole-domain-syntax`. `On` targets a named planar semantic region (`Base`, a named flange, or a verified exposed region/path), not `face(+Z)`. Manufacturing PMI likewise uses `DatumFeature A { Target: Base; }` inside `Pmi`; Model-domain `Datum`/`face(-Z)` syntax is rejected with `sheetmetal-pmi-domain-syntax`.

The JSON `part.features` count is the number of lowered cuts/openings in `SheetMetalPartIr`; bends are reported separately as `part.bends`. Authoring and imported reconstruction are separate workflows. Commands such as `sheetmetal recognize`, `recover-flat`, and `recover` operate on imported STEP and may be partial; they do not turn arbitrary geometry into authoritative authored intent.


### Domain schemas before assembly binding

`Use SheetMetal.ProductFamilies` supplies the family's Record schemas as well as
its Templates. `EnclosureProductFamilies.Compile` and `SheetMetalFirmament.Compile`
resolve those imports through the same owner before shared value admission. API
clients using the low-level Assembly parser directly can call
`SheetMetalFirmament.ResolveImports(source)` first; the Core parser does not
invent or duplicate SheetMetal schemas. This preparation does not materialize
geometry or grant build success.
