# Firmament authoring and composition: proposals from GUITAR-SURFACING-X0

Status: **bounded owl implemented on 2026-10-01; advanced contracts remain explicit follow-ons.**
The checklist below distinguishes shipped bounded capabilities from remaining
contracts. The original numbered proposals remain for traceability; their sketches
are superseded wherever a linked implementation guide specifies different syntax.

The guitar demonstrated that the existing exact geometry and display paths can produce a convincing designed object. It also required a Python source generator to assemble 107 occurrences, calculate transforms, expand repeated hardware, generate spline controls, calculate fret spacing, and construct string routes. That generator emits ordinary Firmament; it does not replace the kernel. Nevertheless, too much design intent lives outside the language.

The objective is to move those responsibilities into typed, finite Firmament authoring and existing compiler owners. This is a proposal for several bounded milestones, not a request for a general scripting language, universal constraint solver, or new surfacing kernel.

Code blocks in the original numbered proposals are historical **design sketches**,
not compile-ready fixtures. Use the linked implementation guides and current
guitar modules for supported syntax and published port names.

## Session checkpoint: original friction checklist

**Addressed** means the first bounded slice works through the real compiler and
guitar witness. It does not mean every extension described in the original
proposal is implemented. **Partial** means useful functionality shipped but a
named part of the original contract remains. **Open** means the proposed
source-level capability has not shipped.

| Original item | Status | What works now | Remaining friction / boundary |
| --- | --- | --- | --- |
| 1. Assembly Patterns | **Addressed, bounded** | Keyed finite occurrence families, shared definitions, stable identities and expanded provenance. Six guitar patterns cover saddle, tuner, control, fret and inlay families. | No nested assembly patterns, arbitrary relationship-pattern bodies or patterned Expose collections. Pickup poles correctly use feature repetition inside one part instead. |
| 2. Readable frames and placement | **Addressed, bounded** | FrameTransform, local translation/rotation, published-frame alignment and single placement authority. Headstock/tuners and point-route forming no longer require Python transform math; fixed placed endpoints use public-frame Bind. | General frame-valued expressions and dynamic endpoint scopes remain follow-ons. |
| 3. Definition-owned semantic ports | **Addressed, bounded** | Ordinary part and SectionChain publications; nested forwarding/privacy; body deck, neck terminals, component mounts and board MarkerOrigin. | No general stable face/curve selector or arbitrary surface port publication. Not every geometric face is source-addressable. |
| 4. Interface depth | **Partial; core seating shipped** | Fixed Gap/Clocking, role/member checks, Concept-plane-directed seating with material evidence, Concept-axis Fixed stacks and complete-component Revolute placement. | Rich reusable dimension/fit predicates and arbitrary cross-scope datum targeting remain limited. Generalized user-defined Lower is superseded, not unfinished work to implement. |
| 5. Profile/section families | **Addressed for the guitar** | Generic Profile outputs with local-guide hygiene, keyed Section Patterns, inline Concept frames, periodic boundary derivatives. Body and flat neck recipes are now source-owned. | Higher-order Profile arguments and general InterpolatingLoop2 remain outside this slice. |
| 6. Straight/fair surfacing intent | **Partial: witness fixed, language contract open** | Approved flat-ish neck, isolated heel stations, existing G1 SectionChain path and side-profile regression checks. | No explicit per-transition Law/endpoint tangent authoring or declarative StraightBack envelope contract. The present fix is an authored station recipe, not a new global straightness proof. |
| 7. Non-planar attachment | **Partial; datum-first model clarified** | Concept planes derive from published physical construction frames with offset/clocking; consumers seat on that scaffold. The guitar deck no longer needs an independent bridge-height guess. | Tangent-frame evaluation on analytic/SectionChain surfaces, explicit location/trim/seam rules and curved-base footprint support are not implemented by frame derivation alone. |
| 8. Engineering expressions and finite sites | **Addressed, bounded** | Pure scalar Function arithmetic and Pow, equation-driven 22-fret series, keyed nut crossings, explicit linear inlay stations, ordinary Linear/Mirrored component sites. | Angle trigonometry, typed vector/frame operations, geometry-valued arguments/results and equation-defined curves remain bounded follow-ons. Functions already run during compilation; no Comptime keyword is needed. |
| 9. Point-driven WireForm | **Addressed, bounded** | Six strings bind final public tailpiece/nut/tuner frames; typed Point3 specialization and placement-aware cache inputs. First finite Concept Line3 Follow guide lowers to exact WireForm. | Fixed assemblies only; tangent exits. Exact Via, multiple corners, circular/spline guides, route outputs and obstacle search remain deferred. |
| 10. Semantic appearance binding | **Addressed, bounded** | Physical Material owns its default Appearance; occurrence with overrides the finish. USD and Cycles consume authored identities. Look-only edits reuse exact geometry. | Physical aliases do not certify engineering properties; procedural sunburst/pearl and pickup shading remain downstream. |
| 11. Unified compilation and diagnostics | **Addressed front door; deeper tooling partial** | Root-aware build/inspect delegates to existing owners; repeat inspection exposes cache reasons/timings. Section keys, bound endpoint provenance, new schema fields and snapshot cross-file navigation are covered. | Aggregate project diagnostic locations, full generated-span mapping, every draft grammar and client integration remain follow-ons. |

