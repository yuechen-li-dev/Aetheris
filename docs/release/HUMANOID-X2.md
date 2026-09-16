# HUMANOID-X2 — constrained anatomical kinematics

**Verdict: Meaningful progression. Not Accepted.**

Aetheris now has a separate constrained hip/elbow/knee research path with
typed interfaces, requested/solved states, deterministic rejection/projection,
rigid-link residual checks and solve-before-skin evaluation. It has **not**
replaced every unconstrained character-rig path or produced an accepted
Antonia humanoid. The real Antonia witness isolates the next blocker:
valid constrained hip flexion still creates catastrophic skin distortion.

## What was removed, and what blocks convergence

Previously, the research path could apply an arbitrary quaternion without a
joint-domain check. The new path rejects impossible hinge/hip coordinates,
supports explicit projection, refuses unsupported joints, and binds an
immutable solved state to its rest skeleton and shape revision. Synthetic
hip/knee and elbow mechanisms prove expected endpoint motion, not only zero
self-reported residuals. All 55 candidate joints are retained; no new
canonical joint, scapular proxy or control-helper influence is invented.

At unchanged **70° hip flexion**, constrained evaluation reproduces X1's
**16.44×** bidirectional edge ratio and **12** normal-reversal flags. At **90°**,
the constrained hip is solved but the surface reaches **17.23× / 55 flags**.
This rules out impossible requested angle, pose-induced dislocation and
pose-induced bone stretching as sufficient explanations. It does **not** rule
out a badly located rest joint center. Rest preparation, center calibration,
generated weights and LBS remain unresolved causes. No corrective deltas,
face edits or reduced flexion limit conceal the failure.

The worst edge joins `antonia:v:008100` (Pelvis) to `antonia:v:031783`
(LeftThigh): **11.7887804 mm → 0.7168677 mm** at hip70. The 16.444849×
metric is therefore severe **compression**, not outward stretching. Its
endpoint hip weights are 0.3416352 and 0.5112091; the remaining influence is
Pelvis. This is a concrete blend-transition witness, not proof that painting
alone will fix it. The metric intentionally captures compression and stretch.

Further surface tuning without a calibrated anatomical reference would
conflate those causes. This progression stops at that evidenced boundary
instead of claiming a completed shoulder mechanism or promoting failed skin.

## Interface and concept status

The [public API guide](../public/humanoid-kinematics.md) specifies equations,
participants, DOFs, engineering ROM, tolerances, solve policies and diagnostics.
Typed domain interfaces follow existing Aetheris mechanical validation
patterns; no generic compiler `Concept<T>` / `Interface<T>` family was added.

Implemented: hip coupled swing/twist domain, elbow/knee hinges, finite and
revision checks, hierarchy and side checks, rest/bind consistency, all-link
length and parent-socket coincidence checks, immutable solve result, surface
screening, deterministic replay. No competing strategy is selected, so
JudgmentEngine is not inserted into deterministic projection math.

Not implemented: compound shoulder, scapulohumeral coupling, pelvis/chest
collision proxies, wrist/ankle/neck/spine/finger domains, full anatomical
pose-concept admission, surface self-intersection/volume, surface influence
recomputation, migrated landmarks/attachments/morphs/measurements, production
runtime migration. Unsupported explicit requests return `HUM209` with no pose.
Legacy `HumanoidPosing` remains necessary for X1 preparation/comparison and can
bypass constraints; this alone prevents overall acceptance.

## Antonia witness

All retained poses are finite and have zero collapsed triangles. Across 918
per-link observations, maximum center residual is 0 mm and maximum link-length
error is 0.00006103515625 mm, below the 0.001 mm tolerance. Values below
are bidirectional edge ratios, not stretch-only values. Per-region
median/p95/p99/max, face IDs, per-link residuals, requested/solved coordinates
and diagnostics are in local `artifacts/local/humanoid-x2/evidence.json`.

