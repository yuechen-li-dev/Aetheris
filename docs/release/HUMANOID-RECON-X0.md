# HUMANOID-RECON-X0 — canonical humanoid bootstrap study

Date: 2026-09-16. Status: **Meaningful progression**. Research only; no production humanoid runtime, canonical asset, importer, public API, or Firmament syntax was added.

## Executive verdict

**Yes, a credible bootstrap path exists, but this study does not qualify a distributable canonical body.** Start with Antonia's explicitly licensed base figure as a reference, retain attribution, and commission or obtain a separately licensed deformation-ready template whose topology Aetheris can version. Register that template using explicit landmarks and a deterministic non-rigid fit. Aetheris owns semantic identities and validation; this does not erase source copyright or establish exclusive ownership of fitted geometry.

The blocker removed is uncertainty about what the actual candidate packages contain and which source roles are credible. The next gates are specific: qualify the provenance of any Antonia conversion/morph/rig data selected beyond the original figure; resolve Reom's missing license version/scope before using it for fitting; obtain the canonical template; then pass the two-reference registration experiment below. No source has been admitted as a complete canonical package.

Use VRoid as a reference for decomposition into body, face, hair, garments, expressions, and runtime adapters. Do **not** use its body meshes as bootstrap data. Its published guidelines require a separate license for certain model-generating applications and prohibit program reverse engineering. A VRM parser is technically feasible from public specifications, but the format does not confer rights to each avatar. [VRoid guidelines](https://vroid.com/en/studio/guidelines), [2023 output-feature restriction update](https://vroid.com/en/studio/notice/4Kz3Qwd7q0eD8ngIuMzwnz).

## Evidence and reproducibility

The adjacent [provenance manifest](HUMANOID-RECON-X0.manifest.json) records source revisions, candidate authors, license evidence, per-file SHA-256 values, exclusions, and permitted research use. [Compact measurements](HUMANOID-RECON-X0.evidence.json) preserve the inspection results. Raw assets, renders, bone hierarchies, and logs remain in ignored `artifacts/local/humanoid-recon-x0/`.

Pinned repositories:

| Repository | Revision | Use |
|---|---|---|
| [CharMorph](https://github.com/Upliner/CharMorph) | `ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2` | Read-only data-model/code audit; no code copied into runtime |
| [CharMorph-db](https://github.com/Upliner/CharMorph-db) | `a8512ac299c0cb1724c7b5417267c94bef024d64` | Only Antonia/Reom checked out; no recursive submodules |
| [VRM specification](https://github.com/vrm-c/vrm-specification) | `821c11b250d8c70d5804ee13431e42bee56ea9c0` | Public schema and binary sample inspection, not body fitting |

Reproduce from repository root (PowerShell; `blender` may be replaced by its installed executable path):

```powershell
python scripts/fetch-humanoid-recon-x0.py
blender --background --factory-startup --disable-autoexec --python-exit-code 1 --python scripts/inspect-humanoid-recon-x0.py
python scripts/summarize-humanoid-recon-x0.py
dotnet run --project Aetheris.CLI -- --help
dotnet run --no-build --project Aetheris.CLI -- modules
dotnet run --no-build --project Aetheris.CLI -- reconstruct --help
dotnet build Aetheris.slnx -f net10.0 --no-restore -m:1
dotnet test Aetheris.slnx -f net10.0 --no-build --filter 'Category!=SlowCorpus'
pwsh -File scripts/Test-RepositoryLayout.ps1
```

Inspection uses Blender 5.2.2 LTS without add-ons or embedded scripts. It reads raw `cm_antonia` / `cm_reom` mesh datablocks, not CharMorph-finalized output, and uses NumPy with pickle disabled. This deliberately avoids invoking GPL add-on code or proprietary ARP tooling. SHA checks precede inspection. Inspection permission is not admission for fitting or redistribution.

## Candidate inventory and license boundary

The pinned database contains `antonia`, `reom`, `mb_female`, and `mb_male`. The first two are the complete set explicitly labeled CC-BY in this revision. The MB directories are excluded from bootstrap and were not checked out. Vitruvian appears in broader CharMorph documentation as CC0 and exists in a separate repository; it is outside this task's **CC-BY-only** candidate scope. Do not silently substitute it. [CharMorph licensing documentation](https://charmorph-docs.readthedocs.io/en/main/Introduction.html).

| Item | Antonia Polygon | Reom |
|---|---|---|
| Path | `characters/antonia/` | `characters/reom/` |
| Primary author | Olaf Delgado-Friedrichs | ComplexAce (Mesh) |
| Additional credits in config | phantom3D, MikeJ, DieTrying (Morphs), Fenrissa and others | Hope (Morphs, Textures) |
| Exact license evidence | Config says CC-BY 3.0; `license.txt` specifies Attribution 3.0 Unported and identifies original Antonia release 1.2.0 | Config says CC-BY; no version, license URL, or `license.txt` in pinned character tree |
| Admission | Original figure is a credible reference candidate; converted package components need scope review | Inspection only; fitting blocked pending exact license and component scope |
| Dependencies | Rigify; external shared rig tweak YAML; shared material names; underwear, eyelashes, textures | Rigify and legacy metarigs; optional ARP rig; tearline asset; eyebrow library; 4K/8K textures |

Primary file evidence: [Antonia config](https://github.com/Upliner/CharMorph-db/blob/a8512ac299c0cb1724c7b5417267c94bef024d64/characters/antonia/config.yaml), [Antonia license](https://github.com/Upliner/CharMorph-db/blob/a8512ac299c0cb1724c7b5417267c94bef024d64/characters/antonia/license.txt), [Reom config](https://github.com/Upliner/CharMorph-db/blob/a8512ac299c0cb1724c7b5417267c94bef024d64/characters/reom/config.yaml).

**Component scope is not proven uniform.** Antonia's notice explicitly distinguishes separately distributed extras. The config credits additional morph/texture authors but is not a per-file grant from them. The base figure's license is stronger evidence than assuming every `.npz`, Blender conversion, texture, generated rig, or shared dependency has the same grant. Reom identifies mesh and morph/texture authors, but supplies only an unversioned license label. Neither package has a complete component-by-component provenance chain. Shared `shared_lib.blend`, `tweaks/`, root textures, ARP implementation, and MB asset submodules are not approved bootstrap inputs. Do not ship bundled Blender text blocks or rig scripts.

For an Antonia-derived artifact, use this credit, with a concrete modification list and artifact ID appended:

> Based on Antonia Polygon by Olaf Delgado-Friedrichs, with contributions credited by the source package to phantom3D, MikeJ, DieTrying (morphs), Fenrissa and others. Source: https://sites.google.com/site/antoniapolygon/ ; CharMorph-db revision a8512ac299c0cb1724c7b5417267c94bef024d64, characters/antonia. Original figure licensed under Creative Commons Attribution 3.0 Unported: https://creativecommons.org/licenses/by/3.0/ . Research changes: neutral-gray rendering with source materials and modifiers removed. No endorsement implied.

This is the attribution for the local Antonia rendering, not clearance of every bundled component. Preserve the original notice when redistributing an authorized figure/adaptation. Reom's provisional credit is “Reom mesh by ComplexAce; morphs and textures by Hope; source CharMorph-db, pinned revision above.” It is **incomplete for distribution** until the exact CC-BY version and scope are established. [CC-BY 3.0 legal text](https://creativecommons.org/licenses/by/3.0/legalcode.en).

Legal questions to resolve before release: scope of converted/added Antonia files, Reom's version and contributors' grants, derivative status of reference-fitted geometry and averaged bodies, and any VRoid-derived output workflow. Independent connectivity and new vertex IDs are not a legal cleansing process. This report records evidence and engineering gates, not a legal opinion.

## Geometry and rig inspection

| Measured property | Antonia | Reom |
|---|---:|---:|
| Raw vertices | 36,030 | 8,434 |
| Raw polygons | 35,990 | 8,438 |
| Polygon sizes | 35,990 quads | 248 triangles; 8,076 quads; 112 pentagons; 2 hexagons |
| Boundary edges | 64 | 76 |
| Edges incident to >2 polygons | 0 | 0 |
| Zero-area polygons at raw-unit area <1e-12 | 0 | 0 |
| Non-finite coordinates | 0 | 0 |
| UV layers | 1 | 1 |
| Material slots | 16 | 11 |
| Reflected nearest-vertex RMS / maximum, normalized mm | 0.00000966 / 0.000240 | 0.934 / 9.309 |
| Rigify metarig bones | 182 | 177; legacy 163 |
| Other rig | none inspected | ARP armature 553 bones |
| Weight-file groups | 183 | 165 |
| Joint-anchor groups | 250 | 267 |
| L2 shape entries | 261 packed | 110 files: 105 sparse NPZ plus 5 dense NPY |
| L3 expression entries | 33 packed | 51 sparse NPZ |

Symmetry is a reflected nearest-vertex diagnostic across local X=0, after rescaling each raw Z extent to 1,750 mm; it is **not** a bijective symmetry map or a fitting error. Raw extents are 1.7468134 and 1.8249911 Blender coordinate units. Scene units were not used to assert real stature. Face counts include internal/separate body components in the named datablock. Boundary counts do not establish which openings are intended. No self-intersection test, deformation sweep, or full anatomical quality qualification was performed.

The local front renders are [Antonia](../../artifacts/local/humanoid-recon-x0/antonia-neutral.png) and [Reom](../../artifacts/local/humanoid-recon-x0/reom-neutral.png). These links require reproduction; images and assets are not committed. Antonia presents an adult feminine body with abducted arms and bent elbows, distinct fingers/toes, face, eyelids/lips and ears. Reom presents a lean adult masculine body with lowered abducted arms. Its raw unfinalized render has pronounced faceting/shading artifacts over shoulders, torso and thighs; do not interpret that as the final CharMorph appearance or as fit failure. Both offer neutral expression geometry, but neither is already the proposed canonical rest pose.

| Region | Evidence and suitability limit |
|---|---|
| Face, eyes, mouth | Antonia config names cornea/iris/sclera/mouth; Reom names cornea/iris/pupil/mouth/tongue/gums/teeth. Facial and jaw/eye rig names plus expression data exist. Config/material regions support interior-geometry investigation, but closed cavities, eyelid thickness and oral contacts are not qualified. Neutral face is sufficient for X0. |
| Ears | Visible on both front renders; no behind-ear surface or deformation qualification. Constrain ears separately during fitting. |
| Shoulders/armpits | Antonia has an explicit shoulder tweak and sliding-elbow configuration; its arm pose differs from Reom's. Raw shoulder shading and underarm correspondence require close-up and posed review. |
| Hands | Separated fingers in source render and finger chains in rigs. No grip, web-space collision, palm-width or fingertip fit evidence yet. |
| Crotch/hips | Surface visible; overlapping thighs and projection across anatomical regions remain critical registration hazards. No collision-free claim. |
| Knees | Source chains and Antonia sliding-knee settings exist. No loaded flexion or volume-preservation evidence. |
| Feet | Separate toes visible; Antonia has individual toe-related rig anchors. Sole support plane, footwear clearance and toe-flex deformation remain unqualified. |

Antonia is the stronger **first reference** because its license notice is explicit, topology is quad-only and raw symmetry is near machine precision. Reom is useful as a lower-density, differently proportioned comparison after license resolution. This is an evidence-based suitability decision, not a beauty ranking or a claim that either is medically representative.

Metarigs are authoring scaffolds, not counts of anatomical joints. Antonia contains spine, face, jaw, eyes, ears, finger/palm and toe chains; Reom adds a separate generated ARP rig with controls/helpers and twist-named bones. Store full imported hierarchy and bone rest matrices in an adapter record; map only reviewed anatomical joints. Generated Rigify DEF/MCH/control bones and ARP controls must not become canonical anatomy. The original Blender files contain neither a finalized skinned body nor body shape keys in these raw mesh datablocks: Antonia has zero mesh vertex groups, Reom two. External weight and morph files are authoritative inputs to CharMorph finalization.

All inspected weight indices fit their source vertex ranges and all weights are finite in [0,1]. Summing **all** groups gives maxima 2.138 (Antonia) and 2.000 (Reom), because support groups are mixed with deformation groups. Do not normalize all groups blindly. Resolve the actual deformation-bone set first, audit coverage, then normalize only its weights. Joint-anchor groups are weighted surface-point constructions, not skin weights; their sums have a different meaning.

## CharMorph data model and MB-Lab lineage

| Concept | Observed representation | Recoverable semantics / limitation |
|---|---|---|
| Base | `char.blend`; `morphs/L1/Default.npy`; Antonia `faces.npy` | Stable source index domain; positions and connectivity must be checked together |
| Morphs | L1 bases; L2 shape controls; L3 expressions; dense arrays or sparse `idx` + `delta`, packed NPZ variants | Named bounded source deformations; names do not make them calibrated dimensions |
| Joints | NPZ names/counts/indices/weights | Weighted surface anchors for bone heads/tails, recomputable after morphing |
| Rig | Blender armatures plus YAML selection, settings, tweaks and sliding joints | Source hierarchy/rest geometry and control policy; generation depends on Blender/add-ons |
| Groups | Packed index/weight records | Deformation weights mixed with support masks; retain role distinction |
| Materials | Ordered config names, Blender material library, texture settings and sets | Face material indices are usable regions, not an anatomical ontology; raw file slots can be empty |
| Presets/poses | Loader supports named JSON data and per-type folders | No preset/pose files found in these two candidate trees; MB package availability must not be assumed for them |
| Measurements | README explicitly lists measures/automodelling as lacking | Do not claim imported anthropometry; Aetheris needs independent conventions |
| Metadata/dependencies | YAML title, authors, license, object names, asset and rig paths | Stronger than a final mesh, weaker than a complete provenance graph |

Code evidence at the pinned revision: [charlib.py](https://github.com/Upliner/CharMorph/blob/ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2/lib/charlib.py), [morphs.py](https://github.com/Upliner/CharMorph/blob/ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2/lib/morphs.py), [rigging.py](https://github.com/Upliner/CharMorph/blob/ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2/lib/rigging.py), [utils.py](https://github.com/Upliner/CharMorph/blob/ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2/lib/utils.py), [README](https://github.com/Upliner/CharMorph/blob/ec5e13dcd5a7330b35fa7474b3abd2f406d77bd2/README.md).

CharMorph's stated MB-Lab lineage is base meshes/morphs and reimplementation of many features, with no MB-Lab code currently claimed in its README. It explicitly changes database representation and reduces hard coding. Listed additions include alternative-topology morph transfer, Rigify full-face support, realtime asset fitting/masks, and hairstyles. MB-Lab's anthropometric measurement concept must not be confused with implemented CharMorph functionality. MB-Lab computes measurements using authored vertex strips/axes; see [upstream morphengine.py](https://github.com/animate1978/MB-Lab/blob/master/morphengine.py). No MB geometry, measurement indices, weights, or morph arrays are admitted here. CharMorph Python files carry GPL-3.0-or-later notices; that code license is separate from character-data licenses.

## VRoid authoring and export audit

This is public-documentation research and public VRM inspection. No local user `.vroid` files were searched, no proprietary project parser was implemented, and no Studio code was decompiled. No newly exported VRoid Studio session was run. Binary evidence below establishes VRM semantics, not preservation of every current Studio export option.

| Format or hook | What is evidenced | Feasibility decision |
|---|---|---|
| `.vroid` | Editable Studio project; official documentation says it stores editor data and can act as a dress-up avatar | Richer pre-export state exists; no public binary schema or supported parameter-reading API found in the reviewed sources |
| `.vroidcustomitem` | Editor parts retained for reuse | Useful conceptual boundary for presets, clothes and hair; do not promise portable slider/guide extraction |
| `.vrm` | Documented glTF 2.0 plus VRM extensions | Implement a bounded public-format adapter later; body/face authoring slider history is not specified |
| `.xavatar`, `.xwear` | Avatar/costume interchange in dress-up workflows | Official Unity XWear Packager imports/exports these workflows; this is not evidence of a Studio authoring-parameter plugin API |
| `.xroid` / XRoid | Dress-up project/container concept in current help | Preserve composition distinction; no binary parser proposed |
| VRoid SDK / Hub API | Loads/distributes Hub avatars to applications | Runtime integration, not an exporter for Studio's parametric authoring state |

Sources: [editable project semantics](https://vroid.pixiv.help/hc/en-us/articles/39513343637529-What-is-vroid), [file formats](https://vroid.pixiv.help/hc/en-us/articles/23731198070809-About-file-formats-used-by-VRoid-Studio), [XWear Packager](https://vroid.pixiv.help/hc/en-us/articles/39513294532377-What-is-the-XWear-Packager), [dress-up tooling](https://vroid.pixiv.help/hc/en-us/articles/38723276855065-What-software-do-I-need-to-use-the-dress-up-feature), [Hub SDK](https://developer.vroid.com/en/).

Body/face sliders, editable hair strokes/groups and bounce settings, UV texture authoring and reusable clothing parts show useful authoring decomposition. Project editability supports the inference that sufficient authoring state persists, but not a claim about its internal schema or exact stored parameter names. Runtime hair meshes/springs are not the original guides. Garment mesh identity is not a sewing pattern. [Studio feature description](https://vroid.com/en/studio).

Local accessibility is documented: projects can be saved/backed up, with custom items under the platform's VRoid data directory. Access to bytes is not a supported interchange contract. A future custom exporter should first request a documented vendor API/permission; absent that, use public VRM plus an independently authored sidecar for explicitly supplied measurement/attachment metadata. Label any future undocumented-project inspection as reverse-engineering research with unresolved terms applicability; do not make it a product dependency. The current guidelines are a material constraint, not merely an unspecified risk. The linked policies page failed to load during research; the official guidelines and official update notice were accessible. [Save locations](https://vroid.pixiv.help/hc/en-us/articles/900007039003-Where-are-3D-models-and-custom-items-saved).

### What survives VRM 1.0

| Semantic content | Public representation | Lost/not guaranteed |
|---|---|---|
| Bone semantics | `VRMC_vrm.humanoid.humanBones` maps standard roles to nodes | Source rig controls, medical joint centers, mechanical limits |
| Geometry/skinning | glTF primitives/accessors; JOINTS/WEIGHTS; skins/inverse bind matrices | Canonical connectivity, authoring construction history |
| Expressions | Preset/custom expressions bind morph targets, material colors and texture transforms; override and binary behavior | Original face sliders and all expression construction rules |
| Materials | glTF PBR/unlit plus MToon | Source paint layers and garment design rules |
| Metadata | Author/license/use permissions in `meta` | Automatic permission for canonical training/fitting datasets |
| Secondary motion | `VRMC_springBone` springs, colliders, collider groups | Hair guides, physical constitutive models or validated contact mechanics |
| First-person / gaze | Mesh annotations and look-at range maps, bone/expression mode | Anatomical vision or ergonomic gaze certification |
| Node constraints | Roll/aim/rotation extension where present | Full source controller graph |

The pinned specification's [VRMC_vrm](https://github.com/vrm-c/vrm-specification/tree/821c11b250d8c70d5804ee13431e42bee56ea9c0/specification/VRMC_vrm-1.0) and sibling spring/material/constraint definitions are the adapter authority. VRM uses meters, right-handed Y-up and a specified +Z-forward T-pose; convert explicitly. Do not infer physical sole height from ankle node height alone. [VRM T-pose](https://github.com/vrm-c/vrm-specification/blob/821c11b250d8c70d5804ee13431e42bee56ea9c0/specification/VRMC_vrm-1.0/tpose.md).

Actual public binary sample observations:

| VRM 1.0 sample | Human roles | Meshes / primitives / skins | Materials | Springs / colliders / groups | Constraints |
|---|---:|---|---:|---|---:|
| Seed-san (VirtualCast) | 51 | 5 / 21 / 5 | 17 | 9 / 8 / 2 | 23 |
| Constraint Twist Sample (pixiv) | 54 | 3 / 13 / 3 | 13 | 22 / 13 / 12 | 14 |

Both contain 18 preset expression keys and first-person annotations. Seed-san uses expression look-at and 43 targets on its morph-bearing primitives; the pixiv sample uses bone look-at and 57. Repeated primitive counts are not independent global shape channels. The manifest preserves their VRM Public License metadata and hashes. They are **format fixtures only**, excluded from canonical geometry/morph fitting. No evidence establishes either binary as an export of the current Studio build.

### Role comparison

| Axis | CharMorph CC-BY candidates | VRoid / VRM |
|---|---|---|
| Anatomy/topology | Adult organic reference meshes, auditable source index domains | Stylized authoring; runtime topology depends on export |
| Morphs/measurements | Source deformation arrays; no CharMorph measurement engine | Rich sliders pre-export; exported expressions are not anthropometry |
| Skeleton/face | Metarigs, weighted anchors, shape/expression data | Standard humanoid role mapping, expressions and gaze in VRM |
| Clothes/hair | Asset fitting/masks; hairstyles and external assets | Strong authoring decomposition and dress-up interchange |
| Openness/license | Open code; mixed data licenses; per-component screening required | Public runtime format, proprietary authoring; output-tool restrictions |
| Automation/recovery | NPY/NPZ/YAML accessible without running add-on | VRM readily inspectable; rich project extraction not established |

The proposed split is supported: CharMorph for screened anatomical reference inputs; VRoid for authoring concepts and public runtime adapter lessons. Neither is permanent semantic authority.

## Bootstrap strategy choice

| Strategy | Benefits | Costs and verdict |
|---|---|---|
| A. Adopt licensed topology | Fastest useful rig/morph bootstrap; avoids new correspondence problem | Permanent source attribution; source density/openings and deformation behavior inherited. Valid fallback if ownership means semantic control, not independent authorship. Not yet qualified as whole package. |
| B. Independently licensed template + non-rigid registration | Stable Aetheris IDs, controlled density/regions; references can change | Needs one good template, landmark authoring, solver and deformation qualification. **Preferred staged direction**, not proof of non-derivative geometry. |
| C. Temporary topology, retopologize later | Quick visual demos | Invalidates vertex-bound morphs/weights/landmarks; expensive migration. Keep temporary imported studies explicitly noncanonical. |
| D. Mean of registered references | Common coordinates enable mean/PCA and controlled variation | Requires B or A first, compatible pose and semantic correspondence, diverse lawful data. Two bodies yield at most one nonzero centered PCA mode. Not an initial neutral-body authority. |
| E. Procedural coarse body fitted to references | Cheap envelope/solver tests; independently generated connectivity | Does not solve hands, face, shoulders or usable edge loops. Engineering proxy only, not a shortcut to high-quality canonical topology. |

The strong prior for B is technically sensible when stable region/landmark authority is the goal. It is not cheaper than A on day one, and cannot manufacture a good template from nothing. Obtain a rights-cleared template once from a skilled topology author or a separately audited reusable source. Do not ask Codex to sculpt production anatomy. If X0 must deliver rapidly, explicitly choose A with retained license obligations rather than calling copied topology independently authored.

## Canonical conventions and topology

Proposed study convention: **millimeters; right-handed; +X anatomical right, +Y forward, +Z up**. Origin is the mid-sagittal projection of pelvis center onto the common sole support plane, not a moving pelvis joint. Nominal adult neutral height is 1,750 mm, a coordinate normalization choice rather than a population mean. Store source-to-canonical affine transform, source unit evidence, and any stature normalization separately. The VRM mapping for this declared frame is **`(x,y,z) -> (-1000*x,1000*z,1000*y)`**, a proper rotation plus scale, followed by the documented origin translation. Verify anatomical side using labeled left/right nodes and determinant >0.

Choose an **A-pose** as the provisional deformation rest pose: arms about 35 degrees away from vertical, slight elbow/knee flexion, neutral wrists, separated fingers, feet parallel and grounded. Store exact joint rest transforms in the eventual template; angles here are design intent, not a binding matrix specification. A-pose reduces shoulder extremes and exposes underarms enough for fitting/clothing. T-pose simplifies VRM exchange and correspondence but stretches the shoulders; relaxed arms hide armpits and create torso/hand proximity. Engineering measurements use a separately specified measurement pose. Clothing fitting and reach tasks use explicit poses, never silently change the bind pose.

One canonical semantic surface, with derived LODs, is preferred. Preserve connectivity, winding, component boundaries, seam charts and stable vertex/face IDs under a versioned `topologyId`; position changes must not change that ID. Derived LODs have their own mesh IDs and mappings back to canonical triangles/regions. Normals and render triangulation are derived caches. Breaking connectivity changes create a new topology version with explicit migration or invalidation of bindings.

Planning bands, not measured performance promises: 1–3k vertices for coarse clearance envelopes; 8–20k for standard body plus useful hands/neutral face; 30–60k+ for detailed face/body. Antonia's 36k and Reom's 8k bracket useful reference densities. X0 should not require cinematic face density. Preserve shoulder/hip flow, elbow/knee flexion loops, wrist/ankle transitions, finger web spaces and tips, eyelid/lip boundaries. Declare eye/oral components and permitted openings explicitly rather than demanding every disconnected component form one solid.

Start adult-only. Fit testing should vary height, shoulder/hip ratio, limb/torso proportions and body mass proxies; the two candidates are not a population sample. Prefer one connectivity domain with multiple validated neutral shapes if it behaves well. Admit separate anatomical base domains when distortion or deformation proves one domain insufficient. Do not promise independent sex/body-mass/age sliders or coverage of children/elderly anatomy.

## Deterministic registration design

No fitted prototype was produced. There is no independently authorized canonical template in this milestone, and Reom is not cleared as a second fitting input. Substituting AGPL content, fitting one model to its own perturbed vertices, or presenting nearest-vertex symmetry as registration success would not answer the intended question. The permitted design path is specified here so HUMANOID-X0 can run without CharMorph internals.

1. **Admit inputs.** Verify manifest revision/hash, component license, allowed use and attribution. Require neutral body mesh, units/frame evidence, pose, anatomical region labels and paired landmarks. Keep imported topology immutable. Missing units or license are blocking diagnostics, not guessed defaults.
2. **Prepare correspondence.** Separate body/eyes/oral interior/hair/garments. Mark holes and unobserved regions. Review left/right identity. Match source and template to the same registration pose with reviewed rig transforms or explicit manual landmarks; do not stretch across pose differences using nearest-point projection alone.
3. **Align.** After physical-unit conversion, solve a positive-determinant rigid landmark transform with scale fixed to one, then bounded regional proportional adjustments of the template. A similarity solve with free uniform scale is permitted only in an explicitly stature-normalized experiment; record that scale separately. Preserve the reference's supplied physical scale. Never flatten limb length by an unconstrained whole-body affine fit.
4. **Fit coarse to fine.** Start with a cage or coarse deformation graph. Use robust same-region closest-surface correspondences with normal/distance rejection. Reduce stiffness on a fixed schedule only when admissible. Freeze unsupported face/interior regions or give them explicit low confidence.
5. **Project cautiously.** Bounded final point-to-triangle projection in the same region; reject crossing the midline, joining fingers, or snapping a limb to torso. Preserve topology and UV indexing throughout.
6. **Regularize and validate.** Recompute derived normals, retain the original connectivity hash, inspect every failure region, and fail closed on collapse/inversion/contact failures. Do not silently remesh or repair after validation to hide a failed fit.
7. **Publish evidence.** Store source/target hashes, solver version, settings, landmark bindings, residuals, rejection reasons, output hash and attribution. Canonical promotion is a separate validated status transition.

Initial objective, with lengths normalized by template height inside the optimizer and reported in mm outside it:

```text
E = wL * mean(confidence_l * ||B_l(V) - target_l||²)
  + wD * mean(area_i * robust(point_to_surface(V_i, same_region_target))²)
  + wR * local_rigidity_or_laplacian_displacement(V, V_rest)
  + wS * paired_symmetry_error(V)       [only for a declared symmetric target]
```

Use landmarks + robust distance + one regularizer first. ARAP/local rigidity helps protect joint neighborhoods; a displacement Laplacian is simpler but may shrink volume. Treat normal compatibility as correspondence gating initially. Edge stretch, anatomy-region crossing, joint regularity and collision tests are admission gates before adding penalty terms. A self-collision penalty is warranted only if rejection alone cannot converge. No need to implement ten competing energies at once. A decreasing-stiffness non-rigid ICP schedule has a published basis: [Amberg, Romdhani and Vetter, 2007](https://shapemodelling.cs.unibas.ch/gravis-site-archive/publications/2007/CVPR07_Amberg.pdf). This report does not claim implementation or convergence equivalence to that paper or R3DS Wrap.

When future code selects among bounded fitting strategies, use the existing JudgmentEngine with explicit admissibility, measured residual/distortion cost, fixed tie-breaking and rejection evidence. Ordinary coordinate conversion and fixed solver steps do not need utility scoring.

### Required fit evidence and acceptance procedure

Run one template against Antonia and a second independently cleared, differently proportioned reference. Use the same topology hash and landmark vocabulary. Report bidirectional **area-weighted sampled point-to-triangle** distance, RMS/p95/max in mm, sampling method/density/seed, and unsupported-region coverage. Nearest-vertex distance is not an adequate substitute. Report landmark RMS/max separately, preferably including held-out landmarks. Preserve errors by face, ears, shoulders/armpits, hands, crotch, knees and feet.

Also record inverted/degenerate triangles, triangle-pair self-intersections excluding adjacent pairs, minimum separation in finger/armpit regions, edge length ratios and percentiles relative to rest, symmetry partner residuals, and joint-center displacement. If a detector is absent, record “not tested,” not zero. Open source cavities must be classified before closed-surface requirements are applied.

Provisional experiment budgets: core landmark RMS <=5 mm and max <=15 mm; body-surface RMS <=5 mm and p95 <=10 mm on admitted outer-skin regions; zero newly inverted/collapsed faces and no newly introduced self-intersections. These are screening proposals, **not ergonomic or medical accuracy certification**. Report max errors and every failed region even if averages pass. Freeze production tolerances only after the two-reference evidence establishes a useful tradeoff.

Render source, template-before, fitted result, wireframe and landmark/error overlays in matched front/side/back views; add close-ups for face, ears, shoulders, armpits, hands, crotch, knees and feet (eight regions; shoulders/armpits are grouped in the inventory table). Store measurement settings with each image. Current fit metrics, flipped-face counts, collisions and before/after overlays are **not available** because no fit ran. Current source renders cannot replace that evidence.

### Handoff contract for the first fitting implementation

This is a design handoff, not an executable solver recipe. Current commands reproduce inspection only. HUMANOID-X0 must supply the missing template, reviewed input sidecars and a frozen experiment configuration before implementing a fit command. No existing `aetheris reconstruct` invocation fills this gap.

The CC-BY-only rule applies to **external anatomical reference data** in this study. A commissioned template may instead have an explicit assignment or license covering modification, redistribution and use in generated output. Record that grant separately; it must not be treated as a CC-BY reference or admitted merely because its topology was independently authored. Each admission record needs the exact files/hashes, component grants, reviewer/date, intended use, attribution, dependency exclusions, and an explicit approved/rejected decision. The current manifest records no approved fits.

Minimum sidecar shape (illustrative research schema, not a new public API):

```json
{
  "schema": "humanoid-fit-input-draft-v1",
  "assetId": "reviewed-reference-id",
  "assetSha256": "REQUIRED",
  "admissionRecord": "REQUIRED-reviewed-record-id",
  "role": "reference",
  "frame": { "unit": "mm", "axes": "+X:right,+Y:forward,+Z:up", "originRule": "pelvis-projection-to-sole-plane" },
  "sourceToCanonicalMatrix": "REQUIRED-16-column-major-values",
  "physicalScaleEvidence": "REQUIRED-source-measurement-or-unit-declaration",
  "pose": { "id": "REQUIRED", "jointTransformsArtifact": "REQUIRED", "preparation": "reviewed-rig-repose-or-authored-neutral" },
  "topology": { "id": "REQUIRED", "connectivityHash": "REQUIRED", "bindingTriangulationVersion": "REQUIRED" },
  "regionsArtifact": "REQUIRED-stable-face-id-to-region-map",
  "landmarks": [
    { "id": "surface.head.crown", "faceId": "REQUIRED", "triangleWithinFace": 0, "barycentric": [1,0,0], "confidence": 1.0, "placementProtocol": "upright-head-highest-outer-skin-point-excluding-hair; ties-reviewed", "reviewed": false }
  ],
  "openingsArtifact": "REQUIRED-boundary-loop-ids-and-reasons",
  "unsupportedRegions": [],
  "normalization": { "mode": "physical", "scale": 1 }
}
```

Placeholders and `reviewed:false` deliberately make this example inadmissible. A second template sidecar supplies the matching semantic landmark IDs and template rights. IDs use `surface.<region>.<feature>[.left|.right]`; joint/measurement/site IDs live in separate namespaces. Each landmark needs a placement protocol and reviewed correspondence, not only a name. Ambiguous crown extrema, shoulder proxies and pelvis proxies require manual review and confidence; no inferred zero values.

Exact pose matrices, reposing weights and source bind interpretation must be supplied with the template. Reposing may use a reviewed source rig and its actual skinning calculation, or an explicitly authored neutral mesh with a recorded transform history. Landmarks by themselves do not define that deformation. The 35-degree A-pose choice remains a design preference until exact template matrices exist.

Freeze one deterministic experiment config in X0: regularizer, weights and units, robust loss/cutoff, stiffness schedule, region/normal/distance correspondence gates, stable nearest-triangle tie-break, deformation bounds, maximum iterations and stopping criteria. Also freeze triangulation, sample density/seed, inversion/degeneracy/collision tolerances, stretch/separation limits and pose-sweep matrices. These are currently **unimplemented and uncalibrated**, not hidden source knowledge. Initial screening error budgets above do not replace that config.

The future output bundle must contain fitted canonical-domain positions, unchanged connectivity hash, correspondence/binding records, skeleton and deformation-weight evidence, per-region residual samples/summary, rejected regions and reasons, configuration hash, all input/output hashes, matched views, and attribution. An output with unresolved license, missing protocol, failed geometry or incomplete required landmarks stays an imported research result. A second fit must use the identical template identity and configuration version or explicitly explain the change.

## Landmark, measurement and attachment semantics

Initial surface anchors: crown, chin, nose tip, inner/outer eye corners, mouth corners, bilateral tragus/ear attachment proxies; sternum, navel, shoulder/acromion proxies; bilateral ASIS-like pelvis surface proxies; elbow surface tips, radial/ulnar wrist proxies; knuckles and fingertips; patella proxies, medial/lateral ankle proxies, heels and toe tips. Optional nipple surface landmarks may aid chest registration but are not joint or measurement authority. Label all surface anatomy as an operational proxy where palpated bone location is unavailable.

Use four distinct kinds:

| Kind | Binding and purpose |
|---|---|
| `SurfaceLandmark` | Canonical triangle + barycentric coordinates; moves with surface; manual/estimated provenance and confidence |
| `JointLandmark` | Canonical joint origin/local offset; kinematic proxy with method/uncertainty, not an inferred skin point |
| `MeasurementLandmark` | Operand in a versioned measurement protocol, with pose and derivation; may reference a surface/joint landmark without changing its meaning |
| `AttachmentLandmark` | Region/site plus local frame and offset/clearance; orientation matters as much as position |

Required registration subset: crown/chin/nose, bilateral shoulders, elbows, wrists, pelvis proxies, knees, ankles, heel/toe tips; add finger/face anchors before fitting those regions. Exact IDs, completeness and confidence are protocol-versioned. Missing landmarks block the regions that need them, not an invented zero coordinate. Bind landmarks to the undeformed canonical domain, evaluate in the requested shape/pose, and preserve symmetry partners explicitly.

Measurement conventions proposed for X0:

| Measurement | Definition |
|---|---|
| Height | Crown height above common sole plane in upright measurement pose; hair excluded |
| ShoulderWidth | Straight 3D distance between declared acromion surface proxies, not mesh X bounds |
| HipWidth | Transverse outer-skin width in a stored pelvis-local slice plane; plane selected by protocol |
| Chest/Waist circumference | Length of selected closed outer-skin section at explicit landmark-derived plane; reject open/multiple ambiguous contours, exclude arms |
| TorsoLength | Defined pelvis-to-neck chain/proxy distance; name the endpoints and whether straight or segmented |
| ArmLength / LegLength | Sum of canonical shoulder-elbow-wrist / hip-knee-ankle link lengths; distinguish from skin tape paths |
| Inseam | Defined crotch surface proxy to sole-plane distance in measurement pose; not hip-to-ankle length |
| HeadWidth | Width in an explicitly located head-local transverse section |
| FootLength | Heel-to-longest-toe extent projected onto foot's sagittal axis in measurement pose |
| HandLength | Wrist midpoint to middle-finger tip in the specified extended-hand pose |

ChestDepth is the anterior/posterior extent in the same declared chest section. Every result includes protocol ID, units, pose ID, binding IDs and uncertainty/validity. Do not emit circumference if the plane/contour selection is missing. No numeric body measurements are asserted from this study.

Attachment sites: head, face, sternum/chest, upper back, waist/pelvis, upper arms, wrists, hands, thighs, shins, ankles and feet. Sites reference semantic regions with tangent/normal frames and pose-following rules; arbitrary imported vertex IDs are not their public identity. Wearable design needs skin offset and clearance policies; harness fit needs region/path constraints. Reach envelopes need joint limits and task posture; exoskeleton fit additionally needs calibrated joint-center assumptions. Neither a game rig nor surface fitting alone supplies those guarantees.

## Preliminary HumanoidIR (design only)

```text
ImportedHumanoid
  SourceManifestId, AssetHash, SourceFrame, SourcePose
  MeshComponents { meshId, nodeTransform, vertices, faces, UVs }
  SourceSkins { meshId, jointIndexMap, rawWeights, inverseBinds, bindSpace }
  SourceRigHierarchy, SourceMorphs, Materials
  AdapterMappings, UnknownSemantics, ImportDiagnostics

CanonicalHumanoid
  SchemaVersion, HumanoidId, AdultDomainId, Metadata
  CanonicalFrame { unit:mm, axes, handedness, originRule, referenceHeight }
  Surface { topologyId, stableVertexIds, stableFaceIds, connectivity,
            neutralPositions, UVCharts, regionIds, symmetryMap,
            landmarkBindings, deformationSkinWeights }
  Skeleton { skeletonId, joints, links, restLocalTransforms,
             inverseBindTransforms, jointProxyEvidence, limits? }
  Landmarks { id, kind, typedBinding, symmetryPartner?, confidence, provenance }
  Measurements { protocolId, poseId, operands, evaluatorKind, result?, validity }
  MorphChannels { id, kind, domain, units?, bounds, representation,
                  affectedRegions, dependencies, calibrationEvidence }
  MaterialRegions { regionId, surfaceFaces, materialIntent }
  Attachments { siteId, regionId, anchorBinding, localFrame, offsetPolicy }
  PoseState { poseId, skeletonId, rootTransform, localJointRotations }
  Provenance { inputs, transformations, copiedComponents, generatedComponents,
               attribution, licenses, validationEvidence, admissionStatus }
```

`typedBinding` is a tagged union: surface triangle/barycentrics, joint/local offset, measurement-protocol operand, or attachment-site frame. Cached world position is derived, not a second authority. Surface bindings carry `topologyId`; joint bindings carry `skeletonId`. No public API is frozen here.

Landmark bindings use stable `(faceId, triangleWithinFace, barycentric)` against a **versioned binding triangulation** stored with the topology identity. Render triangulation can be a derived cache; it cannot silently change binding diagonals. Changing binding triangulation requires migration/invalidation just like connectivity changes.

Canonical promotion has two explicit routes: B registers/transfers onto an existing independently authorized canonical template; A deliberately adopts and versions a rights-cleared source topology, retaining its obligations. A generic nonmatching mesh cannot be relabeled with an existing `topologyId`. Its vertex-indexed morphs/weights must be transferred through reviewed correspondence, never copied by index. Missing measurements, regions or landmarks remain diagnostics until supplied.

For transform clarity, use column vectors and child-local-to-parent transforms with `G_child = G_parent * L_child`. For an imported skin, mesh-node bind matrix `M`, global joint bind matrix `G_j`, and inverse bind `I_j`, the usual mesh-local convention is `I_j = inverse(G_j) * M`; evaluate `p_world = sum_j(w_j * G_j_pose * I_j * p_mesh)`. Each adapter must declare deviations and verify that bind-pose evaluation reconstructs `M * p_mesh` within tolerance before reposing. Preserve separate skin memberships/index maps for multiple meshes; do not collapse equal-looking bone names across skins.

Canonical rest pose equals canonical bind pose for each shaped instance. Bake imported mesh-node placement and the declared source-frame conversion into canonical model-space vertices, construct reviewed canonical joint axes/transforms in that same frame, and recompute canonical inverse binds as `inverse(G_j_bind)` (canonical mesh-node matrix is identity). Validate `sum_j(w_j * G_j_bind * inverse(G_j_bind) * p) = p`. Do not reuse source inverse binds after changing rest pose, frame or joint geometry. When shape-dependent joint proxies change, rebuild that instance's bind transforms/inverse binds before applying pose; cache them by shape/skeleton revision. Root/world placement remains separate. Linear blend skinning is the bounded first adapter convention, not a promise of final shoulder deformation quality.

Joint vocabulary: `Root` (world placement), `Pelvis`, `SpineLower`, `SpineMid`, `Chest`, `Neck`, `Head`; bilateral `Clavicle`, `Shoulder`, `Elbow`, `Wrist`, `Hip`, `Knee`, `Ankle`, `ToeBase`; thumb metacarpal/proximal/distal and index/middle/ring/little proximal/intermediate/distal joints, with optional palm metacarpal links. Distinguish a **joint at shoulder** from the **upper-arm link** joining shoulder to elbow. Optional eye/jaw and auxiliary twist joints are explicitly tagged; helper bones are adapter state.

Mapping example: reviewed Rigify `upper_arm.L` head -> canonical LeftShoulder; its tail / `forearm.L` head -> LeftElbow; `hand.L` head -> LeftWrist. `thigh.L` head -> LeftHip; `shin.L` head -> LeftKnee; `foot.L` head -> LeftAnkle. VRM `leftUpperArm`, `leftLowerArm`, `leftHand` supply semantic node candidates. Confirm side, parent hierarchy, rest transforms and pose; matching names alone is insufficient. A generic ambiguous rig stays `ImportedHumanoid` with an unresolved mapping diagnostic. Never fabricate a canonical neck, finger chain or anatomical joint center.

Canonical invariants: finite mm coordinates; explicit right-handed frame and pose; immutable topology identity; valid noncyclic skeleton with unique semantic joints; bindings in range and on the right topology; declared surface components/openings; complete required landmarks for admitted operations; bijective involutive symmetry metadata when claimed; nonnegative normalized deformation weights on admitted vertices; morph domain/bounds and rest dependencies; preserved provenance. Start with weight-sum numerical tolerance 1e-5 after canonical normalization, treating source deviations as diagnostics. This is not a blanket source-weight acceptance threshold.

Evaluation order: neutral shape + admissible morph deformation -> recompute dependent joint proxies/measurements in prescribed measurement pose -> apply explicit skeletal pose -> apply pose correctives -> derive normals/render meshes and posed attachment frames. Measurement results must say whether neutral or posed; shape controls must not absorb pose corrections.

## Morph basis, diversity and ML

| Representation | Suitable role | Limitation |
|---|---|---|
| Raw deltas | Deterministic baseline on fixed topology; inspectable source transfer | Memory, correlated controls and validity range |
| Sparse semantic fields | Region-limited proportional edits | Need explicit transition/volume control |
| PCA | Compact variation after common registration and pose alignment | Latent modes are correlated, sample-limited and not named dimensions |
| Cage | Coarse fit and broad proportion changes | Insufficient eyelids, fingers and close-contact detail |
| Hybrid | Cage/proportions + bounded deltas + later correctives | Dependency order and validity must be explicit |

Recommend hybrid eventually, beginning with direct deltas and a coarse fit. Separate `MeasuredDimension`, `ShapeLatent` (mass/muscularity proxies), `Expression`, and `PoseCorrective`. Height/shoulder width/hip width/torso and limb lengths/chest depth/waist require calibrated constrained solves against explicit measurements; don't label a source morph “millimeters” based on its name. Body mass proxy is not kilograms, and muscle appearance is not strength.

For mean/PCA: clear each input, pose-normalize, register the same topology, reject failed regions, remove only the nuisance transforms declared by the experiment, and compute a documented weighted mean. Preserve scale if studying stature. Center corresponding coordinates and use SVD; keep bounded coefficients and validate held-out fits/deformations. Source morph variants are correlated synthetic samples, not independent people. Two references support a solver demonstration, not population statistics or a broadly valid body model. Mean geometry remains provenance-bearing derived data.

**Small expert model: not needed yet.** After a deterministic baseline and a diverse licensed dataset, it could predict landmarks, cage displacements or bounded coefficients from measurements/images/scans. Fixed IDs/joints/regions turn output into a constrained deformation problem. The validator remains authority; model output cannot waive license, collision, measurement or topology checks. No training, neural inference dependency or claimed scan reconstruction is introduced.

## Aetheris authority and downstream boundaries

Current code evidence: [TriangleSurfaceMesh](../../Aetheris.Reconstruction/TriangleSurfaceMesh.cs) already distinguishes imported, possibly open triangle evidence from closed authoritative export geometry. [PlasticShellIr](../../Aetheris.PlasticShell/PlasticShellIr.cs) and [its module registration](../../Aetheris.PlasticShell/PlasticShellModule.cs) illustrate explicit domain intent and declared capabilities. [Sculpture source](../../Aetheris.Sculpture/Sol1.cs) is a bounded non-manufacturing domain, not a general humanoid substrate. CLI `modules` currently lists no humanoid module; `reconstruct` is structured mesh recovery from PLY, not licensed anatomical registration.

Recommend a future **domain semantic source model beside existing AIR domains**, with explicit module ownership and lowerings. HumanoidIR owns anatomy/pose/measurements and canonical surface correspondence. Reuse kernel math, provenance/diagnostic patterns and suitable mesh validation; do not make the manufacturing BRep or arbitrary imported tessellation anatomical authority. A future Firmament frontend should lower typed humanoid intent to this one domain model, not build a parallel evaluator. This recon adds no module or syntax.

Illustrative future intent only: `Humanoid Person { Reference: AdultNeutral; Height: 1750mm; Pose: MeasurementNeutral; }`. Independent measured sliders need solver qualification before syntax promises them.

The body should remain an organic mesh/subdivision representation. Exact BRep belongs to manufactured attachments, wearable shells, fixtures and robot hardware. A future STEP output can carry a clearly labeled approximate reference envelope if the selected STEP/export path supports it; do not claim existing exact skin conversion. glTF/VRM is a posed runtime projection; Aetheris scene output retains region IDs; game-engine skins are adapters; a robotics kinematic model needs explicit axes/limits; measurement reports preserve protocols and uncertainty.

Keep `GarmentIR` separate (pieces, seams, material, fit/drape, attachment constraints). Existing CharMorph clothes are fitted assets and VRoid garments are authoring/dress-up parts, neither evidence of sewing-pattern or cloth-simulation semantics. Keep hair guides/groups/cards/materials/physics separate from body topology. X0 face scope is a neutral face and landmark bindings; expression basis and facial rig remain optional later work. `HumanHumanoidIR` and `RobotHumanoidIR` may eventually share kinematics and sites, but robot links do not inherit human skin/anatomy assumptions.

## Fresh-agent reproducibility tests

Two fresh-context agents receive only this report, manifest and compact evidence, with no source-internal knowledge. Test one must explain admission, registration, invariants and evidence for a new licensed reference. Test two must map a generic rigged humanoid into imported/canonical states, including ambiguity, units, binding transforms, weights and missing semantics. Both initial reviews passed conceptual understanding but found actionable contract gaps. Corrections added: explicit imported skins and bind-space equations; canonical bind/rest handling; stable binding triangulation; explicit promotion routes; distinct reference/template license rules; fixed physical scale; an input sidecar and output/admission contract; and named deferred solver/pose configuration. Both agents re-read the revised documents and returned **PASS for conceptual handoff/adapter understandability**. They did not certify an executable solver or a completed fit. Final wording corrections distinguish child-local-to-parent transforms and render triangulation from binding triangulation.

## HUMANOID-X0 recommendation

Bound the next milestone to one adult template, one canonical skeleton, typed landmarks/measurement protocols, one neutral shape, basic height/limb proportion controls, and one reviewed source adapter. Begin by resolving input provenance and obtaining the template; these are concrete prerequisites, not tasks for an AI sculptor.

Acceptance requires two licensed reference fits on identical topology, the regional metrics and visual evidence above, valid deformation weights, a small shoulder/elbow/hip/knee pose sweep, explicit anthropometric conventions and a deterministic replayable manifest. If a second licensed body is unavailable, publish a one-reference foundation with that limitation and do not claim mean-body or diversity qualification. Full character creation, cinematic face work, hair/cloth simulation, ML training, all-age coverage and exact human-skin BRep remain out of scope.

## Validation record

The read-only CLI inspection succeeded. Full solution build passed with `-m:1` (0 warnings, 0 errors); the first parallel build hit CS2012 output-file locks in existing demo/host projects. Full active .NET run: **3,582 passed, 5 failed, 0 skipped** across 3,587 cases; the SlowCorpus category was excluded per the active-test convention. All five failing cases passed on isolated recheck (seven cases including theory variants). The initial full run is not reported as green.

| Initial failure | Observed evidence | Isolated result |
|---|---|---|
| `KnownCaller_DirectRecipeAvoidsFacadeRecognitionOverhead` (cir case) | Timing comparison: direct 172.982 ms versus facade 8.578 ms | Pass |
| `DisplayPrepare_Ftc07_ReturnsPartialDisplayInsteadOfWholeBodyFailure` | Empty collection assertion | Pass |
| `Build_AcceptedScaffoldCase_IsDeterministic` | Boolean assertion | Pass |
| `Ftc07ViewMaterialization_FailsWithDiagnosticInsteadOfHang` | Diagnostic expectation received bounded tessellation timeouts | Pass |
| `FreshAuthoredPhoneUsesGenericPlateauAndMeshesEveryFace` | Tessellation exceeded 5,032 ms budget | Pass |

These results are consistent with contention-sensitive timing/budget tests; no runtime code changed and no assertion/budget was weakened. Logs are `dotnet-tests.log` and `failed-*-recheck.log` under the local output directory. Hash validation passed for 190 pinned inspection files. Blender inspection and JSON summarization succeeded, source renders were visually inspected, and Python scripts compiled. Repository layout guard passed for the existing 4,334 tracked files; the new research files were separately checked against the permitted `docs/release/` and `scripts/` locations. No third-party asset is tracked.
