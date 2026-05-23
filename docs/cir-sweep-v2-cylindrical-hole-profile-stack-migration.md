# CIR-SWEEP-V2: Cylindrical hole-family → ProfileStackExtrude executor migration

## Scope

This milestone migrates cylindrical hole-family execution routing from ad hoc repeated 3D subtract paths to the ProfileStackExtrude executor backend.

Migrated in V2:
- Through-hole
- Stepped-hole

Deferred from profile-stack in V2 (legacy bounded routes retained):
- Blind-hole
- Counterbore

Not migrated in V2:
- Countersink
- Chamfered-entry

## Adapter shape

`ProfileStackExtrudePlanAdapter.TryFromHoleRecoveryPlan(...)` now:
- validates explicit placement metadata,
- enforces rectangular box + Z-axis bounded scope,
- accepts only cylindrical segments,
- rejects/defer conical profiles with explicit diagnostics,
- maps ordered profile segments into contiguous `ProfileStackLayer` entries.

## Layer mapping (active profile-stack routes)

- Through-hole: single cylindrical through layer.
- Stepped-hole: bounded 3-tier cylindrical stack from explicit placement tiers.

## Diagnostics

Execution emits explicit diagnostics for:
- profile-stack adapter selection,
- variant kind,
- per-layer z/radius,
- cylindrical acceptance,
- conical deferral to conical route,
- profile-stack composition invocation/success/failure,
- no 3D subtract route usage for migrated variants.

## STEP smoke

STEP behavior remains unchanged at exporter boundary; migrated variants continue exporting manifold solid BRep in smoke lanes.

## Fallback boundaries

Blind-hole and counterbore currently emit explicit profile-stack deferral diagnostics and execute via legacy bounded placement-driven subtract routes.

Conical variants (`Countersink`, `ChamferedEntry`) intentionally remain on existing conical primitive/Boolean execution lanes in V2.

## V3 recommendation

CIR-SWEEP-V3 can evaluate conical profile-stack/revolve/sweep route candidates for countersink/chamfered-entry without changing public APIs.


## AIR-V1 note

Through-hole and stepped-hole profile-stack routes now materialize a bounded AIR scaffold (`AirProfileStackExtrude`) before executor emission. Blind/counterbore/conical variants are explicitly deferred in AIR-V1 and remain on their legacy executor routes.


## AIR-V2A.1 update (2026-05-23)
Through/stepped remain AIR/profile-stack. Blind/counterbore are explicitly deferred in AIR with legacy bounded execution retained; countersink/chamfer remain conical route.
