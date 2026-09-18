# HUMANOID-RERIG-X0 — Blender re-rig experiment

**EXPERIMENTAL / BRANCH STUDY**

**Verdict: Useful but inconclusive. Moonshot success bar not met.**

Branch: `experiment/humanoid-blender-rerig`. This is not an Accepted release.

Conventional Blender tooling produces a visibly better Antonia baseline cheaply, especially at the hip and shoulder. It does **not** yet establish a mechanically clean whole-body rig across the requested corpus. Automatic weights remove much of X5's obvious groin stretching and shoulder collapse, but high-angle compression and transported-normal screening failures remain. Smoothing, Preserve Volume, and bounded local cleanup produce tradeoffs rather than a universal improvement. No new skin solver, runtime API, Judgment policy, helper skeleton, or topology change was needed or introduced.

The experiment ends in **Meaningful progression**: the native heat-weight failure is resolved, a usable reproducible rig exists, and the remaining blocker is isolated to the high-angle transition field. The evidence does not justify either promoting this field or declaring ordinary DCC tooling fundamentally insufficient. More artist-directed regional work may help; this bounded study does not estimate that effort.

## Cheapest working path

Use the X5 frames and canonical triangle mesh, temporarily scale their **data** tenfold for Blender's native bone-heat solve, restore the original coordinates/frames, and use an ordinary Armature modifier. This takes seconds rather than requiring another Aetheris deformation theory. Keep plain automatic LBS as the general comparison baseline; Preserve Volume is a useful hip-specific experiment, not the default winner across limbs.

The cheapest path to a **better** baseline is demonstrated. The cheapest path to a fully **good** rig is not yet demonstrated. The smallest next step is a bounded artist review of the remaining hip90, shoulder60/90, and hinge creases in this saved scene. If that yields a consistently good field, qualify a Blender-authored weight import against frozen X5 frames and topology. Do not redesign the runtime or adopt Mixamo weights on this evidence.

## Inputs, ownership, and reproducibility

All generated scenes, weights, skeleton dumps, metrics, images, and the HTML gallery live under ignored `artifacts/local/humanoid-rerig-x0/`. The raw FBX remains in the user's Downloads directory. Only the scripts and this compact report belong in Git. Existing X5 source, release records, reference fixture, runtime code, and generated baseline artifacts are unchanged.

| Input | Identity |
|---|---|
| Canonical Antonia candidate | SHA-256 `af45c535ab21d93ae2c23f7a63f71925f3c6cfc6a68e5af9c04e1ab0033834dd` |
| X5 reference skeleton | SHA-256 `0647f681f67043610e8829fff20ca29901648b3c3245b589f2942ba453e13148` |
| Mixamo oracle | `Female Standing Pose (2).fbx`, 1,400,944 bytes; SHA-256 `53038e7c78816b25df1bb2412d33b8d2d1530de5f02a785352c529dd2de376d9` |
| Blender | 5.2.2 LTS, build `d13f752e3b9c` |
| Canonical surface | 27,193 vertices, 54,224 triangles; connectivity `2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2` |

The X5 artifact supplies all 55 semantic frames and hierarchy, including its corrected hips. Its pinned CharMorph provenance remains authoritative: `char.blend` hash `4bc468c44c8ca7c6971cac3692c5d670b09f6723687e89cd49f258c2a8251c69`, metarig hash `eee2e7185331595fb0912e03aace583d7e02c5a467a04f1af0b786e2ecf687e2`. Original source files are not reinterpreted or executed. Antonia attribution remains Olaf Delgado-Friedrichs / Antonia Polygon, CC-BY-3.0; the existing research provenance boundary still applies.

The scene separates `ANTONIA_CANONICAL`, `X5_REFERENCE_RIG`, `EXPERIMENTAL_RIG`, `X5_CURRENT_DEFORMATION`, and `MIXAMO_REFERENCE_LOCAL`. There are 55 experimental bones: 54 deform bones and the non-deforming synthetic Root. No Rigify controls, twist helpers, constraints, or manual joint moves were added. The reference rig is a separate hidden object, available for inspection.

Run from the repository root, after generating the existing X1 candidate and X5 evidence:

