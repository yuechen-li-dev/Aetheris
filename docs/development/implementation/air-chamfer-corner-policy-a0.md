# AIR-CHAMFER-CORNER-POLICY-A0 — chamfer corner-policy investigation

Status: implemented investigation and typed admission prototype  
Date: 2026-08-03

## Decision summary

Aetheris does **not currently need utility scoring for any proven chamfer corner fixture**. Every modern hard-valid fixture has one construction family after typed classification. No fixture materializes two different authoritative BRepPlans for the same corner context.

The old direct-BRep code does not provide contrary evidence. Its `JudgmentEngine` calls choose one constructive route over an always-admitted `reject` route, or choose between mutually exclusive single-edge/pair classifications. That is scored dispatch, not preference among multiple hard-valid constructions. The old claim that adjacent strips admit “miter, setback, or explicit corner face” had no materialized candidates, admission results, BRepPlans, or STEP artifacts. This milestone removes that claim from the executable deferred error.

The implemented A0 rule is therefore:

```text
typed ChamferCornerContext
  -> exhaustive structural Match
  -> finite evidence-backed candidate set
  -> hard admission
  -> 0: typed error / explicit Concept witness required
  -> 1: Direct
  -> N: AmbiguousWithoutPreference (no invented score model)
```

`Utility` remains a representable selection mode, but no production or prototype case reaches it. It should be wired to `JudgmentEngine` only after a future fixture produces at least two distinct, exact, hard-valid, authoritative BRepPlans for the same typed context and demonstrates a real preference that is not a correctness condition.

## Old JudgmentEngine review

### Generic engine

`Aetheris.Kernel.Core/Judgment/JudgmentEngine.cs` provides:

- a candidate name;
- an admissibility predicate;
- a scalar score;
- one rejection-reason callback;
- deterministic tie breaking by priority, name, then input position.

It correctly prevents an inadmissible candidate from winning. It does not itself distinguish geometric hard admission from route availability, fallback, error classification, or preference. That separation is the caller's responsibility.

### Legacy direct-BRep paths

| Path | Input state | Candidate choices | Hard constraints | Utility | Selection/fallback | Geometry/failure | Modern disposition |
|---|---|---|---|---|---|---|---|
| `ChamferAxisAlignedBoxSingleCorner` | synthetic extents, `XMaxYMaxZMax`, one distance | `planar_corner_cut`, `reject` | trihedral convexity, three incident edges, planar supports, equal/bounded distance, coherent cut, manifold flag | constant 200 for orthogonal, 150 otherwise | constructive candidate or selected `reject` | hand-authored corner body; current Enforce preflight finds disconnected coedges | Reject as modern route; useful negative archaeology only |
| `ChamferTrustedPolyhedralSingleCorner` | trusted planar body and corner token | same two candidates | recovered points/normals, valence 3, convexity, bounds | 200/150, but there is still only one constructive candidate | old orthogonal branch passed default extents and threw; A0 now returns a diagnostic requiring an explicit witness; non-orthogonal uses local face rewrite | triangular-prism experiment exports/reimports, but has no BRepPlan | Do not admit in AIR until a topology plan owns the rewrite |
| `ChamferTrustedPolyhedralIncidentEdgePair` | trusted orthogonal body plus two incident-axis tokens | `planar_edge_pair_cut`, `reject` | convex planar trihedron, equal/bounded distance, manifold flag, **orthogonal body** | 180 if orthogonal; the 140 non-orthogonal score is unreachable because admission requires orthogonal | constructive candidate or selected `reject` | selector-driven local face rewrite; changed STEP reimports | Geometry experiment only; no retained/replacement Construction AIR or BRepPlan |
| `ChamferTrustedPolyhedralSingleInternalConcaveEdge` | occupied-cell preflight for one vertical concave edge, optionally one interacting edge | `planar_internal_concave_edge_cut`, `planar_concave_edge_pair_cut`, `reject` | planar source, exactly one of single/pair, equal distance, bounded envelope | constants 100 and 110 | candidates are mutually exclusive; reject otherwise | 2D loop modification followed by extrusion | Not a trihedral cavity-corner policy; preserve as legacy bounded feature code |

The legacy `reject` candidate is always admissible and has score zero. When it wins, callers reinterpret a successful judgment selection as a kernel failure and splice rejection text from the losing candidates. This mixes fallback and error reporting into selection. None of these paths contains two competing geometry candidates admitted for the same context.

### Friction-lab policy scoring

