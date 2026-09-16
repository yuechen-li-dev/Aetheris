# HUMANOID-X3 — judgment-driven deformation

**Verdict: Meaningful progression. Not Accepted.**

The existing Judgment Engine now chooses among explicit local hip deformation
candidates with hard-rule traces, normalized utilities, stable tie-breaking,
runner-up evidence and fail-closed materialization. It does **not** yet solve
Antonia's hip70 deformation. That pose, hip90 and abduction45 have no admissible
candidate and return `HUM300` with no surface. No low score can override a
hard failure, and no invalid baseline is used as fallback.

The detailed [public policy/API guide](../public/humanoid-judgment.md) defines
the construction families, patch/attachment boundaries, equations, weights,
thresholds, diagnostics and current limits. This is a research path over the
unpromoted Antonia candidate, not a claimed canonical-body promotion.

## Source deformation audit

Original source: Antonia Polygon 1.2, revision
`08c9767691daad1382dfc6980ee83e31514b4879`,
`Runtime/libraries/Character/Antonia/Antonia-1.2.cr2`, SHA-256
`ff97bcbff1a0f8f86fbc39122196787fd6b46dec914b6d55574ed764f3142d62`.
The original [attribution/license notice](../../fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt)
is preserved. The existing geometry admission excludes source rigs/weights;
this work performs pinned **declaration inspection only**, without silently
extending canonical/runtime admission. No source CR2, weight map, morph delta
or expanded source dump is committed.

The CR2 contains `sphereMatsRaw`, `calcWeights`, per-axis joint angle/falloff
declarations, `doBulge` controls and `JCM-lThighIn` driven morph data. It is not
a simple reusable array of vertex weights. No qualified Poser deformation
evaluator or correspondence-preserving posed export is present, so **zero
source poses were sampled**. Source baseline, source-displacement agreement,
source-derived calibration and source-weighted candidates are unavailable.
`HUM307` records this honestly; the conservative X1-agreement score is never
labeled source agreement. The original authoring is retained as evidence for
the next experiment, not discarded or approximated as a supposed import.

The authored left-hip channel center `(0.036, 0.386, -0.023)` maps through
X1's documented original-source coordinate conversion to
`(-90.1642, -33.8029, 964.5312) mm`. The existing seam-derived proxy is
`(-80.4492, -17.0089, 857.4617) mm`: a **108.8131 mm difference**.
This is a material reference discrepancy, not proof that a center replacement
alone fixes the body. X3 preserves the X2 solve and center for a controlled
candidate comparison; it does not tune skinning to conceal this discrepancy.

## Shared Judgment Engine and CAD compatibility

`JudgmentModel<TCandidate,TContext>` is a small adapter over the existing
`JudgmentEngine` and `Utility.Weighted`. It adds named rule and utility terms,
measurement/normalization evidence, per-candidate traces, runner-up and tie
reporting. It sorts unique candidate IDs before invoking the existing chooser;
it does not duplicate argmax or change legacy chooser semantics. Rejected
candidates are never scored. Nonfinite/out-of-range utilities fail explicitly.

Audit: bounded fillet uses explicit predicates with legacy strategy scores
100/150/175 and a −1 reject sentinel; bounded chamfer similarly uses predicate
and strategy-ranking candidates. Surfacing BlendBoundary already separates
qualification from normalized utility and calls the shared chooser.
Those existing policies and priorities are left unchanged. Moving their
constants merely for visual uniformity would not improve the hip experiment.
New adapter tests compare its winner with the legacy chooser for equivalent
semantics; CAD regression runs are recorded below.

## Hip70 candidate evidence

The same constrained hip70 solve is fed to all six candidates. All use the
unchanged topology and generated X1 weights; exponents modify only eligible
blend vertices. Boundary and protected vertices remain exact. DQS is a rigid
transform interpolation alternative, not a simulation.

| Candidate | Min edge ratio | Max edge ratio | Max bidirectional distortion | Reversal proxies | Result |
|---|---:|---:|---:|---:|---|
| LBS baseline | 0.06081 | 4.66 | 16.44 | 12 | Reject |
| LBS softened weights | 0.1089 | 7.08 | 9.18 | 4 | Reject |
| LBS sharpened weights | 0.1089 | 7.54 | 9.18 | 38 | Reject |
| DQS baseline weights | 0.06609 | 5.17 | 15.13 | 8 | Reject |
| DQS softened weights | 0.06609 | 7.54 | 15.13 | 12 | Reject |
| DQS sharpened weights | 0.06609 | 8.19 | 15.13 | 46 | Reject |