Implementation references: [assembly foundation](assembly-authoring-foundation.md),
[published-frame and boundary derivation](concept-derivation.md),
[datum seating](datum-seating.md), [coaxial mating](concept-axis-mating-proposal.md),
[profile boundary edits](profile-boundary-edits.md),
[Functions and point collections](concept-points-and-functions.md), and
[point wire routes](wire-point-routes.md).

### Additional wins beyond the original eleven items

- [x] **Component-oriented containment:** guitar.firmasm composes multiple modules;
  knobs, selectors and tuners are complete reusable components, rather than
  assemblies collecting unrelated categories of features.
- [x] **Functional part construction:** two pickups instantiate one feature-built
  Humbucker definition. Housing, coils and repeated poles are part features.
  [Functional pickup construction](functional-pickup.md) records the qualified subset.
- [x] **Basic incremental compilation:** a retained
  [FirmamentCompilationSession](incremental-compilation.md) reuses successful exact
  definition materializations with source/dependency-aware invalidation. Placement,
  validation, tessellation and export still run. Ordinary declaration edits may
  invalidate many parts conservatively; this is not a minimal dependency graph or
  a cross-process CLI cache.
- [x] **Human-authored station convention:** pearl distances are written at 0.1mm
  resolution; the largest migration shift is 0.044mm, inside the stated 0.1mm
  physical tolerance. Compiler intermediates and kernel tolerances stay precise.

### Bounded owl delivered

- [x] **Source-owned profile/section families:** shared generic body and neck
  recipes, keyed section rows, stable spans, periodic derivatives, approved flat
  shaft retained. Python is now a read-only reproduction harness.
- [x] **Authoring front door:** root-aware build/inspect, retained-session timing
  and cache reasons, section associations and route endpoint provenance; schema
  help and snapshot navigation for the added syntax.
- [x] **Placed endpoints and first guide:** fixed public-frame Bind inputs and one
  finite Concept Line3 Follow interval with exact entry fillet and tangent exit.
- [x] **Semantic appearance:** Material default plus with finish override; authored
  identities replace renderer name matching and leave geometry buffers unchanged.
- [x] **Reassess further surface/Interface work against a real case:** guitar deck
  seating already has physical evidence. No new arbitrary curved-attachment case
  was needed; tangent/trim/seam/footprint and richer dimension predicates remain
  named follow-ons, rather than speculative scope added to this milestone.

[Implemented syntax and limits](source-owned-guitar-authoring.md) supersede the
historical sketches below. Procedural rendering remains downstream; no geometry
recipes are generated in Python. Editor coverage is bounded, not a claim of full
source-map or all-client completion support.

### Owl closeout evidence

Release solution build passed; 95 focused Firmament and 27 CLI tests passed.
Fast Core passed 1,004; the full serial solution gate passed **4,225**, with zero
failures and seven existing skips across 21 projects. Fresh STEP/USD and six
full-resolution Cycles views are in ignored artifacts/local/owl, with a short
report, inspection/provenance, timing, mesh checks and input-hash evidence.
Cached/uncached USD is identical. Cold/warm/cached compile times were 11.105s,
5.108s and 0.887s; all 53 definitions reused in the cached lane. Camera changes
in the compiled Cycles scene preserve product topology and invoke no Firmament
build; no refreshed native Storm orbit or measured Zoo ratio is claimed.

### Prior checkpoint evidence

The current root is [guitar.firmasm](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm).
Prior checkpoint code qualification (before the owl slice): Release solution build, 47 focused tests,
1,004 fast core tests, and 4,192 full serial solution tests passed, with 7 existing
skips. The current witness has 91 visible parts, 53 geometry definitions, 10
reusable assembly definitions and six assembly patterns. USD has 163 occurrence
nodes including its root; STEP has 162 occurrences and 63 definitions.

