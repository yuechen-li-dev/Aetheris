# Semantic PMI and AP242

PMI records manufacturing and inspection intent in STEP AP242. Explicit GD&T is
annotation data: it attaches a typed control to geometry without changing the
geometry, solving constraints, or proving that a manufacturing process can meet
the tolerance. Camera and label placement are presentation state. Checked
compile-time requirements can separately be projected into dimensional PMI.

Native Model export supports planar `Datum`, `HoleDiameter`, `HoleCallout`, the
explicit geometric controls below, sizes and locations projected from construction
(`Dimension`, `Location`) and part `Note`s. The diameter record targets a named shaft
hole; on a counterbore it means the shaft `diameter`, not `counterboreDiameter`.
Parsed kinds outside the admitted set, including `Coplanar` and the older
`Distance { targetA targetB }` form, remain rejected by normal export rather than
silently omitted.

Every size is written twice in the AP242 file: in the Aetheris records that
`aetheris analyze` reports, and in the standard semantic form other CAD and
inspection software reads (`DIMENSIONAL_SIZE` or `DIMENSIONAL_LOCATION` with a
`PLUS_MINUS_TOLERANCE`, associated with the measured faces). Readers count each
size once.

## A part specified by its model

The complete example is
[`showcase-bearing-mount.firmament`](../../../fixtures/Canonical/PMI/showcase-bearing-mount.firmament):
a composed block with a boss, two pockets, a bearing bore, four counterbored
mounting holes and a chamfered edge, with datums, sizes, geometric controls,
locations and notes complete enough to make the part without a drawing.

```firmament
Pmi {
    Datum A { target: face(-Z) }
    Datum B { target: face(-X) }
    Datum C { target: face(-Y) }
    HoleCallout MountSize { target: [mountA, mountB, mountC, mountD] dimension: mountDiameter }
    Position MountLocation { target: [mountA, mountB, mountC, mountD] tolerance: 0.25mm zone: Cylindrical datumRefs: [A, B, C] modifiers: [MaximumMaterial] }
    Parallelism HubFace { target: face(hub.Top) tolerance: 0.03mm datumRefs: [A] }
    Dimension PocketDepth { target: [left.Depth, right.Depth] }
    Location LeftPocketFromB { origin: face(-X) to: left }
    Dimension EdgeBreak { target: perimeterBreak.Distance }
    Note Material { text: "MATERIAL: 6061-T6 ALUMINUM" }
}
```

**Targets.**

- `face(-X)` and the other axis faces mean the faces lying in the part's extreme
  plane in that direction, the surface a datum simulator touches. A face split by
  construction is associated in all its pieces. If the extreme in that direction is
  not planar, the target does not resolve and the record rejects.
- On a `Compose` body, `face(op.Top)` names a Base or Boss top, `face(op.Bottom)` a
  Base bottom and `face(pocket.Floor)` a Pocket floor. A cap that is not exactly one
  face rejects.
- A hole is named directly (`target: bore`); `face(bore.Wall)` means the same on a
  Compose body. A list of holes (`target: [a, b, c, d]`) is one requirement on every
  listed hole, written with its quantity ("4X"). It applies to the shaft walls,
  never to a counterbore or countersink wall.
