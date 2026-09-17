# Humanoid X2 constrained kinematics research (historical)

> REST-X1 supersedes the relative-to-rest angle convention described below.
> Current `RequestedHumanoidPose` values are absolute anatomical states. See
> [Semantic humanoid pose normalization](humanoid-pose-normalization.md).

**Status: Meaningful progression, not an accepted humanoid.** Antonia remains
the X1 adoption candidate. The new solve admits hip, elbow and knee requests;
it does not claim a complete anatomical domain or replace the historical
quaternion research API. See the [X2 release record](../release/HUMANOID-X2.md).

## Public API

The `Aetheris.Humanoid` project exposes:

```csharp
using System.Text.Json;
using Aetheris.Humanoid;

var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(
    File.ReadAllText("artifacts/local/humanoid-x1/antonia-adoption-candidate.json"),
    HumanoidArtifactIO.JsonOptions)!;
var skeleton = candidate.PreparedSkeleton;
var surface = candidate.SourcePoseSurface with
{
    Vertices = candidate.SourcePoseSurface.Vertices.Select((v, i) =>
        v with { Position = candidate.PreparedPositions[i] }).ToArray()
};
const long shapeRevision = 0;
var request = new RequestedHumanoidPose(
    "hip-knee-90", skeleton.SkeletonId, skeleton.RestPoseId, shapeRevision,
    [new(HumanoidJointKind.LeftHip, FlexionDegrees: 90),
     new(HumanoidJointKind.LeftKnee, FlexionDegrees: 90)]);
var result = HumanoidKinematicSolver.Solve(skeleton, request, shapeRevision);
if (result.Pose is { } solved)
{
    // GlobalTransforms has the same order as skeleton.Joints.
    // Joints reports solved coordinates; Residuals exposes CenterMm and LinkLengthMm.
    var surfaceResult = HumanoidConstrainedSurface.Evaluate(surface, skeleton, shapeRevision, solved);
    // Positions remain available as diagnostic evidence when Evidence.IsAdmissible is false.
    // Do not present a failed surface as an accepted humanoid or production export.
}
// Inspect result.Diagnostics and result.Projections even when a solve succeeds.
```

`AnatomicalSolvePolicy.Reject` is the default. Pass
`policy: AnatomicalSolvePolicy.Project` to request deterministic parameter
projection. It is radial projection after component clipping, not a minimum
geodesic-distance optimization. Rejected requests have no `Pose`.

`RequestedHumanoidPose` accepts semantic angles only; it cannot express joint
translation, scale, arbitrary quaternion, or link-length edits. These are
not disguised as zero-residual requests. Duplicate/unknown joints, nonfinite
angles, stale skeleton/rest/revision identities, corrupt binds, invalid
hierarchy and side mismatches fail closed. `SolvedHumanoidPose` has no public
constructor or mutable exposed collections. Skin evaluation rejects a solved
pose from another rest skeleton or shape revision.

For the local candidate, deserialize `AntoniaAdoptionCandidate` with
`HumanoidArtifactIO.JsonOptions`, use `PreparedSkeleton`, and replace the
positions in `SourcePoseSurface.Vertices` with `PreparedPositions` using `with`.
Use revision 0. Do not load this candidate as `CanonicalHumanoid` or substitute
the X0 synthetic fixture. This conversion changes positions only; connectivity
and weights are retained.

## Interfaces and pose invariants

`HumanoidKinematicSolver.Interfaces(skeleton)` returns typed
`AnatomicalHingeInterface` and `AnatomicalBallInterface` definitions with
participants, parent socket, axes/ranges. `Links(skeleton)` returns parent/child
identities and rest lengths. They follow Aetheris' participant, constraint,
residual and explicit rejection patterns. This progression does not register a
new Firmament `Concept<T>` or `Interface<T>` compiler family.

