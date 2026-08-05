# CTC01-RECONSTRUCTION-A1 — Firmament V2 reconstruction pressure test

## Verdict

Codex could reconstruct a useful, compiler-routed subset of CTC-01 with current Firmament V2, but could not reconstruct the part naturally or completely. Concept Struct was genuinely useful for exact symmetric hole scaffolding and provenance. The executable result materializes a rectangular primary-plate interval plus two exact four-hole groups through the production compiler route. It does not fake the lobed outer profile, multiple plate levels, slots, side holes, central boss, cutouts, fillets, or chamfers.

The main blocker is not missing low-level analytic geometry: Aetheris already has an internal line/arc profile emitter and specialized hole/section-transition machinery. The blocker is the missing parser-backed, production-authoritative authoring/composition route that connects named profiles and selections to those capabilities. The former M8 loop contradiction was resolved in M8-HOLE-LOOP-X1 at the producer and shared topology-policy layers; no BRep or STEP text was patched.

This milestone therefore ends in **Meaningful progression**: a real Concept-driven subset is emitted, exact repeated features are recovered, the next production blockers are isolated, and the gap structure is recorded without widening production geometry.

## Artifact identity

| Item | Value |
|---|---|
| Reference | `testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp` |
| Reference SHA-256 | `85a5752da05f53c456ca3a9e038c90358e1d5a3141d1f0d6e5f0970f2356e821` |
| Reference bytes | 396,445 |
| Reference schema | AP242 managed-model-based 3D engineering MIM LF |
| Reference file timestamp | `2021-12-21T09:14:03` |
| Reference images used | `artifacts/display-corruption-x1/ctc01-aetheris-default.png`, `ctc01-aetheris-angle-1.png`, `ctc01-aetheris-angle-2.png` |
| Reconstruction source | `testdata/firmament/reconstructions/nist_ctc_01/ctc01_reconstruction_a1.firmament` |
| Reconstructed STEP | `artifacts/reconstruction/ctc01/ctc01-reconstruction-a1.step` |
| Reconstructed SHA-256 | `202d0599248beb926cdfe55f1a1fd833b8db17bfb411871276d6ba92b3903937` |
| Reconstructed bytes | 17,945 |

The source declares `Z_source = Z_reference + 100 mm`. Current Concept `Box3` is centered in XY and fixed to `Z=0..height`; it cannot carry an origin/translation. This normalization is source-level intent, not a patched BRep.

## Reference characterization

### Directly observed facts

- One body, one shell, 117 faces, 318 edges, and 206 vertices.
- Enclosed-manifold under the ordinary STEP analyzer's coedge-incidence assessment.
- Bounds `[-400,-225,-100]..[400,225,50]` mm; envelope size `800 x 450 x 150` mm.
- Surface families: 56 planes, 57 cylinders, 4 cones, and no sphere, torus, B-spline, or other surfaces.
- AP242 validation properties embedded by the producer: volume `14,644,822.6361138 mm^3`; wetted area `807,080.802199914 mm^2`.
- Repeated planar levels include `Z=-100,-60,-50,0,5,50` mm. The maps also expose broad plateaus at `Z=-50` and `Z=0` and a central `Z=50` top.
- Four unique Z-axis centers at `(±325,±175)` have radius 17.5 mm cylinder supports and Ø35 PMI.
- Four unique Z-axis centers at `(±160,±45)` have radius 12.5 mm cylinder supports and Ø25 PMI.
- Two Y-axis centers at `X=±30, Z=-25` have R10 cylinders; four conical supports are coaxial with this pair.
- R50 Z-axis outline supports occur in the end/lobe regions; R20 supports occur at `(-325,0)` and `(-245,0)`; R10 supports form several hole, slot, transition, and finish families.
- The direct STEP support extraction used only entity relationships (`CYLINDRICAL_SURFACE`/`CONICAL_SURFACE` -> `AXIS2_PLACEMENT_3D` -> point/direction). It was analysis-only and did not edit geometry.

