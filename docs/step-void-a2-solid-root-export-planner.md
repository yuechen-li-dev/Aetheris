# STEP-VOID-A2: JudgmentEngine STEP solid-root export planner

Date: 2026-05-14  
Status: Outcome A — success

## Purpose

A2 formalizes STEP solid-root selection behind a bounded planner using `JudgmentEngine`, while preserving existing export behavior.

## Planner

`StepSolidRootExportPlanner` evaluates three bounded policies:

- `ManifoldSolidBrepExportPolicy`
- `BrepWithVoidsExportPolicy`
- `UnsupportedShellTopologyPolicy`

Decision model includes:

- selected `StepSolidRootExportKind` (`ManifoldSolidBrep`, `BrepWithVoids`, `Unsupported`),
- selected policy name,
- policy evaluations (admissible/rejected, score, reasons),
- decision diagnostics,
- shell summary (body count, shell count, outer shell, inner shells).

## JudgmentEngine usage

Planner evaluates all policy candidates through `JudgmentEngine<BrepBody>` with deterministic tie-breaking.

- manifold/void policies score high when admissible,
- unsupported fallback scores low and is admissible when blockers exist,
- diagnostics record both admitted and rejected policy states.

## Behavior preservation guarantee

Exporter emission semantics are unchanged:

- single-shell/no inner shell => `MANIFOLD_SOLID_BREP`
- explicit outer+inner shells => `BREP_WITH_VOIDS`
- multi-shell without explicit shell roles => deterministic unsupported failure

No geometry, topology, importer, or Firmament materialization behavior was changed in A2.

## Through-hole vs void distinction

Through-hole shapes remain `MANIFOLD_SOLID_BREP` when represented as one connected shell.

`BREP_WITH_VOIDS` remains reserved for explicit enclosed inner void shells (`ShellRepresentation.InnerShellIds`).

## Unsupported and ambiguous cases

Unsupported policy blockers include:

- no body,
- multiple topology bodies,
- missing shell representation for multi-shell topology,
- invalid shell ids,
- ambiguous multi-shell role states.

Diagnostics make blocker reasons explicit.

## A3 next

A3 should harden planner diagnostics propagation into exporter diagnostics and add focused negative corpus tests for ambiguous/invalid multi-shell shell-role maps.
