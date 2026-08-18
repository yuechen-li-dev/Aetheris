# CIR-BREP-T0 — tiered analytic circle trim consumption for planar BRep loop emission

## Purpose

CIR-BREP-T0 adds a bounded intake path in `PlanarSurfaceMaterializer` that consumes one admissible `TieredTrimCurveRepresentation` analytic circle and emits one planar trimmed face using the existing F10.7 topology convention (outer rectangle + one inner circular loop).

## Supported path

`EmitRectangleWithTieredInnerCircle(RectWithTieredInnerCircleEmissionRequest)` now accepts:

- bounded rectangular planar source geometry,
- one tiered trim with `Kind == AnalyticCircle`,
- accepted internal analytic candidate state,
- no prior STEP exact export and no prior BRep topology emission.

The method converts tiered UV-circle evidence through `OracleTrimLoopGeometryConverter.TryConvertAnalyticCircle(...)` and then routes to `EmitRectangleWithInnerCircle(...)` (F10.7 emitter).

## Admissibility requirements

Rejected with explicit diagnostics when any guard fails:

- readiness not evidence-ready,
- non-analytic kind,
- analytic candidate not accepted,
- missing analytic payload,
- flags indicating prior STEP exact export or BRep topology emission,
- non-rectangular planar source,
- unsafe UV/world conversion.

## UV-to-world conversion policy

Conversion reuses the T10 converter and preserves bounded safety policy:

- requires rectangular bounded planar patch geometry,
- rejects degenerate axes,
- rejects non-uniform UV/world scaling (circle would map to ellipse),
- returns precise conversion diagnostics.

## Identity metadata behavior

The tiered path emits through F10.7 retained-circle emission, so inner-loop identity behavior is unchanged:

- if ordering token provided, emitted inner edge/coedge/loop entries carry the token,
- if unavailable, topology still emits but token-fabrication remains forbidden and diagnostics remain explicit.

## Relationship to binder fallback

CIR-BREP-T0 does not replace binder-derived retained circular loop flow. Existing binder-driven planar patch-set emission remains active and green; tiered intake is additive and bounded.

## Non-goals

- shell assembly or topology merge,
- STEP export changes,
- numerical-only trim materialization,
- multi-inner-loop support,
- generic trim-to-topology conversion.

## Next milestone

Extend bounded tiered intake from direct call coverage into broader patch-set selection routing only when admissibility remains explicit and deterministic for all selected candidates.
