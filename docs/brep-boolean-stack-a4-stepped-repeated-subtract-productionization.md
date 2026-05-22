# BREP-BOOLEAN-STACK-A4: stepped repeated-subtract productionization

## Outcome
Success: bounded stepped-hole recovery is now executable in production for the canonical three-level coaxial stepped shape.

## A3.1 evidence used
A3.1 architecture-lab evidence showed repeated subtract orders are viable for canonical bounded stepped stacks, while unioned-tool and generic profile-stack routes remained deferred. A4 promotes only the bounded repeated-subtract route.

## Production route enabled
`HoleRecoveryExecutor` now executes stepped plans with deterministic order:
1. small through subtract,
2. medium depth blind subtract,
3. large shallow blind subtract.

The route is enabled only after bounded plan-shape validation:
- `HoleKind.Stepped` + `HoleDepthKind.ThroughWithEntryRelief`,
- host rectangular box,
- Z axis,
- exactly three cylindrical profile segments,
- strict radius order `small < medium < large`,
- strict depth order `large < medium < through`.

Unsupported shape mismatch is rejected before boolean invocation with explicit diagnostics.

## Diagnostics
Stepped diagnostics now explicitly record:
- executor start and no STEP export attempt,
- stepped plan shape validation,
- route selection (`small-through -> medium-depth -> large-shallow`),
- per-stage subtract invocation and success/failure,
- final body production.

## STEP smoke and manifold policy
Stepped STEP smoke now runs via executor-produced body and existing `Step242Exporter.ExportBody(...)`. Expected manifold markers are asserted and `BREP_WITH_VOIDS` remains absent.

## Non-goals preserved
- No generic N-level stepped support.
- No unioned-tool production route.
- No direct N-level topology builder.
- No STEP exporter behavior changes.
- No public CLI/API expansion.

## Remaining limits
Support remains intentionally bounded to the canonical three-level coaxial Z-axis stepped-hole family.
