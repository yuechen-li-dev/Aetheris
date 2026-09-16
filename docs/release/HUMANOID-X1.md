# HUMANOID-X1 — Original Antonia adoption and deformation gate

Date: 2026-09-16. Verdict: **Meaningful progression**. **Not Accepted.**

> Did Aetheris successfully promote a screened CC-BY human topology into a stable,
> semantically rich canonical adult humanoid runtime suitable for future fitting,
> posing, measurement, attachment, and morph workflows?

**No.** The source-admission blocker is removed and original Antonia connectivity
now runs through a deterministic Aetheris adoption and skinning path. The first
Aetheris-generated skin weights fail neutral shoulder quality and bounded hip,
elbow and knee deformation. No Antonia candidate is promoted to
`CanonicalHumanoid`, and no usable canonical adult runtime is claimed.

This is a measured gate stop, not completion of the remaining X1 mission.
Continuing by binding 88 landmarks, 19 sites and geometry-specific morphs to
this visibly defective rest shape would freeze unqualified geometry as authority.
The next bounded problem is **reviewed shoulder/hip joint proxies and skin-weight
transitions on the original adopted mesh**, followed by the same pose sweep.
No second procedural body, shrinkwrap of X0, or independent retopology was attempted.

## Source admission and attribution

The original author repository supplies the original base figure and notice:

