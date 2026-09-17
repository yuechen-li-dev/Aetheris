# HUMANOID-RERIG-X1

**EXPERIMENTAL GOLDEN-PATH STUDY â€” Useful but still insufficient**

Branch: `experiment/humanoid-blender-rerig`. Starting checkpoint: `34fc13a6`.

Ordinary Blender tooling produces a much better, portable development candidate than X5, but this cleanup does **not** establish the requested fully qualified golden path. Hip70's one reversal proxy is removed, and shoulder90 maximum distortion improves from 10.48 to 7.10. Hip70's maximum distortion actually worsens from 4.23 to 4.47. High-angle folds remain, including 39 reversal proxies at hip90. The unchanged Aetheris evaluator accepts 10 of 19 surfaces, although all 19 kinematic requests solve. This is meaningful progression: portable import and independent replay work; high-angle skin compatibility is now the isolated blocker. Neither canonical acceptance nor a production-quality body is claimed.

## Scope and authority

The experiment retains all 55 X5 semantic joints, hierarchy, reference frames and corrected centers. No intentional joint edits, topology changes, helper bones, corrective shapes, Rigify, or core runtime changes. X2 constraints and X3 screens remain intact. Only the research executable gains an explicit weight-import command. The surface remains `aetheris.humanoid.antonia.adoption-candidate.v1`; calling this experiment `adult.standard.v1` would misstate its present identity.

The golden-named file is the best retained **candidate**, not a declaration of acceptance. It contains only canonical Antonia source, the X5-derived rig and cleaned mesh. Mixamo and Genesis 9 are absent from it.

## First reversal and cleanup

The X0 hip70 reversal is triangle index **15278**, stable face `antonia:f:008767:t0`, region Pelvis, vertex indices **7879 / 24612 / 12124**, stable IDs `antonia:v:010549`, `antonia:v:032231`, `antonia:v:016323`. Its transported-normal cosine is -0.0054042. It lies at the front hip crease, not at the separate worst-compressed crotch edge.

| Influence | Vertex 7879 | Vertex 24612 | Vertex 12124 |
|---|---:|---:|---:|
| LeftHip | .414285 | .510949 | .547186 |
| Pelvis | .209749 | .223748 | .157378 |
| SpineLower | .346542 | .256388 | .271734 |
| SpineMid | .029424 | .008915 | .023702 |

The triangle straddles a thigh/torso weight transition. A bilateral soft reduction of existing hip influence removes this reversal, without naming the triangle in the cleanup code or changing geometry. Full adjacency, positions and weight neighborhoods are recorded in local `hip70-reversal-diagnosis.json`. This is a local weight-field fix with a measured tradeoff, not a solution to every hip fold.

The retained recipe in `scripts/humanoid-rerig-x1.py` is:

1. Generate Blender automatic weights from the X5 skeleton. Temporarily scale mesh and armature data by 10 for the heat solve, then restore stored coordinates and frames. Meter-scale heat failed in X0; this workaround is reproduced, not hidden.
2. Normalize. Within 220 mm of each hip center, multiply that hip's existing weight by `1 - 0.15*s`, with `s=max(0,1-(distance/radius)^2)^2`; normalize again.
3. Native Smooth, factor 0.5, **one** iteration, all groups restricted to vertices within 110 mm of either knee; normalize.
4. Native Smooth, factor 0.5, **one** iteration, within 100 mm of either elbow; normalize.
5. Soften existing weight contrast within 180 mm of either shoulder: exponent `1 - 0.5*s`; apply to each existing positive influence, then normalize. This is a scripted local Levels-like operation, not an autorigger or weight solver.
6. Export normalized weights ordered by stable vertex and bone IDs, retaining at most the naturally generated six influences. Evaluate ordinary linear blend skinning.

The masks and brush operations are bilateral. They do not force exact mirror identity onto the slightly asymmetric automatic field. All existing influences are inspected through region summaries, edge weight gradients and opposite-side influence statistics in `weight-inspection.json`; no broad reassignment to unrelated bones was introduced. Mean influence count is 2.467, maximum six, zero unweighted vertices; p95 adjacent-edge weight L1 difference is 0.315, maximum 1.268. This is not a claim that all gradients are smooth.

