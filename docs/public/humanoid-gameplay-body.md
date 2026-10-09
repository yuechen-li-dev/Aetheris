# Antonia gameplay body

The Antonia Polygon A-pose candidate now has an editable Blender body and an
explicit C# gameplay-body loader. This is a qualified development base for the
recorded pose corpus, separate from canonical adult admission. It retains the
original admitted Antonia geometry and notice; Genesis 9 was a local visual
reference only. No reference geometry, weights or corrective deltas were copied.

## Open and customize

The generated bundle is `artifacts/local/humanoid-production/`:

- `antonia-gameplay.blend`: original 27,112 quads, original UV layout, 55-joint
  rig, two hip corrective shapes, subdivision preview, simple authored materials,
  studio lighting and a timeline pose library. Select `ANTONIA_EDITABLE_QUADS`.
- `pose-library.json`: pose names and frame numbers. Timeline markers select the
  corresponding keyed rig rotations and corrective amounts; no embedded script
  execution is necessary.
- `antonia.gameplay-body.json`: fixed 54,224 binding triangles, 27,193 vertices,
  rest skeleton, complete weights, correctives and source provenance.
- `runtime-evidence.json`, `blender-evidence.json`, `saved-blend-evidence.json`:
  runtime screening, independent Blender replay and fresh-process file reopening.
- `ANTONIA-LICENSE.txt`: retained original attribution and notice. Preserve it
  when distributing a derivative. The proprietary reference is not bundled.

The runtime uses dual-quaternion skinning. Weight changes retain central pelvis
support, smooth elbow/knee transitions, strengthen shoulder support and enforce
bilateral correspondence. Sparse pre-skin shapes support hip flexion using an
explicit smoothstep activation from 30 to 90 degrees. No mesh indices or source
UV identities are changed. Subdivision improves the editable/rendered preview;
qualification is on the unsubdivided binding mesh.

## C# usage

Reference `Aetheris.Humanoid`, then load the explicitly typed body:

```csharp
using Aetheris.Humanoid;

var body = HumanoidGameplayBody.Load(bodyPath);
var result = body.Solve("sit", [
    new(HumanoidJointKind.LeftHip, FlexionDegrees: 90),
    new(HumanoidJointKind.RightHip, FlexionDegrees: 90),
    new(HumanoidJointKind.LeftKnee, FlexionDegrees: 90),
    new(HumanoidJointKind.RightKnee, FlexionDegrees: 90),
]);
if (!result.IsSolved)
{
    throw new InvalidOperationException(string.Join("; ", result.Diagnostics));
}

var pose = result.Pose!;
var surface = body.Evaluate(pose);
var palette = body.CreatePalette(pose);
var correctiveAmounts = body.ObserveCorrectives(pose);
```

Coordinates are millimetres, +X right, +Y forward, +Z up. Pose requests are
absolute anatomical states, not source-bone rotation deltas. Unspecified joints
retain the A-pose rest; shoulder rest abduction is 35 degrees. Solving legal
joint coordinates and screening the deformed surface are separate operations:
inspect `surface.Evidence.IsAdmissible` before admitting a pose. Failed screens
remain available as diagnostic geometry.

Bindings snapshot their inputs and reject mismatched rest/shape revisions.
The gameplay loader uses source-generated JSON metadata and requires no
reflection-based discovery. `HumanoidArtifactIO.Load` continues to reject this
artifact as a canonical humanoid.

`CreatePalette` prepares two float4 quaternions per joint in XYZW order, in
O(joints) time. `Evaluate` is the CPU reference, including corrective application.
`Correctives` exposes an immutable snapshot of the authored shape bank for
presentation adapters. Copeland's `Aurelian.Humanoid` integration now implements
Vulkan compute skinning with the pre-skin correctives, the same normalized
dual-quaternion blend, quaternion sign alignment and all six possible influences.
Its Character Lab owns GPU/runtime qualification; run `characterlab.cmd --proof`
in Copeland and inspect its evidence. See Copeland's
`docs/Aurelian/humanoid-animation.md` for the API and bounded animation coverage.

Ordinary glTF/GLB skinning does not encode this dual-quaternion convention.
Do not export this body and silently substitute linear-blend skinning: the
qualification would no longer apply. Use the Blender authoring asset and typed
body artifact with the matching Aurelian DQS adapter.

## Reproduce

With the retained X1, RERIG and REST-X2 inputs and installed Blender:

```powershell
pwsh -File scripts/qualify-antonia-gameplay.ps1
# Authoring iterations after the full .NET gate has already passed:
pwsh -File scripts/qualify-antonia-gameplay.ps1 -SkipFullTests
```

The wrapper builds/tests, generates the recipe, reloads the serialized C# body,
exports actual solved rotations, replays every pose in Blender, saves/reopens
the editable file in a fresh process and checks vertex parity. Missing retained
inputs fail with their paths; the wrapper downloads nothing. Earlier input
reproduction is described in [humanoid research](humanoid-research.md) and the
REST-X1/X2 and RERIG release records. Generated outputs stay ignored and local.

Optional local visual comparison, requiring the retained Genesis reference:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/render-antonia-gameplay-comparison.py
python scripts/assemble-antonia-review.py
```

That comparison uses the same Antonia pose rotations before/after, and a fixed
neutral scale for the separately posed reference. Genesis hip90 actually reaches
87.519 degrees; its column is a visual benchmark, not numerical pose parity.
Reference renders remain local and are not embedded in the open body.

## Qualified scope and limits

The 53-case corpus covers bilateral shoulder/elbow/hip/knee holdouts, lowered
arms, forward/backward strides, a bilateral walking key pose, two-handed aiming,
seated and squat poses. Every intended case passes the unchanged surface screen;
maximum Blender/runtime vertex difference is 0.006363 mm. Fresh-process Blender
reopening verifies eight representative poses within 0.000069 mm. The maximum
qualified edge distortion ratio is 3.435, below the existing 4x ceiling.

The two 100-degree hip stress poses still fail with five/six reversal flags.
The screen measures edge distortion, collapsed triangles and a dominant-bone
normal reversal proxy; it is not an exact self-intersection proof. Discrete poses
do not qualify every interpolated animation or arbitrary twist combination.
Deep crouches and large hip flexion need further authored correction work.

The body is a usable base, not a complete DAZ-grade character product: facial
animation, hair, clothing, LODs, production texture maps, full-body retargeting,
candidate-specific morph/attachment admission remain future work. Aurelian's
bounded GPU animation integration is separate from this body's source
qualification. No automatic canonical promotion is performed.
