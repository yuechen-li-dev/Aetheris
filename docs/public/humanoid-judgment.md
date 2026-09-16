# Humanoid deformation judgment (X3 research)

**Status: Meaningful progression.** The shared Judgment Engine now supports
inspectable rule/utility policies. Six hip candidates are compared on the X1
Antonia candidate after the X2 constrained solve. Hip70, hip90 and abduction45
have **no admissible candidate**; no surface is returned for those poses.
Antonia is not promoted to canonical runtime status. See the
[release record](../release/HUMANOID-X3.md).

## Evaluate a hip

```csharp
using System.Text.Json;
using Aetheris.Humanoid;

var source = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(
    File.ReadAllText("artifacts/local/humanoid-x1/antonia-adoption-candidate.json"),
    HumanoidArtifactIO.JsonOptions)!;
var surface = source.SourcePoseSurface with
{
    Vertices = source.SourcePoseSurface.Vertices.Select((v, i) =>
        v with { Position = source.PreparedPositions[i] }).ToArray()
};
var skeleton = source.PreparedSkeleton;
var request = new RequestedHumanoidPose("hip70", skeleton.SkeletonId,
    skeleton.RestPoseId, 0, [new(HumanoidJointKind.LeftHip, 70)]);
var solve = HumanoidKinematicSolver.Solve(skeleton, request);
if (solve.Pose is not { } solved) throw new InvalidOperationException("Pose rejected.");
var context = HumanoidDeformationContext.Create(surface, skeleton, 0, solved,
    HumanoidJointKind.LeftHip);
var decision = HumanoidDeformationJudgment.Evaluate(context, DeformationPolicy.HipV1);
Console.WriteLine(JsonSerializer.Serialize(new
{
    decision.IsSuccess, decision.WinnerId, decision.RunnerUpId,
    decision.TieBroken, decision.Trace, decision.Diagnostics
}, HumanoidArtifactIO.JsonOptions));
// decision.Positions is null on failure. Never substitute a rejected diagnostic candidate.
```

The [X2 guide](humanoid-kinematics.md) defines the upstream joint domain and
revision checks. X3 never changes that solve, its centers or link lengths.
Only a `SolvedHumanoidPose` can create a deformation context; a stale shape or
rest skeleton fails before candidate generation.

## Candidates and patch ownership

`HumanoidDeformationCandidates.Default` contains six `TransitionCandidateSpec`
values: Linear and DualQuaternion, each with weight exponents 1, 0.5 and 2.
Each generator normalizes the modified X1 generated weights. These are **not
Antonia-authored weights**. DQS is a bounded rigid-transform interpolation
candidate, not a dynamics or tissue solver. Candidate construction contains no
utility constants.

Hip owns the selected-side thigh and pelvis face regions. All incident
vertices and unique edges participate in inspection. A vertex shared with an
outside face is fixed exactly to the existing solved baseline. Editable
vertices must have only pelvis/selected-hip influences, with both nonzero.
Other vertices are `FixedToExistingEvaluation`; eligible ones are
`BlendedTransition`. These are **X3 provisional classifications derived from
the X1 field**, not artist-reviewed anatomical attachments inherited from X2.
No sliding classification or behavior is claimed.

`DeformationCandidate.PatchPositions` indexes `context.Vertices` in stable
surface-index order. `context.AssembleDiagnostic(candidate)` is explicitly a
diagnostic helper; it can assemble rejected geometry for inspection. The
ordinary decision returns `Positions` only after a local winner also passes
the full X2 surface screen. Unported regions use their old deformation only
when the resulting whole surface passes that screen.

## Policy

`DeformationPolicy.HipV1` uses version `humanoid.hip.deformation.v1`.
Hard rules are evaluated before any utility term:

| Rule | Requirement |
|---|---|
| Finite | Finite patch coordinates |
| Noncollapsed | Triangle area at least 0.0001 mm² |
| Orientation proxy | Zero dominant-influence transported-normal reversals |
| Edge compression/stretch | Every unique edge deformed/rest ratio in [0.25, 4] |
| Area compression/stretch | Every triangle deformed/rest area ratio in [0.1, 8] |
| Fixed attachment | Drift from protected baseline at most 0.001 mm |
| Boundary continuity | Drift of shared boundary vertices at most 0.001 mm |

These are provisional engineering screening bounds, not clinical or material
strain limits. Collision/exclusion, self-intersection certification, silhouette
quality and volume preservation are **not implemented**. The normal test is
explicitly a proxy. Passing these rules alone does not establish a valid human.

For admitted candidates only, utility is the normalized weighted mean:

| Term | Weight | Utility [0,1] |
|---|---:|---|
| edge-metric | 0.65 | `1/(1 + mean(abs(log(edge ratio))))` |
| area-metric | 0.25 | `1/(1 + mean(abs(log(area ratio))))` |
| baseline-agreement | 0.10 | `1/(1 + RMS displacement from X1 / 10mm)` |

The last term measures conservative agreement with **X1**, not authored
Antonia source behavior. Source agreement is disabled, with `HUM307`, because
no admitted correspondence-verified posed source surface is available.
Weights and thresholds are explicit code/configuration, not runtime UI settings
and not calibrated from source samples. Do not change them merely to admit
the failing hip70 cases.

The shared `JudgmentModel<TCandidate,TContext>` runs rules and records their
measurements, limits, diagnostics and evidence. It records every score term's
raw value, unit, utility and weight. It uses `Utility.Weighted` and the existing
`JudgmentEngine` for winner and runner-up selection. IDs are unique and sorted
ordinally; equal priorities and utility differences within the engine's
1e-12 epsilon resolve by stable ID. Invalid utilities/weights cannot be
silently treated as useful scores. No second argmax was introduced.

`WinnerId` describes local selection. `IsSuccess` and nonnull `Positions`
describe final surface admission. A local winner can still fail the final
screen. `HUM300` indicates no admitted surface, `HUM301/302` metric limits,
`HUM303` collapse/orientation/nonfinite failure, `HUM304` protected-attachment
drift, `HUM305` boundary failure, `HUM306` a tie, and `HUM307` absent source
behavior. Rejected candidates have **no utility scores**.

## Add a score term without changing generators

`HumanoidDeformationJudgment.Model(policy, extraTerms)` exposes the reusable
policy, and `Evaluate(context, policy, extraTerms: ...)` uses it directly.
Extra terms have type
`JudgmentScoreTerm<MeasuredDeformationCandidate, HumanoidDeformationContext>`
from `Aetheris.Kernel.Core.Judgment`. Their callback returns
`JudgmentScoreEvidence(Utility, RawValue, Unit, Evidence)`.

```csharp
using Aetheris.Kernel.Core.Judgment;
var term = new JudgmentScoreTerm<MeasuredDeformationCandidate, HumanoidDeformationContext>(
    "protected-attachment-residual", 0.2,
    (candidate, _) => new JudgmentScoreEvidence(
        1 / (1 + candidate.Metrics.FixedAttachmentMaximumMm / 0.001),
        candidate.Metrics.FixedAttachmentMaximumMm, "mm",
        "Bounded utility for residual at protected attachments."));
var decisionWithTerm = HumanoidDeformationJudgment.Evaluate(context,
    DeformationPolicy.HipV1, extraTerms: [term]);
```

Useful inputs include `candidate.Metrics.FixedAttachmentMaximumMm`,
`BoundaryMaximumMm`, `BaselineDriftRmsMm`, `MeanAbsoluteLogEdgeRatio` and
`MeanAbsoluteLogAreaRatio`. Attachment drift remains a hard rule even if an
extra utility term rewards lower residuals. The current generators pin those
protected vertices exactly, so attachment utility will normally tie; do not
invent discrimination. `BaselineDriftRmsMm` is not anatomical attachment drift.

## Reuse boundary: knee and composition

The same context factory supports LeftKnee/RightKnee, using selected thigh,
shin and knee regions and only hip/knee blend influences. Use
`DeformationPolicy.KneeV1` for a differently versioned instance of the same
rule/score model. This exists for an isolated architecture reuse witness.
**Knee production migration is deferred because hip qualification failed.**
Shoulder contexts are rejected; X2 has no admitted compound shoulder solve.

There is no multi-patch orchestrator, overlapping-patch arbitration, cache or
quantized pose library in this progression. One context makes one decision;
it preserves all outside vertices. Do not independently apply overlapping
hip and knee winners and claim qualified composition. Candidate constructions
vary continuously with input transforms, but discrete winner changes have not
been certified temporally continuous. Timings are separate from deterministic
trace/geometry outputs.

## Reproduction and evidence

```powershell
pwsh -File scripts/inspect-antonia-deformation-source.ps1
dotnet run --project tools/Aetheris.Humanoid.X0 -- judge-antonia --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json --out-dir artifacts/local/humanoid-x3
```

The nine cases are flexion 0/30/45/70/90 and abduction 0/15/30/45. Each has
`<pose>.trace.json` with all candidates' rule results and measured metrics.
`hip-flexion-70.trace.json` intentionally has no winner. Use
`hip-flexion-45.trace.json` to inspect a selected winner and runner-up.
All candidates are exported only as `*.diagnostic.obj`; `*.winner.obj` is
written only for an admitted full surface. Files live under ignored
`artifacts/local/humanoid-x3/`. Raw CR2 is retained there for read-only audit,
not installed as a runtime dependency or committed.