```powershell
$blender = 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe'
$mixamo = Join-Path $env:USERPROFILE 'Downloads/Female Standing Pose (2).fbx'
& $blender --background --factory-startup --disable-autoexec --python-exit-code 1 `
  --python scripts/humanoid-rerig-x0.py -- `
  --mixamo $mixamo

# Independent rebuild without rendering, for weight/metric comparison:
& $blender --background --factory-startup --disable-autoexec --python-exit-code 1 `
  --python scripts/humanoid-rerig-x0.py -- `
  --mixamo $mixamo `
  --out artifacts/local/humanoid-rerig-x0/replay --no-render

# Optional local gallery; Python requires Pillow:
python scripts/summarize-humanoid-rerig-x0.py
```

The script verifies that the existing X5 evidence hashes match the candidate and skeleton. Embedded Blender scripts are disabled; FBX animation is not imported. Output paths are restricted to ignored `artifacts/local/`. Reproduction depends on the admitted local candidate and the supplied local FBX; this is intentionally not a self-contained distributable asset package.

## Bounded passes and effort

| Pass | Exact operation | Result |
|---|---|---|
| A, initial | `ARMATURE_AUTO`, native meter-scale data | Bone heat warning; all 27,193 vertices unweighted, despite `FINISHED` return |
| A, diagnostic | Disable eye deformation for native solve | 418 vertices remain unweighted; rejected |
| A, retained | All mapped deform bones; temporarily scale mesh and bone data ×10; native heat; restore original positions/frames | Every vertex weighted; solve approximately 0.9 seconds in the isolated probe |
| B | A + Normalize All + native Smooth, factor 0.5, three iterations + Normalize All | Reduces abrupt weight changes, but worsens several extreme-pose metrics |
| C, volume | Normalize A; Armature Preserve Volume | Hip70 clears this study's screen; knee/elbow extrema worsen |
| C, corrective | C volume + native length-weighted Corrective Smooth, factor 0.5, five iterations, limited to mixed-weight body transitions | Mixed improvements; extra reversals and compression elsewhere; not retained |
| C, local | Normalize A; average exact semantic mirror partners; one native smoothing iteration at factor 0.25 within 80 mm of knees/elbows; normalize | Better symmetry and some extrema, but no consistent whole-body advantage |

Manual weight-paint strokes: **0**. Manual per-vertex assignments: **0**. Joint edits: **0**. The scale workaround, mirror averaging, masks, and smoothing are scripted. No source pose reproduction, face polish, topology surgery, or Rigify pass was attempted: control generation would not resolve the observed weight-field tradeoffs.

The operations are standard Blender facilities: [automatic Armature Deform weights](https://docs.blender.org/manual/en/5.0/animation/armatures/skinning/parenting.html) and [Armature Preserve Volume](https://docs.blender.org/manual/en/latest/modeling/modifiers/deform/armature.html). The scale workaround is an observation from this experiment, not a claimed Blender guarantee.

The saved `experimental.blend` opens in neutral with Pass A visible. Alternate fields are local JSON and can be replayed with `--pipeline pass-b`, `pass-c-volume`, `pass-c-corrective`, or `pass-c-local`. A separate FBX/glTF export was unnecessary for the question being tested; the Blender scene and dumps preserve the experiment without an additional exporter changing indices or triangulation.

## Mixamo oracle observations

The supplied binary FBX imports as one 65-bone armature and one weighted mesh. Blender's interpreted hierarchy, rest matrices, inverse rest-world bind matrices, object matrices, and vertex groups are dumped to local-only inventory files. These are inspection results from Blender's importer, not a custom FBX parser or a reconstruction of Mixamo's service.

The imported coordinate frame is −Y-up/+Z-forward. A proper axis rotation and uniform approximately 0.1 scale align it to canonical +Z-up/+Y-forward. After alignment, nearest-surface error is below 0.001 mm, with 27,193 unique nearest matches; indexwise error is also below 0.001 mm. This establishes strong positional correspondence, **not** a claim of identical FBX triangulation. Mixamo metrics use its own Blender loop triangles and nearest-canonical region labels. No direct posed-vertex delta is used to judge differently placed skeletons.

Major-joint correspondence uses `Hips`, `Spine/Spine1/Spine2`, `Neck/Head`, `Left/RightShoulder`, `Arm`, `ForeArm`, `Hand`, `UpLeg`, `Leg`, and `Foot`. Numeric differences below are after alignment. Frame angles are shortest quaternion angles; different bone roll conventions can produce large frame angles without an anatomical axis error.

| X5 joint → Mixamo counterpart | Center difference mm | Frame difference degrees |
|---|---:|---:|
| Pelvis → Hips | 148.62 | 11.29 |
| SpineLower → Spine | 104.69 | 7.68 |
| SpineMid → Spine1 | 90.45 | 1.39 |
| Chest → Spine2 | 90.37 | 4.75 |
| Neck → Neck | 45.36 | 14.32 |
| Head → Head | 45.76 | 0.29 |
| LeftClavicle → LeftShoulder | 47.09 | 179.32 |
| LeftShoulder → LeftArm | 25.76 | 101.41 |
| LeftElbow → LeftForeArm | 12.73 | 83.09 |
| LeftWrist → LeftHand | 9.84 | 80.86 |
| LeftHip → LeftUpLeg | 39.09 | 179.76 |
| LeftKnee → LeftLeg | 33.51 | 175.77 |
| LeftAnkle → LeftFoot | 5.78 | 175.13 |

The local mapping file contains both sides and signed displacement vectors. Right-side distances are comparable. Large trunk differences partly reflect different semantic subdivisions and pelvis origins; they are not evidence that either rig should simply replace the other. Mixamo has end/finger bones but no identified limb twist-helper chain. It is not uniformly better: shoulder60 is stronger, whereas the hip70/90 edge extrema are worse than Blender A. No Mixamo weights, helpers, or joint centers were transferred to the experimental rig.

## Weight inspection

Influence counts use a `1e-4` weight threshold. Adjacency gradient is the L1 change in the full weight vector along an edge, not a physical-distance derivative. Detailed per-region means, influence neighborhoods, gradients, normalization, symmetry, and opposite-side influence diagnostics are in `weight-inspection.json`.

| Field | Maximum influences | Mean influences | p95 edge weight L1 | Maximum normalization error |
|---|---:|---:|---:|---:|
| X1 weights used by X5 | 5 | 1.900 | 0.378 | 3.4e−16 |
| Blender A | 6 | 2.461 | 0.313 | 0.1074 |
| Blender B | 7 | 2.823 | 0.293 | 1.8e−7 |
| Mixamo | 8 | 2.343 | 0.304 | 1.5e−7 |

All retained fields weight every vertex. Blender's Armature modifier normalizes the raw A group weights during evaluation; raw A group sums are not themselves export-ready normalized attachment data. C volume explicitly normalizes them. A's p95 mirror-partner L1 difference is 0.0273, with an outlier maximum 1.0074; global smoothing lowers the maximum but raises p95 asymmetry to 0.0772. The local mirror pass addresses that outlier, but does not establish better deformation. More influences alone do not explain quality.

The region distributions expose a concrete difference. In the canonical pelvis region, X5 averages 91.5% Pelvis and only 2.6% each hip; Blender A averages 38.2% Pelvis and approximately 27.2% each hip. Mixamo averages 27.6% Hips and approximately 36% each upper leg. In the left shoulder region, X5 averages 92.8% clavicle; Blender spreads influence across clavicle (30.5%), upper arm (26.8%), chest (26.2%), and mid-spine (11.3%). Mixamo likewise distributes substantial weight across upper arm and trunk. These are raw-group regional averages, not an instruction to copy another rig's weights. Pelvis adjacency p95 L1 falls from 0.378 in X5 to 0.229 in Blender and 0.233 in Mixamo, indicating less abrupt local field transitions.

Cross-body diagnostics deserve review rather than automatic deletion: Blender A has 81 left-shoulder-region vertices above 1% opposite-side weight, with a maximum 9.57%, chiefly involving the opposite clavicle. Mixamo has 75 left-thigh-region vertices above 1%, maximum 17.46%. Neither field is a universal ideal. Region borders are semantic labels, not proof that every cross-border contribution is anatomically wrong.

## Pose equivalence, metrics, and limits

All 16 X5 requests are read from its evidence: neutral; hip flexion 30/45/70/90; hip abduction 15/30/45; knee flexion 45/90; shoulder abduction 30/60/90; elbow flexion 45/90/120. Three explicit combinations add stride, raised arms, and one-leg balance. Angles are **deltas from the authored rest posture**, so shoulder90 raises an already extended arm overhead. They are not clinical angles from arms-down.

Ball swings use the X5 global anatomical X/Y axes transformed into each bone's rest frame, with side sign; hinges use `cross(child direction, forward)` and the existing knee sign. Mixamo uses the same rule against its own aligned rest frames, not identical Euler channels or eyeballed rotations. No clipping or pose fitting is applied. The original 16 X5 comparison images use the actual existing X5 exported meshes. The three added X5 combinations use Blender LBS replay of X5 weights and frames, explicitly labeled in metrics; they are not claimed as new .NET solver qualification. Native replay matches the real single-pose X5 meshes within approximately 0.0011 mm.

All pipelines share one orthographic camera, projection, material, studio lighting, and image size. Separate side views resolve the folded-knee occlusion. There are 19 × 7 main images, plus side views and fresh-agent evidence.

Metric definitions are identical across pipelines: edge ratio is posed/rest; bidirectional distortion is `max(r,1/r)`; area ratio is posed/rest triangle area. The orientation count compares the posed normal to the rest normal transported by the face's dominant summed influence. It is a **reversal proxy**, not a certified inverted-volume or self-intersection count. A flagged triangle has a >4× bidirectional edge ratio, <1e−6 area ratio, or a reversal proxy. Self-intersection is not qualified. Whole-body p95 is often near 1 because most of the body does not move; region statistics and extrema are retained rather than hiding local failures in that aggregate.

| Pose | X5 max distortion / reversal proxy | Blender A | Mixamo |
|---|---:|---:|---:|
| Hip70 | 6.88 / 118 | **4.23 / 1** | 12.20 / 23 |
| Hip90 | 17.92 / 155 | **9.44 / 40** | 20.94 / 94 |
| Abduction45 | 4.23 / 18 | **2.25 / 0** | 4.80 / 0 |
| Knee90 | 9.10 / 51 | **5.46 / 26** | 9.12 / 40 |
| Shoulder60 | 5.81 / 27 | 9.63 / 6 | **2.89 / 0** |
| Shoulder90 | 8.93 / 75 | 10.48 / 21 | 12.90 / 4 |
| Elbow90 | 5.19 / 17 | 4.07 / 15 | 5.78 / 2 |

These counts are recomputed with one common transported-normal proxy; do not substitute X5's differently defined source-comparison normal counts. Hip70 flagged triangles fall from 169 to 3 with A, and to 0 with C volume. Hip90 remains 92 flagged triangles with A; C volume reduces that to 8. C volume reaches hip70 distortion 3.70 with zero reversal proxies but raises knee90 distortion to 9.09. C corrective lowers shoulder90 distortion to 4.49 but leaves 20 reversal proxies and worsens knee90 to 12.31. B raises hip90 to 12.40 and elbow90 to 9.08. No tested option dominates.

## Visual rubric

Assistant review, not user/artist signoff. `NeedsReview` means continuity is retained but crease, volume, or compression remains unresolved. A metric failure is reported independently of visible plausibility; proxy failures are not automatically proof of a visible fold-through.

| Pose/region | X5 visual | Blender A visual | Mixamo visual | Evidence interpretation |
|---|---|---|---|---|
| Neutral | Pass | Pass | Pass | Surface correspondence retained |
| Hip30 | NeedsReview | Pass | Pass | Low-angle continuity retained |
| Hip45 | Fail | NeedsReview | NeedsReview | X5 groin pull; improved but creased transition in both DCC fields |
| Hip70 groin/crease | Fail | NeedsReview | NeedsReview | A substantially reduces webbing; residual local crease |
| Hip90 glute/hip | Fail | NeedsReview | NeedsReview | Continuous thigh, but compressed crease; no certified collision result |
| Abduction15 / 30 | NeedsReview | Pass | Pass | No gross webbing observed at these lower angles |
| Abduction45 | Fail | Pass | NeedsReview | A clears local screen and visibly separates the leg |
| Knee45 | NeedsReview | NeedsReview | NeedsReview | No separation; hinge shape still warrants regional inspection |
| Knee90 | Fail | NeedsReview | NeedsReview | A retains limb continuity; pointed/strong hinge crease remains |
| Shoulder30 | NeedsReview | Pass | Pass | No gross collapse observed |
| Shoulder60 | Fail | NeedsReview | Pass | Mixamo has the strongest screen here |
| Shoulder90 axilla/deltoid | Fail | NeedsReview | NeedsReview | Large X5 collapse visibly reduced; no scapular/clavicle coupling |
| Elbow45 | NeedsReview | NeedsReview | NeedsReview | Continuous silhouette; no volume qualification |
| Elbow90 / 120 | NeedsReview | NeedsReview | NeedsReview | Continuous limb; sharp crease/volume transition still visible |
| Combined stride | NeedsReview | NeedsReview | NeedsReview | Continuous whole-body stance; local hinge quality remains unqualified |
| Combined raised arms / balance | Fail | NeedsReview | NeedsReview | A remains connected; high-angle local defects persist |
| Wrists / ankles | NeedsReview | NeedsReview | NeedsReview | Carried through corpus; independent wrist/ankle bending was not requested or qualified |

Lower-angle cases and all combinations are included in the local gallery; no claim of facial quality is made. No gross candy-wrapper twist was visible in the reviewed corpus, but the corpus does not include a dedicated axial-twist sweep.

Local visual evidence: [full gallery](../../artifacts/local/humanoid-rerig-x0/comparison.html), [gate comparisons](../../artifacts/local/humanoid-rerig-x0/comparison-gates.png), [extreme comparisons](../../artifacts/local/humanoid-rerig-x0/comparison-extremes.png), [remaining required views](../../artifacts/local/humanoid-rerig-x0/comparison-remaining.png). Each sheet shows X5, Blender A, and Mixamo with matched projection. Images remain ignored local evidence, not redistributed Mixamo assets.

## Integrity and independent replay

Canonical vertex ordering and all 54,224 triangle index tuples are preserved exactly. No import/export remapping is used for the experimental mesh. Neutral evaluated reconstruction error is approximately 0.000365 mm. Maximum X5-vs-Blender rest-matrix element difference is 3.42e−6, reflecting Blender float bone reconstruction; no intentional center or frame changes occurred. `integrity.json`, the skeleton dump, and the pinned input hashes retain this boundary. Blender may recompute shading normals; index/topology identity does not claim unchanged shading-normal serialization.

Independent full rebuilds reproduce A, B, and local-cleanup weight JSON, the pose corpus, all metric JSON, and integrity JSON byte-for-byte. The `.blend` container itself is not claimed byte-stable. Explicit sorted group assignment avoids Python set-order effects in the local cleanup. Saved-scene replay of all 19 poses measures maximum parent-socket center residual 0.000374 mm and link-length residual 0.000158 mm. Thus the residual failure is surface deformation, not a return to guessed pivots or detached joint centers.

A fresh agent received only the scene, corpus, and local scripts, with no Aetheris implementation internals. It successfully rendered hip70, knee90, and shoulder90 with exit code 0, and visually inspected all three. It correctly noted that shoulder90 is a rest-relative delta and that the initial view partially occludes the folded knee; side views were then added. Replay requires no original source mesh or FBX for the default saved Pass A.

```powershell
& $blender --background --disable-autoexec --python-exit-code 1 `
  --python scripts/replay-humanoid-rerig-x0.py -- `
  --scene artifacts/local/humanoid-rerig-x0/experimental.blend `
  --corpus artifacts/local/humanoid-rerig-x0/pose-corpus.json `
  --out artifacts/local/humanoid-rerig-x0/fresh-agent `
  --poses hip-flexion-70 knee-flexion-90 shoulder-abduction-90 --pipeline pass-a
```

Validation: full solution build passed (seven warnings); full active .NET suite ran, with 3,215 passed and one server display-budget test failure under concurrent load. `Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang` passed on isolated rerun. All 63 humanoid tests passed. Aetheris.CLI help was checked; it has no humanoid re-rig command, so the existing X5 research artifacts provide the domain baseline rather than an invented CLI capability.

## Promotion boundary

Do not promote this scene or its weight field yet. It includes the local Mixamo reference collection and remains research-only. No Mixamo raw asset or dense generated skeleton/weight data is embedded in this report or tracked as SDK content. No model training, service probing, proprietary algorithm reconstruction, or runtime dependency was introduced. Any later proposal to promote Mixamo-derived skeleton/weights requires a separate license review first.

The useful conclusion is concrete: **Blender heat weights are a cheap, reproducible improvement over the old field, but small generic cleanup has not yet produced one consistently good full-corpus rig.** Keep this DCC baseline and its evidence; do not return to inventing a new solver merely because a few extreme creases remain.
