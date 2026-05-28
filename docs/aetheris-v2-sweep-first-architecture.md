# Aetheris V2 — Sweep-first architecture

## 1. Executive summary

Aetheris V2 is a deliberate architecture shift:

- from a primarily 3D-primitive and 3D-Boolean-first modeling mindset,
- to a resolved-profile-first, sweep-first analytic construction mindset.

The V2 position is not that 3D Boolean subtraction is useless; it is that subtraction should be an explicit fallback when constructive intent cannot be expressed as bounded analytic profile/sweep/revolve/transition operations.

Short doctrine:

- **Boolean union composes.**
- **Boolean subtraction discovers.**
- **Aetheris prefers declared topology over discovered topology.**

V2 therefore treats AIR as the atomic constructive-intent layer between CIR/FRep and BRep, and treats BRep primarily as lowered boundary topology emission and validation substrate rather than as the preferred authoring foundation.

## 2. Why V1 reached this point

Aetheris V1 built a strong 3D-first analytic kernel substrate and delivered real capability:

- deterministic STEP/AP242 round-trip work,
- exact analytic primitive construction,
- bounded BRep emitters,
- SafeBooleanComposition,
- profile-stack execution lanes,
- semantic recovery/rematerialization,
- AIR profile-stack migration,
- and early primitive-as-AIR migration (box).

This is not a failure narrative. It is a success narrative that produced enough evidence to expose architectural leverage.

Repeated 3D subtract families (stepped holes, counterbores, blind pockets, and related host-localized features) plus importer/reconstruction pressure consistently show the same cost center: topology discovery at runtime via surface/surface intersection, trim/split/classify/stitch pipelines.

V1 assets such as SafeBooleanComposition and ProfileStackExtrude were already directional signals toward V2: move intent earlier, make admissibility explicit, and lower bounded construction into topology rather than discovering topology late.

## 3. The core mistake: treating 3D primitives as foundations

In V2 terms, canonical 3D primitives are convenience constructors, not foundational modeling atoms.

- box = rectangle extrude,
- cylinder = circle extrude (or revolve of a radial segment),
- cone/frustum = revolve of a line/segment,
- sphere = revolve of an arc,
- torus = revolve of an offset circle.

AIR-X3 and AIR-V3 already provide concrete evidence for this framing: box production routing has been migrated to rectangle-extrude while preserving topology and STEP parity expectations.

The architectural implication is to normalize around constructive families first, then expose primitive conveniences as policy-constrained front doors over those families.

## 4. The real primitive: resolved 2D profile regions

Aetheris V2 is **not** sketch-solver-first.
It is **resolved-profile-first**.

Definitions:

- **Sketches**: optional authoring/UI/source layer; may be underconstrained, redundant, or ambiguous.
- **Resolved profiles**: explicit 2D loops/regions with deterministic inside/outside, holes/islands, orientation, and bounded analytic curve content.
- **AIR profiles**: kernel-consumable, validated profile data with no unresolved constraint state.

Explicit V2 statement:

- 2D constraint solving is not kernel foundation.
- 2D region topology is kernel foundation.

Aetheris can accept sketch-derived inputs, but the kernel contract begins at resolved profile topology, not at interactive constraint solving.

## 5. Compile-time Boolean vs runtime Boolean

A useful analogy:

- runtime 3D subtraction discovers topology after surfaces collide;
- compile-time profile normalization resolves subtractive intent before 3D emission.

Why this matters:

- 2D Boolean typically resolves point intersections plus winding/containment.
- 3D Boolean requires intersection curves, face classification, trimming, splitting, and re-stitching.
- Therefore 2D/profile-space normalization is generally cheaper, more diagnosable, and more deterministic in bounded analytic scope.

V2 directive:

- Prefer subtractive intent realization in profile/AIR space (e.g., profile-stack intervals, layered radii, resolved profile region normalization).
- Use 3D subtract only when intent cannot be declared from bounded constructive relations.

This direction is consistent with:

- SafeBooleanComposition’s bounded policy posture,
- AirProfileStackExtrude execution lane,
- AIR-V2B counterbore/blind interval semantics,
- explicit blind fallback discipline when AIR admissibility is not met.

## 6. V2 constructive basis set

### 6.1 AirProfile2D / ResolvedProfile2D

First-class constructive data primitive:

- bounded analytic 2D curve segments (initially lines/arcs and equivalent deterministic bounded families),
- loops, regions, holes/islands,
- profile validation and topology checks,
- optional 2D Boolean normalization,
- no sketch constraint solving in kernel foundation.

### 6.2 AirExtrude

Core prismatic constructor:

- plates, boxes, slots, pockets, and generic prismatic bodies,
- rectangle/circle/arbitrary admissible resolved-profile extrusion,
- primary replacement for many former direct primitive routes and many subtractive feature routes.

### 6.3 AirProfileStackExtrude

Layered 2.5D constructor:

