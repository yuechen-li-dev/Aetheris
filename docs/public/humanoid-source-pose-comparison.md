# Humanoid source-pose comparison

HUMANOID-X4 adds a corresponding-index diagnostic path for the admitted
Antonia topology. It does not add a deformation algorithm or retune Judgment.

## Source lineage and current boundary

Antonia 1.2 was authored as a Poser figure. The pinned original OBJ supplies
the adopted geometry and the pinned Poser CR2 supplies declaration evidence
for joint centers, falloff spheres, bulge controls and joint-controlled morphs.
The later CharMorph `char.blend` is a Blender conversion/package, not the
original rig authority.

The current legal/admission record permits read-only local research inspection
of the hash-pinned CharMorph Antonia inputs. It does not admit the conversion's
Rigify generation, shared tweaks, weights, sliding-joint behavior, drivers or
modifier stack as a canonical/runtime dependency. X4 therefore extracts
neutral geometry and rest frames safely, but refuses to label an approximate
Blender rig as an authored source pose evaluator.

## Reproduce the available evidence

Prerequisites are the existing X1 adoption candidate and the pinned local
CharMorph research inputs from HUMANOID-RECON-X0. Blender is launched with
factory startup and auto-execution disabled.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/qualify-humanoid-x4.ps1 -SkipFullTests
```

The script:

1. verifies pinned source hashes;
2. extracts the neutral CharMorph mesh without executing bundled scripts;
3. creates and hashes the explicit canonical-to-source vertex map;
4. proves every admitted quad exists in the source mesh;
5. extracts source metarig rest frames as data blocks;
6. compares the neutral source positions through the real constrained and
   Judgment-driven Aetheris path;
7. renders the measured hip centers/axes; and
8. replays all deterministic JSON/OBJ evidence byte for byte.

Generated output is under `artifacts/local/humanoid-x4/` and remains ignored.

## Comparison command

Once a source pose dump has the X4 schema and a verified canonical mapping:

```powershell
dotnet run --project tools/Aetheris.Humanoid.X0 -- compare-source-pose `
  --input artifacts/local/humanoid-x1/antonia-adoption-candidate.json `
  --source-pose artifacts/local/humanoid-x4/source-neutral.json `
  --output artifacts/local/humanoid-x4/neutral-comparison.json
```

The command refuses topology or connectivity mismatches. It solves the
declared left-hip request through current Aetheris kinematics, evaluates all
current deformation candidates, records the Judgment result, and reports
corresponding-vertex RMS/p50/p95/p99/max, signed mean displacement, regional
metrics, edge ratios, orientation reversals, and the known X2/X3 hip-edge
witness. It never performs pose-time nearest-surface matching.

## Pose dump contract

A source pose dump records:

- canonical topology and connectivity IDs;
- the SHA-256 of the explicit neutral index map;
- the hash-pinned source asset and extractor versions;
- positions in canonical vertex order and millimeters;
- joint, side, angle, frame, convention, and rest basis; and
- limitations preventing a stronger interpretation.

The current extractor accepts only `neutral`. Non-neutral source dumps must not
be produced until the exact conversion/deformation stack is reviewed and the
pose control semantics are documented. A future posed extractor must retain
this topology/mapping contract and export all materially changed rig nodes.

## Current semantic result

The Poser `lThigh_jointx` center is a **deformation pivot**. The CharMorph
metarig `thigh.L` head is a **control-rig pivot / weighted-anchor candidate**.
They are independently derived and land 1.8182 mm apart in the canonical
frame. The current Aetheris `LeftHip` bind origin is a **socket proxy derived
from a mesh-group seam**, not a proven anatomical joint center. It is 108.8132
mm from the Poser deformation pivot. Aetheris has no distinct femur-head frame.

Those concepts must remain explicit. Neither source rig pivot should be
renamed into an anatomical hip center without anatomical evidence.