The prior inlay migration preserves every definition's geometry buffers and all
non-inlay transforms. Cycles verifies nine pearl assignments, two pickup treatments
and unchanged imported product topology. These are current model/export/render
checks, not a new camera-orbit or Zoo timing comparison. Earlier build/performance
numbers in linked milestone guides are explicitly historical snapshots.

Reproducible local evidence is under ignored `artifacts/local/linear-stations/`:
report.md/json, inspection, comparison, STEP/USD, full-gate TRX and the Cycles
preview/scene. This checkpoint also refreshes CLI inspection of the current root.

## Evidence and current baseline

The motivating source is [the guitar generator](../../../scripts/create-guitar-x0.py), its [current composition root](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm), and the [witness report](../demos/guitar-surfacing-x0.md).

Current capabilities should be extended, not accidentally proposed a second time:

- [Feature Patterns, Records, Static data and Templates](templates.md) already provide finite specialization. The missing pattern target is the assembly occurrence and relationship graph.
- [Typed Interfaces and subassemblies](assemblies.md) already support Fixed, Axial, Revolute, Prismatic, Custom and Gear contracts, public `Expose` ports, and shared solved definitions. Fixed/Revolute/Prismatic datum-frame contracts already derive full 3D placement. Lack of arbitrary orientation is therefore not the fundamental gap.
- Existing reusable Interfaces have named roles, capability checks, atomic lowering, fits and admitted motion. The needed depth is member-specific requirements, parameterized seating and geometry-derived port publication.
- [SectionChain](section-chains.md) already creates capped exact solids from framed profiles, with G0/G1 transitions and bounded local dependencies. It is not necessary to add another loft implementation to simplify this guitar.
- [WireForm](wire-form.md) already constructs exact straight/bend routes and publishes winding semantics. Point-driven routing would be an authoring layer over that machinery.
- [OpenUSD](openusd.md) already preserves shared geometry and occurrence placement. The full sunburst remains downstream Cycles shading in this witness.

During this audit, `aetheris asm inspect fixtures/Canonical/AssemblyInterfaces/two-link-arm.firmament --json` succeeded through the current CLI. The source audit also checked `AssemblyM0Parser`: explicit occurrence placement currently reads matrices, datum frames read explicit bases, and reusable Interface lowering accepts a bounded atomic relation set. These observations are specific to the checkout reviewed on 2026-09-30.

## Suggested order

| Proposal | Priority | First bounded slice | Depends on |
| --- | --- | --- | --- |
| 1. Assembly Patterns | P0 | Named finite sets of parts and fixed relationships | Existing finite expansion; proposal 2 for readable placement |
| 2. Frame composition and placement | P0 | Rigid local offsets/rotations and named frame alignment | Existing datum-frame and placement IR |
| 3. Definition-owned semantic ports | P0 | Authored frames and SectionChain terminal frames | Existing semantic values and Expose |
| 4. Deeper Interface contracts | P0 | Frame member requirements, seating gap and clocking | 2–3 |
| 5. Profile/section families | **Addressed for the guitar** | Generic Profile outputs with local-guide hygiene, keyed Section Patterns, inline Concept frames, periodic boundary derivatives. Body and flat neck recipes are now source-owned. | Higher-order Profile arguments and general InterpolatingLoop2 remain outside this slice. |
| 6. Explicit straight/fair transition intent | P1 | Endpoint tangent constraints and named transition laws | Existing SectionChain tangent/materializer path |
| 7. Placement on curved supports | P1 | Named analytic support, explicit location and clocking | 2–4; surface evaluator |
| 8. Dimensioned engineering expressions | P1 | Pure scalar arithmetic, small math set and finite sampling | Existing compile-time value evaluator |
| 9. Point-driven WireForm | **Addressed, bounded** | Six strings bind final public tailpiece/nut/tuner frames; typed Point3 specialization and placement-aware cache inputs. First finite Concept Line3 Follow guide lowers to exact WireForm. | Fixed assemblies only; tangent exits. Exact Via, multiple corners, circular/spline guides, route outputs and obstacle search remain deferred. |
| 10. Semantic appearance binding | **Addressed, bounded** | Physical Material owns its default Appearance; occurrence with overrides the finish. USD and Cycles consume authored identities. Look-only edits reuse exact geometry. | Physical aliases do not certify engineering properties; procedural sunburst/pearl and pickup shading remain downstream. |
| 11. Unified compilation and diagnostics | **Addressed front door; deeper tooling partial** | Root-aware build/inspect delegates to existing owners; repeat inspection exposes cache reasons/timings. Section keys, bound endpoint provenance, new schema fields and snapshot cross-file navigation are covered. | Aggregate project diagnostic locations, full generated-span mapping, every draft grammar and client integration remain follow-ons. |

