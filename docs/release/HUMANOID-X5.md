# HUMANOID-X5 — reference rig adoption and pose parity

**Verdict: Meaningful progression. Not Accepted.**

Aetheris now has a deterministic, source-attributed 55-joint canonical rest-frame baseline derived from the known-good Antonia Blender metarig. The old seam-derived hip and silhouette-derived limb proxies are no longer authoritative in the X5 path. Rest globals, parent-relative locals, binds, inverse binds, local pose axes, and the full requested pose corpus execute through the existing X2 constraint and X3 Judgment systems without Blender at runtime.

The result is not Accepted because unchanged X1-generated weights still produce unacceptable surface deformation at the larger poses. This is now a narrower downstream failure: all joint-center and link-length residuals are at or below 0.001 mm, while the failed meshes show groin webbing, compression, and normal reversals. Copying the reference frames removed the upstream semantic mismatch; it did not make the existing skin field compatible with those corrected pivots.

## Source skeleton and deterministic artifact

The extractor reads the hash-pinned CharMorph Antonia inputs with embedded script execution disabled. It inventories 182 metarig bones, maps 54 source bones plus the synthetic canonical root into the existing 55 semantic joints, records 128 ignored/collapsed helpers, and imports no generated Rigify controls or source weights.

| Evidence | Value |
|---|---|
| `char.blend` SHA-256 | `4bc468c44c8ca7c6971cac3692c5d670b09f6723687e89cd49f258c2a8251c69` |
| `metarig.blend` SHA-256 | `eee2e7185331595fb0912e03aace583d7e02c5a467a04f1af0b786e2ecf687e2` |
| canonical reference artifact SHA-256 | `0647f681f67043610e8829fff20ca29901648b3c3245b589f2942ba453e13148` |
| source bones / canonical joints / ignored helpers | 182 / 55 / 128 |
| neutral bind reconstruction maximum | 0.0006808 mm |
| source-to-canonical mapped center maximum | 0 mm |
| source-to-canonical mapped orientation maximum | 0° |
| left/right center symmetry residual | 0 mm |

The checked artifact retains source IDs, source parents, heads/tails, source globals, canonical parents, mapped globals, confidence, roles, and collapsed helpers. Extraction replay is byte-identical. The [public reference-rig guide](../public/humanoid-reference-rig.md) freezes the row-vector accumulation, quaternion placement, handedness, and exact coordinate conversion.

## Major joint mapping

| Canonical joint | Source joint | Parent | Canonical center mm (X, Y, Z) | Center / orientation delta | Confidence |
|---|---|---|---|---|---|
| LeftHip | `thigh.L` | Pelvis | −91.12, −34.06, 963.01 | 0 mm / 0° | High |
| RightHip | `thigh.R` | Pelvis | 91.12, −34.06, 963.01 | 0 mm / 0° | High |
| LeftKnee | `shin.L` | LeftHip | −145.26, −8.44, 528.36 | 0 mm / 0° | High |
| LeftAnkle | `foot.L` | LeftKnee | −196.56, −12.19, 91.06 | 0 mm / 0° | High |
| LeftShoulder | `upper_arm.L` | LeftClavicle | −155.38, −56.41, 1414.85 | 0 mm / 0° | High |
| LeftElbow | `forearm.L` | LeftShoulder | −418.07, −40.92, 1399.54 | 0 mm / 0° | High |
| LeftWrist | `hand.L` | LeftElbow | −618.41, 64.13, 1485.88 | 0 mm / 0° | High |

Right knee, ankle, shoulder, elbow, and wrist are exact mirrored mappings. The artifact also records spine, neck, head, clavicles, toes, eyes, and all retained finger joints. Palm, face, and intermediate trunk helpers are explicitly collapsed rather than leaked into the public joint vocabulary.

## Before and after hip authority

| Authority | Left center mm | Distance from Blender reference |
|---|---|---:|
| Original Poser deformation pivot | −90.164, −33.803, 964.531 | 1.818 mm |
| Blender `thigh.L` metarig head | −91.121, −34.063, 963.007 | 0 mm |
| old Aetheris seam proxy | −80.449, −17.009, 857.462 | 107.446 mm |
| X5 canonical `LeftHip` | −91.121, −34.063, 963.007 | 0 mm |

The historical Poser-to-old-Aetheris discrepancy was 108.813 mm. X5 does not reinterpret that authored pivot as medically exact anatomy; it adopts it faithfully as the baseline deformation authority.

![Reference frames, front](../../artifacts/local/humanoid-x5/reference-frame-overlay.front.png)

Blue source centers and smaller green canonical centers are concentric. RGB lines show the imported canonical rest bases. Side view and deterministic render metadata are in the same artifact directory.

## Pose corpus and visual review

All 16 cases solve deterministically. The maximum joint-center and link-length residuals are below 0.001 mm. Degenerate triangles are zero throughout; self-intersection is not qualified. Whole-body p95 edge ratios round near 1 because most of the mesh is stationary, so the minimum and maximum plus reversal counts are the useful failure witnesses.

