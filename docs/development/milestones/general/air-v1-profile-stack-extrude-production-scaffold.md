# AIR-V1: production AirProfileStackExtrude scaffold for cylindrical contiguous stacks

## DVT scope

AIR-V1 productionizes the first AIR atom (`AirProfileStackExtrude`) for bounded cylindrical contiguous profile-stack extrusion.

Supported in V1:
- Through-hole route.
- Stepped-hole route.

Deferred to V2:
- Blind-hole no-hole interval execution.
- Counterbore overlap/non-uniform execution.
- Conical countersink/chamfer profile-stack AIR execution.

## Production model location

`Aetheris.Kernel.Firmament/Materializer/AirProfileStackExtrude.cs`

Namespace: `Aetheris.Kernel.Firmament.Materializer`.

## AIR model shape

- `AirProfileStackExtrude`
- `AirProfileStackLayer`
- `AirProfileRegion2D`
- `AirRectangleProfile`
- `AirCenteredCircleLoop`

Model is constrained to:
- outer rectangle,
- optional centered inner circular loop,
- ordered contiguous positive z-span layers,
- explicit global z bounds,
- provenance + diagnostics.

## Conversion route

- `ProfileStackExtrudeSpec -> AirProfileStackExtrude`
- `HoleRecoveryPlan -> ProfileStackExtrudeSpec -> AirProfileStackExtrude`
- `AirProfileStackExtrude -> ProfileStackExtrudeSpec -> ProfileStackExtrudeExecutor`

## Diagnostics

AIR route emits:
- `air-profile-stack-extrude`
- `air-converted-from-hole-plan`
- `air-to-profile-stack-executor`

Deferred diagnostics:
- `air-profile-stack-v1-blind-deferred`
- `air-profile-stack-v1-counterbore-deferred`
- `air-profile-stack-v1-conical-deferred`

## Behavior boundary

No emitter rewrite was done. BRep/STEP behavior remains on existing production executor and exporter route; only bounded AIR scaffold/routing diagnostics are introduced.

## AIR-V2 next scope

- Introduce AIR execution semantics for blind/counterbore no-hole/overlap intervals.
- Add explicit conical profile region for countersink/chamfer adaptation.


## AIR-V2A.1 update (2026-05-23)
Blind-hole and counterbore remain explicitly deferred in AIR; legacy bounded routes are preserved to keep gates green.