Ship 2–3 first as a small foundation, then 1 and the first part of 4. Do not require curved-support placement or a material language before making ordinary assemblies pleasant to author.

## 1. Assembly Patterns: repeated parts with stable identity

**Friction:** Python unrolls pickup poles, saddles, frets, tuner groups and controls into occurrence tags. Repetition belongs to the product tree, but the language's current feature pattern path does not offer that assembly expansion.

Extend `Pattern ... Over` to assembly tree content. The collection is finite compile-time data, and the body yields ordinary parts/subassemblies and relationships, rather than source text.

```firmament
Static PoleSites: Set<PoleSite> {
    LowE  => PoleSite { X: -25.5mm }
    A     => PoleSite { X: -15.3mm }
    D     => PoleSite { X: -5.1mm }
    G     => PoleSite { X: 5.1mm }
    B     => PoleSite { X: 15.3mm }
    HighE => PoleSite { X: 25.5mm }
}

<Assembly BridgePickup>
    <Part Base = PickupBase></Part>
    Pattern Poles Over PoleSites {
        site => <Part Pole = PolePiece<Radius: 2.2mm>>
            Placement {
                From: Pole.Seat
                To: Base.PoleRail
                OffsetLocal: [site.X, 0mm, 0mm]
            }
        </Part>
    }
</Assembly>
```

The pattern contributes a namespace: `BridgePickup.Poles.LowE.Pole`. Changing collection order does not rename a named item. Anonymous arrays may use indices, but inspection must disclose that insertion can change those identities. Do not use rendered names or template arguments as occurrence keys.

Allow keyed row references such as `Guitar.Strings.LowE.Wire`, and permit a generated body's Interface to name its own generated participants. Cross-pattern joins should require explicit matching keys; never zip unrelated collections implicitly. Missing keys, duplicate keys and duplicate generated declarations fail before geometry construction.

The first slice supports finite parts and fixed placements; movable joints and patterned Expose collections can follow. An expansion budget rejects excessive or recursive expansion. Shared specialization identity remains separate from occurrence identity: six identical poles get one BRep definition, not six rebuilds.

**Owner and acceptance:** finite semantic expansion into the existing Assembly parser/binder/IR, with source maps for pattern and row. Replace the guitar's pole and saddle loops; demonstrate stable named paths after reordering, shared definition counts, STEP/USD export and deterministic inspection. Retain the existing feature Pattern spelling and behavior.

## 2. Frame composition and readable placement

**Friction:** the generator calculates trigonometry, basis vectors and 16-element matrices for an angled headstock, veneer, tuners and strings. A placement should express engineering intent in local coordinates.

```firmament
FrameTransform HeadTilt {
    From: Neck.Nut.Frame
    TranslateLocal: [0mm, 0mm, -7mm]
    RotateLocal: { Axis: X; Angle: -13deg }
}

<Part Headstock = HeadstockPart>
    Placement {
        From: Headstock.Base.Frame
        To: HeadTilt.Frame
    }
</Part>
```

`FrameTransform` produces a datum frame, not geometry. Define its order explicitly: translate in the input frame's axes, then rotate the basis about the translated origin. A chain of named transforms expresses another order; do not infer Euler conventions. `Axis: X` refers to the input local X axis.

Placement maps a definition-local source frame to a target frame expressed in its containing occurrence's coordinates. The derived transform is `T_target * inverse(T_source)`. Occurrence-qualified target references compose the existing parent transforms once; they must not be mistaken for definition-local coordinates or transformed twice.

Offer a bounded `DatumFrame` constructor using `Origin`, `Normal` and `Up` as authoring sugar over the complete orthonormal basis. Reject a zero normal or parallel Up; do not silently choose a roll axis. Imported occurrence matrices remain accepted for interchange and compatibility.

A child gets one placement authority: authored frame alignment, mate-derived placement, or imported/legacy explicit transform. A conflicting second driver is an error, not an undocumented override. Frame placement is for layout; a declared Interface is for a relationship with a contract. Both lower to existing rigid-transform machinery.

**Acceptance:** tilt the headstock and place its veneer using composed frames with no Python trigonometry or raw matrices. Check local/world composition, right-handedness, nested subassemblies and export parity. Camera movement must not reevaluate these expressions or build geometry.

## 3. Definition-owned ports: publish construction meaning once

**Friction:** a surface/neck definition knows its sections and support frames, but the assembly repeats attachment coordinates. Inline occurrence datums are useful, yet repeated authored coordinates drift from the constructed part.