## Bounded alternatives and effort

The recorded search comprises 28 native smoothing/gain variants, five hip-local brush variants, 16 local contrast variants, and influence limits 2/4/8. Broader hip smoothing and strong inner-thigh reassignment did not consistently improve high-angle behavior. Limiting to two influences makes hip70 maximum distortion 66.6 with 49 reversals; four gives 4.74 with 12 reversals. Eight leaves the natural six-influence field unchanged. Six is retained instead of imposing a four-weight runtime shortcut.

One knee smoothing pass improves knee45 maximum distortion (4.60 to 3.75), but barely changes knee90 and adds one proxy reversal. One elbow pass removes one elbow90 proxy while slightly worsening its maximum distortion. Shoulder contrast softening supplies the largest new benefit. These limitations are explicitly retained in the comparison rather than selecting only favorable statistics.

X0 already tested native preserve-volume skinning and Corrective Smooth: their improvements in some regions came with worse knee or other folds. X1 adds no helper or pose corrective: there is no ablation evidence that a twist helper fixes these flexion folds, and adding runtime corrective machinery would exceed this bounded weight-cleanup result. This is not proof that every possible DCC edit has failed. A focused artist or one bounded corrective remains a plausible next experiment.

Manual paint strokes: **0**. Manual per-vertex assignments: **0**. Artistic sculpting: **0**. Cleanup consists of the five scripted local operations above. Approximate agent effort is under an hour for diagnosis, bounded trials, reference inspection, import and qualification; it is not a measured human painting time. Final build including the 19 overview renders took approximately **17.6 seconds** on this machine. Reproduction cost is distinct from investigation cost.

## Visual qualification

The local `comparison.html` contains X5 / X0 automatic / X1 cleaned / Mixamo, plus Genesis 9 where available. X5, X0, X1 and Mixamo use the same orthographic camera. Front/side/rear closeups accompany the difficult X1 poses. Overview silhouette alone is insufficient to qualify the groin or hinge crease.

[Open the local comparison gallery](../../artifacts/local/humanoid-rerig-x1/comparison.html). These links resolve after generating the ignored local evidence; reference images are not distributed with this report.

![Hip70 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--hip-flexion-70.jpg)
![Hip90 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--hip-flexion-90.jpg)
![Abduction45 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--hip-abduction-45.jpg)
![Knee90 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--knee-flexion-90.jpg)
![Shoulder90 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--shoulder-abduction-90.jpg)
![Elbow90 comparison](../../artifacts/local/humanoid-rerig-x1/comparison--elbow-flexion-90.jpg)

| Pose/region | X1 review | Observation |
|---|---|---|
| Neutral | Pass | Rest reconstruction retained |
| Hip30 | Pass | Continuous pelvis/thigh |
| Hip45 | NeedsReview | Crease remains visible |
| Hip70 | NeedsReview | Sane connected silhouette, much less X5 webbing; compressed crease remains despite zero proxy reversals |
| Hip90 | Fail | Pronounced inner-thigh/crotch folded bulge in closeup; not qualified as a usable high-angle baseline |
| Abduction15 / 30 / 45 | Pass | Leg separation and pelvis continuity retained; no reversal proxies |
| Knee45 | NeedsReview | Improved transition, still shaped by a simple hinge |
| Knee90 | NeedsReview | Limb connected; pointed back-of-knee compression and 27 proxies remain |
| Elbow45 | NeedsReview | Continuous, limited volume qualification |
| Elbow90 / 120 | NeedsReview | Sharp angular crease/mound; no limb separation, but local stress persists |
| Shoulder30 | Pass | No gross collapse |
| Shoulder60 | NeedsReview | Better broad transition; not commercial deltoid/clavicle behavior |
| Shoulder90 | NeedsReview | Material improvement; still pinched axilla, nine proxies, no scapular coupling |
| Combined stride | NeedsReview | Coherent whole-body stance; local folds retained |
| Combined raised-arm reach | NeedsReview | Coherent reach; inherits elbow stress |
| Combined balance/kick | NeedsReview | Coherent silhouette; inherits knee stress |
| Wrists / ankles | NeedsReview | Carried continuously through this corpus; independent wrist/ankle extremes were not qualified |