- stepped holes,
- counterbores,
- blind-pocket modeling with explicit solid/cut interval semantics,
- layered/lattice-like region stacks in admissible bounded forms,
- avoids repeated runtime 3D subtract where profile-stack intent is explicit.

### 6.4 AirRevolve

Rotational constructor family:

- turned components,
- cylinder/cone/sphere/torus families via resolved profiles,
- rotationally symmetric features with bounded analytic profiles and explicit seam/pole policy.

### 6.5 AirPathSweep / AirPipeSweep / AirHelicalSweep

Path-driven sweep family (bounded analytic scope):

- pipes,
- wires,
- springs,
- worm-gear-like/helical channels/features,
- profile transport along analytic paths.

Guardrail: this is not a claim of arbitrary freeform NURBS sweep support.

### 6.6 AirRuledTransition

Aetheris’s bounded no-NURBS answer to common loft-like manufacturing intent:

- linear transition between compatible resolved profiles,
- square-to-round adapters, ducts, and sheet-metal-style transitions,
- ruled side walls with deterministic representation,
- no generic freeform loft interpolation.

### 6.7 AirSurfaceOffset

Necessary but high-risk family:

- emboss/deboss,
- shell,
- thicken,
- bounded offset features.

Known hazards:

- self-intersection,
- curvature collapse,
- trim/join failure,
- topology fragmentation.

V2 posture: bounded analytic host surfaces first; explicit admissibility and rejection reasons required.

### 6.8 AirUnion

Additive composition of already-constructed bodies/features.

Union is generally less pathological than subtract for many analytic constructions because it more often composes declared regions instead of inferring hidden void boundary structure. It still requires bounded policies, diagnostics, and validation.

### 6.9 AirScaffoldedFeature

Future architecture lane (not immediate production target):

- construction-time topology carrier for bounded cross-axis features,
- captures entry/exit curves and host/tool relations,
- finalizer emits topology from declared constructive relations,
- does not perform generic unconstrained surface/surface discovery as primary mechanism.

### 6.10 AirBooleanFallback

Explicit fallback for irreducible 3D interactions:

- unrelated solid composition where direct constructive relation is absent,
- arbitrary cross-axis/interfering interactions,
- cases where topology cannot be declared from bounded construction intent.

Fallback must be visible, diagnosable, and policy-bounded.

## 7. Ruled transition doctrine: “loft” without NURBS

“Loft” is a CAD workflow concept, not a single mathematical primitive.

Historically, commercial kernels often route loft workflows through NURBS interpolation. Aetheris V2 intentionally scopes a bounded analytic alternative where possible: ruled transitions between compatible resolved profiles.

Ruled surface form:

`S(u,t) = (1 - t) P(u) + t Q(u)`

Where:

- `P(u)` and `Q(u)` are compatible profile curves,
- side generators are straight in `t`,
- resulting walls are ruled (manufacturable-friendly in many contexts).

Benefits in V2 scope:

- straight side-wall semantics,
- trapezoidal longitudinal cross-sections common in fabrication,
- deterministic exactness over bounded analytic inputs,
- natural alignment with STEP ruled-surface representation families,
- no NURBS dependency required for this family.

## 8. Scaffolding doctrine

Scaffolding is analogous to support planning in slicer/tree-support pipelines, but with a critical distinction:

- scaffolds are not temporary geometry bodies to subtract away,
- scaffolds are construction-time topology metadata that guide bounded finalization.

Scaffolding helps when:

- features cross axes and a single sweep axis is insufficient,
- patterned/lattice-like local structures need declared host/tool relation,
- topology remains knowable from intent but awkward for single-operation emission.

Explicit limits:

- scaffolding does not remove all Boolean fallback,
- scaffolding reduces the fallback domain,
- scaffolding must remain planner-driven, bounded, and auditable.

## 9. BRep’s role in V2

BRep remains essential in Aetheris V2.

- BRep is the emitted boundary topology representation.
- BRep remains the validation substrate.
- BRep remains the STEP export/import substrate and fallback runtime substrate.
- Existing V1 BRep infrastructure remains high-value engineering capital.

What changes is architectural role:

- BRep is not the preferred high-level constructive design language.
- V1’s BRep-first “cathedral” becomes the lower-level runtime/emission layer beneath AIR-centered construction intent.

## 10. STEP/AP242 implications

V2 aligns naturally with analytic STEP/AP242 surface families already central to Aetheris evidence lanes:

- `PLANE`,
- `CYLINDRICAL_SURFACE`,
- `CONICAL_SURFACE`,
- `SPHERICAL_SURFACE`,
- `TOROIDAL_SURFACE`,
- surface of linear extrusion,
- surface of revolution,
- ruled surface.

Implications:

- export can preserve analytic-family fidelity while AIR intent stays internal,
- import can evolve from topology-only interpretation toward AIR-candidate reconstruction when admissibility evidence is sufficient,
- deterministic analytic topology remains the interchange contract boundary.