| Pose | Solved | Max edge ratio | Reversal proxies | Surface screen |
|---|---|---:|---:|---|
| Neutral | Yes | 1.00 | 0 | Pass, not visual acceptance |
| Hip flexion 45° | Yes | 3.48 | 0 | Pass |
| Hip flexion 70° | Yes | 16.44 | 12 | Fail |
| Hip flexion 90° | Yes | 17.23 | 55 | Fail |
| Hip flexion 120° | Yes | 11.41 | 68 | Fail |
| Hip abduction 30° | Yes | 3.72 | 0 | Pass |
| Hip abduction 45° | Yes | 5.48 | 26 | Fail |
| Hip abduction 70° | Projected to 45° | 5.48 | 26 | Fail; not a same-angle improvement |
| Elbow flexion 90° | Yes | 4.85 | 1 | Fail |
| Knee flexion 90° | Yes | 8.91 | 19 | Fail |
| Hip 90° + knee 90° | Yes | 17.23 | 74 | Fail |

The hip ellipse admits useful 90° and 120° flexion. It is not tightened to
game surface acceptance. The provisional 4× screening threshold catches
catastrophic ratios but does not certify anatomy. Historical X1 knee +X
rotation is opposite anatomical flexion in this frame; X2 corrects the hinge
direction. Historical elbow +Y rotation is also replaced by a rest-derived
forward-flexion hinge. Those are not identical-pose visual improvements.

Invalid witnesses: backward elbow −45° rejects with `HUM200`; knee −30°
projects to 0° with `HUM205`; hip abduction 160° rejects with `HUM200`;
the 120°/45°/45° hip corner projects onto the coupled ellipse with 22.5° twist.
Shoulder 90° returns `HUM209` rather than inventing a compound solution.
Joint translation and bone scaling are unrepresentable in the new request
type; arbitrary transform-import validation is not implemented.

## Visual evidence

![X1 left, X2 right, identical hip 70-degree request](../../artifacts/local/humanoid-x2/x1-left-x2-right-hip70.png)

The same-angle render confirms unchanged deformation. The groin/thigh fold
persists, and hooked shoulder contours already exist in neutral. Separate
[hip 90° surface](../../artifacts/local/humanoid-x2/hip-flexion-90.png) and
[mechanism](../../artifacts/local/humanoid-x2/hip-flexion-90.mechanism.png),
[combined pose](../../artifacts/local/humanoid-x2/hip-knee-90.png),
[elbow](../../artifacts/local/humanoid-x2/elbow-flexion-90.png) and
[knee](../../artifacts/local/humanoid-x2/knee-flexion-90.png) are diagnostic
outputs. Blender only renders Aetheris-computed positions.

Assistant visual screen: shoulder/clavicle/armpit **Fail** from unchanged
neutral contour; groin/hip transition **Fail** under flexion. Elbow/knee
numerical screens **Fail**. Wrist/ankle and all attachment semantics
**NeedsReview**. **Human review: NeedsReview, no signoff supplied.** No
surface-screen pass is presented as a human-review pass.

## Local FBX oracle

The three supplied files are inspected in place, never copied or committed.
They are ASCII FBX 7.7. The installed Blender 5.2 importer reports
`ASCII FBX files are not supported`; qualification therefore records only
lexical model/parent, geometry, cluster and animation-curve declarations plus
SHA-256. This is a hierarchy inventory, not an FBX transform, skin-weight or
posed-deformation interpretation. Detailed local inventories are under the
X2 artifact directory. No legal permission or redistribution is inferred.
No runtime or ML dependency is introduced. Each file declares 65 limb nodes,
2 geometries, 129 skin clusters and 315 animation curves. Declared parent
chains include Spine2 → Shoulder → Arm → ForeArm and Hips → UpLeg → Leg.
These names do not establish a mechanical scapula or validate our rest centers.

## Recommended next bounded milestone

