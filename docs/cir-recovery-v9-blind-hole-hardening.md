# CIR-RECOVERY-V9: bounded blind-hole hardening pass

## Scope

V9 hardens the bounded `BlindHoleVariant`/`HoleRecoveryExecutor` route introduced in V8 without expanding family scope.

Included:

- explicit top-entry (`+Z`) and bottom-entry (`-Z`) blind-hole coverage,
- translation-wrapper normalization diagnostics and translated-case regression tests,
- non-ambiguity assertions versus through-hole and counterbore variants,
- boundary/tolerance rejection tightening around near-through and no-entry states,
- unsupported non-translation transform rejection diagnostics.

Excluded (non-goals):

- countersink implementation,
- counterbore behavior changes,
- generic profile-stack/generic blind-hole executors,
- STEP exporter behavior changes,
- public API/CLI expansion,
- topology naming/provenance expansion (SEM-A0 preserved).

## Entry-side behavior

`BlindHoleVariant` now emits explicit diagnostics for detected entry side:

- `top(+Z)` entry,
- `bottom(-Z)` entry.

The bounded executor continues to infer placement from plan geometry and executes both sides using the same primitive+subtract route.

## Translation support

V9 explicitly validates translated `Subtract(translated box, translated cylinder)` blind-hole inputs.
Diagnostics include normalized host/tool translation traces. Execution and STEP smoke remain manifold-solid and avoid `BREP_WITH_VOIDS`.

## Boundary and tolerance policy

V9 preserves conservative admission:

- reject if the tool fails to reach exactly one entry face,
- reject if it effectively reaches both entry/exit faces (near-through/through in tolerance band),
- reject if blind bottom exits host bounds,
- reject tangent/grazing XY clearance.

New diagnostics distinguish entry-face reach, opposite-face reach/reject, and tangent/grazing rejection.

## Non-ambiguity versus other hole variants

Tests assert:

- simple through-hole selects `ThroughHoleVariant`,
- canonical counterbore selects `CounterboreVariant`,
- blind-hole variant is evaluated but inadmissible with explicit reasons when not blind.

Scoring remains deterministic; ambiguity handling is admissibility-first (explicit rejection), not score-only.

## Unsupported transforms

Non-translation transforms (for example rotation-wrapped tool cylinders) remain unsupported by bounded blind-hole recognition and now emit explicit `unsupported transform rejected` diagnostics.
No false rematerialization fallback success is expected.

## STEP export status

No exporter changes were made. V9 smoke stays on existing exporter API and conventions:

- expected: `MANIFOLD_SOLID_BREP`,
- not expected: `BREP_WITH_VOIDS` for this bounded open-blind topology.

## Next milestone

CIR-RECOVERY-V10 should begin bounded countersink recognition/plan/execution hardening, reusing the same policy-family judgment path and admissibility-first diagnostics discipline.