The current analyzer labels imported length units as assumed rather than preserved. The STEP dimensional-unit declarations and NIST fixture convention support millimetres, but that provenance limitation remains an `AnalysisGap`.

### Strong geometric inferences

- The dominant construction family is a multi-level prismatic plate/web in XY. The absence of freeform surfaces and the repeated Z planes are strong evidence.
- The four Ø35 and four Ø25 split-cylinder groups are through-hole families. The current Concept matcher finds zero center/diameter deviation but returns `Candidate`, not `Matched`, because each physical hole contributes two split analytic candidates.
- R20 supports 80 mm apart at Y=0 define a natural 40 mm-wide `−X` capsule slot.
- Four R10 centers at X `245..325`, Y `-15..15`, together with adjacent planes, define a `+X` rounded-rectangular opening candidate.
- The central feature is a raised hexagonal region reaching Z=50, with six planar sides and R5 edge supports at three in-plane orientations.
- The shape has strong paired structure about X=0 and partial paired structure about Y=0. It should use patterns/mirroring where semantics are identical, but exact global mirror symmetry is not asserted.

### Weak workflow hypotheses

- Likely robust construction order: dominant base profile and plate levels; major openings; repeated vertical holes; side holes; central raised feature; local cutouts; edge finishes last.
- The STEP does not prove the original vendor feature tree. These are reconstruction choices, not historical facts.

### Feature inventory

| Feature | Evidence and classification |
|---|---|
| Primary outer profile | Strong inference: broad prismatic web with R50 lobes, straight segments, steps, and local reliefs |
| Raised/recessed regions | Strong inference: broad Z=0/-50/-60 plateaus plus central Z=50 raised region |
| Through holes | Strong: four Ø35 and four Ø25 Z-axis groups; likely additional smaller Z groups |
| Blind holes | Uncertain; analyzer lacks cylinder-span/face-group reporting needed to distinguish every R10/R25 family |
| Slots | Strong: `−X` R20 capsule; `+X` R10 rounded rectangle/opening candidate |
| Pockets | Strong but not fully grouped: lowered planar regions and right opening/relief |
| Boss | Strong: central hexagonal raised region |
| Ears/lobes | Strong: paired end regions using R50 outer supports and Ø35 holes |
| Cutouts/notches | Strong: stepped side silhouette and relief planes; exact grouping incomplete |
| Fillets | Strong support families R5/R10; semantic chains and authorship not proven |
| Chamfers | Sloped central planar transitions and four side-hole cones observed; exact feature rules partly uncertain |
| Repetition | Exact center pairs for Ø35 and Ø25 groups; broad bilateral repetition elsewhere |

Centroid remains unsupported. It is not embedded in the reference STEP, and a live M8 reference evaluation exceeded 120 seconds. The coarse resolution-24 voxel result (`9,257,142.857 mm^3`) classified 35.7738% of samples unknown and is deliberately not substituted for the exact embedded validation volume.

## Reconstruction strategy

The primary body is naturally one explicit line/arc profile for the broad plate family, followed by stacked/local profile intervals. It is not naturally a union of boxes and cylinders. Primitive composition would create many Boolean operations, hide the named perimeter, make edge finishes selection-dependent, and still fail to describe the stepped levels cleanly.

The preferred source architecture is:

1. named global/reference frame and thickness planes;
2. one line/arc outer profile with named vertices/segments and symmetry-derived landmarks;
3. a base profile extrusion for the broad plate interval;
4. additive/subtractive local profile levels;
5. semantic hole/slot/pocket groups;
6. central raised profile;
7. semantic edge-loop/chain finishes last.

Current Firmament cannot express steps 2–7 as one production-authoritative composition. The largest honest executable subset was therefore restricted to the primary plate envelope plus two exact hole groups. This overfills the silhouette and is classified `Approximate`, not equivalent.

## Concept Struct scaffold

`Ctc01Scaffold` is parser-backed and erased before Feature AIR. It contains:

