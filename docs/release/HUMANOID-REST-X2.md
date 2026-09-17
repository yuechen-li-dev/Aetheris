# HUMANOID-REST-X2 — normalized cross-rig benchmark and A-pose weight ablation

## Executive verdict

**Accepted.** Aetheris now produces an absolute-anatomical benchmark across
the open runtime, Blender, a local Mixamo FBX, and the locally installed
Genesis 9 runtime. The canonical rest remains
`aetheris.humanoid.rest.apose.v1`; no X1 axis, semantic zero, public pose API,
Concept, Interface, or Judgment policy changed.

Fresh A-pose weighting materially improves the worst high-angle deformation
screen, especially at the shoulder, but is not uniformly better on every
metric. The selected open candidate is **A-pose auto plus the unchanged
RERIG-X1 bounded cleanup**. It uses no manual paint strokes, helper bones,
correctives, proprietary payloads, or topology edits.

## Authoritative outputs

- Labeled five-column sheet: `artifacts/local/humanoid-rest-x2/five-column-gallery.png`
- One labeled row per pose: `artifacts/local/humanoid-rest-x2/gallery/`
- Open Blender candidate: `artifacts/local/humanoid-rest-x2/golden-apose.blend`
- Portable weights: `artifacts/local/humanoid-rest-x2/selected-weights.json`
- Ablation: `artifacts/local/humanoid-rest-x2/weight-ablation.json`
- Mixamo adapter: `artifacts/local/humanoid-rest-x2/mixamo-adapter.json`
- Genesis derived evidence: `artifacts/local/humanoid-rest-x2/genesis-local/pose-observations.json`
- Aetheris import/parity: `artifacts/local/humanoid-rest-x2/aetheris/evidence.json`

The first three columns are open/promotable. Mixamo and Genesis are local
proprietary oracles; their raw geometry is ignored and absent from the golden
`.blend` and portable artifact.

## Adapter residuals

Mixamo's source bind is a near-T pose (fresh measurement: left shoulder
abduction 82.505°, flexion 2.443°). The adapter neutralizes all benchmark ball
and hinge roles to the frozen canonical state before applying each override.
Across the full corpus its maximum roundtrip residual is 0.025°; fresh
shoulder90 measured flexion −0.000006°, abduction 90.000044°.

Genesis controls are calibrated from evaluated world-space joint centers after
every DAZ update. Shoulder90 uses native `l_upperarm` Bend 47.218° to produce
90.000071° canonical abduction—not a native value of 90°. Isolated shoulder,
elbow, hip45/70, abduction, knee, walking, reach, and the bounded balance/kick
pose converge below 0.001°. Two source-range exceptions remain explicit:

- the installed base control range cannot fully straighten the Genesis elbow;
  canonical neutral bottoms out at about 5.34° flexion;
- hip90 reaches 87.519° through the admitted thigh Bend control, a 2.481°
  residual. It is `NeedsReview` and excluded from quantitative parity.

These are recorded exceptions, not hidden slider equivalences. Genesis remains
a local quality ceiling and never a canonical dependency.

## A-pose ablation

All ratios are posed/rest edge-length distortion ratios. Self-intersection is
unavailable; every listed case has zero degenerate triangles.

| Variant | Pose | reversals | p95 | p99 | max | minimum compression |
|---|---|---:|---:|---:|---:|---:|
| T-pose auto, rebound | shoulder90 | 0 | 1.0000 | 1.0524 | 3.4108 | 0.3242 |
| T-pose cleaned, rebound | shoulder90 | 0 | 1.0000 | 1.0689 | 2.5729 | 0.5386 |
| A-pose auto | shoulder90 | 0 | 1.0000 | 1.0536 | 2.2564 | 0.4432 |
| **A-pose cleaned** | **shoulder90** | **0** | **1.0000** | **1.0668** | **1.7692** | **0.5652** |
| T-pose cleaned, rebound | hip70 | 2 | 1.06 | 1.37 | 8.87 | 0.11 |
| **A-pose cleaned** | **hip70** | **0** | **1.06** | **1.38** | **6.70** | **0.15** |
| T-pose cleaned, rebound | hip90 | 67 | 1.06 | 1.50 | 11.08 | 0.09 |
| **A-pose cleaned** | **hip90** | **71** | **1.07** | **1.51** | **9.06** | **0.11** |
| T-pose cleaned, rebound | knee90 | 27 | 1.00 | 1.04 | 5.69 | 0.18 |
| **A-pose cleaned** | **knee90** | **28** | **1.00** | **1.03** | **5.62** | **0.18** |

A-pose generation reduces worst shoulder distortion by 31% versus T-pose
cleaned and improves hip70/hip90 worst-case stretch and compression. Hip90 and
knee90 reversal counts do not improve. Hip remains the limiting region, not an
unrelated whole-body regression. Cleanup takes about 0.50 s after about 1.15 s
of Blender bone heat and uses the exact recorded RERIG-X1 recipe.

## Golden path and runtime parity

```text
canonical A-pose → X5 skeleton → Blender bone heat → normalize → unchanged
bounded cleanup → portable canonical IDs → Aetheris import
```

