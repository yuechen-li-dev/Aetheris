# Aetheris V2-A1 — Resolved Profile2D contract

## 1. Executive summary

`ResolvedProfile2D` is the foundational constructive data primitive for Aetheris V2. It is not a sketch and not a constraint-solving system; it is a validated, explicit 2D region-topology contract that AIR atoms consume deterministically.

This contract formalizes that subtractive/additive constructive intent should be represented in profile space first (loops/regions/containment/interval layering), then lowered to 3D boundary topology emission. In V2 terms: declared profile topology is preferred over runtime-discovered 3D topology whenever admissibility permits.

## 2. Why Profile2D exists

Aetheris V2 needs one common profile contract that multiple AIR constructors can consume:

- `AirExtrude`
- `AirProfileStackExtrude`
- `AirRevolve`
- `AirPathSweep` / `AirPipeSweep` / `AirHelicalSweep`
- `AirRuledTransition`
- future 2D Boolean normalization lanes
- future surface-feature planning/materialization lanes

Current production/lab evidence already contains profile-like structures, but as fragmented per-feature contracts:

- rectangular outer loops (`AirRectangleProfile`)
- centered circular inner loops (`AirCenteredCircleLoop`)
- hole family radial stacks (`HoleProfileSegment`)
- z-ordered interval layers (`AirProfileStackLayer`)
- ruled-transition circle-profile assumptions
- surface-feature descriptors that imply bounded profile regions

V2-A1 extracts this into one architecture-grade contract without changing production behavior.

## 3. Non-goal: sketch constraint solving

Resolved profile work in V2-A1 explicitly excludes sketch-system responsibilities:

- no underconstrained line sets
- no geometric constraint solving (coincident/tangent/dimension)
- no solver history replay
- no interactive editing state
- no implicit intent archaeology from partially defined sketches

Sketch systems may exist above kernel level in the future. The kernel AIR boundary consumes resolved profiles only: validated 2D topology with explicit admissibility and diagnostics.

## 4. Definitions

### `AirCurve2D`

An abstract bounded deterministic 2D curve primitive admissible in kernel profile topology.

### `AirLineSegment2D`

A bounded line segment with finite start/end points in profile-local coordinates.

### `AirCircularArc2D`

A bounded circular arc with explicit center/radius/start/end parameterization and deterministic orientation.

### Optional future bounded deterministic families

Deferred unless existing bounded support is proven with deterministic topology behavior (e.g., specific non-rational polynomial segments). Not part of first production profile contract by default.

### `AirLoop2D`

An ordered closed sequence of bounded curves whose adjacent endpoints match within tolerance. Loop carries deterministic orientation and validation metadata.

### `AirRegion2D`

A containment-classified set of loops describing material and void boundaries in one profile frame.

### `ResolvedProfile2D`

The validated normalized profile payload consumed by AIR atoms. Contains curve/loop/region topology, frame, tolerance provenance, and diagnostics.

### Outer loop

A material boundary loop for an island (first production scope: one outer loop).

### Inner loop / hole loop

A void boundary fully contained by its parent outer loop.

### Island

A material-containing outer loop subtree in region topology.

### Winding/orientation

Deterministic loop orientation convention bound to profile-frame handedness and normal direction.

### Containment tree

Explicit parent/child hierarchy of islands and holes derived from normalized non-overlapping loop containment.

### Profile frame

Local 2D coordinate basis plus associated 3D embedding contract used by AIR atoms during placement/emission.

### Tolerance policy

Numerical tolerances used for endpoint closure, orientation tests, containment classification, and degeneracy rejection.

### Provenance/diagnostics

Structured source lineage plus machine-checkable validation/admissibility diagnostics for determinism and debuggability.

## 5. Minimal V2-A1 admissible curve set

Recommended first admissible set:

- line segments
- circular arcs
- full circles represented explicitly (or as unambiguous closed arc primitive form)

Deferred by default (unless concrete bounded support evidence emerges):

- other bounded deterministic non-rational segment families

Out of scope:

- rational NURBS
- arbitrary freeform curves
- implicit infinite curves (line/ray primitives without bounds)
- self-intersecting segment definitions

Why lines + arcs are sufficient for near-term AIR basis:

- box/plate/rectangular extrusion families
- cylinder and circular-hole families
- counterbore and stepped circular cut intervals
- frustum/cone families via revolve/ruled transition
- ruled circle↔circle transitions now, square/round via bounded correspondence later
- slot/capsule pathways via line+arc loops in later milestones