- reference and primary-plate envelopes;
- X-max, Y-max, bottom, primary-top, and reference-top planes;
- the global Z axis;
- exact four-point pattern scaffolds for `(±325,±175)` and `(±160,±45)`;
- landmark frames for outer lobes, the `(±225,±150)` family, left slot, and central boss.

The two materialized point sets are derived from centered helper boxes and 25 mm insets. The build expands both four-item patterns before Feature AIR and retains stable paths such as `concept:Ctc01Scaffold.MountHoleCenters[0]` through materialization.

Concept Struct helped in three concrete ways:

1. It made the two exact symmetric groups one semantic construction each instead of eight copied holes.
2. It kept center provenance visible in build evidence.
3. It separated measured spatial facts from materialized geometry, allowing unsupported features to remain named without being faked.

It also exposed its current limits. There is no translated frame, center plane, literal named point, arbitrary point set, line, profile landmark, symmetry relation, derived dimension, or named selection value. `Box3` helper dimensions had to be invented solely to obtain `Grid` points. Also, the matcher treats every scaffold `Box3` as a whole-body-bounds assertion, producing conflicts for landmark boxes; semantic roles are only available for hole-center point sets.

## Firmament source architecture and compiler route

The executable source has one materialized struct:

```text
Concept Struct Ctc01Scaffold
  -> PrimaryPlateEnvelope
  -> MountHoleCenters (4)
  -> QuarterHoleCenters (4)

Struct Ctc01ReconstructionA1
  -> Box PrimaryPlateSubset
  -> Pattern MountHolePattern: four Hole<Shaft>, Ø35, ThroughAll
  -> Pattern QuarterHolePattern: four Hole<Shaft>, Ø25, ThroughAll
```

The real route observed in build JSON is:

```text
Concept IR
  -> pattern expansion before Feature AIR
  -> semantic Hole features
  -> AirHoleCompositeMaterializer / HoleProfileStack
  -> STEP serialization
  -> successful ordinary STEP reimport
```

The build reported eight semantic features, exact resolved points, eight cylindrical faces in the final artifact, and per-feature `stepReimportSucceeded=true`. No direct BRep construction, legacy substitute, inline STEP, or emitted-text editing was used.

## Stage-by-stage journal

| Stage | Result | Bounds (source coordinates) | Volume | Major features | Deviation/blocker | Source complexity |
|---|---|---:|---:|---:|---|---|
| Reference | analyzed | `[-400,-225,-100]..[400,225,50]` | `14,644,822.636` exact embedded | 117 faces / 57 cylinders / 4 cones | centroid and semantic groups unavailable | n/a |
| Concept scaffold | compiled | no materialized bounds | n/a | 2 exact point groups plus datum/landmark values | no visualization; no translations/literal points | 1 Concept Struct |
| Primary plate subset | planned/materialized as Box | `[-400,-225,0]..[400,225,100]` | `36,000,000` analytic | 1 box | severe silhouette overfill | 1 Box |
| Ø35 pattern | materialized | unchanged | `35,615,154.900` analytic | 4 holes | intended centers and diameter exact | 1 Pattern |
| Ø25 pattern | materialized | unchanged | `35,418,805.359` analytic | 8 holes total | intended centers and diameter exact | 2 Patterns |
| Slots/boss/side holes/levels | blocked | unchanged | unchanged | not reconstructed | missing profile/composition routes | omissions explicit |
| Finishes | blocked | unchanged | unchanged | not reconstructed | selection and AirFillet gaps | omissions explicit |
| Export/reimport | emitted | unchanged | analytic route available | 14 faces, 36 edges, 40 vertices | ordinary analyzer accepts; M8 rejects disconnected loops | one 17,945-byte STEP |

### Failed and bounded attempts

- A live full reference `analyze`/`verify` batch exceeded 120 seconds. Checked-in output from the same analyzer route was used for the topology/bounds baseline; direct STEP support extraction supplied feature measurements.
- Concept-to-STEP matching returned `Conflicted`: two members matched, two hole groups were zero-deviation `Candidate`, one plane was `Ambiguous`, ten helper/bounds members conflicted, and three members were unverifiable. This is useful evidence that matching needs role-scoped members and split-cylinder grouping.
- M8 verification of the emitted subset returned `BRepRejected`, listing disconnected coedges in loop 1 and loops 23–30. The ordinary reimport analyzer nevertheless returned `enclosed-manifold`. No attempt was made to edit STEP topology around the disagreement.