| Pose | Min edge ratio | Max bidirectional ratio | Reversals | Mechanical / visual review |
|---|---:|---:|---:|---|
| Neutral | 1.00 | 1.00 | 0 | Pass |
| Hip flexion 30° | 0.27 | 3.76 | 0 | Pass; NeedsReview visually |
| Hip flexion 45° | 0.08 | 13.15 | 23 | Fail |
| Hip flexion 70° | 0.14 | 6.88 | 175 | Fail; groin webbing |
| Hip flexion 90° | 0.06 | 17.92 | 268 | Fail |
| Hip abduction 15° / 30° | 0.71 / 0.54 | 2.07 / 3.17 | 0 / 0 | Pass; NeedsReview visually |
| Hip abduction 45° | 0.30 | 4.23 | 36 | Fail |
| Knee 45° / 90° | 0.25 / 0.11 | 3.95 / 9.10 | 2 / 113 | Fail |
| Shoulder abduction 30° | 0.59 | 1.70 | 0 | Pass; NeedsReview visually |
| Shoulder abduction 60° / 90° | 0.17 / 0.11 | 5.81 / 8.93 | 50 / 186 | Fail |
| Elbow 45° / 90° / 120° | 0.23 / 0.19 / 0.22 | 4.45 / 5.19 / 4.57 | 0 / 98 / 3508 | Fail |

Assistant visual classification: neutral **Pass**; hip45/70/90 and abduction45 **Fail**; knee90 **Fail**; shoulder60/90 **Fail**; elbow90 **NeedsReview** visually but **Fail** mechanically. No user signoff is claimed.

![Hip flexion 70°](../../artifacts/local/humanoid-x5/hip-flexion-70.png)

![Shoulder abduction 90°](../../artifacts/local/humanoid-x5/shoulder-abduction-90.png)

The femur now rotates around the authored source pivot rather than the crotch seam. The remaining webbing follows from applying a field generated around the old proxy to the corrected mechanism; no pose vertex or historical bad edge was special-cased.

## X3 Judgment replay

The same six X3 candidates and unchanged policy were replayed. No candidate becomes admissible for the three required failures.

| Pose | X3 LBS min / max / reversals | X5 LBS min / max / reversals | Winner change |
|---|---|---|---|
| hip70 | 0.0608 / 4.66 / 12 | 0.1454 / 6.37 / 118 | none → none |
| hip90 | 0.0580 / 5.44 / 55 | 0.0558 / 7.61 / 155 | none → none |
| abduction45 | 0.1825 / 2.48 / 26 | 0.3045 / 4.23 / 18 | none → none |

Compression improves at hip70 and abduction45, while maximum stretch and some reversal counts worsen. This is not a claimed overall surface improvement. It demonstrates that X3 failure was not only the old pivot: corrected frame semantics expose an incompatible downstream skin/deformation model. X1 and X2 used the old proxy; X3 added candidate selection but retained it; X5 changes frame authority and preserves both architectures.

## Runtime independence and impact audit

The runtime adapter accepts the serialized artifact and recomputes local rests, global binds, and inverse binds without Blender. `HumanoidSkeleton.GetJoint` and `GetRestFrame` expose canonical IDs; Blender names remain inspectable provenance only. The constraint solver now supports adopted nonidentity rest rotations and bounded shoulder ball interfaces without changing its fail-closed behavior.

A fresh-agent genericity review found and prompted removal of two avoidable couplings: the shared loader now proves the exact intended 55-joint set rather than merely counting enum values, and `CanonicalReferenceRigAdapter` accepts arbitrary source IDs plus an artifact-specific adapter ID. A test renames the full source inventory (for example `thigh.L` to `foreign::thigh_L`) and reconstructs the identical semantic skeleton. Antonia-specific topology admission and Blender extraction remain in the thin Antonia wrapper/profile.

The Antonia research candidate contains no migrated semantic landmarks, attachment sites, measurements, or morph channels. Therefore X5 cannot honestly claim their recomputation. Surface-only values would be unchanged; joint-derived values and attachment bases must be introduced against this baseline later. World-frozen joints across morphs are explicitly not admitted.

## Remaining boundary

- Source weights, generated Rigify controls, drivers, sliding joints, and Poser joint-controlled morphs are not admitted.
- The full source posed-deformation stack is not reproduced, so same-pose geometry parity is unavailable.
- Existing X1 weights were generated around the old skeleton and are the isolated blocker for larger poses.
- Shoulder coupling/scapular motion, collision, certified self-intersection, facial rig behavior, and shape-dependent joint propagation remain unqualified.
- The full converted geometry component retains its prior research-only provenance boundary; X5 admits the derived frame baseline, not the entire source package.

The next convergent milestone is bounded skin compatibility against this frozen frame reference—not another joint-center reinterpretation.

## Independent comprehension and regression

Three fresh read-only agents were given only the public contract and artifacts. The frame-mapping agent traced `LeftHip` to the hash-pinned `thigh.L` head and correctly distinguished it from the crotch seam. The same-pose agent independently recovered the 70° request, source-derived center, solver/version/hash identity, 0 mm center residual, 0.0001259 mm maximum link residual, and failed Judgment result. The generic-adapter agent found the domain/name coupling described above; the shared fix and differently-named-rig test were then added rather than waiving the test.

Validation after that fix:

- X2 + X5 focused tests: 24 passed; full `Aetheris.Humanoid.Tests`: 63 passed.
- Core Judgment/fillet/chamfer focus: 112 passed.
- Firmament surfacing/fillet/chamfer focus: 141 passed.
- Full solution build: passed with the two existing WebAssembly SQLite varargs warnings.
- Full parallel active suite: four unrelated bounded-tessellation tests timed out or observed load-sensitive fallback output. Each of the four passed immediately in isolation (two Core, one Server, one Firmament). No humanoid test failed.

The qualification script also reproduces the reference artifact byte-for-byte, replays evidence byte-for-byte, and enforces all frame, bind, symmetry, corpus, focused humanoid, Judgment, and CAD gates.
