# STEP-VOID-A1: BREP_WITH_VOIDS import support and shell-role preservation

Date: 2026-05-14  
Status: Outcome A — success

## Scope

A1 extends the exact STEP BRep importer to accept `BREP_WITH_VOIDS` rigid roots in addition to `MANIFOLD_SOLID_BREP`, then preserves shell roles by assigning imported shells into `BrepBodyShellRepresentation(outer, inner[])`.

No exporter behavior was changed.

## Root classification change

`Step242RigidRootClassifier` now classifies exact rigid roots from either:
- `MANIFOLD_SOLID_BREP`
- `BREP_WITH_VOIDS`

Assembly-like classification still triggers when multiple exact rigid roots are found.

## Import behavior for BREP_WITH_VOIDS

Importer now parses:
- outer shell reference at argument 1,
- void shell list at argument 2.

Rules:
- empty void list is a deterministic validation failure,
- each shell reference must resolve to `CLOSED_SHELL` directly or through `ORIENTED_CLOSED_SHELL`.

## Oriented shell handling

`ORIENTED_CLOSED_SHELL` is minimally supported for root shell references:
- importer resolves its referenced shell,
- importer records a deterministic info diagnostic for the resolution,
- no orientation normalization beyond reference resolution is introduced in A1.

## Shell role preservation

Importer builds one topology body with multiple shells and sets:
- `OuterShellId` = first (root outer) shell,
- `InnerShellIds` = remaining (void) shells.

This preserves topology role semantics without converting voids into booleans.

## Diagnostics added/clarified

- missing rigid root message now covers both root kinds,
- assembly-like message now references exact rigid roots generically,
- `BREP_WITH_VOIDS` empty void-list failure source: `Importer.TopologyRoot.BrepWithVoids`,
- unsupported shell-reference kinds return precise shell-type diagnostics.

## Through-hole policy unchanged

Through-hole exports remain governed by existing exporter semantics and still emit `MANIFOLD_SOLID_BREP` when represented as a single shell.

## A1.1 regression cleanup (gate restore)

Observed failures after initial A1 landing:
- `Step242ExporterTests.ExportBody_Ctc02RoundTrip_PreservesPlanarFaceWithCircularBoundaryNearExpectedCenter` failed.
- NIST canonical hash snapshots drifted.
- CLI assembly-like JSON test emitted `rigidRootCount: null`.

Root causes and fixes:
- **Shell assignment bug in importer shell-role materialization**: orphan planar faces were appended to the outer shell before later void-shell assignment, causing face sharing/omission side-effects in round-trip cases. Fix: map imported `faceEntityId -> FaceId`, assign shell face sets explicitly by entity id, and append only truly orphan (unreferenced) faces to the outer shell.
- **CLI root-count parser stale expectation**: parser only recognized legacy `" MANIFOLD_SOLID_BREP"` wording. Fix: parse numeric rigid-root count from the generalized assembly-like message and update human-readable labeling to “exact BRep rigid roots”.

Result:
- Named CTC02 regression passes.
- Focused `BrepWithVoids|Step242|Void|Shell` test filter passes.
- Canonical `./scripts/test-all.sh` gate passes (warnings unchanged; no suppression).

## Next step (A2)

Implement export root selection planning via JudgmentEngine policies while preserving current emission behavior:
- manifold policy,
- brep-with-voids policy,
- unsupported topology policy with explicit rejection reasons.
