# Humanoid reference-rig baseline

HUMANOID-X5 makes an authored reference rig, rather than a surface seam, the authority for canonical humanoid joint centers and rest frames. The checked artifact is `fixtures/Canonical/Humanoid/antonia-reference-skeleton-v1.json`. Normal Aetheris execution reads that JSON; Blender, Rigify, and CharMorph Python are extraction and qualification tools only.

## Source and canonical records

`AntoniaReferenceRigArtifact` keeps the imported inventory and the canonical mapping separate. Each imported record retains its source joint ID, source parent, head/tail, deform flag, semantic name, and canonical global rest transform. Each canonical record retains its `HumanoidJointKind`, canonical parent, source ID, confidence, role, collapsed helpers, global rest frame, and mapping errors.

The adapter validates the topology ID and exact 55-joint domain. It maps reviewed source relationships rather than guessing by string alone. The source inventory contains 182 bones; 55 canonical records are emitted and 128 irrelevant or collapsed source helpers are recorded but not promoted. The synthetic canonical `Root` has no source bone. Generated Rigify controls are not imported.

`CanonicalReferenceRigAdapter` is source-name-independent: a different importer may use arbitrary stable joint IDs and its own adapter ID, provided it supplies the same reviewed records and exact canonical joint domain. Shared validation checks source hierarchy integrity, unique mapped source IDs, a single synthetic root, helper existence, and canonical/source frame agreement. `AntoniaReferenceRigAdapter` is the thin topology-specific admission wrapper; the Blender extractor remains intentionally Antonia-specific.

Use the semantic API rather than source names:

```csharp
var artifact = AntoniaReferenceRigAdapter.Load(path);
var skeleton = AntoniaReferenceRigAdapter.BuildSkeleton(artifact, artifactSha256);
var hip = skeleton.GetJoint(HumanoidJointKind.LeftHip);
var hipRest = skeleton.GetRestFrame(HumanoidJointKind.LeftHip);
var sourceEvidence = skeleton.Reference!.JointMappings
    .Single(mapping => mapping.CanonicalJoint == HumanoidJointKind.LeftHip);
```

The left hip comes from the mapped `thigh.L` metarig head at approximately `(-91.121, -34.063, 963.007) mm`. It does not come from the crotch seam. A surface landmark describes visible geometry; `LeftHip` describes the internal parent-socket/femur-head interface. X5 models those coincident interface origins through the adopted `LeftHip` rest frame rather than inventing two independently offset anatomical points.

## Transform convention

The canonical frame is right-handed millimetres: +X anatomical right, +Y forward, +Z up. Extraction applies `diag(-1,-1,+1)`, a uniform `1001.8242360918738 mm/unit` scale, and translation `(0, 23.8019447, -2.2292280) mm`; the axis conversion has positive determinant.

Aetheris uses `System.Numerics` row vectors. A child local rest transform accumulates as:

```text
globalChild = localChildToParent * globalParent
posedGlobal = localRotation * localRest * posedParentGlobal
posedPoint = modelPoint * inverseBind * posedGlobal
```

The Blender column-vector matrix is transposed during extraction. Local transforms are recomputed from the mapped globals, then every global bind and inverse bind is regenerated. The runtime consistency and kinematic residual tolerance is 0.001 mm, appropriate for float-backed human-scale matrices.

## Posing and current boundary

`RequestedHumanoidPose` addresses canonical joint kinds. Hip and shoulder use bounded ball interfaces; knee and elbow use bounded hinge interfaces. Their axes are converted into each adopted source-derived local rest frame. The X2 constraint architecture and X3 Judgment candidate architecture remain in place.

REST-X1 now treats those request values as absolute anatomical states. The
adapter measures the source rest from canonical joint centers, solves the
requested canonical bone direction, and keeps the resulting source-local
rotation private. See [Semantic humanoid pose normalization](humanoid-pose-normalization.md).

The X5 frame baseline is qualified, but the entire Antonia surface is not yet an accepted production humanoid. Existing X1-generated weights were deliberately retained to isolate the effect of corrected frames. Neutral, hip 30°, hip abduction 15°/30°, and shoulder 30° pass the mechanical screen; larger poses expose downstream weight/deformation failures. Landmarks, attachments, measurements, and morph channels were never migrated into the Antonia research candidate, so there is nothing valid to recompute yet. Shape-aware propagation remains a later bounded task.

Reproduce locally:

```powershell
pwsh -File scripts/qualify-humanoid-x5.ps1
```

The extraction script opens the pinned files with embedded script execution disabled, verifies their hashes, and emits the deterministic reference artifact. The qualification command can then run solely from the candidate JSON and checked reference artifact.