| Item | Pinned identity |
|---|---|
| Source | [odf/Antonia.Polygon](https://github.com/odf/Antonia.Polygon/tree/08c9767691daad1382dfc6980ee83e31514b4879) |
| Release/revision | 1.2.0 / `08c9767691daad1382dfc6980ee83e31514b4879` |
| OBJ | `Runtime/geometries/Antonia/Antonia-1.2.obj` |
| OBJ SHA-256 | `0d9e918f19a497adebb51dc8f4ac921c41753b157c50809a1d760dd45ec01194` |
| Notice | `Runtime/Docs/Antonia/README` |
| Notice SHA-256 | `e9385525cf613450e8673703f7959cd869e2d9c1290d474a99ca4863d734f4ae` |
| License | [CC BY 3.0 Unported](https://creativecommons.org/licenses/by/3.0/) |

Based on **Antonia Polygon by Olaf Delgado-Friedrichs**, with phantom3D, MikeJ
and others credited in the original notice. Aetheris adapts the base figure by
selecting body/eye components, converting coordinates, freezing binding
triangulation, assigning stable IDs, constructing semantic proxies and skin
weights, and experimenting with rest-pose preparation. **No endorsement is
implied.** Aetheris does not claim exclusive authorship of source connectivity.
Attribution is also in [THIRD_PARTY_NOTICES](../../THIRD_PARTY_NOTICES.md);
the [original notice](../../fixtures/Canonical/Humanoid/antonia-original-LICENSE.txt)
is preserved byte-for-byte beside the [admission sidecar](../../fixtures/Canonical/Humanoid/antonia-original.sidecar.json).

This is the original base-figure route admitted by the mission, not a new grant
inferred for `char.blend`. The old CharMorph sidecar remains unchanged and its
conversion remains unadmitted. No source rig, weights, morphs, textures,
Blender-specific data, shared library or CharMorph code is consumed. The author's
separate mesh-only CC0 release was discovered but is not the input or license
used here. No Reom or VRoid geometry is used.

Selected OBJ material labels: `skin_ARMS`, `skin_BODY`, `skin_HEAD`, `skin_LEGS`,
`lips`, `nailsFingers`, `nailsToes`, and bilateral sclera/iris/pupil/cornea.
Mouth interior, teeth, tongue, toe caps, invisible polygons, eyebrows, eyelashes
and lacrimals are excluded. Materials are selection evidence only; no texture
or material library is loaded. Subset openings are retained and watertightness
is not asserted.

## Deterministic adoption artifact

The original OBJ contains **38,822 vertices / 38,614 quads**, not the recon's
36,030-vertex converted Blender mesh. The difference is explicit, not silently
treated as identical connectivity.

| Property | Candidate result |
|---|---:|
| Selected vertices | 27,193 |
| Selected authored quads | 27,112 |
| Fixed binding triangles | 54,224 |
| Body triangles | 51,408 |
| Eye triangles | 1,408 per eye |
| Provisional region labels | 33 |
| Semantic joints | 55 |
| Vertex symmetry max reflected residual | 0.00004338 mm |
| Prepared symmetry max reflected residual | 0.00030634 mm |
| Uncovered skin vertices | 0 |
| Maximum skin sum error | below 1e-15 |

Candidate identity: `aetheris.humanoid.antonia.adoption-candidate.v1`.
This is **not** `adult.standard.v1`. The latter remains the historical X0
synthetic identity for regression replay. A future promotion must deliberately
version the final rest geometry, regions, landmarks and binding policy; the
candidate ID cannot silently become a different qualified topology.

Source vertices are selected in original index order; IDs retain their original
indices (`antonia:v:NNNNNN`). Original quad indices and four original vertex
indices are stored in `SourcePolygons`. Two triangles per quad use the fixed
0–2 diagonal; their IDs are `antonia:f:NNNNNN:t0/t1`. Vertex compaction has an
explicit original-index map. No weld, remeshing or face deletion by quality
heuristic occurs. Triangle connectivity hash is
`2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2`.

Region assignment is provisional original-group mapping: collar→shoulder,
shoulder actor→upper arm, forearm→forearm, hand/finger chains→corresponding
regions, thigh/shin/foot/toes→limb regions, waist/abdomen→abdomen, hip→pelvis,
and separate eye regions. Boundary vertex ties use ordinal group ordering;
face ownership retains the original group. This covers every admitted face but
does not yet resolve face/ear, elbow, wrist, patella or ankle subregions.
It is not represented as a completed migration of X0's semantic partition.

Vertex symmetry is stored as a bijective involution, using reflected matching
within 0.001 mm and deterministic tie-breaking. Region/joint semantic pairs
are explicit. Triangle symmetry is not claimed because fixed quad diagonals
need not reflect onto the same triangle. Landmark pairing awaits migration.

## Frame, rest preparation and bind semantics

The source OBJ uses +Y up and has positive-X `l*` groups. Canonical mapping is:

```text
X = -sourceX * 2504.5606618474717
Y = (sourceZ - (-0.00950344604166667)) * 2504.5606618474717
Z = (sourceY - 0.00089007) * 2504.5606618474717
```

The axis transform has determinant +1. Coordinates are mm, +X anatomical
right, +Y forward, +Z up. Scale uniformly normalizes the selected outer extent
to 1750 mm; it is an engineering normalization, not a source physical-scale or
population-average claim. Sole is the selected body's minimum source Y;
pelvis forward origin is the hip/waist shared-boundary centroid. Bilateral
grounding and a reviewed sole protocol remain promotion checks.

Joint vocabulary reuses X0's 55 joints, including its two eye joints. Centers
come from shared source mesh-group boundary centroids: collar/upper arm,
upper arm/forearm, forearm/hand, hip/thigh, thigh/shin, shin/foot and finger
segment seams. Central torso/head and eye proxies are explicit. These are
geometric proxies requiring review, not source control-bone locations.

Skin seeds follow group membership, then undergo exactly 16 half-self,
half-neighbor averaging passes over authored quad-edge adjacency. Binding
diagonals are excluded: the first experiment exposed a 6.74 mm left/right bias
when smoothing used them. Correcting adjacency reduces prepared symmetry
residual to 0.000307 mm. Influences
below 1e-8 are discarded and remaining weights normalized. There are no
helper/control influences. This is a reproducible baseline, **not a qualified
heat/geodesic solver**. Uniform topological averaging has no anatomical
volume-preservation guarantee.

Source-pose binds are rebuilt from these centers. Shoulder rotations align
upper-arm chains 35° from vertical; elbow rotations align forearms to the
same direction. Exact quaternions are in the candidate artifact. The ordinary
`HumanoidPosing` LBS evaluator prepares the connected mesh; regions are not
rotated as disconnected pieces. Prepared joints and inverse binds are rebuilt
in the prepared frame. No source or X0 inverse-bind matrices are reused.
Neutral wrist/finger orientation is not qualified by this chain alignment.

Preparation has zero collapsed triangles and zero dominant-joint-normal
reversal proxies. Source-bind reconstruction maximum is 0.000309 mm.
Numerical preparation success does **not** override the visible shoulder defect.

## Pose gate and diagnosis

All 12 fixed poses are finite and have zero collapsed triangles. The table
reports **max(before/after, after/before)** edge-length ratio: it measures both
stretch and compression and is not an Antonia fitting residual.

| Movement | 35° reversal proxies / max ratio | 70° reversal proxies / max ratio |
|---|---:|---:|
| Shoulder abduction | 0 / 1.616 | 0 / 2.465 |
| Shoulder flexion | 0 / 1.397 | 0 / 1.973 |
| Elbow flexion | 0 / 2.135 | 5 / 5.726 |
| Hip flexion | 0 / 2.960 | 12 / 16.445 |
| Hip abduction | 2 / 4.852 | 56 / 7.128 |
| Knee flexion | 0 / 2.052 | 2 / 4.611 |

At hip-abduction 70°, 31 flagged triangles belong to pelvis and 25 to left
thigh. Hip-flexion 70° flags 10 pelvis / 2 thigh. Elbow-flexion 70° flags 5
upper-arm triangles; knee-flexion 70° flags 2 thigh triangles. Shoulder
screens report zero reversals but still fail visual contour quality.
Exact stable face IDs and worst-edge vertex IDs are retained
in [compact evidence](HUMANOID-X1.evidence.json).

The orientation screen transports each original triangle normal by its
dominant skin influence and compares against the deformed normal. It is a
**proxy**, not a certified intersection or local Jacobian test. Bind
reconstruction is within tolerance, topology is unchanged, and defects occur
at generated blend transitions. Evidence therefore implicates joint proxies
and weighting/rest conversion rather than corrupt connectivity or reused binds;
it does not isolate weights versus proxy placement causally. Self-intersection,
collision and volume-preservation checks remain unqualified.

X0's reversal problem is **not resolved** by adopting better topology alone.
Changing thresholds or smoothing until proxy counts vanish would not establish
anatomical usefulness. Reviewed placement/weights must precede downstream
semantic migration.

## Render evidence and review

Local renders use existing Blender evidence tooling on exported Aetheris OBJ,
flat shading and neutral material. Blender does not calculate the deformation.

![Prepared candidate, front](../../artifacts/local/humanoid-x1/neutral-front.png)

[Side](../../artifacts/local/humanoid-x1/neutral-side.png),
[back](../../artifacts/local/humanoid-x1/neutral-back.png),
[iso](../../artifacts/local/humanoid-x1/neutral-iso.png),
[source pose](../../artifacts/local/humanoid-x1/source-front.png),
[wireframe front](../../artifacts/local/humanoid-x1/wireframe-front.png),
[shoulder](../../artifacts/local/humanoid-x1/wireframe-shoulder.png),
[pelvis](../../artifacts/local/humanoid-x1/wireframe-pelvis.png),
[hand](../../artifacts/local/humanoid-x1/wireframe-hand.png),
[face](../../artifacts/local/humanoid-x1/wireframe-face.png),
[feet](../../artifacts/local/humanoid-x1/wireframe-feet.png).

![Failed hip-abduction witness](../../artifacts/local/humanoid-x1/pose-hip-abduction-70.png)

[Shoulder](../../artifacts/local/humanoid-x1/pose-shoulder-flexion-70.png),
[elbow](../../artifacts/local/humanoid-x1/pose-elbow-flexion-70.png),
[hip flexion](../../artifacts/local/humanoid-x1/pose-hip-flexion-70.png),
[knee](../../artifacts/local/humanoid-x1/pose-knee-flexion-70.png).

These are **assistant visual-screen findings**, not fabricated human signoff:

| Region | Screen | Specific observation |
|---|---|---|
| Face/ears | NeedsReview | Recognizable neutral facial loops, eyelids and ears; no landmark-placement or oral-boundary review yet. |
| Shoulders | Fail | Bulky hooked transitions in prepared neutral instead of coherent shoulder contour. |
| Armpits/chest | Fail / NeedsReview | Armpit crease is distorted by preparation; chest detail remains visible, but shoulder weighting intrudes into its transition. |
| Pelvis/crotch | Fail under pose | Hip abduction creates a sharp folded groin transition; high edge distortion and localized reversal proxies corroborate concern. |
| Hands/fingers | NeedsReview | Fingers remain distinct; original wrist orientation is carried through preparation, not proven neutral; no finger pose sweep. |
| Knees/ankles | NeedsReview / Fail numerical gate | Neutral envelope is legible; 70° knee flexion reports two reversals. No calibrated joint-center review. |
| Feet | NeedsReview | Toes and heels remain distinct; no footwear/sole protocol or posed foot qualification. |

**Human review status: NeedsReview for every region; no human reviewer has
signed off.** Failures already prevent acceptance. Landmark/frame overlays and
morph images are not generated because those semantics are not admitted on
this candidate.

## X0 migration, runtime boundary and unfinished acceptance gates

Preserved: domain types, skeleton vocabulary, shared skin evaluator,
registration qualification, measurement/landmark/attachment types, morph
infrastructure and regression tests. The old body now explicitly says
`NONCANONICAL-SyntheticRegressionFixture` in metadata/provenance. Its legacy
topology ID is retained solely to avoid silently relabeling old artifacts.

Added: original source admission, selected authored connectivity and source
index maps, provisional region/component maps, stored vertex symmetry,
source/prepared bind skeletons, generated weights, controlled preparation,
12-case sweep, hash replay and original attribution. The runtime loader
rejects candidate-shaped or incomplete JSON instead of returning a malformed
canonical instance.

Deferred at the failed deformation gate: all 88 landmark migration/review,
all 19 attachment sites/frames, measurement qualification, morph endpoint
qualification, 1800-mm and longer-limb witnesses, contour protocols and
canonical artifact embedding. No measurements are reported as anatomically
valid merely because an extent is finite. No cloned X0 mappings or hardcoded
X0 morph coordinates are applied to Antonia.

There is no new accepted `adult.standard.v1` artifact. Candidate replay uses
only pinned original source inputs and .NET; saved output contains full
geometry, joints, weights and transforms and requires no source tooling to
inspect. That is not yet proof of an independent **qualified canonical
runtime**. Normal unit tests remain offline and do not fetch Antonia.

Three fresh-context agents received only the public guide, this report and
the candidate artifact, with no implementation source:

- **Basic use: BLOCKED.** The real `inspect --input .../antonia-adoption-candidate.json`
  command exited 1: `Expected a canonical humanoid artifact; an adoption candidate
  is not a canonical runtime instance.` No height, shoulder width or wrist frame
  was invented, and X0 was not substituted.
- **Morph: BLOCKED.** The real `morph` command with `--height 1800 --leg-length 50`
  produced the same rejection and no output file. Therefore no measurements or
  topology-invariance result exists for a morphed X1 body.
- **Adapter thinking: PASS for conceptual understandability only.** The agent
  correctly retained a new reference as imported evidence, preserved future
  canonical IDs/connectivity and required reviewed correspondence. It identified
  the ambiguous request to target legacy `adult.standard.v1`; the public guide
  now explicitly blocks adapter implementation until a qualified target and
  its versioned identity are selected.

## Reproduction and validation

```powershell
pwsh -File scripts/qualify-humanoid-x1.ps1
pwsh -File scripts/qualify-humanoid-x1.ps1 -SkipFetch -SkipFullTests
```

The command chain verifies pinned mesh/license inputs, builds IDs and maps,
constructs joints/weights, prepares the rest-pose candidate, emits the bounded
sweep and JSON/OBJ evidence, checks a second byte-identical replay, renders,
and runs tests. It exits on tool/test errors. A completed reproduction is
explicitly reported as **canonical promotion blocked**, not a passed sweep.
Usage is in [the public research guide](../public/humanoid-research.md).

| Deterministic item | SHA-256 |
|---|---|
| Candidate artifact | `af45c535ab21d93ae2c23f7a63f71925f3c6cfc6a68e5af9c04e1ab0033834dd` |
| Connectivity | `2e2f787499ba2da1f02bab14f352b86070938fbb5d7cae60e8bd7d59edb2b7a2` |
| Source-pose OBJ | `099572385a643aa369eb0a999a90c49a507a1818adae52730c76f7f5993fb764` |
| Prepared-pose OBJ | `fe3b785bfaab444a01b3a9f8d89ef422a6eade69cc05adf5e290fff9d270675f` |
| Hip-flexion 70° OBJ | `9b71dbaa8401e57b62feee35f5b3cb267852e9348745f97e4915b61cf52d909b` |

No canonical/morph-result hash exists. Candidate/evidence JSON and all 14 OBJ
files were byte-identical across two runs. Tests: focused humanoid **30/30
passed**; final solution build passed with 0 errors and 2 existing WebAssembly
warnings. The initial build passed with 7 WebAssembly/analyzer warnings. A
premature rebuild during the full CLI tests hit MSB3027/MSB3021 DLL locks;
the final serialized rebuild after test completion passed.

Full active solution tests (`Category!=SlowCorpus`): **3,615 passed, 2 failed,
0 skipped**, 3,617 cases across 20 test assemblies. This initial run was not
green. The two failures were:

- `PublicMarkdownRelativeLinksResolveInsideRepository`: the new public guide
  was checked while the release report was still being written. The report now
  exists; the isolated recheck passed.
- `DisplayPrepare_Ftc07_ReturnsPartialDisplayInsteadOfWholeBodyFailure`: an
  existing STEP display test received an empty collection under the full run.
  The unchanged isolated recheck passed. No timing budget/assertion was weakened.

The repository layout guard passed for 4,357 tracked files. New files were
separately checked against the allowed module/tests, scripts, public docs,
release records and fixture locations; no expanded generated asset is tracked.
`git diff --check` passed. Logs remain under `artifacts/local/humanoid-x1/`.

The actual Aetheris.CLI `--help` and `modules --json` paths were inspected; there is no
humanoid-specific inspection command, so the existing research tool was
extended with one `adopt-antonia` command rather than introducing a second CLI.