These are assistant visual reviews, not artist signoff. Hip90 is admitted by the kinematic solver, so its failure cannot be hidden by claiming an out-of-range request. The evidence isolates a skin-weight/high-angle fold limitation, with collision/contact and lack of pose-dependent volume correction possible contributors. It does **not** prove a canonical topology defect; topology remains untouched.

## Metrics

Distortion is `max(edge length ratio, reciprocal ratio)` on canonical edges. Compression is the minimum posed/rest edge ratio. Reversal means a negative dot product against the dominant-influence transported rest normal; it is a proxy, not certified triangle inversion or self-intersection. Hip90 cosines reach -0.734, knee90 -0.978, shoulder90 -0.982 and elbow90 -0.848: the residual counts cannot all be dismissed as floating-point sign noise. No degenerate triangles were found. Certified self-intersection/contact testing was not performed. Local JSON also includes stretch percentiles, area ratios, reversed face IDs and worst edges.

| Pose | X0 max / reversals | X1 p50 | p95 | p99 | max | min compression | reversals |
|---|---:|---:|---:|---:|---:|---:|---:|
| neutral | 1.001 / 0 | 1.000 | 1.000 | 1.000 | 1.001 | 0.999 | 0 |
| hip-flexion-30 | 1.599 / 0 | 1.000 | 1.027 | 1.140 | 1.595 | 0.627 | 0 |
| hip-flexion-45 | 2.291 / 0 | 1.000 | 1.039 | 1.219 | 2.269 | 0.441 | 0 |
| hip-flexion-70 | 4.228 / 1 | 1.000 | 1.055 | 1.373 | 4.468 | 0.224 | 0 |
| hip-flexion-90 | 9.443 / 40 | 1.000 | 1.061 | 1.497 | 9.352 | 0.107 | 39 |
| hip-abduction-15 | 1.440 / 0 | 1.000 | 1.009 | 1.079 | 1.426 | 0.818 | 0 |
| hip-abduction-30 | 1.861 / 0 | 1.000 | 1.019 | 1.166 | 1.834 | 0.640 | 0 |
| hip-abduction-45 | 2.253 / 0 | 1.000 | 1.028 | 1.259 | 2.213 | 0.483 | 0 |
| knee-flexion-45 | 4.695 / 0 | 1.000 | 1.000 | 1.020 | 3.749 | 0.267 | 0 |
| knee-flexion-90 | 5.457 / 26 | 1.000 | 1.000 | 1.036 | 5.417 | 0.185 | 27 |
| shoulder-abduction-30 | 2.168 / 0 | 1.000 | 1.000 | 1.037 | 1.530 | 0.654 | 0 |
| shoulder-abduction-60 | 9.631 / 6 | 1.000 | 1.000 | 1.083 | 3.807 | 0.263 | 0 |
| shoulder-abduction-90 | 10.478 / 21 | 1.000 | 1.000 | 1.136 | 7.102 | 0.141 | 9 |
| elbow-flexion-45 | 5.082 / 0 | 1.000 | 1.000 | 1.003 | 3.486 | 0.287 | 0 |
| elbow-flexion-90 | 4.074 / 15 | 1.000 | 1.000 | 1.007 | 4.124 | 0.243 | 14 |
| elbow-flexion-120 | 4.866 / 23 | 1.000 | 1.000 | 1.009 | 5.072 | 0.197 | 22 |
| combined-stride | 5.154 / 0 | 1.000 | 1.055 | 1.216 | 4.181 | 0.239 | 0 |
| combined-raised-arm | 9.631 / 27 | 1.000 | 1.018 | 1.187 | 4.124 | 0.243 | 14 |
| combined-balance | 5.456 / 27 | 1.000 | 1.090 | 1.420 | 5.417 | 0.185 | 27 |