## 6. Loop contract

Loop invariants for admissibility:

- closed under endpoint matching tolerance
- ordered curve sequence
- no dangling edges
- no zero-length/degenerate edges
- no uncontrolled self-intersections
- deterministic orientation
- explicit semantic role:
  - outer boundary
  - hole boundary
  - island boundary (if multi-island support is enabled)

Expected failure diagnostics:

- `profile-loop-open`
- `profile-loop-endpoint-mismatch`
- `profile-loop-zero-length-segment`
- `profile-loop-self-intersection`
- `profile-loop-ambiguous-orientation`
- `profile-loop-unsupported-curve-type`

## 7. Region contract

Region invariants:

- a region is one or more loops plus deterministic containment classification
- holes must be contained by parent material loop
- holes cannot overlap each other or escape parent boundaries
- containment tree rules must be explicit when nesting is admitted
- winding+containment interpretation must be deterministic

V2-A1 recommendation for first production `ResolvedProfile2D` scope:

- support exactly one outer loop
- support zero or more hole loops

Deferred by default:

- multiple disjoint material islands
- deep alternating island/hole nesting
- arbitrary overlapping-loop normalization

## 8. Boolean normalization boundary

V2 direction includes eventual 2D Boolean normalization, but V2-A1 defines boundary/contract first:

1. already-resolved profiles (`ResolvedProfile2D`)
2. unresolved profile expressions requiring normalization

Future shape (conceptual, not implemented here):

- `AirProfileExpr2D`
  - `Union`
  - `Intersect`
  - `Difference`
  - `Offset`
  - `Normalize() -> ResolvedProfile2D`

Boundary rule:

- AIR atoms should consume `ResolvedProfile2D`, not raw unresolved Boolean expression trees, unless a specific atom explicitly owns normalization.

Compile-time analogy:

- profile-space Boolean normalization = constructive compile-time lowering
- 3D BRep Boolean subtraction = runtime topology discovery fallback

## 9. Profile frames and coordinate contracts

Profile coordinate rule:

- profiles are authored/stored in local 2D frame space
- AIR atoms place them in 3D using axis/path/frame metadata
- world coordinates are not baked into reusable profile payloads except by explicit placement wrappers

Frame contract should include:

- origin
- orthonormal basis vectors
- handedness
- associated normal direction
- orientation conventions (e.g., outer CCW in +normal frame)

This is required to keep consumption deterministic across:

- extrude
- revolve
- ruled transition
- path/pipe sweeps
- helical sweeps

## 10. Atom consumption contracts

### AirExtrude

- consumes one `ResolvedProfile2D`
- outer/hole loops emit cap boundaries and side surfaces
- line edges map to planar ruled side faces
- arc/full-circle edges map to cylindrical side families (bounded by emission support)

### AirProfileStackExtrude

- consumes ordered z-interval layers containing resolved profile semantics
- each layer represents solid interval or cut interval semantics
- inter-layer transitions emit shoulders/caps/annular boundaries under deterministic interval ordering

### AirRevolve

- consumes axis-relative profile curve/region in a known profile frame
- line segments produce planar/cylindrical/conical families as admissible
- circular arcs may map to spherical/toroidal families when explicitly supported
- requires explicit seam/pole policy and diagnostics

### AirPathSweep / AirPipeSweep / AirHelicalSweep

- consumes resolved profile plus analytic path and frame transport policy
- early scope should remain bounded (circular/annular/simple profile classes)
- excludes arbitrary NURBS/freeform sweep behavior

### AirRuledTransition

- consumes two compatible resolved profile boundary parameterizations
- current proven early scope: circle→circle frustum class
- future square↔circle/square↔round lanes require explicit segment correspondence contract

### AirSurfaceOffset

- conceptual relation only in V2-A1
- may consume profile-like regions on host surfaces / offsets later
- high-risk family remains deferred pending bounded admissibility policy

### AirMaskIntersection / future mask algebra

Optional architecture note:

- resolved profiles can serve as explicit masks
- multi-axis allowed-material masks may compose at AIR level via intersection
- no implementation in V2-A1

## 11. Diagnostics contract

Profile diagnostics should be:

- deterministic
- machine-checkable (stable code strings + payload)
- location-bearing where possible (curve index, loop index, region node)
- explicit about failure class

Suggested categories:

- validation failure
- unsupported curve family
- unsupported topology class
- normalization required before atom consumption
- tolerance ambiguity / unstable classification
- admissible profile but current emitter unsupported

## 12. Serialization/test artifact shape

