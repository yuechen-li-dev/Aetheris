# Programmable Profile loops — design proposal

Status: the bounded closed-seed/Apply/straight-member Replace subset is now
implemented; see [the current authoring guide](profile-boundary-edits.md).
The examples below record the original design, including deferred syntax.
Audited on 2026-09-30. This proposal changes authoring, not geometry authority.

## Recommendation

Make a Profile an immutable closed region that can be derived from a closed
Concept boundary or another Profile by applying named boundary edits.

The author selects an existing directed span and replaces it. The compiler
preserves everything outside that span and inherits its endpoints. Tabs, recesses,
corner treatments and smooth replacements are different edit families within
this model. A closed seed removes endpoint bookkeeping; validation still has to
establish that the resulting region is admissible.

Use the existing Sheet Metal semantic edge/corner machinery for engineering edits,
and the existing Profile arrangement routines for bounded curve splitting and
intersection checks. Keep the resolved Profile as the consumer-facing value for
Extrude, SectionChain and Concept boundary derivation.

## What the audit found

| Existing owner | Already does | Gap relevant to this proposal |
| --- | --- | --- |
| `ClosedBoundary2Authoring` | Rect2, Circle2, Ellipse2, rounded rectangles and polygons; named members; lowering into ordinary guides/Profile routes | No general named boundary-edit program |
| `ProfileAuthoringParser` | Ordered guide pipelines, explicit loops, bounded guide sub-spans, Profile resolution | Authors still connect low-level curves; no closed-seed-plus-edits contract |
| `SemanticEdgeProfileResolver` | Anchored tabs/notches/ProfileDelta; retains untouched carrier spans; rejects out-of-bounds and overlapping edits | Carrier is a straight directed edge; descendants are line/circular-arc geometry |
| `SemanticCornerProfileResolver` | Named corner programs, edge consumption and conflict handling | Not a general editable closed-loop frontend |
| `Profile.Modifications` | Typed reusable Tab and Recess templates; immutable Record specifications; attachment to Sheet Metal and ordinary rectangular Profile edges | Immutable closed-seed derivation and explicit Apply were missing |
| `SheetMetalAuthoredLowering` | Combines directed edges and corner descendants into an exact planar contour; preserves formed/flat correspondence | Composition is embedded in flange/domain lowering |
| `ProfileArrangementBuilder` | Bounded intersection, trimming and splitting for lines/arcs/cubics; region composition for existing material features | General Profile validation does not use its full intersection capability; bounded trim does not admit ellipses |
| `ResolvedProfile2DValidator` | Closure, endpoint matching, winding, some degeneracy checks, nonadjacent line-line crossings | Does not establish general curved-loop simplicity or complete hole relationships |
| Concept `Curve2 { From: Profile; ... }` | Explicit boundary derivation and placement without BRep edge selection | Copies/transforms boundaries; does not edit their topology |

The sheet-metal pilot really does implement “start with a region, modify named
edges.” It is not an arbitrary polygon boolean API. `BlankCompositionPlan` also
keeps material additions, corner relief and through-cuts under explicit domain
ownership. Share the boundary machinery; retain that manufacturing authority.

Audited implementation files:

- [Closed boundary authoring](../../../Aetheris.Kernel.Firmament/FirmamentV2/ClosedBoundary2Authoring.cs)
- [Profile authoring](../../../Aetheris.Kernel.Firmament/FirmamentV2/ProfileAuthoringParser.cs)
- [Semantic edge programs](../../../Aetheris.Kernel.Firmament/FirmamentV2/SemanticEdgeProfileIr.cs)
- [Semantic corner programs](../../../Aetheris.Kernel.Firmament/FirmamentV2/SemanticCornerProfileIr.cs)
- [Reusable modifications](../../../Aetheris.Kernel.Firmament/Resources/ProfileModifications.firmament)
- [Sheet Metal lowering](../../../Aetheris.SheetMetal/SheetMetalAuthoredLowering.cs)
- [Blank composition](../../../Aetheris.SheetMetal/BlankCompositionPlan.cs)
- [Shared curve/arrangement operations](../../../Aetheris.Kernel.Firmament/Materializer/ProfileArrangement2D.cs)
- [Resolved Profile and validation](../../../Aetheris.Kernel.Firmament/Materializer/ResolvedProfile2D.cs)

## Proposed authoring

### Closed seed, named edits, ordinary Profile result