`AirChamferPolicyLab` mixes recognition, correctness, deferral, fallback, and preference more aggressively. Candidate names include successes, invalid-input errors, deferrals, and `fallback-legacy-chamfer`; all are scored by the same engine. For example, both convex deferral candidates are admissible and constants choose `defer-convex-replacement-geometry`. Invalidity is represented as a high-scoring candidate rather than as a pre-selection failure. Its consideration values (`geometry-support`, `offset-stability`, `corner-policy`, `legacy-readiness`) were useful diagnostics during the strangler experiment, but they are not legitimate corner preferences.

Keep the lab as historical evidence. Do not copy its decision-as-candidate design into modern AIR.

### Modern route selection and lowering

`AirRouteSelector` already has the correct simpler split:

- canonical Construction AIR nodes use `Direct`;
- finite structural Feature AIR classifications use `SwitchMatch`;
- `JudgmentUtility` is reserved and unwired;
- unsupported structures are explicit.

`ChamferLoweringResult<T>` already prevents a failed feature-to-construction lowering from reaching topology emission. A0 extends its typed error kinds and uses it for corner resolution.

## Actual corner-policy problem

The first problem is not “which aesthetic patch wins?” It is whether a corner construction owns a closed, exact replacement region with coherent material side and an authoritative topology plan.

The smallest fixture-supported context is `ChamferCornerContext`:

```text
CornerId                         AIR/construction identity, not a BRep topology ID
Convexity                        Convex | Concave | Unknown
IncidentSelectedEdgeCount       selection fact
VertexValence                   underlying construction-topology fact
SupportSurfaces                 typed surface families
MaterialSide                    RetainInterior | RetainExterior | Ambiguous
Rule                            UniformEqualDistance | Asymmetric | NonUniform
TopologyKind                    OpenChain | ClosedLoop | Junction
HasConstructionHistory
IsSymmetric
SourceProvenance                GeneratedHistoryKnown | RecoveredTopology | ExplicitConceptWitness
hard-admission evidence flags
AvailableWitness                exact construction plus authoritative AirBRepPlan
```

Firmament does not see raw topology IDs. `CornerId` and witness identities live at the Construction AIR/BRepPlan boundary.

## Fixture matrix

| Fixture | Classification | Generated candidates | Hard admission | Selection | Proof/result |
|---|---|---|---|---|---|
| Rectangular top closed loop | convex; 2 selected edges at each valence-3 planar vertex; uniform equal distance; history-known; closed loop; symmetric | `SectionTransitionJunction` | admitted: generated profiles, closed replacement, retained/replacement ownership, manifold plan, exact authoritative BRepPlan | **Direct** | modern AIR; 12V/20E/10F; Enforce export; STEP reimport enclosed manifold; SHA-256 `DA592F...13A7` |
| Two-edge convex box junction | convex; 2 selected of valence 3; planar; uniform; junction | `PlanarEdgePairCut` | rejected: no Construction AIR/BRepPlan **and CAD Assistant cannot read the STEP** | **Error** `ConstructionWitnessRequired` | legacy experiment changes geometry and passes Aetheris Enforce/reimport at 10V/15E/7F, exposing a false-negative internal gate; SHA-256 `DE1499...9D0F` |
| Three-edge convex triangular-prism vertex | convex; 3 selected at valence 3; planar; uniform; junction | `PlanarTriangularCut` | rejected: no authoritative BRepPlan **and CAD Assistant cannot read the STEP** | **Error** `ConstructionWitnessRequired` | legacy experiment passes Aetheris Enforce/reimport at 8V/12E/6F, exposing the same interop gap; SHA-256 `212284...95CF` |
| Three-edge convex synthetic box vertex | same structural class | `planar_corner_cut` in legacy code | rejected by proof gate | **Error** before trusted modern topology | nominal legacy construction fails Enforce with `brep-preflight-coedge-disconnected`; trusted orthogonal entry now fails before topology rather than throwing on default extents |
| Concave internal trihedral corner | concave; 3 selected; valence 3; cavity material side | `ExplicitWitness` only | rejected: no exact patch/ownership/BRepPlan fixture exists | **Error** `ConstructionWitnessRequired` | old concave vertical-edge pair extrusion is a different 2D-profile family and is not evidence for this corner |
| Asymmetric/policy constrained | closed-loop structural class with asymmetric rule | structural candidate is generated, then `UnsupportedChamferRule` | hard-rejected: no asymmetric exact construction | **Error** `UnsupportedSelection` | no syntax or score model added; verified as unscored hard rejection |

The legacy two-edge and three-edge STEP files have different hashes and geometry, but they answer different selections. They are not alternative policies for one context.

## Candidate-policy model

A0 contains only policies backed by code or a concrete fixture:

