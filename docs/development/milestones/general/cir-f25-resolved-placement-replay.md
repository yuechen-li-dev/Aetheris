# CIR-F2.5: resolved placement facts in NativeGeometry replay log

CIR-F1 replay operations only carried `PlacementSummary` text. That was readable but not deterministic enough for fall-forward replay because future replay would have to re-resolve placement from selector text and context.

CIR-F2.5 adds structured placement facts captured from the production execution pass and stored on each `NativeGeometryReplayOperation`.

## Structured facts recorded

Each replay operation now includes:

- placement kind (`None`, `Offset`, `OnFace`, `AroundAxis`, `Unsupported`)
- authored anchor feature/port when selector-shaped
- authored offset vector
- resolved final translation vector from production placement resolution
- resolved/unresolved flag
- unresolved diagnostic message

`PlacementSummary` is intentionally retained for human-readable debugging.

## Supported in CIR-F2.5

- no placement: resolved zero translation
- `place.offset`: resolved offset + translation
- `place.on_face`: authored selector anchor + offset + resolved translation

## Deferred/unsupported in CIR-F2.5

- `place.around_axis` replay facts are recorded as unresolved (`Kind=AroundAxis`, `IsResolved=false`) with explicit diagnostics.
- This is intentional scaffolding; production behavior remains unchanged and successful models still succeed.

## F3 consumption guidance

CIR-F3 fall-forward should consume `ResolvedPlacement` as the authoritative replay placement input where `IsResolved=true`, and should not re-derive placement from summary strings.

## Behavior statement

This milestone is behavior-preserving:

- no runtime transition to `CirOnly`
- no CIR-to-BRep materialization enablement
- no placement semantic expansion
- no selector/topology naming expansion (SEM-A0 preserved)