## 11. What changes immediately vs later

### Already happening

- AirProfileStackExtrude production lane exists.
- Counterbore AIR migration in admissible contiguous layered-radii cases is active.
- Blind interval semantics exist with explicit fallback discipline.
- Box primitive has been migrated internally to rectangle-extrude routing.

### Near-term

- complete remaining primitive AIR evidence matrix,
- expand cylinder/cone/sphere/torus parity evidence,
- production-migrate next safe primitive once topology/STEP parity is proven.

### Medium-term

- introduce resolved `Profile2D` IR as explicit first-class kernel contract,
- add bounded 2D profile Boolean/normalization lane,
- establish `AirRevolve` production foundation,
- run ruled-transition lab,
- run path/pipe/helical sweep lab.

### Long-term

- surface offset/shell/thicken families with strict admissibility envelopes,
- scaffolded cross-axis feature finalization,
- importer reconstruction into AIR candidates (not only topology-soup recognition).

## 12. Non-goals

Aetheris V2 does **not** imply:

- adding generic NURBS surfacing,
- adding a general sketch constraint solver as kernel foundation,
- replacing or removing BRep,
- deleting Boolean operations,
- claiming all geometry can be sweep-first,
- implementing arbitrary freeform loft/surfacing,
- broadening beyond deterministic bounded analytic construction doctrine.

## 13. Risks and guardrails

### Key risks

- overgeneralizing sweep into implicit freeform/NURBS-like territory,
- regressing resolved profiles back into sketch-constraint state,
- production migration before topology parity proof,
- hiding Boolean fallback instead of exposing it explicitly,
- underestimating surface-offset complexity,
- scaffolding degenerating into undisclosed Boolean discovery.

### Guardrails

- bounded analytic families only,
- explicit admissibility/rejection diagnostics,
- lab-first then production migration,
- preserve legacy fallback until parity is proven,
- deterministic tests and STEP smoke markers per lane,
- topology parity where observable behavior depends on topology conventions.

## 14. Recommended V2 roadmap

Suggested staged roadmap (renumberable to avoid existing doc-series conflicts):

- **V2-A0**: this architecture doctrine (current milestone).
- **V2-A1**: resolved `Profile2D` IR audit and contract extraction from existing AIR/profile-stack evidence.
- **V2-X1**: 2D profile Boolean normalization lab (bounded analytic scope).
- **V2-X2**: remaining primitives AIR evidence matrix (or consume an existing AIR-X5-equivalent if already present).
- **V2-V1**: next primitive production migration with parity contract.
- **V2-X3**: ruled transition lab.
- **V2-X4**: path/pipe/helical sweep lab.
- **V2-X5**: surface offset architecture/risk audit.
- **V2-X6**: scaffolded cross-axis hole/feature lab.
- **V2-VN**: importer reconstruction into AIR candidates (incremental productionization track).

Roadmap principle: each stage must shrink fallback ambiguity and increase declared-topology coverage with explicit admissibility evidence.

## 15. Glossary

- **CIR/FRep**: Constructive/evaluable representation layer that describes volumetric evaluation semantics.
- **AIR**: Atomic Intermediate Representation; bounded constructive intent layer between CIR/FRep and BRep.
- **BRep**: Boundary representation of topology/surfaces used for validation, downstream operations, and STEP interchange.
- **Resolved profile**: validated 2D region topology with explicit loops/holes/islands and deterministic inside/outside.
- **Sketch**: authoring-layer geometric/constraint input that may be incomplete or ambiguous before resolution.
- **Sweep-first**: architectural preference for profile/path/revolve/transition construction before considering 3D Boolean discovery.
- **Profile-stack extrude**: layered profile interval construction (e.g., stepped/counterbore/blind semantics) lowered as bounded stacked extrusion intent.
- **Ruled transition**: profile-to-profile transition whose side walls are ruled (straight generators), used as bounded loft-like operation.
- **Surface offset**: bounded offset/thicken/shell-like construction on host analytic surfaces with strict admissibility checks.
- **Scaffolded feature**: construction-time topology metadata/finalization strategy for bounded cross-axis features.
- **Boolean fallback**: explicit, policy-bounded 3D Boolean route used when topology cannot be sufficiently declared in AIR construction space.
- **Declared topology**: topology implied directly by validated constructive intent.
- **Discovered topology**: topology inferred at runtime through intersection/classification/trimming/stitching operations.

## Update note (V2-A3)
V2 emitters and legacy emitters may intentionally coexist as parallel lanes when legacy adjacency/corner topology is load-bearing for downstream feature recognition. Declared-topology preference remains intact, but production replacement requires full migration parity gates, including recognizer parity, before legacy authority is retired.

Reference: `docs/v2-a3-legacy-topology-contracts-and-parallel-emitter-lanes.md`.