- `SectionTransitionJunction`: the modern rectangular closed-loop construction;
- `PlanarEdgePairCut`: the legacy two-selected-edge experiment;
- `PlanarTriangularCut`: the legacy all-three-edge corner experiment;
- `ExplicitWitness`: the route for a validated Concept-supplied construction.

`Miter`, `Setback`, `PreservePrimaryEdge`, `PreserveLargestSupportFace`, and generic `PlanarPatch` are not enums in A0. The repository has prose suggesting them but no distinct exact constructions for a common context. Adding them would turn an old assertion into type-level fiction.

Each candidate produces `ChamferCornerCandidateEvidence`. Admission requires a matching `ChamferCornerConstructionWitness`, and that witness contains the authoritative `AirBRepPlan`. A direct-BRep artifact alone cannot make a candidate modern-valid.

## Hard admission model

The resolver treats these as elimination conditions, never scores:

- supported valence and finite structural classification;
- support-surface compatibility;
- unambiguous material side;
- distance inside the local envelope;
- non-self-intersection;
- closed replacement region;
- retained-region ownership;
- replacement-region ownership;
- manifold topology;
- exact construction availability;
- matching authoritative BRepPlan witness.

Failures are recorded per candidate as typed `ChamferCornerAdmissionFailure` values. A failed candidate has no utility score.

The prototype uses evidence booleans at the boundary because each construction family currently proves them differently. A production next milestone should replace each boolean supplied by callers with validation output computed from the localized topology plan; it must not trust arbitrary authored claims.

## Utility considerations

No utility model is active in A0. If future fixtures prove multiple admitted plans, legitimate considerations are likely:

- explicit user policy preference;
- symmetry preservation;
- analytic-surface preference;
- topology simplicity / fewer added faces;
- primary semantic edge or larger support-face preservation;
- history continuity;
- sliver-risk margin after hard non-self-intersection admission.

The following old considerations are rejected as utility inputs:

- whether geometry is supported;
- offset/distance validity;
- whether a corner policy exists;
- whether legacy topology is required;
- accept/defer/reject route status.

Those are admission, availability, or error facts. Scoring them can let a large constant conceal a missing predicate.

If utility becomes necessary, use the existing `JudgmentEngine<ChamferCornerUtilityContext>` only on the already-admitted evidence list. The engine must not classify the corner, construct fallback candidates, mutate BRep, or produce witnesses.

## Selection traces

### Closed-loop control

```json
{
  "corner": "TopLoop.Corner0",
  "classification": { "convexity": "Convex", "incidentSelectedEdges": 2, "vertexValence": 3, "supportSurfaces": ["Plane", "Plane", "Plane"], "topologyKind": "ClosedLoop" },
  "candidates": [
    { "policy": "SectionTransitionJunction", "admitted": true, "score": null, "reason": "hard invariants and authoritative PrismaticSectionTransition BRepPlan present" }
  ],
  "selectionMode": "Direct",
  "selectedPolicy": "SectionTransitionJunction",
  "constructionWitness": "PrismaticSectionTransition",
  "bRepPlan": "authoritative; 12V/20E/10F",
  "stepReimport": "enclosed-manifold"
}
```

### Two-edge convex junction

```json
{
  "corner": "Box.MaxCorner",
  "classification": { "convexity": "Convex", "incidentSelectedEdges": 2, "vertexValence": 3, "supportSurfaces": ["Plane", "Plane", "Plane"], "topologyKind": "Junction" },
  "candidates": [
    { "policy": "PlanarEdgePairCut", "admitted": false, "score": null, "reason": "MissingAuthoritativeBRepPlan" }
  ],
  "selectionMode": "Error",
  "error": "ConstructionWitnessRequired",
  "legacyEvidence": { "topology": "10V/15E/7F", "enforcePreflight": "false-negative pass", "aetherisReimport": "enclosed-manifold", "cadAssistant": "read error" }
}
```

### Three-edge convex vertex

```json
{
  "corner": "TriangularPrism.MaxCorner",
  "classification": { "convexity": "Convex", "incidentSelectedEdges": 3, "vertexValence": 3, "supportSurfaces": ["Plane", "Plane", "Plane"], "topologyKind": "Junction" },
  "candidates": [
    { "policy": "PlanarTriangularCut", "admitted": false, "score": null, "reason": "MissingAuthoritativeBRepPlan" }
  ],
  "selectionMode": "Error",
  "error": "ConstructionWitnessRequired",
  "legacyEvidence": { "topology": "8V/12E/6F", "enforcePreflight": "false-negative pass", "aetherisReimport": "enclosed-manifold", "cadAssistant": "read error" }
}
```