## Materialization matrix

The machine-readable matrix is `artifacts/reconstruction/ctc01/feature-matrix.json`.

| Reference feature | Firmament representation | Route | Status | Workaround / gap |
|---|---|---|---|---|
| Overall envelope | Concept `ReferenceEnvelope` | Concept IR only | exact size, translated Z | Box3 lacks origin |
| Multi-lobed primary plate | rectangular `PrimaryPlateSubset` | Box -> profile extrusion | Approximate | severe envelope overfill; profile front door missing |
| Four Ø35 holes | `MountHolePattern` | Pattern -> Hole AIR -> composite materializer | EquivalentWithinTolerance intent | helper Box3/Grid required |
| Four Ø25 holes | `QuarterHolePattern` | same | EquivalentWithinTolerance intent | helper Box3/Grid required |
| Left capsule slot | landmark only | Concept IR only | NotReconstructed | slot/profile cut not production-routed |
| Right rounded opening | none | none | NotReconstructed | rounded profile cut missing |
| Y-axis side holes/cones | none | none | NotReconstructed | composed side-hole route missing |
| Central raised region | landmark only | Concept IR only | NotReconstructed | additive profile and translation missing |
| Fillets/chamfers | none | none | NotReconstructed | semantic selections and production fillet/transition route missing |

## Pretzel ledger

| Pretzel | Why it was needed | Classification | Consequence |
|---|---|---|---|
| 700x400x100 helper `Box3` plus 25 mm inset | derive `(±325,±175)` because literal point sets are absent | temporary ergonomic inconvenience | preserves exact hole intent but helper volume has no design meaning |
| 370x140x100 helper `Box3` plus 25 mm inset | derive `(±160,±45)` | temporary ergonomic inconvenience | same |
| Global +100 mm Z normalization | `Box3` has no placement/origin | acceptable explicitness for subset; semantic distortion for whole-part matching | bounds comparison needs a declared transform |
| Rectangular envelope as primary plate | authored line/arc profile unavailable | semantic distortion | candidate volume is 2.419x reference and silhouette is wrong |
| Landmark `Box3` values for slots/boss | no point/line/profile-landmark types with usable constructors | temporary ergonomic inconvenience | matcher misreads them as whole-body bounds contracts |
| Duplicate materialization/reference point sets on +Z/-Z faces | hole materializer needs top-plane points while matcher needs reference Z=0 | temporary ergonomic inconvenience | provenance is clear but source repeats one XY group |
| Omitting finishes rather than choosing raw edge IDs | semantic chains are unavailable | acceptable explicitness | visual fidelity lower, intent remains honest |

No dangerous workaround was admitted. The earlier V1 checkpoint corpus's long Boolean chain was not adopted as the modern result because it would test legacy bounded Boolean behavior rather than the intended Concept/AIR route.

## Profile-path assessment

**Verdict: CTC-01 demonstrates a real need for first-class handwritten profile paths.** The primary XY outline is naturally a single named sketch/profile containing lines, exact arcs, concave relief transitions, and repeated/symmetric landmarks. The internal line/arc emitter proves the kernel representation is plausible, but there is no current parser-backed production source form for it.

A natural source representation would need only the operations demonstrated by this part:

- `MoveTo`/named start landmark;
- `LineTo`;
- exact center/radius or endpoint/radius `ArcTo` with sweep/orientation;
- tangent transition validation (a `TangentArc` convenience is valuable but not strictly required if exact arcs can be constrained tangent);
- `MirrorSubpath` for the repeated end/lobe construction where exact symmetry is confirmed;
- `Close` with deterministic closure/winding diagnostics;
- named vertices and segments so later slots, cutouts, and finishes can reference design intent;
- landmark/dimension references from Concept Struct.