```firmament
SectionChain Neck {
    // Existing framed sections and capped construction.
}

Expose {
    Semantic NutMount {
        DatumFrame Frame = Neck.Section.Nut.Frame
        Dimension Width = NutWidth
    }
    Semantic HeelMount {
        DatumFrame Frame = Neck.Section.Heel.Frame
    }
}
```

Extend definition-level semantic publication to ordinary executable parts, including `SectionChainFile` and `LoftFile`. The aliases above reference compiler-owned construction semantics. A terminal section's frame is not automatically a mechanical seating frame: the author must choose or derive its intended origin, orientation and offset through proposal 2.

Start with explicitly authored frames, named SectionChain sections and primitive construction supports. Later add named surface and curve ports where those owners can publish stable bindings. Keep these references independent of face numbers, triangle indices or nearest-surface guesses. Do not claim every kernel face is source-addressable.

Publication preserves local coordinates, capability/type, construction provenance and declaration identity. An occurrence qualifies the port with its own transform. Subassembly `Expose` continues to enforce privacy and can forward it without copying geometry. Private implementation details must not become public merely because a pattern or mate references them.

**Acceptance:** change the neck's nut station once and observe the dependent headstock placement follow its published frame. Verify shared definitions, nested Expose visibility and diagnostics when the section/port is removed.

## 4. Interface depth: member contracts, seating and clocking

**Friction:** a frame coincidence is useful, but often insufficient as an authoring contract. Veneer thickness, pickup seats and mounting gaps currently become absolute Z coordinates. Coarse role capability checks do not express which named members must exist or what dimensions must agree.

First extend the existing Fixed frame contract with explicit local seating fields:

```firmament
Interface<Fixed> VeneerSeat {
    A: Guitar.Headstock.Front
    B: Guitar.Veneer.Back
    Orientation: OpposedDirection
    Gap: 0mm
    Clocking: 0deg
}
```

Define `Gap` along A's outward local Z, and `Clocking` about that axis. `OpposedDirection` must have a documented right-handed mapping, including its X reference; flipping only Z is invalid. The result is a fixed offset, not a hidden translational/rotational DOF. Absence of these new fields preserves current zero-coincident behavior.

**Review correction: the general Lower-language sketch is superseded.**
The chosen model declares Concept layout geometry first, then realizes published
material ports relative to that scaffold. It does not ask Firmament to reconcile
multiple material-to-material constraints or introduce a user-defined constraint
lowering language.

The implemented [Concept-directed seating](datum-seating.md) form separates a
shared datum contract from per-member realization:

```firmament
Concept Struct Layout {
 Plane Deck { Origin: [0mm,0mm,53mm]; Normal: [0,0,1]; Up: [0,1,0] }
 DatumFrame BridgeSeat { On: Deck; At: [0mm,-35mm]; X: [1,0] }
}
Interface<Fixed> DeckSeat {
 Datum: Layout.Deck
 Members: [Hardware.Bridge.BottomSeat]
}
Mate BridgeOnDeck: DeckSeat {
 Member: Hardware.Bridge.BottomSeat
 At: Layout.BridgeSeat
 Orientation: SameDirection
}
```

The same design extends to [Concept axes](concept-axis-mating-proposal.md), with
station and clocking. Shared scaffolding is erased; existing placement and
kinematic machinery own the resulting physical realization. Published-frame
[derivation](concept-derivation.md) supplies explicit offsets/clocking when the
scaffold should follow a physical construction reference.

Role/member capability checks, membership, privacy and placement authority are
implemented. Rich reusable dimension predicates remain separate work: a width
comparison may eventually read published dimensions, but it is not yet the
complete WidthFits contract in the original sketch and never proves curved
footprint contact. No new generic Lower language is needed to add such checks.

Reuse current fits, tolerance and capability semantics. Inspection should distinguish fully located, deliberately movable, unresolved and contradictory relationships, name the owning driver, and report meaningful residuals. Do not add generic loop closure, nonlinear contact solving or arbitrary equation solving in this milestone.

**Acceptance:** changing veneer thickness or mounting gap changes placement through one Interface parameter; a missing `Frame` or incompatible width fails before solving; an explicit placement competing with the Interface is rejected. Keep the existing typed-joint and legacy atomic fixtures passing.

## 5. Profile families and finite section authoring

**Friction:** Python repeats cubic control declarations for each body station and each changing D-shaped neck profile. Generic numeric parameters exist, but reusable profile values and finite section expansion are not composable enough here.