## Reference benchmarks

Mixamo remains the same local FBX oracle as X0: `Female Standing Pose (2).fbx`, SHA-256 `53038e7c78816b25df1bb2412d33b8d2d1530de5f02a785352c529dd2de376d9`. X0's mapping and weight observations remain applicable; X1 reuses its matched-pose renders. It shows that ordinary auto-rigging improves X5, but does not establish a uniformly superior high-angle solution. X1 does not import its weights, helpers or mesh into the candidate.

Genesis 9 was locally available. A **separate headless DAZ instance** loaded the installed base figure and evaluated neutral, hip70, hip90, abduction45, knee90, shoulder60, shoulder90 and elbow90. The user's interactive scene was left alone. `benchmark-genesis9-local.dsa` records actual native control values and exports evaluated local reference meshes for matched-camera rendering. Native controls used thigh X Bend -70/-90, thigh Z Side-Side +45, shin X Bend +90, upper-arm Z Bend +60/+90, forearm Y Bend -90. Different rest poses and axes make this an approximate quality comparison; particularly, shoulder90 does not have identical world-space arm orientation. No topology or vertex-difference comparison is claimed.

Genesis shows broader, rounder transitions and illustrates a mature rig's separate twist chains (two segments in each thigh, upper arm and forearm). That observation does not prove those helpers cause superior bending: no helper/corrective ablation was performed, and proprietary JCM payloads were not inspected or copied. The remaining gap is substantial: high-angle fold shape, volume, coordinated shoulder motion and contact handling. Genesis quality is not the acceptance threshold, and no DAZ runtime dependency is introduced.

All raw reference exports, inventories and images stay in ignored local output. No Mixamo/DAZ raw data is embedded in this report, source, or final Blender file. No ML training or service probing occurred. Any later proposal to promote reference-derived data requires a separate license review.

## Portable artifacts and validation

Local output root: `artifacts/local/humanoid-rerig-x1/`.

- `golden.blend`: `ANTONIA_CANONICAL`, `X5_DEFORM_RIG`, `GOLDEN_DEFORMATION`; embedded compressed canonical surface, reference skeleton, corpus and edit history. No external oracle objects.
- `golden-weights.json`: `aetheris.humanoid.experimental-weights.v1`, stable vertex IDs, canonical bone IDs, sorted normalized weights, input rig hash and generation provenance. SHA-256 `f117189857e6c54be6f58b08b4d2847322ebca8fb4230ecf8328b43c5004ae29`.
- `golden-skeleton.json`: complete X5 reference plus empty `deformationRigExtensions`.
- `pose-corpus.json`: 19 explicit semantic requests including three combined poses; angles are translated through the existing frame convention, not eyeballed.
- `metrics.json`, `integrity.json`, `weight-inspection.json`, per-pose positions, overview and closeup renders, and `comparison.html`.
- `aetheris/evidence.json`, `aetheris/parity.json`, per-pose diagnostic/screened OBJ files and ordered positions.

Before/after: **27,193 vertices, 54,224 triangles**, exact ordered triangle indices and stable vertex IDs unchanged. Connectivity hash `2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2`. Candidate SHA-256 `af45c535ab21d93ae2c23f7a63f71925f3c6cfc6a68e5af9c04e1ab0033834dd`; X5 reference SHA-256 `0647f681f67043610e8829fff20ca29901648b3c3245b589f2942ba453e13148`.

Blender neutral maximum residual **0.000365 mm**; normalized field error at floating-point precision; finite transforms. Maximum frame-element round-trip difference is **3.4124e-6**, reflecting Blender storage precision, not an intentional skeleton adjustment. Aetheris inverse-bind/neutral reconstruction maximum **0.000679 mm**; maximum joint-center residual **0**, link residual **0.000126 mm**.