This attempt does not demonstrate a need for `RepeatSubpath` or a general constraint solver. `FilletVertex` would be useful for authored R10/R50 outline corners, but should lower to explicit line/arc profile geometry with named provenance. The actual workaround introduced one oversized Box and omitted the perimeter features; trying to reproduce the outline through primitives would add many Booleans and obscure intent, so it was rejected.

## Fillet and chamfer pressure test

CTC-01 contains enough observed evidence to require a serious finish route, but the current workflow cannot select the intended chains safely:

- R5 supports around the central hexagonal region suggest an equal-radius group involving multiple in-plane directions.
- R10 supports are mixed: some are holes, some rounded opening corners, some outline transitions, and some likely finishes. Radius grouping alone is insufficient.
- R50 outer lobes are primary-profile arcs, not necessarily post-extrusion fillets.
- Four cone supports belong to two Y-axis side-hole entry transitions.
- Sloped planar faces at the central top are compatible with a section transition/chamfered profile, not necessarily local arbitrary edge mutation.

No finish was applied merely for appearance. Current top-face-loop chamfer and controlled section-transition evidence is too narrow for CTC-01's composed body, and generic AirFillet/edge-chain materialization remains non-authoritative. Concept Struct helped name approximate zones but could not yield semantic edge selections after topology-changing operations. Finish ordering would materially affect topology, so finishes must remain last.

## Pattern and symmetry assessment

The existing `Pattern` + Concept `Grid` route was the strongest part of the workflow. Two four-hole groups compiled naturally and avoided copied feature declarations. It is adequate for centered rectangular point grids on a known planar host.

Missing high-value pattern facilities are:

- literal/named point-set patterns not forced through a rectangular `Grid`;
- mirroring a semantic feature group, including its local scaffold and selections;
- pattern-local frames with translation;
- symmetry-derived profile subpaths;
- matcher grouping of split faces into one physical patterned feature.

General polar patterns were not demonstrated by CTC-01 and are not recommended solely from this case.

## Concept Struct visualization assessment

**Verdict: a visualization/debug path is high value.** It would have saved the most time during Z-frame alignment and pattern/slot landmark checking.

Concrete moments where visibility would have helped:

- showing the reference envelope and normalized primary-plate envelope together would immediately reveal the +100 mm Z transform and missing Z=100..150 source interval;
- rendering the Ø35 and Ø25 point sets over the reference would expose the correct XY centers and the wrong/default plane before matching;
- axes at `(±30,*,−25)` would distinguish side-hole intent from nearby R10 vertical features;
- construction lines joining `(-325,0)` to `(-245,0)` would make the left capsule slot obvious;
- the central boss landmark bounds and six R5 axes would show whether the inferred hexagon and its rounds agree;
- named perimeter/slot edge selections would make finish-chain gaps concrete rather than numeric.

### Proposed visualization contract

- Visible entities: points, axes, planes, frames, bounded boxes/regions, construction lines, profile landmarks/segments, pattern anchors, symmetry relations, derived dimensions, and named semantic selections.
- Layering: a non-materialized `Concept` overlay rendered over either an imported reference BRep or current materialized BRep, with separate toggles for datums, landmarks, profiles, patterns, dimensions, and selections.
- Provenance: each overlay primitive retains Concept stable ID, source span, derivation chain, erasure status, coordinate frame, and any source-to-reference transform.
- Inspection: click or CLI-query by stable name; show resolved coordinates, type, matched STEP evidence, tolerance, ambiguity, and materialized consumers. Selection must not expose raw BRep IDs as source contracts.
- Ownership: emit a CLI JSON/DisplayIR overlay artifact first, consumed by the Aetheris-native viewer/CAD Assistant. Do not encode compile-time scaffolding as authoritative STEP construction geometry by default. Optional STEP presentation/construction export may follow only as a diagnostic derivative.
- Minimum feature: render named Concept points/axes/planes/boxes and their labels over an imported or built body from one build/match JSON artifact. For CTC-01, point and plane overlay plus the declared Z transform would have delivered most of the value.