### Concave trihedral corner

```json
{
  "corner": "Pocket.CavityCorner0",
  "classification": { "convexity": "Concave", "incidentSelectedEdges": 3, "vertexValence": 3, "supportSurfaces": ["Plane", "Plane", "Plane"], "topologyKind": "Junction" },
  "candidates": [
    { "policy": "ExplicitWitness", "admitted": false, "score": null, "reason": "MissingAuthoritativeBRepPlan" }
  ],
  "selectionMode": "Error",
  "error": "ConstructionWitnessRequired"
}
```

No trace has `selectionMode: Utility` because no trace has more than one admitted candidate.

## Result and error model

The result boundary is:

```csharp
ChamferLoweringResult<ChamferCornerConstruction>
```

The success value retains policy, selection mode, all candidate evidence, construction witness, authoritative BRepPlan, and provenance. The existing lowering error enum now includes:

```text
NoCandidatePolicy
UnsupportedValence
UnsupportedSurfaceCombination
AmbiguousWithoutPreference
InvalidMaterialSide
MissingRetainedRegion
MissingReplacementRegion
ConstructionWitnessRequired
```

Existing `SelfIntersection`, `CornerPolicyRequired`, `MissingConstructionWitness`, and other lowering errors remain available. `AmbiguousWithoutPreference` is deliberately an error today; it prevents an unproved local score model from silently deciding geometry.

## Implemented prototype changes

- Added `ChamferCornerPolicyResolver`, the typed context, evidence, policy, selection, and witness records.
- Extended the shared chamfer lowering error vocabulary.
- Added direct-selection and hard-rejection tests, including proof that hard failures have no score.
- Added an asymmetric-rule pressure test; it is hard-rejected rather than utility-ranked.
- Corrected `AirDeferredChamferLowerer` so adjacent junctions report missing authoritative construction evidence rather than asserting several valid patches.
- Added Enforce-export/reimport tests for legacy two-edge and non-orthogonal three-edge experiments.
- Added a regression proving the synthetic three-edge constructor fails Enforce preflight.
- Replaced the trusted-orthogonal three-edge exception path with a typed pre-topology failure requiring an explicit witness.

No Firmament syntax, production route, STEP format, or BRepPlan semantics were expanded.

## BRepPlan, STEP, and visual evidence

Artifacts are under `artifacts/air-chamfer-corner-policy-a0/`:

| Artifact | Authority | Enforce | Reimport/analyze | SHA-256 |
|---|---|---|---|---|
| `modern-closed-loop.step` | authoritative AIR BRepPlan | pass | 12V/20E/10F, 10 planes, enclosed manifold | `DA592F28636D3F1D46A5BD3B798545F67E72CC79931DBCBE3B073DC91E8713A7` |
| `legacy-two-edge-convex-junction.step` | direct-BRep experiment only | false-negative pass | Aetheris: 10V/15E/7F enclosed; CAD Assistant: read error | `DE14995B4B99DEC293DE2D7A3CCE9D2E8083580CF3F6A0DCCC619165C1759D0F` |
| `legacy-three-edge-convex-vertex.step` | direct-BRep experiment only | false-negative pass | Aetheris: 8V/12E/6F enclosed; CAD Assistant: read error | `212284BD9BDD6FC26D041D6C232BA796E2A8E6B5803F337EE7B9C47842CF95CF` |

The two legacy artifacts prove only that Aetheris can serialize and re-read its own changed planar topology. They do **not** prove STEP interoperability or a hard-valid corner: CAD Assistant reports “Error occurred reading STEP file” for both. Neither has Construction AIR retained/replacement ownership or an authoritative BRepPlan. The fact that `BrepExportPreflight` Enforce and `Step242Importer` accept both is a concrete validation gap to close before either route can be admitted.

CAD Assistant independently rejected both legacy corner artifacts at import. The user supplied the three-edge error capture; the same read failure occurred for the two-edge file. No legacy junction artifact is credited with an independent visual pass. The modern rectangular loop remains covered by its existing CAD/STEP evidence. This is a hard-admission failure for the legacy candidates and a false negative in the current internal preflight/reimport combination.

The CLI planar volume result was not used for admission: a 10×8×6 control box reports 160 rather than 480, so the current signed-shell volume path is not a reliable comparison oracle for these fixtures.

## Recommended modern architecture

