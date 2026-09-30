# Firmament authoring and composition: proposals from GUITAR-SURFACING-X0

Status: **roadmap; Foundations A–C now have a bounded implementation.**
See [the implemented assembly authoring foundation](assembly-authoring-foundation.md)
for supported syntax, limits, and the guitar reassessment. Remaining sketches
below retain their proposed status; Authoring D is deferred for review.

The guitar demonstrated that the existing exact geometry and display paths can produce a convincing designed object. It also required a Python source generator to assemble 107 occurrences, calculate transforms, expand repeated hardware, generate spline controls, calculate fret spacing, and construct string routes. That generator emits ordinary Firmament; it does not replace the kernel. Nevertheless, too much design intent lives outside the language.

The objective is to move those responsibilities into typed, finite Firmament authoring and existing compiler owners. This is a proposal for several bounded milestones, not a request for a general scripting language, universal constraint solver, or new surfacing kernel.

All code blocks below are **proposed syntax**, including blocks that reuse existing constructs. They are design sketches, not compile-ready fixtures. Names such as `Body.Top`, `Neck.Nut` and `Pickup.Seat` denote proposed published ports; they are not currently emitted by the guitar fixtures. Implementations must publish those ports before the examples can resolve.

## Evidence and current baseline

The motivating source is [the guitar generator](../../../scripts/create-guitar-x0.py), its [authored fixtures](../../../fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar-x0.firmament), and the [witness report](../demos/guitar-surfacing-x0.md).

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
| 5. Section/profile family authoring | P1 | Parameterized profiles and finite section sets | Existing Templates, Patterns and SectionChain |
| 6. Explicit straight/fair transition intent | P1 | Endpoint tangent constraints and named transition laws | Existing SectionChain tangent/materializer path |
| 7. Placement on curved supports | P1 | Named analytic support, explicit location and clocking | 2–4; surface evaluator |
| 8. Dimensioned engineering expressions | P1 | Pure scalar arithmetic, small math set and finite sampling | Existing compile-time value evaluator |
| 9. Point-driven WireForm routes | P1 | Three points, one explicit bend radius | 2–3, existing WireForm |
| 10. Semantic appearance binding | P2 | Named simple looks bound to definitions/occurrences | Existing USD appearance path |
| 11. Unified build and source diagnostics | P0 alongside each slice | Dispatch, generated-source provenance and dependency reports | Existing CLI, schema and language analysis |

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

Then deepen reusable named Interfaces without inventing a new compiler-owned generic family for every product:

```firmament
Interface MountedHardware {
    Parameter Gap: Length = 0mm
    Role Support requires {
        Frame: DatumFrame
        AvailableWidth: Length
    }
    Role Hardware requires {
        Frame: DatumFrame
        RequiredWidth: Length
    }
    Lower FrameCoincident Support.Frame Hardware.Frame OpposedDirection
    Lower OffsetAlongAxis Support.Frame.Z Hardware.Frame.Origin Gap
    Require WidthFits => Hardware.RequiredWidth <= Support.AvailableWidth
}

Mate BridgeSeat: MountedHardware<Gap: 2mm> {
    Support: Guitar.Body.BridgeSeat
    Hardware: Guitar.Bridge.Mount
}
```

The two `Lower` lines illustrate intent. The implementation must normalize them into **one seated-frame relation** before solving, rather than demand zero frame coincidence and a nonzero offset simultaneously. Prefer exposing that normalized relation directly if the combined spelling is misleading during syntax review.

Role member requirements are checked before lowering, with exact member/type diagnostics. Parameters are finite typed values. `Require` reads resolved semantic dimensions, not arbitrary mesh measurements. A `WidthFits` check is a width contract, not proof that a curved mounting footprint is collision-free.

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

**Friction:** each guitar string currently needs Python to calculate directions, bend angle, tangent setbacks and its placement basis. WireForm already owns the exact straight/bend construction.

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

The highest-value initial decision is the composition model: should direct frame placement be an explicit layout authority alongside Interfaces, or syntax that lowers to a fixed Interface? This draft prefers the former, with one driver per occurrence and a common transform owner. Mechanical contracts remain visible when the author declares them.

Review the seated-frame syntax before implementation. A combined relation is clearer than contradictory primitive relations with a hidden precedence rule. Also settle named pattern identity, the coordinate space of frame references, and definition-level port publication before introducing surface attachment syntax.

Each milestone should include one valid guitar-sized fixture, targeted invalid cases, CLI inspection, schema/language-analysis coverage, and the full existing solution test lane. Geometry changes require exact topology/pcurve/STEP qualification; appearance-only changes require binding and unchanged-geometry evidence. Generated artifacts stay under ignored `artifacts/local/`; durable examples belong under `fixtures/`.

Do not promise general nonlinear mating, dynamic geometry-bound routes, arbitrary freeform seat cutting, G2 surfacing, topology-changing lofts, manufacturing-perfect guitar joinery, or a renderer-portable procedural lacquer shader in these slices.

The success criterion is that the guitar becomes compact, editable Firmament whose semantic source carries repetition, attachments and relationships. Python may remain a reproducibility/render harness; it should no longer be the place that knows how the guitar is assembled.