| Interface | Admitted coordinates | Projection and equation |
|---|---|---|
| Hip | Flexion −20..120°, abduction −25..45°, twist ±45° at rest | Normalized flexion/abduction squared sum ≤1; twist limit decreases to ±22.5° at swing boundary |
| Elbow | Flexion 0..145° | Hinge axis from rest forearm × forward; abduction and twist zero |
| Knee | Flexion 0..140° | Hinge axis opposite rest lower leg × forward; abduction and twist zero |

The original X2 implementation used an exponential-map delta from the supplied
rest. REST-X1 replaced that behavior with a direct solve from absolute flexion
and abduction to the canonical bone direction. Positive hip flexion moves the
leg forward; positive knee flexion moves the lower leg backward. Source elbow
and knee bends are removed in their measured source plane before canonical
flexion is applied.

These bounds are explicit engineering assumptions. The
[CDC reference study](https://archive.cdc.gov/www_cdc_gov/ncbddd/jointrom/index.html)
provides context for useful adult flexion ranges; the coupled domain and
twist reduction above are not values validated by that study. No medical
certification or complete human ROM coverage is claimed.

Every parent socket transformed by the parent's solved frame must coincide
with the child origin within **0.001 mm**. All parent-child distances must
remain within **0.001 mm** of rest length. Rest/bind consistency uses a
0.0001 matrix-component tolerance. Finite rigid transforms, acyclic ordered
hierarchy, side consistency and current revision are checked before skinning.
Input order is canonicalized by joint kind. Unsupported joints remain at rest
when omitted; explicitly requesting one returns `HUM209`, even at zero.

Shoulder ball coordinates are now admitted for bounded REST-X1 comparison.
They do not model clavicle/scapular coupling. Wrist, ankle, neck, spine and
finger requests remain unsupported. Collision, self-contact, scapular motion,
and a complete clinical biomechanics model remain unimplemented.

## Surface screening and migration boundary

Order: validate rest and request → reject/project coordinates → compute rigid
transforms → validate center/length residuals → LBS → inspect surface.
`HUM200` means a rejected range; `HUM201/202` identify failed center/length
residuals; `HUM205` records projection; `HUM208` identifies invalid/stale input;
`HUM209` identifies unsupported interfaces. Surface `HUM206/207` diagnostics
are independent of pose success.

The surface screen rejects nonfinite positions, triangles below 0.0001 mm²,
bidirectional edge ratio above 4, or a dominant-influence normal reversal.
It reports unique edges per face region with nearest-rank median/p95/p99/max.
An edge shared by different regions participates in each region's statistics.
The 4× bound is a provisional catastrophe screen, not visual acceptance.
The reversal count is a transported-normal proxy, not a certified inversion
or self-intersection count. Self-intersection and volume are unavailable.

There are **no admitted Antonia foot attachment frames, landmarks, morphs or
measurements**. The existing 88 landmarks/19 attachments belong to the X0
regression fixture. A solved ankle matrix is not a sole attachment frame.
Morphing must rebuild binds and frames and increment the shape revision before
solving again. Antonia morph qualification is still blocked.

`HumanoidPosing` remains the unconstrained historical evaluator needed for
source-pose preparation and X0/X1 comparison. It can bypass X2 constraints;
therefore the overall runtime replacement criterion is not met. Callers of
the new constrained path must use `HumanoidKinematicSolver` followed by
`HumanoidConstrainedSurface`, and preserve failure evidence.

## Reproduce

```powershell
dotnet run --project tools/Aetheris.Humanoid.X0 -- constrained-antonia --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json --out-dir artifacts/local/humanoid-x2
dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj
```

The candidate is produced by the existing [X1 workflow](humanoid-research.md).
`evidence.json` is deterministic; `timings.json` is separate. Failed OBJ
outputs include `.failed-diagnostic` in their filenames. Passing numeric
screens do not constitute human visual signoff.

Render with Blender's background mode and `scripts/render-humanoid-x2.py`.
Optional repeatable `--fbx <local-file>` arguments inspect supplied references
without copying them into the repository. ASCII FBX receives a declaration
inventory because the installed Blender importer rejects ASCII; no FBX posed
deformation comparison is claimed in that case.