The panel example's From/Apply/straight-member Replace syntax is implemented.
Explicit Curve2 replacement payloads, equation curves, periodic intervals and
Profile-valued Features below remain design sketches, not current compiler syntax.

```firmament
Use Profile.Modifications;

Concept Struct Layout {
    Rect2 Blank { Center: [0mm,0mm]; Size: [120mm,80mm] }
}

Static ServiceTab: ProfileTabSpec {
    Center: 60mm; Width: 24mm; Extension: 8mm
}

ProfileDelta ServiceTabEdit = Tab<P: ServiceTab, Owner: Layout.Blank.Top>

Profile Panel Using Layout {
    From: Blank
    Apply ServiceTabEdit

    Replace Grip {
        On: Blank.Right
        Range: [20mm,60mm]
        Through: [[48mm,-10mm], [48mm,10mm]]
        Join: Tangent
    }
}
```

`From` initializes the region. It accepts a closed boundary or a Profile value.
It excludes an additional explicit `Loop Outer` in the same declaration.
`Apply` uses the existing ProfileDelta template/specification language; it does
not redefine what Tab/Recess mean.

`Replace` is a named pure edit. `On` selects a directed base member; `Range` is
distance from that member's start and is initially admitted only on straight
members. Omitting Range selects the whole member. Grip inherits both endpoints;
the through-points are in the Profile's local Concept frame. In this example,
Blank.Right goes from bottom-right to top-right, so the range endpoints are
`[60mm,-20mm]` and `[60mm,20mm]`.

`Join: Tangent` derives endpoint tangent directions from the retained source
boundary. Default `Join: Position` requires only shared endpoints. Explicit
`StartTangent` and `EndTangent` direction vectors override the corresponding
source direction, with finite nonzero vectors required. At a source corner,
Tangent is rejected unless the relevant one-sided tangent is unambiguous or
explicitly supplied. These are construction rules followed by verification,
not constraints sent to a solver.

Through is shorthand for a finite open cubic interpolating chain. It must have a
documented, fixed interpolation rule, not a best-fit optimizer: chord-length
parameterization, interior derivatives from the adjacent secants weighted by
their parameter intervals, and endpoint derivatives from the selected directions
with adjacent chord-speed magnitudes. Publish the exact formula and regression
fixtures before qualification. Reject repeated points, zero tangents and cusps.
The resulting cubic control points are compiler output, inspectable but usually
not authored.

A replacement must choose exactly one construction payload: Through, an explicit
bounded open `Curve2` chain, or a named line/arc construction. Explicit cubics
remain an escape hatch when the interpolation rule does not express the design.
This does not promise that interpolation cannot overshoot.

### Identity, ordering and functional composition

One derivation block is an atomic edit set against its immutable seed. Named
edits on disjoint intervals are composed in boundary traversal order. Source order
is retained for diagnostics; it does not make overlapping edits legal.

To edit the result of a previous edit, derive another Profile explicitly:

```firmament
Profile FinishedPanel {
    From: Panel
    Replace Crown {
        On: Panel.ServiceTabEdit.Crown
        Through: [[0mm,52mm]]
        Join: Position
    }
}
```

The result remains a Profile value. A future `Feature ... -> Profile` should
package exactly this pure recipe using existing typed Feature call mechanics.
Profile returns are an extension to the currently qualified Feature subset,
not something this syntax assumes is already supported. No mutable sketch or
separate execution language is needed.

Names address engineering intent: `Panel.Grip`, `Panel.ServiceTabEdit.Crown`. A member can
have several exact descendants. Untouched spans retain their source identity
and interval; split fragment identities derive from their bounding named edits,
not enumeration order. Consumed seed members remain provenance references, not
aliases silently redirected to a different curve. Inspection must distinguish
source members from selectable result members.

### Circles and ovals

A full circle/ellipse has no natural start/end and must not silently acquire a
nearest-edge selector. Rectangles already provide named edges; periodic curves
need explicit named landmarks and a directed interval between them.

Proposed later form:

```firmament
Concept Struct Layout {
    Ellipse2 Blank { Center: [0mm,0mm]; AxisLengths: [340mm,440mm] }
    Point2 Shoulder { On: Blank.Boundary; Parameter: 0.12 }
    Point2 Heel { On: Blank.Boundary; Parameter: 0.30 }
}
Profile Body Using Layout {
    From: Blank
    Replace Cutaway {
        On: Blank.Boundary
        From: Shoulder; To: Heel; Direction: CounterClockwise
        Through: [[55mm,125mm]]
        Join: Tangent
    }
}
```

