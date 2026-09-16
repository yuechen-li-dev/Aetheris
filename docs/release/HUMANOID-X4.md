# HUMANOID-X4 — source pose diff and frame verification

**Verdict: Meaningful progression. Not Accepted.**

X4 proves the neutral topology correspondence and resolves the reported
108.8 mm discrepancy semantically and numerically. It does not yet generate
the required posed Blender source corpus, so it cannot apportion the remaining
posed surface error among source-rig coupling, skin behavior, candidate
limitations, and Judgment policy. No deformation algorithm, anatomy family,
or Judgment weight was added or retuned.

## Executive verdict

The current evidence identifies a major **JointSemanticMismatch**: Aetheris
rotates about a seam-derived socket proxy that is roughly 107 mm below two
independently derived source deformation/control pivots. Coordinate conversion
is not the primary explanation: the original Poser pivot and later Blender
metarig pivot agree within 1.8182 mm after canonical conversion, and the full
neutral vertex mapping is sub-micron accurate at the millimeter scale.

That does not establish that either source pivot is an anatomical femoral-head
center. The Poser point is a deformation pivot; the Blender point is a metarig
control/weighted-anchor candidate; the Aetheris point is a socket proxy from a
mesh-group seam. Aetheris currently has no separately modeled femur-head
frame. The concepts were previously conflated under the word “hip.”

Root-cause status:

| Classification | Evidence status |
|---|---|
| `FrameMappingError` | Not supported as primary cause; independent source pivots agree and neutral mapping is precise. |
| `JointSemanticMismatch` | Supported; Aetheris seam proxy differs from source deformation/control pivots by about 107–109 mm. |
| `RestPoseMismatch` | Present outside the hip: current Aetheris rest is a prepared A-pose while Blender extraction is source neutral. |
| `RotationConventionError` | Not tested for motion; no admitted source pose evaluator yet. |
| `SourceRigCouplingMissing` | Plausible and unmeasured. |
| `SkinWeightDifference` | Plausible and unmeasured. |
| `CandidateDeformationLimitation` | X3 proves current candidates fail hip70, but source agreement is unavailable. |
| `JudgmentPolicyIssue` | Not evaluated and not retuned. |

## Source authority and lineage

Antonia 1.2 is originally a Poser figure. X1 adopted the pinned original OBJ
at revision `08c9767691daad1382dfc6980ee83e31514b4879`, SHA-256
`0d9e918f19a497adebb51dc8f4ac921c41753b157c50809a1d760dd45ec01194`.
X3 pinned the original Poser CR2 at the same revision, SHA-256
`ff97bcbff1a0f8f86fbc39122196787fd6b46dec914b6d55574ed764f3142d62`.
The original attribution notice remains preserved.

The hash-pinned CharMorph Antonia `char.blend` and `metarig.blend` are later
Blender conversion/package inputs. They are useful independent frame evidence,
not the original authoring pipeline. Embedded scripts are not executed.

## Same-topology neutral proof

The Blender mesh has 36,030 vertices. The admitted canonical surface selects
27,193 vertices and 27,112 original quads. X4 constructs an explicit mapping
from every canonical vertex to a unique Blender source vertex:

| Check | Result |
|---|---:|
| Canonical vertices mapped | 27,193 / 27,193 |
| Unique selected Blender vertices | 27,193 |
| Admitted quads found in Blender connectivity | 27,112 / 27,112 |
| Neutral mapping RMS | about 0.000089 mm |
| Neutral mapping maximum | 0.000259 mm |
| Mapping SHA-256 | `966e6d15c7872d8cda12cfcd475c75bfb07ae3d4d6a02a8b447c6dd1c4703805` |

The mapping is derived once from neutral vertex identity, checked bijectively
over the admitted subset, checked against connectivity, stored, and hashed.
All pose comparisons consume canonical-order positions. There is no
nearest-surface matching in the primary comparison.

## Rest and neutral audit