```firmament
Template<Width: Length, Depth: Length>
Profile NeckSection {
    // Named cubic spans: BackLeft, BackRight, Seat.
    // Pure parameter expressions replace repeated literal controls.
}

Static Stations: Set<NeckStation> {
    Heel => NeckStation { Y: 145mm; Width: 55.4mm; Depth: 40mm }
    Shaft => NeckStation { Y: 240mm; Width: 53.1mm; Depth: 13mm }
    Nut => NeckStation { Y: 660mm; Width: 43mm; Depth: 7mm }
}

SectionChain Neck {
    Continuity: G1
    Pattern Sections Over Stations {
        station => Section {
            Frame: FrameAtY(station.Y)
            Profile: NeckSection<Width: station.Width, Depth: station.Depth>
            Seam: BackLeft
        }
    }
    Start: Cap
    End: Cap
}
```

`FrameAtY` here is a proposed finite frame-construction helper, not an existing user-defined function. Its final spelling can be the proposal 2 constructor. The example illustrates section family composition, not sufficient stations for the final fairing.

Add `Profile` as a concrete Template output before considering higher-order Profile/Template parameters. For scaled body outlines, add a pure 2D profile transform that preserves named spans, seam and orientation. Start with positive uniform scale about an authored center; reflection, nonuniform arc scaling and arbitrary knot conversion need separate contracts.

A companion bounded interpolation constructor would remove the generator's other large expansion: calculating cubic handles around every body outline point.

```firmament
InterpolatingLoop2 BodyOutline {
    Guides: BodyOutlinePoints
    Law: PeriodicCatmullRom
    Parameterization: Uniform
    Seam: LowerBout
}
Profile BodySection From BodyOutline
```

`BodyOutlinePoints` is a finite ordered keyed set of Point2 guides. This spelling proposes a specific interpolation law, not an unconstrained "make it smooth" request. It computes controls and lowers to existing [CubicBezier2 spans](cubic-bezier-profiles.md), with span identities derived from adjacent guide keys. Guides are interpolated; the selected curve shape is authored design intent, not an exact reconstruction of a photograph. Loop orientation, crossings, seam and compatible span order still require ordinary validation. Inserting a guide changes topology/correspondence and must be reported; positive profile scaling should preserve the original span identities.

Generated sections retain keyed source identity and explicit order. Correspondence remains by semantic spans with the existing validation; no nearest-point rematching. Expanded source must not need to become the durable document.

**Acceptance:** express the body station family and neck profiles in compact source, preserve correspondence/pcurve evidence, and compare exact exports against the current generator-authored fixtures.

## 6. Surfacing intent: straight runs and local fairing controls

**Friction:** the first rounded neck looked acceptable from the front but bulged in side profile. SectionChain's automatic tangent selection cannot know that the author intends a straight shaft and a short fairing at the heel. Adding more coordinates is a poor substitute for expressing that intent.

```firmament
SectionChain Neck {
    Continuity: G1
    // Existing section declarations.
    Transition HeelToShaft {
        From: Heel
        To: Shaft
        Law: SmoothPolynomial
        EndTangent: ShaftDirection
    }
    Transition ShaftToNut {
        From: Shaft
        To: Nut
        Law: Ruled
    }
    Require StraightBack {
        Spans: [BackLeft, BackRight]
        From: Shaft
        To: Nut
        Envelope: ShaftEnvelope
    }
}
```

Per-transition law and tangent references are proposed additions. A named direction alone does not determine a full tangent field or magnitude; the binder must derive the corresponding control-field constraints and disclose any remaining selection. A smooth-to-ruled join must satisfy the requested G1 continuity. Reject incompatible intent rather than silently relaxing G1.

The `StraightBack` form is a candidate declarative requirement over authored support/envelope semantics; its precise proof scope needs review. Do not mark a sampled silhouette check as an exact global proof. The first slice can simply expose deterministic endpoint tangent constraints and report their satisfaction, without implementing this larger envelope predicate.

Keep existing local transition dependencies, polynomial representation, pcurves and eligibility checks. JudgmentEngine may choose among admissible bounded fairing policies; it must never trade away a hard straight-run or continuity requirement for a prettier score. Caps remain independently G0 unless explicitly designed otherwise.

**Acceptance:** recreate the approved flat-ish neck with an explicit straight-run/fairing declaration and stable side silhouette. Contradictory tangent/continuity intent fails with a named section/transition diagnostic.

## 7. Non-planar placement: surface attachments with explicit orientation

**Friction:** guitar controls and hardware use manually chosen heights on a carved top. An arbitrary rigid orientation is already representable; what is missing is deriving a stable attachment frame from a curved support.

```firmament
SurfaceAttachment VolumeKnobSite {
    Support: Body.CarvedTop
    Location: SurfaceUV(0.62, 0.28)
    Side: Outside
    Clocking: Along(Body.Centerline)
    Offset: 1mm
}

Interface<Fixed> VolumeKnobMount {
    A: Guitar.Body.VolumeKnobSite
    B: Guitar.VolumeKnob.Seat
}
```