Parameter is normalized canonical curve parameter, not arc-length fraction;
zero/seam and axis conventions must be specified independently of rendering.
Direction disambiguates the two periodic paths. Full periodic replacement needs
a separate explicit whole-member form, not equal endpoints interpreted magically.
Circle arcs can use existing analytic geometry. Ellipse interval trimming and
intersection qualification are additional work: do not pretend the current
bounded line/arc/cubic helper already supplies them. Rectangles/straight-member
edits and the guitar polygon scaffold can ship before ellipse edits.

## Guitar dogfood

The current [body outline](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/body-outline.firmament)
has 54 point declarations, 18 cubic declarations and 18 Segment entries. It
already describes one shared boundary, but its source exposes compilation detail
instead of lower bout, waist, horn and cutaway intent.

A rectangle is a useful seed for brackets. For this guitar, use a closed polygon
through a handful of named landmarks, then replace its chords. The polygon is
Concept scaffolding and is never an intermediate solid. Reuse the existing named
Set<Point2> and its adjacent-name edge identities, rather than inventing another
polygon literal or exposing generated edge indices.

```firmament
Static Landmarks: Set<Point2> {
    Tail => Point2(0mm,-220mm)
    TrebleWaist => Point2(105mm,28mm)
    Horn => Point2(113mm,160mm)
    NeckSeat => Point2(28mm,204mm)
    BassShoulder => Point2(-111mm,174mm)
    BassWaist => Point2(-95mm,40mm)
}
Concept Struct BodyLayout {
    Polygon2<Explicit> Scaffold { Vertices: Landmarks }
}

Profile BodyOutline Using BodyLayout {
    From: Scaffold
    Replace LowerTreble {
        On: Scaffold.Tail_TrebleWaist
        Through: [[115mm,-195mm], [168mm,-115mm], [148mm,-35mm]]
        StartTangent: [1,0]; EndTangent: [-15,65]
    }
    Replace HornRise {
        On: Scaffold.TrebleWaist_Horn
        Through: [[118mm,95mm], [124mm,140mm]]
        StartTangent: [-15,65]; EndTangent: [-16.5,1]
    }
    Replace Cutaway {
        On: Scaffold.Horn_NeckSeat
        Through: [[91mm,142mm], [48mm,137mm]]
        StartTangent: [-16.5,1]; EndTangent: [-45,34]
    }
    Replace Shoulder {
        On: Scaffold.NeckSeat_BassShoulder
        Through: [[-42mm,205mm]]
        StartTangent: [-45,34]; EndTangent: [-38.5,-45]
    }
    Replace UpperBass {
        On: Scaffold.BassShoulder_BassWaist
        Through: [[-119mm,115mm]]
        StartTangent: [-38.5,-45]; EndTangent: [-9,-73.5]
    }
    Replace LowerBass {
        On: Scaffold.BassWaist_Tail
        Through: [[-137mm,-32mm], [-168mm,-115mm], [-115mm,-195mm]]
        StartTangent: [-9,-73.5]; EndTangent: [1,0]
    }
}
```

This is a proposed readable redesign, not a verified curve-equivalent migration.
Coordinates reuse existing landmarks, and shared endpoint directions express G1
intent; generated derivative magnitudes can change the silhouette. During
implementation, compare bounds, area, silhouette and neck-side/back views before
replacing the witness. If exact preservation is needed, allow inline Hermite
knots with Length-valued derivatives; those map directly to the current cubic
control points without guessing a fit.

Keep `BodyOutline` as the same public Profile export. CarvedMaple, MahoganyBack
and IvoryBinding continue using their existing Concept Curve2 derivations.
SectionChain consumes stable member/descendant correspondence. Knot insertion
may change descendant count; surface correspondence must reject mismatches
between independently edited sections rather than pair spans by position.

## Compiler integration and validity contract

1. Bind closed seed and edit references through the existing semantic namespace.
   Resolve values before erasing Concept scaffolding; reject cyclic Profile
   derivations. No references to incidental BRep edges.
2. Introduce a small typed closed-boundary edit plan: seed reference, frame, named
   member/interval, edit family/payload, stable identity and source range.
   Generalize the straight-edge resolver only where its contract remains true.
   Curved replacement is a new sibling payload; do not coerce every curve into
   a straight carrier or a distance/offset ProfileDelta.