Exact solver local rotations are replayed through Blender's armature modifier
for parity, avoiding unconstrained display-frame roll.

| Pose | RMS mm | p95 mm | max mm |
|---|---:|---:|---:|
| shoulder90 | 0.00031 | 0.00054 | 0.00074 |
| hip70 | 0.00218 | 0.00681 | 0.00706 |
| hip90 | 0.00232 | 0.00727 | 0.00765 |
| knee90 | 0.00136 | 0.00437 | 0.00469 |

Bind reconstruction is 0.000716 mm and Aetheris semantic residuals are below
0.00005°. Pose solving totals about 5.2 ms and deformation about 2.08 s for the
17-pose corpus on this machine. The Mixamo adapter pass is about 0.28 s; full
render/contact-sheet generation is roughly 45 s. No strict performance gate is
asserted.

## Judgment, morphs, attachments, and visual review

X3 policies are unchanged. Hip-flexion70 remains inadmissible, while
hip-abduction45 changes from no winner to `lbs.baseline`, with
`dqs.baseline-weights` runner-up. One case therefore gains admissible
candidates and a winner without policy tuning.

Tests cover default and 1800 mm bodies through normalization and shoulder90.
Wrist, upper arm, chest, waist, thigh, and foot attachment frames remain finite.
`RestPose`, `MeasurementPose`, and `CurrentPose` remain separate.

| Active region | Aetheris / auto | Blender cleaned A | Mixamo | Genesis 9 |
|---|---|---|---|---|
| shoulder/armpit 60–120 | NeedsReview | Pass at 60/90; NeedsReview at 120 | NeedsReview | Pass |
| elbow 45–120 | NeedsReview | NeedsReview | NeedsReview | Pass |
| hip/groin 45–90, abduction | NeedsReview; Fail at hip90 fold | NeedsReview; Fail at hip90 fold | NeedsReview | Pass, except hip90 semantic mismatch |
| knee 45–90 | NeedsReview | NeedsReview | NeedsReview | Pass |

Compact representative summary (`residual / deformation screen / visual`):

| Pose | Aetheris | Blender auto A | Blender cleaned A | Mixamo | Genesis 9 |
|---|---|---|---|---|---|
| canonical A-pose | 0.000° / Pass / Pass | 0.000° / Pass / Pass | 0.000° / Pass / Pass | 0.000° / Pass / Pass | 5.342° / exception / NeedsReview |
| shoulder90 | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | 0.000° / Pass / Pass | 0.000° / Pass / NeedsReview | 0.000° / Pass / Pass |
| elbow90 | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | <0.025° / Pass / NeedsReview | <0.001° / Pass / Pass |
| hip70 | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | <0.025° / Pass / NeedsReview | <0.001° / Pass / Pass |
| hip90 | 0.000° / Fail / Fail | 0.000° / Fail / Fail | 0.000° / Fail / Fail | <0.025° / Pass / NeedsReview | 2.481° / exception / NeedsReview |
| abduction45 | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | <0.025° / Pass / NeedsReview | <0.001° / Pass / Pass |
| knee90 | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | 0.000° / Pass / NeedsReview | <0.025° / Pass / NeedsReview | <0.001° / Pass / Pass |

Genesis retains broader shoulder/armpit and hip volume. The remaining gap is a
combination of topology/surface resolution, helper/twist chains, weighting, and
pose-dependent correctives. Ordinary runtime behavior indicates shoulder and
hip correctives are active. No proprietary delta was extracted. Materials and
lighting were replaced by the common neutral display.

After normalization, Mixamo is broadly comparable rather than consistently
superior to the open path. Retain it as an occasional adapter/oracle test, not
routine open qualification.

## Fresh-agent and optional-reference checks

Missing Mixamo writes an explicit skip record while the open ablation succeeds;
DAZ is a separate optional process. Three fresh read-only checks passed:

1. Mixamo shoulder90 reproduced at 90.000044° with a 0.000044° requested-joint
   residual.
2. Genesis shoulder90 independently verified 90.000071° from native control
   47.218°. The check identified the former native-neutral labeling defect;
   the final adapter calibrates canonical neutral before every case.
3. The open ablation reproduced `apose-cleaned`; fresh weights were byte-identical
   and shoulder90 metrics matched exactly.

Old RERIG-X1/REST-X1 cross-rig galleries are marked
`PRE-NORMALIZATION / NONCOMPARABLE` and are not acceptance evidence here.

## Validation

- `dotnet build Aetheris.slnx --no-restore`: succeeded with no errors.
- `Aetheris.Humanoid.Tests`: 69/69 passed.
- `qualify-rest-x2`: accepted; maximum Blender/Aetheris parity 0.007653 mm.
- Python benchmark/render scripts compile and the full five-column corpus renders.
- The parallel whole-solution test invocation passed the humanoid suite but
  triggered four existing bounded-tessellation/performance tests under shared
  load. All four exact failures passed when rerun sequentially (3/3 recipe
  cases plus one test each for FTC07 display, server FTC07, and Firmament
  plateau meshing).