Resolve the attachment in definition-local geometry, then compose occurrence transforms. The oriented surface normal provides Z; a projected named direction provides X. Reject singular locations, a vanishing clocking projection, points outside trim domains, and ambiguous support selectors.

Start with named analytic planes/cylinders/cones/spheres. Add explicitly identified SectionChain patches and their stable parameter charts next. A raw normalized UV is tied to that chart, not a persistent physical location under all topology edits; inspection and documentation must say so. Later location modes may use an authored curve/section intersection or an explicit projection ray with a selected hit rule. Never silently choose a nearest rendered triangle or nearest geometric hit.

This locates a point/frame. It does **not** prove that a wide flat hardware base conforms to a curved top, cuts a mounting pocket, or makes contact everywhere. Those are separate footprint, seat-construction and clearance contracts.

**Acceptance:** place a control on a known curved support without absolute Z; edit the support and track its attachment; reject seam/singularity/trim failures. Display/export must consume the compiled frame without rerunning surface placement on camera rotation.

## 8. Pure engineering expressions and finite sample sets

Implemented bounded slice: [`Function` and `Points<Point2|Point3>`](concept-points-and-functions.md).
The guitar uses a keyed `Series` for its 22 frets. The `SampleSet` spelling below
is the original proposal, superseded by the implemented Concept geometry syntax.
Angle trigonometry and vector intrinsics remain deferred.

**Friction:** fret positions require `ScaleLength * (1 - 2^(-n/12))`; the Python generator also handles taper interpolation and geometry-vector calculations. A finite value calculation should not force source generation.

```firmament
Static ScaleLength: Length = 628mm
Static NutY: Length = 658mm
Static Frets = SampleSet {
    Index: 1..22
    Key: index
    Value: FretSite {
        Y: NutY - ScaleLength * (1 - pow(2, -index / 12.0))
    }
}
```

Extend the existing typed value evaluator rather than inventing an assembly-only expression engine. Admit arithmetic and a small pure math set, including dimensionless `pow`, length interpolation and explicit Angle trigonometry. Define real versus integer division, unit rules, finite results and evaluation ordering. Frame/vector operations should be typed intrinsics, not arbitrary numeric lists.

`SampleSet` is a proposed bounded collection producer, with a finite count limit and stable keys. It is not a runtime loop, recursion, IO, mutable state or source-string macro. Existing Static Record/Set syntax remains valid. Use shared specialization and schema machinery wherever possible.

**Acceptance:** author all 22 frets without Python; diagnose dimensional misuse, nonfinite results and expansion limits at their expressions. Generated sites retain the formula and index in source provenance.

## 9. Point-driven WireForm routes

Extended design: [Wire routing and Concept guides](wire-routing-and-guides-proposal.md)
adds hard Via points, locked arc-length guide intervals and bounded connector
planning. The [three-point guitar migration](wire-point-routes.md) is implemented; guided
routing now has a bounded Line3 guide and placed-port binding; obstacle search remains deferred.

**Original friction (three-point slice resolved):** each guitar string needed Python to calculate directions, bend angle, tangent setbacks and its placement basis. WireForm already owns the exact straight/bend construction.

```firmament
WireRoute LowE {
    Diameter: 0.68mm
    Material: Standard.Materials.StainlessSteel.304_Annealed
    Start: Tailpiece.LowE.Exit.Point
    Corner NutBreak {
        At: Nut.LowE.Guide.Point
        Radius: 3mm
    }
    End: Tuners.LowE.Post.Point
}
```

First admit exactly three non-collinear points and one explicit centerline bend radius. `At` is the intersection of the untrimmed straight legs, not a point that the rounded centerline passes through. Derive the plane, tangents, angle and setbacks; lower to the existing WireForm forming sequence. Reject overlapping setbacks, insufficient leg length, degenerate geometry and inadmissible radius.

All inputs must resolve in one declared definition/assembly coordinate scope. If the route depends on other occurrences, evaluate their fixed placement first and retain that dependency; cycles or dynamic-joint endpoints are deferred, not iteratively guessed. Publish Start/End frames and the compiler-derived centerline length through the same semantic authority.

The route is geometry, not string tension/vibration, wound-wire detail, a shortest-path solver, or automatic collision avoidance. Preserve current material and clearance validation.

**Acceptance:** replace `formed_string()` in the generator with six route declarations or an assembly Pattern; compare route endpoints, lengths and exact cylinder/torus construction; retain existing thin-string regression coverage.

