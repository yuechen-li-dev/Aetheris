# Semantic PMI and AP242

Firmament concepts and explicit PMI lower into the semantic engineering model, STEP AP242 product-definition entities, and Cadmata's semantic presentation. PMI records are measurable requirements; annotations are engineer-authored notes. Camera or label orientation is presentation state, not the requirement.

Native Model export supports `Datum` plane records and `HoleDiameter` records, including typed dimensional tolerances. The diameter record targets a named shaft hole; on a counterbore it means the shaft `diameter`, not `counterboreDiameter`. Other parsed PMI kinds may be reported as deferred and cannot be silently omitted by a successful build.

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
scope and arithmetic policy are in [syntax](syntax.md#typed-tolerances-and-pmi).
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
at `ParsedNotBuilt`. Datum-referenced geometric controls need a separately
qualified PMI capability. The shipped examples request independent datums and
size dimensions; they no longer request a relationship that was never emitted.

This is semantic product-definition PMI, recovered by `aetheris analyze`;
graphical dimension-label placement is separate. Explicit `value:` and
`tolerance: PlusMinus(upper, lower)` remain supported compatibility forms.
Existing `Require`-based `from:` / `as:` projections continue to mean a checked
semantic constraint, rather than an implicit export of all Static values.

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