1. Feature AIR owns semantic edge-finish intent and typed selection.
2. A construction-family classifier creates `ChamferCornerContext` without raw Firmament BRep IDs.
3. Exhaustive Match enumerates only implemented candidates for that classification.
4. Each candidate planner attempts an exact localized Construction AIR witness and authoritative BRepPlan.
5. A shared hard-admission validator checks plan topology, ownership, material side, distance, intersections, exactness, and manifold closure.
6. Zero admitted plans returns a typed error or validates an explicit Concept witness.
7. One admitted plan is selected directly.
8. More than one admitted plan is initially `AmbiguousWithoutPreference`. Once fixture evidence establishes stable considerations, pass only those admitted plans to `JudgmentEngine`.
9. The selected witness alone enters BRep emission. There is no legacy fallback.

This is option **D** for JudgmentEngine: keep it available for future ambiguous cases, while Match/direct selection is the complete architecture for today's supported fixtures.

## Fillet-readiness assessment

Reusable for fillets now:

- typed corner classification;
- finite candidate enumeration;
- hard admission before ranking;
- candidate evidence and typed errors;
- retained/replacement ownership requirements;
- direct-versus-ambiguous selection mode;
- explicit Concept witness path;
- authoritative BRepPlan boundary.

Fillet-specific work still required:

- tangent arc profiles;
- cylinders, tori, rolling-ball or other blend surfaces;
- radius propagation;
- tangent/curvature continuity checks;
- fillet-specific self-intersection and setback construction.

The architecture is ready for a **single bounded fillet construction with no interacting junction**. It is not mature enough to claim a corner-bearing fillet milestone: even chamfer two-edge and trihedral junctions lack authoritative localized BRepPlans, and no ambiguous preference case is proven.

## Decision record

**Keep:**

- `JudgmentEngine` as the repository's deterministic utility ranker;
- `ChamferLoweringResult<T>` as the Feature AIR → Construction AIR result boundary;
- direct/switch selection for canonical and closed structural cases;
- legacy chamfer experiments as regression and negative evidence;
- explicit Concept witnesses for cases automatic lowering cannot prove.

**Change:**

- move all correctness predicates into hard admission;
- make construction witnesses and authoritative BRepPlans prerequisites for candidate admission;
- make adjacent-junction deferral say what is actually missing;
- fail trusted orthogonal legacy corner requests before topology rather than throwing;
- treat internal Enforce/reimport success as insufficient when independent OCCT import fails;
- use `JudgmentEngine` only on an admitted candidate list if that list ever has size greater than one.

**Reject:**

- scoring `accept`, `reject`, `defer`, and `fallback` as peer candidates;
- always-admitted synthetic reject candidates;
- the unproved assertion that miter, setback, and explicit patch are all valid;
- speculative policy enums without geometry fixtures;
- metadata-only success or silent legacy fallback;
- utility penalties for manifoldness, support compatibility, material side, distance, intersection, ownership, exactness, or plan boundedness.

**Defer:**

- a utility weight model;
- `PreservePrimaryEdge` / `PreserveLargestSupportFace` / symmetry preferences;
- asymmetric and nonuniform junctions;
- recovered/imported topology corner resolution;
- concave trihedral construction;
- Firmament corner-policy syntax;
- corner-bearing fillets.

**Next implementation milestone:**

`AIR-CHAMFER-LOCALIZED-PLAN-A1` — implement one history-known planar localized replacement as Construction AIR plus authoritative BRepPlan, first for a single straight convex edge with explicit end ownership; then extend that same plan to the two-edge convex junction and materialize at least two policies only if the geometry genuinely supports them. Add plan-derived hard-admission validators and CAD Assistant smoke before introducing utility ranking.

## Validation record

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 0 errors; existing C#/xUnit and JavaScript package-audit warnings remain.
- Core requested filter (`Judgment|Utility|Corner|Chamfer|BrepPlan|Air`): 113 passed, 0 failed.
- Firmament requested filter (`Corner|Chamfer|Air|Concept|Construction|Step`): 249 passed, 0 failed.
- CLI requested filter (`Corner|Chamfer|Air|Step|Analyze`): 221 passed, 4 failed. All four are ordinary box volume assertions: expected 480/576/672-family volumes are reported at one third of the expected value. The failure reproduces sequentially and matches the A0 control-box observation. A0 does not touch volume analysis; this is recorded rather than hidden.
- Focused A0 resolver/legacy evidence tests and `ChamferM6LoweringTests`: passed.
- Modern rectangular loop fixture built through the real CLI path; authoritative BRepPlan, Enforce export, Aetheris STEP reimport, and changed topology passed.
- Both legacy junction STEP artifacts passed Aetheris Enforce/reimport but failed CAD Assistant import. They remain hard-rejected and expose an internal validation false negative.