## 10. Semantic appearance assignment

**Friction:** preview colors are keyed by specialization strings in JSON, and Cycles material selection uses occurrence-name matching. That makes visual meaning brittle even though geometry remains authoritative.

```firmament
Appearance CherryWood {
    BaseColor: [0.15, 0.025, 0.012]
    Metallic: 0
    Roughness: 0.25
}
AppearanceAssignment NeckFinish {
    Target: Guitar.Neck
    Look: CherryWood
}
```

Start with named portable simple appearances and semantic definition/occurrence selectors. Define precedence explicitly: occurrence assignment over definition assignment over default. Keep engineering stock material separate from visual finish. A look edit changes bindings/scene data, not exact geometry or tessellation.

A later `ExternalLook` may bind a USD material prim or downstream renderer recipe. Do not imply that Cycles node graphs are portable USD PreviewSurface shaders. Procedural sunburst/edge-distance coloration remains an honest downstream capability until an explicit supported shader contract exists. Do not broaden P0 into a shading language or texture-generation project.

**Acceptance:** rename an occurrence without losing its authored finish; export deterministic USD bindings; preserve BRep/display hashes when changing roughness. STEP geometry continues to stand independently of the rendering recipe.

## 11. One compilation experience, with useful expanded-source evidence

**Friction:** the witness uses separate assembly, section-chain and ordinary build commands. Profile/SectionChain source can enter an extrusion parser through ordinary `build`. Generated names and handwritten matrices also obscure which source declaration owns a defect.

```text
aetheris build guitar.firmament --target step,usd --out artifacts/local/guitar
aetheris inspect guitar.firmament --expanded --placements --dependencies --json
```

Unified dispatch should identify the root semantic product and invoke existing domain compilers/exporters. It must not flatten all profiles into an extrusion grammar or replace `asm`/`section-chain` compatibility commands. Requesting unsupported output reports the specific domain boundary.

Expanded inspection is read-only compiler evidence, not another authored source file. For every patterned part/section, retain its declaration span, pattern key, specialization, port binding, placement authority and dependency chain. A contradictory placement should name both drivers and the affected occurrence; a fairing failure should name the transition and intent it violated.

Generate schema/completion/hover data with each new construct, and maintain browser project-snapshot parity. Keep code editing source-owned. Preview and camera changes consume compiled geometry; semantic edits invalidate their dependency neighborhood. CLI timing should separate binding/expansion, exact materialization, tessellation and export rather than call all of them a rebuild.

**Acceptance:** build the compact guitar through one command; inspect the source row behind a selected generated fret or pole; diagnose malformed proposed constructs rather than silently ignore them. Preserve multi-file dependency hashes and existing CLI behavior.

## Review decisions and boundaries

The composition model is settled for the implemented lane:

- Concept geometry supplies the design scaffold; physical ports realize a declared
  relationship to it. Direct readable Placement remains an explicit layout option.
- Each occurrence has one placement authority. Fixed seating and coaxial mating
  delegate to existing frame/kinematic owners rather than introducing general
  simultaneous constraint solving.
- Definition-owned semantic publication and explicit subassembly forwarding
  preserve privacy and local/world coordinate meaning.
- A part owns its features; an assembly contains complete reusable components.
  Keyed Pattern expansion preserves identity independently of specialization.
- Functions evaluate during compilation. Finite point recipes, profile edits and
  routing lower to existing typed IR and materializers; no runtime language is added.
- Explicit scalar placements use conventional 0.1mm authoring resolution unless
  a more specific physical tolerance is stated. Numeric computation stays precise.

Future proposals should extend these decisions. The old Lower language,
material-to-material constraint network and a mandatory universal SurfaceAttachment
constructor are not prerequisites. Surface-driven placement should derive an
explicit Concept frame under a qualified support/location contract.

Each milestone should include one valid guitar-sized fixture, targeted invalid cases, CLI inspection, schema/language-analysis coverage, and the full existing solution test lane. Geometry changes require exact topology/pcurve/STEP qualification; appearance-only changes require binding and unchanged-geometry evidence. Generated artifacts stay under ignored `artifacts/local/`; durable examples belong under `fixtures/`.

Do not promise general nonlinear mating, dynamic geometry-bound routes, arbitrary freeform seat cutting, G2 surfacing, topology-changing lofts, manufacturing-perfect guitar joinery, or a renderer-portable procedural lacquer shader in these slices.

The success criterion is that the guitar becomes compact, editable Firmament whose semantic source carries repetition, attachments and relationships. Python may remain a reproducibility/render harness; it should no longer be the place that knows how the guitar is assembled.