3. Resolve Apply through existing ProfileDelta and CornerProfile machinery. Share
   boundary composition below ordinary Profile and Sheet Metal authoring.
   Sheet Metal still owns bend boundaries, attachability, thickness and flat
   correspondence; ordinary Profile edits cannot grant those capabilities.
4. Split only selected spans, inherit endpoints exactly and retain untouched
   geometry. Use existing TrimBounded/SplitBounded for admitted families. Build
   one `ResolvedProfile2D` with source/edit/descendant provenance.
5. Validate the complete resulting region before materialization. Reject span
   overlap, stale targets, duplicate names, zero-length pieces, endpoint gaps,
   reversed winding, crossings, positive coincident overlap and extra contacts.
   Adjacent curves may share their declared endpoint only; do not skip all
   adjacent-pair intersection checks. Check cubic self-intersection within an
   individual span as well as intersections between spans.
6. Reuse `ProfileArrangementBuilder.IntersectBounded` for qualified pair checks.
   Audit same-cubic loops, tangencies and numerical exhaustion explicitly. Its
   cubic candidate search currently has a depth limit and numerical refinement;
   exact cubic representation is not an exact global simplicity proof. Report
   tolerance/method, and reject unsupported/indeterminate cases rather than
   declaring them proven. Do not substitute a sampled display polygon as truth.
7. Retain existing material consumers and their validity checks. One outer loop
   is required. An interior cut is a named inner-loop operation, not a boundary
   splice; containment, winding and inter-loop intersections need qualification
   before that operation ships. Splitting a region into multiple islands is
   rejected in this lane.
8. Use existing compilation-session cache keys with expanded included source.
   An edited exported Profile invalidates dependent parts; unrelated definitions
   remain reusable. This needs no new incremental dependency engine.

Suggested diagnostics: `profile-edit-overlap`, `profile-edit-target-consumed`,
`profile-edit-range-invalid`, `profile-edit-join-ambiguous`,
`profile-edit-self-intersection`, `profile-edit-validation-indeterminate`, and
`profile-edit-curve-not-qualified`. Include the two conflicting member identities
and source spans, not just “invalid profile.”

Inspection should show seed, operations, selected source intervals, preserved
members, generated curves, join residuals, validation method and downstream
dependency. This makes the compact source explainable without exposing control
points in every authored document.

## Bounded delivery order

1. **Closed seed + engineering edits:** From, Apply of existing straight-carrier
   ProfileDelta, named members and ordinary Profile result. Prove identical
   Sheet Metal formed/flat output while ordinary extrusion gains the same recipe.
2. **Smooth replacement + guitar:** whole named straight-member replacements,
   straight-member ranges, compact through-points/explicit cubic escape hatch,
   explicit tangent directions,
   complete admitted curved validation, inspection and SectionChain correspondence.
   Dogfood the shared guitar outline here; compare hero and side views.
3. **Curved/periodic carriers:** bounded arc/cubic intervals, circle landmarks
   and directed periodic spans; ellipse intervals only after their exact trim and
   validation path is qualified. Add interior-loop edits separately.

Acceptance includes valid and rejected corpus fixtures, deterministic identities,
source provenance, unchanged input Profile values, real CLI inspection/STEP
roundtrip, and full Release test gates for implementation. No generic solver,
runtime editing engine, public arbitrary booleans or fallback mesh path.

## Audit evidence

Current CLI checks passed:

```powershell
dotnet run --project Aetheris.CLI -c Release --no-build -- sheetmetal paths fixtures/Canonical/SheetMetal/profile-delta-tab-family.firmament --json
dotnet run --project Aetheris.CLI -c Release --no-build -- section-chain inspect fixtures/Canonical/AssemblyInterfaces/GuitarX0/CarvedMaple.firmament --json
```

The Sheet Metal fixture exposes 41 semantic paths, including 12 ServiceTab paths
across formed and flat views. CarvedMaple inspection succeeds with no diagnostics
through its SectionChain path. Generic `inspect` does not dispatch this SectionChain
document correctly; it reports `firmament-v2-phase3-edge-finish-syntax-invalid`.
Use the schema-specific command above for this audit. That CLI dispatch friction
is separate from Profile editing and was not patched here.

Original audit JSON is under ignored `artifacts/local/profile-loop-audit/`.
Implementation evidence is separate under `artifacts/local/profile-loop-implementation/`;
the current guide distinguishes the admitted subset from this broader proposal.