## Human CAD workflow assessment

The `docs/llm-cad-strategy` workflow materially helped. Its most useful instruction was to separate geometry inventory from modeling strategy and prefer a prismatic blockout/semantic dependency graph over copying final faces. It correctly prevented premature finish authoring and an arbitrary vendor-tree claim.

| Documented instruction | Classification | CTC-01 result |
|---|---|---|
| establish geometry inventory before strategy | directly useful | produced bounds, families, levels, and repeated axes before source |
| use prismatic blockout first | directly useful, with a boundary | established XY/Z family, but a full Box was too coarse for final base intent |
| distinguish fact/inference/hypothesis | directly useful | prevented R10 supports and feature order from being presented as facts |
| build a semantic dependency graph | useful after translation into Concept Struct | informed scaffold and finish-last ordering |
| profiles are constructive regions | directly useful | correctly identified the missing primary authoring route |
| holes are semantic features | directly useful | enabled exact Pattern + Hole AIR construction |
| finishes last and select by semantic role | directly useful but unsupported by current selections | finishes were honestly omitted |
| use visual silhouette to choose base feature | too visual or implicit for an LLM unless converted to sections/maps | screenshots helped, but exact outline could not be transcribed reliably |
| inspect section changes after each feature | unsupported by current comparison tools | section generation exists; loop correspondence/deviation does not |
| infer manufacturing access/tooling | useful narrative, weak as geometry evidence | supported feature ordering but not exact dimensions |

Direct answers:

- It helped identify the dominant base feature: yes, a multi-level prismatic/profile construction.
- It helped establish dimensions and symmetry: partially; the analysis tools and direct analytic supports supplied exact values, while the narrative supplied the questions.
- It helped order construction: yes, especially holes/features before finishes.
- It reduced blind trial and error: yes; it prevented a large primitive/Boolean detour.
- Instructions depending on unavailable visual judgment: tracing the precise silhouette, recognizing which R10 surfaces belong to one chain, and choosing feature ownership from appearance alone.
- Instructions that should become machine-readable: units/provenance check, analytic grouping, plane-level extraction, symmetry scoring, pattern candidate tables, profile/section extraction, and stage comparison.
- Instructions that should remain narrative: robustness heuristics, intent-vs-history caution, manufacturing plausibility, and when not to force a feature interpretation.

### LLM-focused reconstruction protocol

1. Hash the artifact; read STEP units, validation properties, and schema provenance.
2. Run bounded topology/surface analysis; extract exact planes, axes, radii, spans, and principal levels.
3. Group coaxial/coplanar supports and score repetition/symmetry. Keep facts, inferences, and workflow hypotheses separate.
4. Generate principal sections and a candidate profile/feature table before source authoring.
5. Declare the global frame, explicit source/reference transform, datums, levels, points, axes, regions, and pattern roles in Concept Struct.
6. Match only role-bearing scaffold members against reference evidence; resolve ambiguities before materialization.
7. Reconstruct the dominant authored profile/stack; reject a primitive blockout if it materially distorts intent.
8. Add major additive levels and semantic holes/slots/pockets using patterns or templates.
9. Compare bounds, exact/embedded mass properties, sections, axes, diameters, and feature correspondence after each stage.
10. Apply semantic loop/chain finishes last, only when radius/rule and ownership are established.
11. Export, reimport, run M8, and treat analyzer/verifier disagreement as a blocker.
12. Inspect reference and candidate with the same external viewer; archive hash-tied overlay and screenshot evidence.

## Analysis and comparison tool audit

