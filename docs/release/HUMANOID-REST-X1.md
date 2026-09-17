# HUMANOID-REST-X1 — canonical A-pose and semantic pose normalization

**Verdict: Meaningful progression.**

Aetheris now has an explicit, versioned 35° canonical A-pose; an absolute
anatomical pose API; deterministic Antonia T-ish→A-pose surface repose and
rebind; measured semantic residuals; and a normalized Aetheris benchmark
gallery. The central representation defect is fixed: `Shoulder.Abduction=90°`
no longer means “add 90° to whatever bind pose arrived.”

The milestone is not Accepted because Mixamo and Genesis adapters have not yet
been rerun through this semantic authority, the five-column comparison gallery
is therefore incomplete, and Blender weights have not yet been regenerated
from the canonical A-pose. The current Antonia candidate also has no admitted
morph or attachment channels to migrate. These are concrete downstream gaps,
not ambiguity in canonical pose meaning.

## Canonical A-pose

| Identity/state | Frozen value |
|---|---|
| topology | `aetheris.humanoid.antonia.adoption-candidate.v1` |
| skeleton | `aetheris.humanoid.antonia.reference-rig.v1` |
| source rest | `antonia.charmorph-metarig.rest.v1` |
| canonical rest | `aetheris.humanoid.rest.apose.v1` |
| semantic frame | `aetheris.humanoid.anatomical-frame.v1` |
| deformation policy | `aetheris.humanoid.lbs.v1` |
| shoulder | abduction 35°, flexion 0°, twist 0° |
| elbow | flexion 0° |
| hip | flexion 0°, abduction 0°, twist 0° |
| knee | flexion 0° |
| palms/wrists | source-neutral palm frame preserved; wrist 0° |
| feet | parallel to forward, grounded |

The rest-position SHA-256 is
`3a924e6180e5f92500adfa8862c887f1d6c7a41e5c6c94f2655c823eb4b3981a`.
The generated OBJ SHA-256 is
`206fda6f9111a96ad845b57c855b722b2f1be36dedb77059410ea53593b5b0e9`.

![Old source-native rest](../../artifacts/local/humanoid-rest-x1/source-native-rest.png)

![Canonical A-pose](../../artifacts/local/humanoid-rest-x1/canonical-apose.png)

![Canonical joint centers and axes](../../artifacts/local/humanoid-rest-x1/canonical-apose-skeleton-overlay.png)

## Deterministic repose, rebind, and identity

The Antonia source rest measured shoulder abduction **86.66°**, shoulder
flexion **3.37°**, elbow flexion **33.97°**, hip flexion **3.35°**, hip
abduction **7.10°**, and knee flexion **3.86°**. Those are adapter inputs, not
public zeroes. `HumanoidRestPoseNormalizer` drives the semantic neutral,
deforms the surface once, and rebuilds local rests, global binds, and inverse
binds from the solved frames.

- vertices: 27,193 → 27,193;
- faces: 54,224 → 54,224;
- connectivity hash unchanged:
  `2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2`;
- ordered triangle connectivity unchanged;
- neutral bind reconstruction maximum: **0.000715 mm**;
- normalization semantic residual maximum: **0.0198°** (right elbow; all
  other qualified neutral residuals round below 0.01°).

The generic canonical-humanoid overload rematerializes attachment rest frames.
Tests exercise Height, ArmLength, LegLength, and ShoulderWidth after
normalization while preserving connectivity and valid binds. The Antonia
research artifact itself has no admitted morphs or attachments, so this is API
proof rather than a claim about missing Antonia channels. Measurement pose
remains the separate `aetheris.humanoid.adult.measurement.v1` identity.

## Absolute semantics and adapter independence

Old behavior:

```text
source local rotation = request
```

REST-X1 behavior:

```text
absolute anatomical target
  -> canonical target link direction
  -> source-rest-specific local transform
  -> measured canonical state and residual
```

A regression fixture maps both a 90° T-pose source and a 35° A-pose source to
the same 90° shoulder state. The T-pose adapter emits identity while the
A-pose adapter emits a nonidentity local transform; their canonical elbow
centers coincide within 0.001 mm. Mirrored left/right requests also produce
mirrored joint centers without camera-side sign leakage.

## Normalized Antonia benchmark

Tolerance is 0.05°. Every requested state is below it.

| Pose | Maximum semantic residual | Existing surface screen | Max edge ratio / reversal proxies |
|---|---:|---|---:|
| neutral A-pose | 0° | pass | 1.0004 / 0 |
| shoulder 60° | 0.000019° | pass | 1.7453 / 0 |
| shoulder 90° | 0.000007° | pass | 2.5729 / 0 |
| shoulder 120° | 0.000019° | fail | 4.3907 / 0 |
| elbow 90° at shoulder 35° | 0.000034° | fail | 6.4165 / 10 |
| hip flexion 70° | 0.000011° | fail | 8.8707 / 2 |
| hip flexion 90° | 0° | fail | 11.0816 / 67 |
| hip abduction 45° | 0.000009° | pass | 2.4564 / 0 |
| knee flexion 90° | 0.000038° | fail | 5.6868 / 27 |
| combined pose | 0.000035° | fail | 6.4161 / 10 |