Following the user's mannequin example, the recommended sequence now starts
with a **semantic segment assembly**, then authors its skin attachment field.
Lines are a useful lowering/debug view but cannot retain socket surfaces,
segment extent, exclusion volume or attachment-region semantics by themselves.

### Concept Struct / assembly feasibility

Current code already supports Concept Struct authoring, typed exposed semantic
members, reusable ordinary Struct parts, hierarchical occurrences and
Interface/Mate constraints. The live command
`dotnet run --no-build --project Aetheris.CLI -- asm inspect fixtures/Canonical/AssemblyInterfaces/executable-machine.firmament --json`
returned `success: true`; its included cell defines geometry from a Concept
Struct. Local output is `concept-assembly-feasibility.json`/`.log`.
`FirmamentV2ConceptStructStepPipelineTests` demonstrates that Concept Structs
are erased before feature AIR while semantic provenance survives. Therefore
simply naming a struct `Pelvis` is insufficient: anatomical authority must be
retained in a typed segment descriptor before lowering.

Proposed bounded architecture (not implemented in X2):

- **Pelvis segment:** reviewed spatial extent and exclusion proxy, left/right
  socket frames and supported pelvis-skin attachment region.
- **Thigh segment:** invariant head-to-knee structure, femoral-head frame,
  shaft/condylar proxy and thigh-skin attachment region.
- **Lower-leg segment:** knee/ankle frames, invariant extent and calf/shin
  attachment region.
- **Joint interfaces:** use the actual exposed participants to solve requested
  configurations, enforce socket/head coincidence, ROM and exclusions, and
  report residuals. The current Assembly compiler supports Fixed/Axial/Revolute
  and atomic placement relations; an admitted free rotation is not yet a
  bounded anatomical actuator. Hip ball domains and shoulder coupling require
  explicit domain extensions.
- **Continuous skin:** deform one connected Antonia mesh through segment-local
  attachment fields. Stable region/vertex IDs and reviewed weights select
  support; material/deformation constraints determine admissibility. Do not
  split flesh into rigid mesh islands or imply that a more anatomical-looking
  mannequin automatically improves interpolation.

The existing `TypedSemanticAuthorityBinding<TAuthority>` provides a retention
pattern; the Gear bridge demonstrates typed endpoints across assembly
occurrences. Reuse those seams with an anatomy-owned segment authority and
the existing pose evaluator, rather than a second parser or executor. Solved
matrices can remain the efficient skinning/compatibility output, with segment
geometry and interface semantics preserved above that lowering.

First witness: pelvis + left thigh + lower leg at neutral, hip45/70/90 and
hip90+knee90. Show segment volumes, socket/head frames and exclusion checks
before skin evaluation. Then compare the existing and reviewed attachment
fields at those exact poses. Extend to chest/clavicle/scapula/humerus only
after this three-segment path is demonstrated. This is feasible incrementally;
it is not already supplied by the current general Assembly solver.

### Attachment-field experiment

The user's marked image identifies the groin/upper-thigh crease in both
70° renders. Test **anatomically constrained hip attachments and reviewed
weights** next, with the current pose solver and deformation gates retained.

1. Review the hip center against orthogonal rest-surface sections and a visible
   pelvis/femur proxy. Center coincidence alone does not establish correct
   anatomical placement. Freeze the reviewed center before comparing weights.
2. Author three explicit regions: pelvis-anchored, femur-anchored, and a
   transition across groin/gluteal tissue. Paint influence support and boundary
   weights on the existing stable vertex IDs. Do not smooth uniformly across
   every neighboring group or use weights to move the joint itself.
3. Use existing Blender weight painting for the first experiment, exporting
   only a topology-hash-bound, normalized weight/region sidecar. Preserve
   protected regions, mirror through the existing correspondence and reject
   unknown links, negative/nonfinite weights or sum errors. Aetheris owns
   validation and evaluation; no new painting application is needed.