Current Aetheris X3 uses the X1 prepared A-pose as its rest/bind surface. The
safe Blender extraction is the CharMorph source-neutral mesh. Consequently,
the whole-body neutral diff is intentionally not a pass: arm preparation gives
288.07 mm RMS and 667.88 mm maximum even though half the vertices remain within
0.00013 mm. This is a documented `RestPoseMismatch`, not pose-deformation
evidence.

The hip gate is clean because the A-pose preparation changes the arms, not the
hip surface:

| Region | RMS mm | Maximum mm |
|---|---:|---:|
| Pelvis | 0.0000799 | 0.0003680 |
| Left thigh | 0.0001025 | 0.0002484 |
| Abdomen | 0.0001355 | 0.0003692 |

Aetheris selects `dqs.baseline-weights` at neutral. The known hip edge is
11.7888410 mm in the Blender neutral subset versus 11.7887804 mm in Aetheris
neutral (rest 11.7887804 mm). The X2/X3 0.7168677 mm value remains the
Aetheris hip70 failure witness; no Blender hip70 value is claimed.

## The 108.8 mm discrepancy

All coordinates below are millimeters in the declared right-handed canonical
frame: +X anatomical right, +Y forward, +Z up.

| Point | Classification | Left coordinates |
|---|---|---|
| Poser `lThigh_jointx center` | Deformation pivot | `(-90.1642, -33.8029, 964.5312)` |
| Blender metarig `thigh.L` head | Control-rig pivot / weighted-anchor candidate | `(-91.1212, -34.0625, 963.0071)` |
| Aetheris `LeftHip.globalBind` | Socket proxy from mesh-group seam | `(-80.4492, -17.0089, 857.4617)` |

Poser → Aetheris difference (`Aetheris - source`) is
`(+9.7150, +16.7940, -107.0695)` mm, distance **108.8132 mm**.
Blender → Poser distance is **1.8182 mm**. Blender → Aetheris distance is
**107.4456 mm**. Right-side distances are identical with mirrored X offsets;
Poser, Blender and Aetheris mirror residuals are all 0 mm.

The 108.8 mm observation therefore did not compare two established anatomical
hip centers. It compared a source deformation pivot with an Aetheris seam
proxy currently used as the kinematic hip origin. The independent Blender
metarig point corroborates the source pivot family and makes a simple axis or
handedness mistake unlikely.

![Front hip-frame overlay](../../artifacts/local/humanoid-x4/hip-frame-overlay.front.png)

Green is the Poser deformation pivot, blue is the nearly coincident Blender
metarig thigh head, and red is the lower Aetheris seam-derived proxy. The wire
surface is the same admitted canonical topology. Side and iso views are in the
same artifact directory.

## Source rig behavior actually observed

Safe data-block inspection observes a 182-bone Antonia metarig and a separate
165-name Rigify weight archive. The neutral `char.blend` body has no vertex
groups or armature modifier. The config requests Rigify, shared
`rigify_extended.yaml` tweaks and elbow/knee sliding-joint behavior. X3's
original Poser CR2 inspection observes joint falloff matrices, `calcWeights`,
bulge controls and a driven `JCM-lThighIn` morph.

No claim is made that Blender Rigify reproduces Poser behavior. The exact
CharMorph path would have to generate the rig, import weights, apply shared
tweaks/sliding joints, attach modifiers, establish the intended controls and
then document how those controls correspond to Poser channels and Aetheris
requests. Those components are not presently admitted as a source deformation
authority, so X4 does not execute them and does not fabricate posed references.

## Pose-by-pose corpus status

| Pose | Blender source | Aetheris status | X4 comparison |
|---|---|---|---|
| hip flexion 30° | Blocked | X3 admits LBS baseline | Not sampled |
| hip flexion 45° | Blocked | X3 admits LBS baseline | Not sampled |
| hip flexion 70° | Blocked | No admissible X3 candidate | Not sampled |
| hip flexion 90° | Blocked | No admissible X3 candidate | Not sampled |
| hip abduction 15° | Blocked | X3 admits LBS baseline | Not sampled |
| hip abduction 30° | Blocked | X3 admits LBS baseline | Not sampled |
| hip abduction 45° | Blocked | No admissible X3 candidate | Not sampled |