| Missing ability | Specific CTC-01 obstacle it solves | Priority |
|---|---|---|
| coaxial-cylinder grouping with axial span and split-face merge | turns 8 R17.5 faces into four Ø35 holes and removes matcher `Candidate` ambiguity | High |
| coplanar-face grouping and named Z-level report | distinguishes plate levels and additive/subtractive intervals | High |
| profile/section loop extraction with line/arc primitives | supplies the primary outer profile and left/right opening contours | Blocker |
| section-loop correspondence/deviation | validates each reconstruction stage without global visual judgment | High |
| symmetry/repetition scoring | separates exact four-hole patterns from only approximate silhouette repetition | High |
| tangent-chain and radius-role grouping | distinguishes R10 holes, slot corners, outline arcs, and fillets | High |
| feature candidates with confidence/provenance | prevents raw surface inventories from masquerading as features | High |
| centroid and bounded exact/numerical mass properties | completes required comparison without relying on producer PMI | Medium |
| point-to-surface deviation | quantifies local silhouette/level error after partial reconstruction | Medium |
| role-scoped Concept matching | prevents landmark helper `Box3` from being compared as whole-body bounds | High |
| cached/local map queries | avoids repeated multi-minute whole-part runs | Medium |

This is intentionally not a generic computer-vision roadmap. Every row maps to a failure observed in this part.

## Reference versus reconstruction

| Item | Reference | Reconstruction | Classification |
|---|---:|---:|---|
| Units | mm | mm | EquivalentWithinTolerance, analyzer provenance caveat |
| XY bounds | `[-400,-225]..[400,225]` | same | EquivalentWithinTolerance |
| Z bounds | `[-100,50]` | `[0,100]` | Approximate; normalized and missing raised interval |
| Volume | `14,644,822.636 mm^3` | `35,418,805.359 mm^3` | Approximate; +20,773,982.723, ratio 2.4185 |
| Surface area | `807,080.802 mm^2` | `1,033,774.331 mm^2` analytic | Approximate; ratio 1.2809 |
| Centroid | unavailable | `[0,0,50]` by symmetry | Unsupported comparison |
| Bodies/shells | 1/1 | 1/1 | EquivalentWithinTolerance |
| Planes/cylinders/cones | 56/57/4 | 6/8/0 | Approximate |
| Four Ø35 axes/diameters | exact centers above / 35 | same | EquivalentWithinTolerance intent |
| Four Ø25 axes/diameters | exact centers above / 25 | same | EquivalentWithinTolerance intent |
| Slots, boss, side holes, cutouts | present | absent | NotReconstructed |
| Fillets/chamfers | present/inferred groups | absent | NotReconstructed |
| Major section deviation | no correspondence tool | no result | Unsupported |
| Point-to-surface deviation | no tool | no result | Unsupported |

The candidate is not geometrically equivalent as a whole and is not claimed to be. Its value is that the eight repeated holes are exact semantic reconstructions through the intended modern route.

## M8 verification evidence

| Gate | Status | Evidence |
|---|---|---|
| CompilerVerified | yes | build success; Concept IR resolved and erased; two patterns expanded; eight semantic holes lowered |
| BRepVerified | **no** | `BRepRejected`; disconnected coedges in loop 1 and loops 23–30 |
| SerializationVerified | yes | STEP written, SHA tied, ordinary importer succeeded, 1 body/1 shell/14 faces |
| ExternalInspection | `ExternalInspectionPending` | no candidate CAD Assistant inspection was completed |

The reference has existing Aetheris visual-house-call evidence showing a recognizable imported CTC-01 using mixed analytic + bounded mesh fallback. That is not evidence that the reconstruction looks equivalent.

## Ranked gap ledger and next milestones

The full ledger is `artifacts/reconstruction/ctc01/gap-ledger.json`.