The softened LBS construction improves one compression statistic but worsens
stretch and still reverses normals. It is not a winner or qualified fix.
All hip70 utilities are intentionally unevaluated because hard rules fail.
There is no runner-up among inadmissible constructions.

## Pose domain

| Pose | Admitted result |
|---|---|
| Flexion 0 | DQS baseline weights |
| Flexion 30 | LBS baseline |
| Flexion 45 | LBS baseline |
| Flexion 70 | No candidate; no mesh |
| Flexion 90 | No candidate; no mesh |
| Abduction 0 | DQS baseline weights |
| Abduction 15 | LBS baseline |
| Abduction 30 | LBS baseline |
| Abduction 45 | No candidate; no mesh |

No ROM or rejection threshold is reduced/relaxed to improve the verdict.
Neutral ties can favor the ordinal DQS ID; the generated geometry is still
screened. Discrete winner-switch continuity is not yet qualified.

## Remaining boundaries and next convergent step

X3 introduces provisional fixed/blended classifications from the old field;
X2 did not already provide reviewed skin attachment classes. Protected drift
is measured against the existing solved evaluation, not a validated tissue
attachment manifold. Collision/exclusion, side-crossing, certified surface
intersection, silhouette and volume constraints are absent. Orientation is a
transported-normal proxy. These limitations are visible in the policy docs;
the report does not claim complete geometric or anatomical admissibility.

Only one local patch is selected at a time. No overlapping hip/knee composition
or caching is qualified. Knee uses the same model/context machinery only for
an isolated reuse witness; production migration waits for a credible hip
candidate. Shoulder remains blocked by the missing X2 compound solution.

Next: obtain correspondence-preserving posed reference output from the pinned
original Antonia setup, verify the authored center/frame conversion, and
compare a deliberately revised shape/bind definition separately from candidate
policy changes. Characterize its falloff and region behavior before adding a
source-informed candidate. Do not guess Poser bulge/JCM evaluation, import
unverified weights wholesale, or continue adding candidate variants around an
uncalibrated reference frame.

Reproduce through the public guide. Generated traces, all candidate diagnostic
meshes, admitted winners, timings and source audit live under
`artifacts/local/humanoid-x3/`.

## Visual review and heatmaps

![Hip70 baseline left and rejected softened candidate right](../../artifacts/local/humanoid-x3/hip-flexion-70.REJECTED-softened-candidate.front.png)

![Hip70 signed edge-distortion heatmap](../../artifacts/local/humanoid-x3/hip-flexion-70.REJECTED-softened-candidate.heatmap-front.png)

Blue is compression; red is stretch; grey is near-preserved. Color is the
largest absolute log edge ratio per triangle, saturated at the 4× policy
bound. Saturation does not mean the metric is only 4×: exact extremes remain
in the trace. Matched front/back shaded and heatmap views exist for hip45,
hip70, hip90 and abduction30/45. The camera/material is identical across each
comparison. Failed examples explicitly say `REJECTED-softened-candidate`.
No source-reference panel is fabricated.

Assistant visual review: hip70's crease persists in both constructions;
the softened candidate does not resolve the transition. **Fail**. Hip45's
selected LBS equals its baseline and retains the pre-existing body contours;
screen pass is not anatomical acceptance. **NeedsReview**. Human signoff is
not supplied. Existing shoulder abnormalities are not hidden or addressed.

## Fresh-agent qualification

Three fresh agents used only the public policy guide, permitted traces or
candidate artifact, and exported DLL signatures. No implementation source
or release report was supplied.

- **Explain decision: Pass.** Hip70 has no winner/runner-up and no utility
  evaluations. For hip45 the agent independently recomputed LBS utility
  **0.969265389** against DQS runner-up **0.960241703**; both are admitted,
  four alternatives fail hard rules, and there is no tie.