![Normalized shoulder 90°](../../artifacts/local/humanoid-rest-x1/shoulder-abduction-90.screened.png)

**PRE-NORMALIZATION / NONCOMPARABLE for cross-rig conclusions.** The Antonia-only normalized gallery remains valid REST-X1 evidence, but any inherited Mixamo/Genesis comparison is historical. The complete matched-camera gallery is generated at
`artifacts/local/humanoid-rest-x1/gallery.html`. Each filename names an
absolute anatomical state; failed surfaces retain `.failed-diagnostic`.

## Shoulder and rerig verdict

Input normalization materially improved benchmark validity. Historical
“shoulder90” applied +90° to an 86.66° source rest and was not a 90° anatomical
comparison. It failed with max edge ratio 7.1019 and nine reversal proxies.
The normalized 90° state has 0.000007° residual, max ratio 2.5729, zero
reversal proxies, and passes the existing screen. These numbers must not be
presented as an A-pose weight-quality ablation because the historical and new
poses were not anatomically equal.

The retained Blender-cleaned field was generated from the former source rest
and deterministically rebound to A-pose. An auto-weight regeneration from the
canonical A-pose has not been run, so the hypothesis “A-pose generation itself
improves shoulder deformation” remains unproven. Shoulder 120° still fails,
and no clavicle/scapular coupling or contact model was added. Hip remains the
expected control and still fails at 70°/90°.

## External adapters and proprietary boundary

Antonia is the only adapter qualified here. Existing local Mixamo and Genesis
assets remain ignored, comparison-only, and absent from runtime. No
proprietary mesh, weights, controls, JCMs, or DAZ/Mixamo dependency entered the
repository. Because those local adapters and normalized renders are missing,
the required X5/Blender-auto/Blender-cleaned/Mixamo/Genesis gallery is not
claimed.

## Performance and validation

One observed local run (not a hard target): normalization 20.7 ms, ten pose
solves 3.8 ms total, and ten CPU LBS/surface evaluations 916.5 ms total.
Timing is environment-dependent and deliberately excluded from deterministic
hash claims.

The focused test assembly passes **67/67**. New tests cover source-rest
independence, left/right sign convention, topology preservation, bind rebuild,
attachment frames, four morph controls, and deterministic semantic targets.
The full solution build passes with seven existing WebAssembly analysis/SQLite
warnings. The parallel active suite passed every humanoid test and reported
four unrelated Core display-preparation failures under load; all four passed
immediately when rerun individually. This matches the repository's known
contention-sensitive tessellation behavior and is not waived as humanoid proof.

Reproduce:

```powershell
dotnet run --project tools/Aetheris.Humanoid.X0 -c Release -- `
  normalize-rest-antonia `
  --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json `
  --rig fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json `
  --weights artifacts/local/humanoid-rerig-x1/golden-weights.json `
  --out-dir artifacts/local/humanoid-rest-x1

dotnet test Aetheris.Humanoid.Tests/Aetheris.Humanoid.Tests.csproj -c Release
dotnet build Aetheris.slnx -c Release --no-restore
```

## Acceptance gap

Completed: explicit/versioned A-pose, deterministic Antonia repose, unchanged
topology, rebuilt binds, absolute semantic API, two-rest abstraction proof,
low residual shoulder/elbow/hip/knee corpus, proprietary-free canonical path,
side tests, generic morph/attachment checks, and normalized Aetheris gallery.

Still required for Accepted: semantic Mixamo and Genesis adapters, regenerated
normalized comparison columns, an A-pose auto-weight regeneration/ablation,
and migration evidence for real Antonia morph/attachment data if those
channels are admitted. The next blocker is isolated to source adapters and
weight-generation evidence; canonical pose meaning is no longer the blocker.

## Fresh-agent comprehension

Three read-only agents received only the public guide and named entry points.
The pose agent authored `LeftShoulder` abduction 90° without bind, Blender-axis,
or source-name knowledge and caught a named-argument casing typo in the guide;
the documentation was corrected. The adapter agent independently explained
identity for a T-pose source versus a nonidentity 55° swing for the 35° source,
while requiring equal canonical state and elbow center. The benchmark agent
recovered the exact CLI and Blender commands and the absolute-label rationale;
it initially assumed elbow90 would receive a `.screened` suffix. The guide now
states the actual `.failed-diagnostic` result and explicitly separates exact
semantic solve from failed deformation. Pose meaning and adapter abstraction
pass; on a second read of the corrected guide plus evidence, the benchmark
agent recovered both suffixes and the kinematics/deformation distinction. The
initial mistake is useful evidence that surface admission must remain visible
in benchmark instructions.
