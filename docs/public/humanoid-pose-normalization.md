# Semantic humanoid pose normalization

Aetheris exposes pose as an anatomical state, not as a source-rig rotation
delta. `Shoulder.Abduction = 90°` therefore means a 90° canonical shoulder
state whether the imported rig starts in a T-pose, A-pose, or another reviewed
rest configuration.

## Frozen canonical convention

`aetheris.humanoid.anatomical-frame.v1` is right-handed millimetres:

- +X is anatomical right, independent of camera view.
- +Y is forward.
- +Z is up.
- flexion/extension is measured toward/away from +Y;
- abduction/adduction is measured away from/toward the sagittal plane;
- left and right use anatomical side, not screen side.

The canonical rest is `aetheris.humanoid.rest.apose.v1`: shoulder abduction
35°, shoulder flexion/twist 0°, elbow flexion 0°, hip flexion/abduction/twist
0°, and knee flexion 0°. Wrists are neutral and retain the reviewed source
palm frame during the bounded upper-limb repose. Feet are parallel to forward
and grounded. The current deformation policy identity is
`aetheris.humanoid.lbs.v1`; it is distinct from topology, skeleton, and rest
identity.

## API

```csharp
var request = new RequestedHumanoidPose(
    "left-shoulder-90",
    skeleton.SkeletonId,
    skeleton.RestPoseId,
    ShapeRevision: 0,
    [new AnatomicalJointRequest(
        HumanoidJointKind.LeftShoulder,
        AbductionDegrees: 90)]);

var result = HumanoidKinematicSolver.Solve(skeleton, request);
if (result.Pose is { } pose)
{
    var actual = HumanoidPoseSemantics.Measure(
        skeleton, pose.GlobalTransforms, HumanoidJointKind.LeftShoulder);
    var residual = pose.SemanticResiduals.Single();
    // Local rotations remain adapter output. Do not author against them.
}
```

`HumanoidPoseSemantics.Measure` derives shoulder/hip state from the canonical
joint-to-child direction and elbow/knee flexion from adjacent link directions.
The solver constructs a source-local rotation that reaches the requested
canonical direction. Source rest quaternions, Blender Euler axes, and native
bone names never enter the request.

`HumanoidRestPoseNormalizer.Normalize(surface, skeleton)` drives all qualified
major joints to the frozen neutral state, deforms the surface once, rebuilds
parent-relative local rests, global binds, and inverse binds, and verifies
neutral reconstruction within 0.001 mm. Connectivity, vertex/face order,
stable IDs, and topology identity are retained. The `CanonicalHumanoid`
overload also materializes attachment neutral frames in the new rest.

## Qualified boundary

REST-X1 directly proves that a T-pose source and a 35° A-pose source use
different local rotations to reach the same 90° canonical shoulder state.
The checked Antonia adapter measures its native shoulder at 86.66° and reaches
the 35° canonical rest deterministically. Hip, shoulder, elbow, and knee
benchmark residuals are below 0.05° in the current evidence.

This is an engineering pose vocabulary, not a medical claim. Twist is bounded
but has not yet received the same cross-source orientation qualification.
Clavicle/scapular motion, wrist pronation/supination, collision, and
self-contact remain unsupported. Mixamo and Genesis adapters are local
benchmark work, not canonical runtime dependencies.

## Normalized local benchmark

`scripts/humanoid-rest-x2.py` applies the same absolute anatomical requests to
the open Aetheris/Blender path and an optional local Mixamo FBX. Source names
only seed the role map; expected parent chains verify shoulder-elbow-wrist and
hip-knee-ankle roles. The adapter records source rest matrices, joint centers,
major axes, handedness, scale, requested state, measured state, and roundtrip
residual. If `--mixamo` is omitted, the open qualification succeeds with an
explicit skipped-column diagnostic.

Genesis 9 is evaluated only through the installed local DAZ runtime by
`scripts/benchmark-genesis9-local.dsa`. Native controls are searched against
measured canonical-space joint directions; slider values are never treated as
anatomical angles. Normal DAZ helpers and pose correctives remain enabled.
Raw FBX/DAZ geometry, weights, morphs, textures, and JCM payloads remain under
ignored `artifacts/local/` and are never embedded in the open `.blend` or
portable weight artifact.

From the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --factory-startup --disable-autoexec --python-exit-code 1 `
  --python scripts/humanoid-rest-x2.py -- `
  --mixamo $env:AETHERIS_MIXAMO_FBX

dotnet run --project tools/Aetheris.Humanoid.X0 -- qualify-rest-x2 `
  --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json `
  --rig fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json `
  --source-weights artifacts/local/humanoid-rerig-x1/golden-weights.json `
  --weights artifacts/local/humanoid-rest-x2/selected-weights.json `
  --blender-dir artifacts/local/humanoid-rest-x2
```

The source-rest weight argument deterministically recreates the REST-X1
canonical A-pose geometry before importing weights generated in that A-pose.
This preserves the explicit distinction between source rest, canonical rest,
measurement pose, and current pose.

## Reproduce the Antonia evidence

```powershell
dotnet run --project tools/Aetheris.Humanoid.X0 -c Release -- `
  normalize-rest-antonia `
  --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json `
  --rig fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json `
  --weights artifacts/local/humanoid-rerig-x1/golden-weights.json `
  --out-dir artifacts/local/humanoid-rest-x1

& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --factory-startup --disable-autoexec --python-exit-code 1 `
  --python scripts/render-humanoid-rest-x1.py -- `
  --out-dir artifacts/local/humanoid-rest-x1
```

Generated research output remains under ignored `artifacts/local/`.
Pose files retain `.screened` only when the existing surface screen passes;
for example shoulder 90° passes, while elbow 90° is intentionally emitted as
`.failed-diagnostic`. A semantic solve can be exact even when deformation fails.