- **Add score term: Pass.** A separate probe added protected-attachment
  utility `1/(1+residual/0.001mm)` through `extraTerms`, with deliberately
  overwhelming weight 1000. Neutral scored six candidates, hip45 scored two,
  hip70 scored zero. Hip70 still returned null positions. Protected residual
  is zero for existing survivors, so this term honestly ties; it is not
  confused with baseline RMS drift. The guide now includes the concrete
  score-term constructor example after this API feedback.
- **New region: Pass for isolated reuse.** The same architecture admitted
  neutral (six candidates), knee45 (four), and knee90 (two). Knee90 selected
  `dqs.weights-softened`; knee90+hip70 had no candidate and returned `HUM300`
  with null positions. This is not a production knee migration or proof of
  multi-patch composition. Hip qualification is still required first.

## Validation

Focused tests: **52/52 humanoid**, **112/112 core Judgment/chamfer/fillet**,
**141/141 Firmament/surfacing/chamfer/fillet**. These include a known
half-rotation DQS endpoint witness, boundary/protected-attachment checks,
stale-context rejection, hard-rule score suppression, invalid utility/weight
handling, deterministic tie/reordered enumeration, and policy extension.
Existing CAD candidate code, scores and tie priorities were unchanged.

Full build passed with 0 errors and 7 warnings (existing WebAssembly/analyzer
warnings). Full-suite results and the measured performance comparison follow.

Replay reproduced all **70** deterministic files byte for byte: nine traces,
54 diagnostic meshes, six admitted meshes and aggregate evidence. The
aggregate SHA-256 is
`281042725bb8fe31e656eeaa28c219167cd55e0b45fded87864a45ba6fd61666`.
Timings are deliberately separate from deterministic evidence. Qualification
also removes a previous pose's winner file if a later run rejects that pose.

Repository layout validation passed (4374 tracked files inspected), and
`git diff --check` reported no whitespace errors.

The full active solution suite (`Category!=SlowCorpus`) finished with **3645
passed, 3 failed**. It is not a green full-suite result. The failures were:

- `ThroughHole_RecipeLayerHasNoMeaningfulRuntimeRegression`: legacy 12.921 ms
  versus recipe 98.916 ms for 100 iterations, failing its timing assertion.
- `Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang`: the bounded
  viewer returned tessellation-timeout diagnostics instead of the expected
  diagnostic match.
- `FreshAuthoredPhoneUsesGenericPlateauAndMeshesEveryFace`: display meshing
  exceeded its bounded budget after 5603 ms on a plane face.

These paths were not modified. **All three passed on individual isolated
reruns** (`isolated-core.log`, `isolated-server.log`, `isolated-firmament.log`);
this is consistent with execution-budget sensitivity, but does not erase the
failures above. Full output is in
`artifacts/local/humanoid-x3/full-tests.log`.

The reproduction script passed with `-SkipFetch -SkipRender -SkipFullTests`,
rechecking pinned source inspection, tool build, corpus, byte replay and all
305 focused tests. Rendering and the full solution checks were run separately
as documented above. Public documentation qualification also passed.

## Performance

The qualification runner times the old X2 constrained evaluation independently
on each of the same nine poses. This includes skinning and its surface screen,
but excludes the already-solved kinematic pose. X3 context creation also runs
that baseline and prepares the patch; its judgment time is additional.

| Stage | Mean ms | Range ms |
|---|---:|---:|
| Old constrained deformation and screen | 120.77 | 91.86–303.61 |
| X3 context, including baseline and patch preparation | 124.38 | 99.34–268.44 |
| Six-candidate generation | 1.68 | 1.12–4.31 |
| Candidate metric measurement | 39.91 | 34.88–46.20 |
| Hard rules and admitted utility | 0.52 | 0.01–4.46 |
| Shared-engine winner/runner selection | 0.60 | 0.02–4.19 |
| Assembly and final screen | 64.62 | 0–108.63 |
| Total judgment after context | 108.03 | 39.16–173.34 |

Mean X3 context plus judgment is **232.41 ms**, approximately **1.92 times**
the independently measured old path in this run. Rejected cases skip final
assembly/screening. These are single-process qualification measurements,
including first-use overhead, not a statistically controlled benchmark or
interactive-performance claim. Timing evidence is in `timings.json`; the
dominant additional work is geometry measurement/final screening, not argmax.