4. Compare old and authored weights with identical centers, LBS, geometry and
   **45°/70°/90° flexion, 30°/45° abduction, hip90+knee90**. Keep both-side
   front/side/back renders, edge percentiles/max, reversal flags and attachment
   drift. The marked crease must materially improve without transferring a
   collapse to the buttock or opposite groin. Do not relax the gates or ROM.
5. If reviewed weights still fail, record that failure before selecting a
   bounded deformation extension: constrained tangential sliding or regional
   volume preservation. Painting influences alone does not impose these
   material behaviors. Full FEM and a general tissue solver remain outside
   this experiment.

This recommendation treats painting as authoring the attachment field.
Joint admissibility and allowable surface deformation remain constraints.
Blender supports locked groups and normalization in its existing
[weight-paint workflow](https://docs.blender.org/manual/en/5.0/sculpt_paint/weight_paint/tool_settings/options.html);
that tooling is optional authoring infrastructure, not anatomical authority.

## Migration and verification

All 88 landmarks and 19 attachments remain X0-only. Antonia has no admitted
foot attachment frame, measurement protocol or bounded morph endpoints.
The shape-revision guard works, but that is not Antonia morph qualification.

Reproduce using the public guide. The existing Aetheris.CLI `--help` was run;
there is no humanoid inspection command. The existing humanoid research tool
therefore owns `constrained-antonia`; no second CAD CLI or general dynamics
solver was introduced. Generated geometry, images, detailed evidence and logs
remain under ignored `artifacts/local/humanoid-x2/`.

Fresh-agent qualification used only the public guide, candidate JSON and
exported DLL signatures. The probe rejected impossible knee/elbow/hip requests
with `HUM200`; projection returned `HUM205`. Hip90+knee90 solved without
projection with the residual maxima reported above. It correctly refused to
invent a foot attachment or compound shoulder solution (`HUM209` under both
policies). This passes bounded API understandability, not the complete X2
fresh-agent acceptance criteria. The guide's example now includes imports,
candidate deserialization and exact residual property names after that review.

Replay evidence SHA-256:
`c30b66c74849613a8912caf6ef5ddb68dfc0dd3b5215afa2c7e9503dffcd4e78`.
Timing is stored separately because it is not deterministic; observed warm
solves were sub-millisecond in the initial run, while combined surface
evaluation/validation was roughly 100–330 ms. These are measurements, not a
performance guarantee or separate skin/validation profiling.

Final validation:

- .NET SDK 10.0.401 is available. Final `dotnet build Aetheris.slnx -m:1`
  passed with 0 errors and 2 existing WebAssembly warnings.
- Focused humanoid tests: **45/45 passed**, including 15 new X2 cases.
- `qualify-humanoid-x2.ps1 -SkipRender -SkipFullTests` passed after the final
  numerical hardening. **35** evidence/OBJ/mechanism files match their replay
  byte-for-byte. The earlier Blender render and local FBX inventory completed.
- Full active solution suite (`Category!=SlowCorpus`): **3,625 passed,
  6 failed, 0 skipped**, 20 assemblies. That run preceded the final added
  extreme-finite-projection test; the final focused run above includes it.
  The full suite is **not green**.
- The six failures were four `DisplayPreparationFallbackBuilderTests`
  (accepted scaffold determinism, single-face scaffold, unsupported-body
  fallback, scaffold with hole) and two `RecognizedConstructionRecipeTests`
  (direct recipe overhead for `cir`, no meaningful runtime regression).
  Their unchanged two classes passed an isolated **17/17** recheck. No kernel
  code, test tolerance or timeout was changed; the isolated pass does not
  erase the full-run failures.
- Repository layout guard passed over 4,357 tracked files. New files follow
  existing module/test, scripts, public-guide and release-record locations.
  Existing X1 work was preserved; no raw FBX or generated mesh was staged.

Detailed logs, timings, fresh-agent probe and failed geometry remain in
`artifacts/local/humanoid-x2/`. This release record is the compact evidence;
the milestone remains Meaningful progression.