- An authored datum that resolves to no face rejects.
- Any face of a native part can be named the way inspection names it, with the
  selector Helios shows as the face's **Selector**: `face(bracket.Side("profile:section.Outer.Vertex0_Vertex1"))`
  for a side of an extruded profile, `face(bracket.Top)` and `face(bracket.Bottom)`
  for its caps, `face(+X)` for a box wall, `face(mount.Wall)` for a hole wall. See
  [Annotating native faces](#annotating-native-faces).

**`HoleCallout`** takes the same fields as `HoleDiameter` and adds the hole's
counterbore diameter and depth, projected from its construction:
`4X Ø6.6 ±0.1 ⌴Ø11 ↧6.5`. The counterbore of every listed hole must be alike.

**`Dimension`** names a construction parameter; the compiler supplies the value from
the built geometry, so it cannot disagree with the part:

| Target | Value |
|---|---|
| `base.Height`, `base.Length`, `base.Width` | Base thickness; X and Y extent between its walls |
| `boss.Height`, `boss.Diameter`, `boss.Length`, `boss.Width`, `boss.Radius` | Boss height above its support; round or rectangular size; corner radius |
| `pocket.Depth`, `pocket.Diameter`, `pocket.Length`, `pocket.Width`, `pocket.Radius` | Pocket depth to its floor; size; corner radius |
| `finish.Distance` | An equal-distance 45° chamfer (`EdgeFinish<Chamfer>`) |

`Length` runs along X and `Width` along Y, as `Rect2` writes `size: [x, y]`. A list
of the same parameter on several features (`[left.Depth, right.Depth]`) is one
requirement with a quantity; the values must agree. A value with no stated
tolerance falls under the part's general-tolerance note; `dimension:` a toleranced
`Static` or `tolerance: PlusMinus(..)` attaches one.

**`Location`** runs from an axis face (`origin: face(-X)`) to a hole axis or to the
centre of a boss or pocket (`to: left`). `basic: true` makes it theoretically exact
(drawn boxed) and admits no tolerance.

**`Note`** records part-level text such as material, general tolerances and finish.
They read back as annotations.

Fillet radii, countersink callouts and locations between two features are not yet
projected. A `Dimension` or `Location` that does not resolve to faces rejects.

## Annotating native faces

The complete buildable example is
[`extruded-bracket-faces.firmament`](../../../fixtures/Canonical/PMI/extruded-bracket-faces.firmament):

```firmament
Extrude bracket { profile: section; from: 0mm; to: 20mm }
Pmi {
    Datum A { target: face(bracket.Side("profile:section.Outer.Vertex0_Vertex1")) }
    Datum B { target: face(bracket.Side("profile:section.Outer.Vertex5_Vertex0")) }
    Distance Thickness { between: [face(bracket.Side("profile:section.Outer.Vertex2_Vertex3")), face(bracket.Side("profile:section.Outer.Vertex0_Vertex1"))] value: 3mm tolerance: PlusMinus(0.1mm, 0.1mm) }
    Distance Width { between: [face(bracket.Bottom), face(bracket.Top)] value: 20mm tolerance: PlusMinus(0.2mm, 0.2mm) }
    Parallelism Leg { target: face(bracket.Side("profile:section.Outer.Vertex3_Vertex4")) tolerance: 0.1mm datumRefs: [B] }
}
```

- `Datum` and every explicit geometric control accept any named face. A datum on
  a cylindrical face is the cylinder's axis.
- `Distance` measures between two parallel planar faces, as on an imported part:
  a size (`thickness`) when the material lies between them, otherwise a location
  from the first face to the second.
- A side of an extruded profile is named by its profile segment; segments are
  numbered from `Vertex0`. The caps are `Bottom` (at `from`) and `Top` (at `to`).
  A box's walls are its axis faces, `face(-X)` to `face(+Z)`, while they remain
  single planar faces.
- The face is found when the part is built. A selector whose owner is not a body
  or feature of the part rejects when parsed; one naming no face of the built
  body rejects at export (`pmi-geometric-target-unresolved`,
  `pmi-datum-target-unresolved`, `pmi-dimension-target-unresolved`).

## Explicit GD&T

The complete buildable example is
[`explicit-gdt.firmament`](../../../fixtures/Canonical/PMI/explicit-gdt.firmament).
Its Box and Hole are ordinary construction. The following records annotate them
independently of Concept requirements or dimensional projections:

```firmament
Pmi {
    Datum A { target: face(-Z) }
    Datum B { target: face(-X) }
    Datum C { target: face(-Y) }
    Flatness Seat { target: face(+Z) tolerance: 0.03mm }
    Position MountLocation {
        target: face(Mount.Wall)
        tolerance: 0.0254mm
        zone: Cylindrical
        datumRefs: [A, B, C]
        modifiers: [MaximumMaterial]
    }
}
```

| Controls | Datum references |
|---|---|
| `Straightness`, `Flatness`, `Roundness`, `Cylindricity` | Omitted |
| `Parallelism`, `Perpendicularity`, `Angularity`, `CircularRunout`, `TotalRunout` | Required |
| `Position`, `LineProfile`, `SurfaceProfile` | Optional |

`target` must resolve to geometry. `tolerance` is a finite nonnegative Length in
millimeters, including for `Angularity`; it describes a length-valued tolerance
zone. `datumRefs` lists up to three distinct, previously declared datums in
primary/secondary/tertiary order. Legacy `datum: A`, `Parallel` and `Perpendicular`
spellings remain admitted. Form controls reject datum references.

Optional `modifiers` admits `MaximumMaterial`, `LeastMaterial`, `FreeState`,
`CommonZone` and `Statistical`. Unknown, duplicate or contradictory modifiers
reject. Optional `zone` admits `Cylindrical`, `Spherical` and `BetweenSurfaces`.
Omitting it does not invent an explicit zone form. The compiler validates the
record and its references; the author remains responsible for selecting an
appropriate control, feature, zone and modifier combination.

Common-datum groups, datum-reference modifiers, composite frames, projected
zone parameters and other advanced forms are not yet authoring syntax. Imported
semantic forms can be retained as described below. Unknown fields reject.

## Imported semantic PMI and units

An unchanged `InlineStep` part retains source semantic PMI by default, together
with its datum systems, modifiers, tolerance-zone definitions, dimensional
allowances and proved geometry associations. Aetheris resolves source face and
edge identities through importer provenance rather than entity ordering.
Authored annotations may be added; their names must not collide with retained
source annotation names.
CLI `analyze --json` reports `geometricItemEntityIds` for the direct source usage
items (faces, edges, solids or auxiliary datum geometry). Its
`geometricFaceEntityIds` is the face projection used for presentation; an edge's
adjacent faces do not replace its source association identity.
### Annotating imported faces

Authored records on an imported part target faces by their source entity,
`part.face("#51")`, or a recognized region, `part.region("bore")`:

```firmament
Pmi {
    Datum A { target: part.face("#51") }
    Datum B { target: part.face("#191") }
    HoleDiameter Bore { target: part.face("#191") value: 2mm tolerance: PlusMinus(0.1mm, 0.05mm) }
    Distance Thickness { between: [part.face("#102"), part.face("#51")] value: 6mm tolerance: PlusMinus(0.1mm, 0.1mm) }
    Position BorePos { target: part.face("#191") tolerance: 0.1mm zone: Cylindrical datumRefs: [A] }
    Note Material { text: "PA12, black" }
}
```

- A `Datum` on a planar face is a datum plane; on a cylindrical face it is the
  cylinder's axis.
- `HoleDiameter` targets a cylindrical face.
- `Distance` measures between two parallel planar faces. When the material lies
  between them it is a size and exports as `DIMENSIONAL_SIZE` named `thickness`;
  otherwise it is a location and exports as `DIMENSIONAL_LOCATION` from the first
  face to the second. Faces that are not parallel planes are refused
  (`firmament-v2-imported-pmi-distance-faces`).
- Every explicit geometric control accepts any proved imported face.
- A hole recognized on the body is targeted by its region,
  `part.region("Bore")`, and covers every face of the hole, including a hole the
  exporting system split into half-cylinders. A recognized hole is one cylinder;
  faces of different cylinders in one region are refused
  (`firmament-v2-imported-hole-not-one-cylinder`).
- `HoleDiameter` and geometric controls accept a list of recognized holes,
  `target: [Hole1, Hole2, Hole3, Hole4]`: one requirement, exported with quantity 4
  and shown as `4X`. The holes must have one diameter
  (`firmament-v2-imported-holes-differ`). The list is what the record says; no
  pattern is inferred from it.

```firmament
Recognize part {
    Region Bore { kind: HoleShaft stepFaceEntities: ["#76", "#177"] }
}
```
- A size whose band does not contain the imported geometry's measured value
  compiles with the warning `pmi-nominal-outside-geometry`, naming the measured
  value. The requirement is exported as written.

Unique retained datum labels are available directly in an authored control's
`datumRefs`; they bind to the imported AP242 datum entities rather than creating
duplicate datums. See the complete
[`nist-ctc01-passthrough.firmament`](../../../fixtures/Canonical/PMI/nist-ctc01-passthrough.firmament)
witness. Existing source-integrity receipts (`sha256`, `textSha256`, or a project
lock) still apply to foreign STEP inputs.

Length-valued measures are normalized using their own declared units. An inch
tolerance of `0.001` exports as `0.0254` mm. This conversion does **not** use the
0.1 mm authoring placement grid or round away small tolerances. Angular units
are separate. Firmament authoring itself continues to require metric lengths.

Source graphical PMI is distinct from semantic PMI. Foreign glyphs are excluded
with an explicit diagnostic because they may contain inch text; metric labels
are regenerated from semantic records where the presentation path supports the
target and all of its parameters. Advanced controls whose labels cannot be
projected completely reject annotated 3D export rather than omit parameters.
This does not preserve a vendor's camera, font or label arrangement.
Graphical-only annotations reject default passthrough. Missing dependencies,
unresolved units, unsupported compound measures, unmapped body topology and changed bodies reject. Derived
units retain their dimensional exponents while their length components and values normalize to mm. Non-metric
auxiliary PMI pcurves also reject when their parameter domains cannot be proved
safe to normalize. Geometry-only editing of annotated imports still requires
explicit `geometryOnly: true`; source-PMI migration through topology edits is
not admitted.
Unchanged annotation/re-export uses strict output admission: valid topology and
bindings, closed imported shells, canonical geometry and 3D boundary consistency.
A source hash proves identity; it does not require complete UV reconstruction.
Unqualified optional UV traces are omitted with a diagnostic, while the emitted
STEP retains its 3D edge curves, vertices and face associations. Source recovery
status remains inspectable and is not promoted to fully qualified.
Geometry edits which depend on reconstructed trims still require those trims to
be qualified. A physical boundary contradiction or unsupported normalization
rejects export, including when `geometryOnly: true` is requested.

`geometryOnly: true` explicitly excludes source PMI and reports that exclusion;
it never modifies the original STEP file. Imported assembly-wide PMI and
automatic promotion of part annotations into assembly requirements remain
outside this single-part transfer path.

## Project a compile-time tolerance

Declare a requirement once, use its nominal value for construction, and select
it explicitly with `dimension:` in PMI. The complete witness is
[`typed-tolerance.firmament`](../../../fixtures/Canonical/PMI/typed-tolerance.firmament):

```firmament
Model TypedHole {
  units: mm
  Static drillDiameter: Length = 8mm tol +0.10mm -0.05mm
  Box Body { size: [40mm, 30mm, 8mm] }
  Modify Body {
    Hole<Shaft> Mount {
      on: +Z
      center: Point2(0mm, 0mm)
      diameter: drillDiameter
      end: ThroughAll
    }
  }
  Pmi {
    HoleDiameter MountSize { target: Mount dimension: drillDiameter }
  }
}
```

The exported diameter is nominally 8 mm with upper allowance +0.10 mm and lower
allowance −0.05 mm. `tol 0.1mm` supplies equal allowances on both sides. The
typed value and allowance survive Static aliases, Record members, Template
specialization and Feature parameter/local bindings; normal geometry uses only
the nominal. Unprojected values generate no PMI. `dimension:` requires a
toleranced Length; Angle values can be checked at compile time but are not a
HoleDiameter target and do not grant angular PMI export. The scalar admission
scope and arithmetic policy are in [syntax](../reference/language/requirements.md#tolerances).
The complete [Record/Template/Feature witness](../../../fixtures/Canonical/PMI/record-template-feature-tolerance.firmament)
projects the original Record requirement after using it in a named Hole recipe.
Validation JSON includes the authored projection source and structured
`dimensionOrigins`; those receipts describe binding provenance, not additional
PMI annotations. AP242 continues to carry the admitted nominal, allowances and
geometric association through the existing semantic exporter.

HoleDiameter projection exports a size requirement attached to the hole wall.
A Datum may be exported separately. A `datum:`/`DatumRefs:` relationship on a
size record rejects normal compilation for every spelling, including legacy
literal records: native size export cannot preserve that association.
Parse-only analysis can bind it, but identifies the record as export-deferred
at `ParsedNotBuilt`. Use an explicit geometric control for a datum-referenced
geometric requirement. The shipped size examples request independent datums and
size dimensions; they no longer request a relationship that was never emitted.

This is semantic product-definition PMI, recovered by `aetheris analyze`;
graphical dimension-label placement is separate. An annotated GLB draws it in 3D:
callouts sit on their features, feature control frames and datum symbols follow
ASME Y14.5 shapes, basic dimensions locate position-toleranced holes, and each
feature's leaders share a colour. Explicit `value:` and
`tolerance: PlusMinus(upper, lower)` remain supported compatibility forms.
Existing `Require`-based `from:` / `as:` projections continue to mean a checked
semantic constraint, rather than an implicit export of all Static values.

## Sheet-metal requirements

The authored sheet-metal frontend also admits `dimension:` references to checked
toleranced Length values from Static/Record data. It preserves nominal geometry,
allowances and binding origins. Its `Dimension` kinds are Thickness, Linear and
Diameter; targets are bound sheet regions, bends, parts and cuts, including
Pattern members. `DatumFeature` and `Position` retain the sheet exporter's datum
relationships and geometric associations. Native Model explicit GD&T uses the
controls described above. See [sheet-metal layout and release syntax](sheet-metal.md#layout-requirements-and-release-data)
and the [complete typed example](../../../fixtures/Regression/SheetMetalSemantics/modern-layout-release.firmament).

Use one owning Pmi block. Unknown kinds/fields, missing targets, duplicate blocks,
negative allowance magnitudes and nonpositive quantities reject. Manufacturing
process metadata is optional and does not implicitly create PMI. A successful
export can be inspected with `aetheris analyze file.step --json`.

## Targets and inspection

For a simple `Hole<Shaft>` constructed in a Box, `face(H.Wall)` names the stable wall output of Hole `H`. It is a semantic role selector, independent of the current BRep face number. `HoleDiameter` accepts it as a target:

```firmament
Model WallReference {
    Units: mm
    Box Body { Size: [40mm, 30mm, 8mm] }
    Modify Body { Hole<Shaft> H { On: +Z Center: Point2(0mm, 0mm) Diameter: 8mm End: ThroughAll } }
    Pmi { HoleDiameter WallDiameter { Target: face(H.Wall) Value: 8mm } }
}
```

The complete file is [`hole-wall-selector.firmament`](../../../fixtures/Canonical/PMI/hole-wall-selector.firmament). The compiler binds `H` as a Hole symbol, then resolves `Wall` through the construction-owned Hole-wall correspondence. Renaming `H` changes the selector spelling; diameter and position edits preserve it. Deleting `H` gives a Hole-specific unresolved-symbol diagnostic. Box `face(+Z)`, imported `face(#id)`, and existing PMI targets retain their meanings. `face(H.Wall)` is currently qualified for shaft walls in direct Box/Hole construction; it does not name blind bottoms, entry/exit rims, counterbore sections, patterned instances, or imported Hole-like faces. A planar `Datum` cannot consume a cylindrical Hole wall.

The complete qualified example is [`multiple-hole-dimensions-with-chamfer.firmament`](../../../fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament):

```powershell
aetheris validate fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament --json
aetheris build fixtures/Canonical/PMI/multiple-hole-dimensions-with-chamfer.firmament --output artifacts/pmi.step --json
aetheris analyze artifacts/pmi.step --json
```

The build's `pmiExportEvidence` and the analyzer's `semanticPmi` are independent public evidence surfaces. Supported validate records and inspected AP242 records must agree at the semantic-record level; one record may lower to several STEP entities.

Pattern-generated geometry may coexist with PMI, but the native lane does not yet publish a stable selector for individual generated pattern instances or quantity/repeated-feature PMI authoring.

## Assembly release records and design notes

PMI is owned by the document that declares it. A Part's `Pmi` describes that
Part; assembling it does not copy, promote or reinterpret those requirements as
assembly requirements. An assembly exported without its own `Pmi` or `Provenance` declaration has no
assembly-owned PMI, even when its component sources contain PMI. This is
intentional. Declare assembly requirements explicitly in the root `.firmasm`
file, using the admitted assembly PMI forms below. The standalone Part export
retains its own supported PMI. Mates determine placement; they do not infer
manufacturing requirements or propagate tolerances.

The assembly lane supports root-owned product annotations and a selected typed
release Record. These lower through the existing semantic PMI note emitter to
AP242 `SHAPE_ASPECT` and `PROPERTY_DEFINITION` entities, and are recovered by
`aetheris analyze assembly guitar.step --json` under `semanticPmi`. They are semantic notes, not rendered
dimension labels or newly qualified manufacturing requirements.

```firmament
Record ReleaseInfo {
    Author: String
    Date: Date
    Version: Version
    Description: String
    AuthoringTolerance: Length
}
Static Release: ReleaseInfo {
    Author: "GPT 6.1 Sol Codex"
    Date: 2026-10-01
    Version: 0.1.0
    Description: "Carved-top guitar presentation witness"
    AuthoringTolerance: 0.1mm
}
Assembly GuitarX0 {
    Provenance: Release;
    Pmi {
        Note DesignIntent {
            Target: GuitarX0;
            Text: "Presentation demonstrator; not released for manufacture.";
        }
    }
    // Ordinary XML-like occurrence tree and placement declarations follow.
}
```

`Provenance` selects one ordinary `Static` Record. The common Record binder checks
its fields, including real calendar dates, three-component semantic versions, and
dimensioned lengths. `Author: String`, `Date: Date`, `Version: Version`, and
`Description: String` are required. Optional fields are flat `String`, `Date`,
`Version`, `Length`, `Angle`, `Int`, `Float`, or `Bool` data; `Organization: String`
sets the STEP organization when present. Field values are exported as annotations
named `Release.Field`, in stable field-name order. The selected date supplies a
deterministic midnight STEP header timestamp; it is the authored release date,
not the time of export. There is no implicit wall-clock mutation.

`Pmi` and `Provenance` belong directly to the root Assembly. `Note` takes
`Target` then `Text`, with explicit semicolon terminators. A target must resolve
to a complete occurrence path, including the root, such as `GuitarX0.Bridge`;
unknown targets, duplicate note names, malformed text, and unsupported PMI kinds
stop compilation. Text supports JSON string escapes and preserves apostrophes,
braces, semicolons, and literal `//` without interpreting them as geometry or
comments. Control characters are rejected.

Notes are associated with the target's AP242 product-definition shape. For a
shared definition the explicit occurrence path remains in the semantic target;
this does not invent an occurrence-specific BRep face association. Face-targeted
assembly tolerances, assembly GD&T, nested reusable-definition annotation
authoring, and graphical PMI presentation remain outside this bounded lane.
Existing Model PMI contracts above retain their separate selector rules.
Assembly package reconstruction does not yet reauthor these annotations; inspect
the original STEP directly rather than assuming package import/re-export
preserves release metadata.

The assembly IR preserves the release data and note source spans. USD also
carries them as root `aetheris:provenance:*` and `aetheris:pmi:*` custom attributes.
Editing metadata refreshes these outputs while reusing unchanged geometry.
The complete guitar example is
[`guitar.firmasm`](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm).