Recommended future deterministic artifact representation (JSON-like or project convention equivalent):

- profile metadata/version
- curve table
- loop table (ordered curve refs + orientation)
- region containment tree
- normalized orientation state
- diagnostics list
- bounds (AABB)
- signed area / per-loop area
- optional stable hash / identity key

V2-A1 does not require implementation of this artifact; it establishes target shape for future labs/tests.

## 13. Relationship to existing code

Existing structures and their mapping pressure toward `ResolvedProfile2D`:

- `AirRectangleProfile`: specialized rectangular outer loop primitive
- `AirCenteredCircleLoop`: specialized centered circular hole loop primitive
- `AirProfileRegion2D`: minimal region contract (rectangle outer + optional inner circle + role)
- `AirProfileStackLayer`: layered z-interval wrapper around region semantics
- `HoleProfileSegment`: axis-relative radial/depth segment stack encoding profile behavior in 1D radial form
- profile-stack specs/adapters/executors: interval/material semantics that anticipate generalized resolved profile regions
- ruled-transition frustum evidence: compatible boundary transition contract already present for circle classes
- surface-feature descriptors/planning bridges: encode region-like bounded feature intent that should align with shared profile contract

Duplication and likely consolidation targets:

- multiple ad hoc profile shapes (`PolylineProfile2D`, AIR rectangle+circle records, hole radial segments) should converge behind one resolved profile contract with adapters
- validation/diagnostic logic should be centralized rather than repeated per lane
- interval/layer semantics should consume shared region topology payloads instead of per-feature special tuples

## 14. Recommended implementation ladder

Post V2-A1 staged plan:

- **V2-X1**: minimal `ResolvedProfile2D` lab (rectangle + circle loops, validation, diagnostics)
- **V2-X2**: circular-profile `AirExtrude` lab (foundation for eventual cylinder-as-extrude pathways)
- **V2-X3**: profile-with-hole extrude lab (square-with-circle-through case)
- **V2-X4**: bounded line/arc 2D Boolean normalization lab
- **V2-X5**: slot/capsule (line+arc) profile extrusion lab
- **V2-X6**: square-to-round ruled transition lab with explicit correspondence rules
- **V2-V1**: production `ResolvedProfile2D` foundation adopted by selected AIR atoms

(Sequence may be renumbered to align with roadmap management, but ordering intent should remain.)

## 15. Risks and guardrails

Primary risks:

- accidental expansion into sketch solver responsibilities
- prematurely supporting complex deep nesting without deterministic containment
- tolerance policy causing nondeterministic region classification
- spline/freeform creep beyond bounded analytic scope
- unresolved profile Boolean expression leakage into emitters
- invalid profiles reaching BRep emission and producing topology garbage

Guardrails:

- resolved profiles only at AIR atom boundaries
- bounded line/arc-first admissibility
- explicit deterministic diagnostics
- lab-first, production-second progression
- no NURBS/freeform in foundational scope
- topology normalization before emission
- negative tests for invalid loops/hole containment/tolerance ambiguity

## 16. Open questions

- Should first production profile contract allow multiple disjoint material islands?
- What containment nesting depth is truly required by near-term AIR features?
- Should full circles be a dedicated primitive or arc + closure metadata form?
- How should tolerance policy be represented (global, per-profile, per-atom, or mixed)?
- At what stage should 2D Boolean normalization be performed in AIR pipelines?
- Should profiles carry provenance labels that inform topology naming downstream?
- How should mask algebra and profile contracts meet at AIR planning/materialization boundaries?
- Where should surface-on-host profile contracts live for emboss/deboss/thicken lanes?

## 17. Glossary

- **resolved profile**: validated explicit 2D region topology contract consumed by AIR atoms.
- **sketch**: authoring/constraint-space construct, not kernel constructive foundation.
- **loop**: ordered closed bounded-curve chain in profile frame.
- **region**: containment-classified set of loops defining material/void.
- **island**: material outer-loop subtree within region containment.
- **hole**: void loop contained by an island.
- **winding**: loop orientation convention under frame handedness/normal.
- **containment tree**: explicit parent/child hierarchy for loops/islands/holes.
- **normalization**: conversion from potentially ambiguous/overlapping expressions to deterministic resolved topology.
- **profile frame**: local coordinate basis and orientation contract for profile placement.
- **admissibility**: bounded capability check that decides if an atom/emitter can consume a profile deterministically.
- **emitter support**: currently implemented geometry/topology lowering capability for admissible profiles.