An independently assigned import agent consumed only the portable weights and corpus through existing `AntoniaReferenceRigAdapter`, `HumanoidKinematicSolver` and `HumanoidConstrainedSurface`. **19/19 solves**, **10/19 surface-screen passes**, zero collapsed triangles. Nine failures remain diagnostic exports: hip70, hip90, knee90, shoulder90, elbow90, elbow120, and all three combined poses. Maximum Blender/.NET vertex-position difference **0.0011472 mm**, RMS per pose **0.000202â€“0.000325 mm**. This proves runtime portability, not deformation acceptance.

Import rejects mismatched topology/rig hash/IDs, duplicate IDs/bones, unknown bones, nonfinite/negative/unnormalized weights, unsupported extensions and excessive influences. Eleven malformed-artifact probes were rejected. It supports LBS, canonical joints and up to eight influences; it does not silently normalize or waive existing screens. Research-tool build: **zero warnings/errors**. Humanoid tests: **63 passed**, none failed/skipped. No production library changes required.

A separate fresh replay agent rebuilt from the embedded scene, cleanup scripts and corpus without Aetheris implementation internals, then inspected hip70, knee90 and shoulder90 renders. All 19 renders reproduced. Replay is **approximately deterministic, not bit-exact**: maximum stable-ID weight delta 5.8181e-6; maximum positional difference 0.00089782 mm; maximum scalar metric difference 0.00028744. Reversal/degeneracy counts, stable IDs, ordered topology and corpus are unchanged. Replay neutral residual is 0.00033154 mm. Local `fresh-replay/replay-evidence.json` records the comparison. Sub-millimetre study reproducibility is established; byte-identical heat-solver output is not.

## Reproduce

Use Blender **5.2.2 LTS**, Python with Pillow for the gallery, and the repository's .NET SDK. From the repository root (PowerShell):

```powershell
$blenderExe = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
& $blenderExe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/humanoid-rerig-x1.py -- --phase build
& $blenderExe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/review-humanoid-rerig-x1.py
python scripts/summarize-humanoid-rerig-x1.py
dotnet run --project tools/Aetheris.Humanoid.X0 -- golden-weights-antonia --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json --rig fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json --weights artifacts/local/humanoid-rerig-x1/golden-weights.json --corpus artifacts/local/humanoid-rerig-x1/pose-corpus.json --out-dir artifacts/local/humanoid-rerig-x1/aetheris
```

Initial build uses the existing X0 corpus and canonical candidate. A fresh replay needs only the saved scene and the X1 script plus its X0 helper, not Aetheris internals:

```powershell
& $blenderExe --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/humanoid-rerig-x1.py -- --phase build --scene artifacts/local/humanoid-rerig-x1/golden.blend --out artifacts/local/humanoid-rerig-x1/fresh-replay
```

The optional Genesis script uses documented [DAZ command-line options](https://docs.daz3d.com/public/software/dazstudio/4/referenceguide/tech_articles/command_line_options/start) and the [native OBJ exporter API](https://docs.daz3d.com/public/software/dazstudio/4/referenceguide/scripting/api_reference/samples/file_io/export_obj_silent/start). Pass the installed library root and an ignored output directory as two `-scriptArg` values in a separate headless instance. Oracle availability is not required for either rebuild or runtime import. Blender operations follow its [weight-paint tooling](https://docs.blender.org/manual/en/5.2/sculpt_paint/weight_paint/index.html).

## Cheapest retained path and next boundary

**X5 skeleton â†’ Blender heat weights â†’ normalization â†’ small local cleanup â†’ deterministic portable field â†’ existing Aetheris LBS.** Keep this development candidate and the stronger shoulder result. Do not promote it as a fully accepted humanoid or weaken Judgment screens to make it pass.

The smallest next step is a bounded hip90 fold experiment, with artist review or one local corrective and explicit contact inspection, judged against these same closeups. It should not start by moving joints or replacing topology. Only after that quality gap closes should generated weights be admitted as canonical data. Weight generation and cleanup may eventually move inside Aetheris, but the demonstrated portable import removes any immediate need to internalize Blender's tools. The remaining 20% is now a concrete high-angle fold problem, not a reason to invent another skinning system.