This is the explicit reason X4 is Meaningful progression rather than Accepted.
Heatmaps, displacement vectors, posed edge metrics and source coupling traces
would be misleading without those source positions.

## Tooling and deterministic evidence

`scripts/extract-antonia-x4-source.py` runs under Blender with factory startup
and autoexec disabled. It verifies pinned hashes, reads mesh and armature data
blocks, emits canonical neutral positions, the explicit topology map, source
rest frames, frame classifications, hashes and Blender/extractor versions.

`compare-source-pose` is a bounded tool-project command. It rejects mismatched
topology/counts, runs current constrained kinematics and Judgment candidates,
and reports direct vertex, semantic-region, edge, orientation and known-edge
metrics. The same code accepts future verified posed dumps without changing
the comparison definition.

`scripts/qualify-humanoid-x4.ps1` reproduced extraction and comparison byte for
byte, rendered front/side/iso frame overlays, and checked the hip-region
neutral gate.

## Smallest next fix

Do not move the canonical anatomical hip center merely because a DCC/Poser
pivot differs. First introduce an explicit source-adapter relation:

`SourceDeformationPivot -> CanonicalHipSocketFrame`

Reclassify the existing seam point honestly as a surface-derived proxy and add
a controlled research rebind using the source deformation pivot while keeping
the same topology, request, weights, candidates and Judgment policy. If that
single change materially reduces the hip pose failure, review whether a new
canonical socket/femur-head frame is justified. If it does not, proceed to the
source coupling/weight comparison after admission. Do not tune Judgment first.

For future qualification, the now-observed neutral gates can be fixed at:
all 27,193 vertices mapped uniquely, all 27,112 admitted quads matched, mapping
maximum <= 0.001 mm, and bilateral frame mirror residual <= 0.001 mm. Posed
agreement thresholds remain intentionally unset until baseline source poses
exist.

## Validation

The X4 qualification replay passed. Humanoid focused tests passed **54/54**,
including new corresponding-index metric, known-edge and orientation witnesses.
Focused core Judgment/CAD tests passed **112/112** and focused
Firmament/surfacing CAD tests passed **141/141**. The full solution build passed
with 0 errors and 7 existing WebAssembly/analyzer warnings.

The full active solution suite (`Category!=SlowCorpus`) completed with **3,648
passed and 2 failed**. Both failures are the same bounded FTC07 display-budget
paths seen under concurrent full-suite load in X3:

- `DisplayPrepare_Ftc07_ReturnsPartialDisplayInsteadOfWholeBodyFailure`
  returned no display mesh after the bounded run;
- `Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang` returned
  tessellation-timeout diagnostics rather than the test's expected diagnostic.

Both tests passed on immediate isolated reruns (1/1 each). This is consistent
with existing execution-budget sensitivity but does not relabel the full suite
green. Full and isolated logs are under `artifacts/local/humanoid-x4/`. No
existing test or admissibility gate was weakened.

## Fresh-agent qualification

Three fresh, read-only agents received documentation/evidence but no
implementation internals.

- **Root-cause classification: Pass.** The agent independently selected
  `JointSemanticMismatch`, cited the 108.8132 mm Poser/Aetheris separation,
  1.8182 mm Blender/Poser agreement, exact bilateral symmetry, neutral hip
  agreement and absent femur-head frame. It retained Meaningful progression
  because posed coupling/weights/rotation/Judgment remain unmeasured.
- **Reproduction: Pass.** From the public guide and local pinned fixtures, the
  agent reproduced extraction/comparison with exit code 0, all topology counts,
  0.000259 mm maximum mapping error, the three frame distances, all 307 focused
  tests, and byte-identical replay evidence. It did not try to route around the
  posed-source admission block.
- **Semantic mapping: Pass.** The agent kept Poser deformation pivot, Blender
  control/weighted-anchor candidate, Aetheris seam socket proxy and the absent
  femur-head frame distinct, including exact coordinates, difference vectors
  and symmetry evidence.