1. **M8-HOLE-LOOP-X1 — Blocker.** Reconcile the ordinary analyzer's enclosed-manifold result with M8 disconnected-loop rejection for a parser-backed multi-hole artifact. Do not broaden geometry; establish one authoritative loop-connectivity contract.
2. **CTC-PROFILE-X1 — Blocker.** Add a parser-backed named line/arc profile source form routed to the existing resolved-profile/line-arc emitter, with closure, winding, tangent validation, names, and provenance. This milestone is not authorized here.
3. **CTC-COMPOSE-X1 — Blocker.** Admit composition of one authored profile body with local profile levels and semantic cuts/additions through Feature AIR/Construction AIR/BRepPlan.
4. **ANALYZE-GROUP-X1 — High value.** Group coaxial split cylinders, coplanar faces, spans, hole/slot candidates, and exact line/arc section loops; integrate with matcher roles.
5. **CONCEPT-SPATIAL/VIZ-X1 — High value.** Add translated frames, literal named points/point sets, center planes, lines/profile landmarks, and a provenance-rich overlay artifact/viewer layer.
6. **SEMANTIC-LOOPS-X1 — High value.** Name face-boundary loops and deterministic chains from authored profiles/features so CTC finishes need no raw edge identity.
7. **COMPARE-SECTIONS-X1 — High value.** Add section-loop correspondence and bounded deviation reports, then point-to-surface deviation for final audit.
8. **SIDEHOLE-COMPOSITE-X1 — Medium.** Only after composition is stable, admit the observed paired Y-axis shaft/conical entry family on a composed host.
9. **LLM-RECON-PROTOCOL-X1 — Medium.** Rewrite the human workflow as the explicit evidence-driven protocol above, keeping judgment heuristics as narrative.

Fillet expansion should follow semantic profile/selection support, not precede it. CTC-01 does not justify a generic arbitrary-edge mutation system.

## Machine-readable evidence

- `artifacts/reconstruction/ctc01/reference-analysis.json`
- `artifacts/reconstruction/ctc01/reconstruction-plan.json`
- `artifacts/reconstruction/ctc01/feature-matrix.json`
- `artifacts/reconstruction/ctc01/gap-ledger.json`
- `artifacts/reconstruction/ctc01/comparison-report.json`
- `artifacts/reconstruction/ctc01/verification-report.json`

## Commands exercised

The pressure test used the current CLI front door for help, build, analyze, match, compare, and verify. Important invocations included:

```text
dotnet run --project Aetheris.CLI -- --help
dotnet run --no-build --project Aetheris.CLI -- build testdata/firmament/reconstructions/nist_ctc_01/ctc01_reconstruction_a1.firmament --out artifacts/reconstruction/ctc01/ctc01-reconstruction-a1.step --json
dotnet run --no-build --project Aetheris.CLI -- match testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp testdata/firmament/reconstructions/nist_ctc_01/ctc01_reconstruction_a1.firmament --linear-tolerance 0.01 --angular-tolerance 0.1 --json
dotnet run --no-build --project Aetheris.CLI -- analyze artifacts/reconstruction/ctc01/ctc01-reconstruction-a1.step --json
dotnet run --no-build --project Aetheris.CLI -- analyze compare testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp artifacts/reconstruction/ctc01/ctc01-reconstruction-a1.step --approximate-volume --resolution 24 --json
dotnet run --no-build --project Aetheris.CLI -- verify artifacts/reconstruction/ctc01/ctc01-reconstruction-a1.step --evidence-dir artifacts/reconstruction/ctc01/verification --json
```

No production geometry feature, parser feature, STEP importer/exporter behavior, or BRep topology code was changed by this milestone.

## Repository validation

- `dotnet restore Aetheris.slnx`: passed.
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: passed with 58 pre-existing warnings and no errors.
- Core filtered tests: 271 passed, 1 failed. The failure is `Step242HoleSemanticsTests.ImportBody_MultiLoopSphericalFace_ReturnsUnsupportedSurfaceForHolesValidationFailure`; the current importer accepts a case the test expects to reject. This is in the pre-existing modified STEP importer area and was not changed for this audit.
- Firmament filtered tests: 497 passed, 2 failed. Failures are `FirmamentV2StaticLogicTests.Match_NonExhaustive_ReportsEveryMissingVariant` and `EnumDuplicate_InvalidScrutinee_AndSelectedFailureAreDiagnosed`; both are in the pre-existing modified parser area and were not changed for this audit.
- CLI filtered tests: 128 passed, 0 failed.
- Final reconstruction build: passed and reproduced SHA-256 `202d0599248beb926cdfe55f1a1fd833b8db17bfb411871276d6ba92b3903937`.
