# STEP-ORIENTATION-X1 — Derived Face Orientation and `same_sense` Demotion

## Executive verdict

**Meaningful progression, not accepted.** The implemented path derives and projects face orientation once at STEP import, retains `same_sense` as cross-check evidence, and prevents downstream face bindings from exposing a raw-or-canonical boolean with ambiguous meaning. Hostile flags, void shells, open shells, nonmanifold rejection, Difference Engine, sheet-metal, PMI/identity, and the isolated NIST display-orientation corpus are qualified. Acceptance remains blocked by pre-existing/now-exposed NIST export-import volume instability on `STC-10`, `FTC-08`, `FTC-11`, and `CTC-02`, plus the intentional canonical-orientation hash changes that still require a reviewed snapshot re-baseline.

## Algorithm

1. Decode support surfaces and trims without applying `ADVANCED_FACE.same_sense`.
2. Preserve the source face ID, source surface ID, and source flag in `SourceFaceOrientationEvidence`.
3. Derive per-loop support-space winding, build deterministic per-shell adjacency through shared edges, and propagate from the smallest face ID. More than two incident faces is rejected as nonmanifold. Contradictory propagation is diagnosed and classified ambiguous; the independently derived support-boundary orientation is retained instead of guessing from the source flag.
4. Tessellate the locally consistent candidate as a bounded signed-volume oracle. Outer shells target positive signed volume. Inner void shells target negative signed volume. A required change flips the whole connected component.
5. Leave open shells locally consistent and globally unknown. Classify near-zero or unavailable closed-shell volume as ambiguous rather than trusting the source flag.
6. Cross-check each derived orientation with source evidence and emit `step-orientation-source-mismatch` warnings without changing the derived result.

The existing exact/qualified occupancy infrastructure remains the independent material-side authority for Continuum and FEA. X1 does not introduce another point-in-solid engine or mutate support parameterizations.

## Type boundary

```text
ImportedFacePatch
  FaceId + SurfaceGeometryId + SourceFaceOrientationEvidence
  (orientation absent)
        |
        v
StepFaceOrientationResolver
        |
        v
FaceGeometryBinding.Orientation : ResolvedFaceOrientation
OrientedFacePatch : qualification + provenance + comparison
```

Downstream canonical consumers are display tessellation, SurfaceMeshIR, mass properties, STEP export, CLI section analysis, Continuum boundary queries, Firmament selection/materialization, SheetMetal recognition, and Surfacing support selection. `FaceGeometryBinding.SameSense` no longer exists.

## Diagnostics and reporting

- `Importer.StepOrientation.SourceOrientationMismatch`
- `Importer.StepOrientation.ShellOrientationAmbiguous`
- `Importer.StepOrientation.NonManifoldOrientation`
- `Importer.StepOrientation.GlobalOrientationUnknown`
- `Importer.StepOrientation.OrientationPropagationConflict`

`aetheris analyze ... --json` now reports derived faces, source true/false/missing distribution, agreements, mismatches, ambiguous components, global flips, and qualification counts.

## Hostile fixtures

The focused suite exports a canonical Aetheris box, flips one `ADVANCED_FACE.same_sense`, and separately flips every face flag without changing topology or geometry. Both hostile imports produce the same canonical face orientations and signed volume as the trusted control. Mismatch diagnostics identify the corrupted evidence. A synthetic duplicated-face shell is rejected rather than guessed.

## Void, open-shell, and round-trip behavior

The `BREP_WITH_VOIDS` witness uses a box with a spherical cavity. The outer shell resolves to positive signed contribution and the inner shell to negative signed contribution. The open planar witness resolves local orientation but reports global orientation unknown. STEP export serializes canonical orientation, and reimport preserves topology and canonical orientation rather than reproducing hostile source flags.

## Cleanup

- Removed the raw `SameSense` property from face bindings.
- Replaced downstream reads with typed `ResolvedFaceOrientation`.
- Kept pcurve and `EDGE_CURVE.same_sense` handling intact; those are parameter/traversal mappings, not face orientation.
- Removed Web Runtime's second display-patch orientation pass.
- Preserved Assembly export's existing single-projection behavior.

## Validation

Qualified on 2026-09-22:

- STEP orientation semantics, hostile flags, void/open/nonmanifold, export projection: 29/29 passed.
- Difference Engine storage gates and line/arc mass witness: 10/10 passed.
- Chamfer CLI fixtures: 7/7 passed.
- Sheet metal: 99/99 passed.
- Firmament inline STEP, canonical construct safety, and advanced grammar/PMI identity: 86/86 passed.
- NIST display orientation corpus in isolation: 16/16 passed. `CTC-04` is structurally open and is now required to report `DerivedLocallyConsistentGlobalUnknown` rather than pretending to have an outward solid orientation.
- Three independent fresh-agent checks passed: one corrupted flag remains canonical with a mismatch diagnostic; an open shell makes no outwardness claim; and no second face-orientation application remains in Assembly/Web/tessellation.

Representative CLI corpus report (`aetheris analyze ... --json`):

| Fixture | Faces | Source true/false | Agree/mismatch | Qualification | Global flips |
| --- | ---: | ---: | ---: | --- | ---: |
| NIST CTC-01 | 117 | 63/54 | 115/2 | 117 derived-qualified | 0 |
| NIST FTC-06 | 187 | 102/85 | 182/5 | 187 derived-qualified | 0 |
| NIST CTC-04 | 484 | 99/385 | 3/481 | 484 local-only/global-unknown (open shell) | 0 |
| NIST STC-06 | 144 | 65/79 | not comparable | 144 ambiguous (one component) | 0 |

The STC-06 row is intentionally reported as unresolved rather than being counted as derived merely because its display path succeeds.

Full Kernel.Core qualification was attempted and stopped after it isolated the remaining blockers:

- `nist_stc_10_asme1_ap242-e2.stp`: export-import volume changed from approximately `197357` to `-52028.4`.
- `nist_ftc_11_asme1_ap242-e2.stp`: export-import volume changed from approximately `3989.48` to `-4389.53`.
- both admitted `FTC-08` variants and `CTC-02` also fail the existing enclosed-volume round-trip gate.
- audit snapshots report unchanged topology/diagnostic counts but changed canonical hashes on files whose emitted face orientations changed. These snapshots are deliberately not auto-approved while the round-trip volume gate is red.
- several display tests exceeded their execution budgets only during the concurrently scheduled full-project run; the dedicated 16-file display-orientation run passed in 1 minute 49 seconds.

The next isolated blocker is periodic/degenerate support-boundary winding through STEP export-import. The resolver reports exact shell/component/edge/loop evidence for propagation conflicts, so the remaining work is bounded and diagnosable; promoting source `same_sense` back to authority would hide rather than solve it.
