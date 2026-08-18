# ANALYZE-MAP-A1 analytic backend provenance

`aetheris analyze map` is an LLM-oriented spatial measurement tool: it samples a selected view plane, shoots rays through model space, and reports hit positions, depths/heights, face ids, and surface families. It is not a topology metadata report and should not treat display meshes as geometric truth.

## Why tessellation is not the source of truth

Display tessellation answers, "How should this model be drawn?" The map analyzer answers, "Where is the model in 3D space?" Those are different contracts. Tessellation is useful for approximate fallback coverage, but it can be slow, display-policy dependent, and fragile for trimmed or highly curved surfaces. A1 therefore makes tessellated hits explicit and approximate rather than silently reusing display triangles as authoritative measurements.

## Backend ladder

The map ray path now reports the intended backend policy:

```text
analytic-cir-tessellated-fallback
```

The ladder is:

1. **Analytic exact intersection** for supported bounded faces.
2. **CIR/evaluable intersection** as a future hook when a usable map ray evaluator is admitted.
3. **Tessellated fallback** for unsupported analytic families.
4. **Unsupported/no-hit diagnostic** when no backend can produce a hit.

A1 implements the dispatch/provenance shape and exact bounded planar face hits. CIR remains a documented future backend; A1 does not infer CIR mirrors from arbitrary STEP input.

## A1 plane analytic handler

Planar faces are intersected by exact ray-plane math. The candidate point is then checked against the current face bounds using imported face vertices projected into the plane basis. When the bounded check succeeds, the hit is reported as:

```json
{
  "surfaceFamily": "plane",
  "intersectionMode": "analytic",
  "confidence": "exact",
  "diagnostics": []
}
```

If a future planar case cannot establish bounded containment reliably, it must not be promoted to an exact analytic face hit. It should fall back or report a diagnostic rather than claiming that an infinite-plane candidate is a bounded face hit.

## JSON provenance

Each hit carries backend provenance:

```json
{
  "position": { "x": 0, "y": 0, "z": 6 },
  "faceIndex": 4,
  "surfaceFamily": "plane",
  "intersectionMode": "analytic",
  "confidence": "exact",
  "diagnostics": []
}
```

Each sample summarizes modes observed along that ray:

```json
{
  "i": 0,
  "j": 0,
  "hit": true,
  "hitCount": 2,
  "intersectionModes": {
    "analytic": 2,
    "cir-evaluated": 0,
    "tessellated-fallback": 0,
    "unsupported": 0
  }
}
```

The result summary includes backend counts and the top-level backend policy:

```json
{
  "backendPolicy": "analytic-cir-tessellated-fallback",
  "summary": {
    "analyticHitCount": 100,
    "cirHitCount": 0,
    "tessellatedFallbackHitCount": 24,
    "unsupportedSampleCount": 0
  }
}
```

## Fallback semantics

Unsupported A1 families such as `cylinder`, `cone`, `sphere`, `torus`, `linear-extrusion`, `surface-of-revolution`, and `bspline` may still map through tessellation. When that happens, hits use:

```json
{
  "intersectionMode": "tessellated-fallback",
  "confidence": "approximate",
  "diagnostics": [
    "Exact ray intersection unavailable for linear-extrusion; used tessellated fallback."
  ]
}
```

The same diagnostic is also surfaced at the top level once per surface family so callers can decide whether approximate samples are acceptable.

## Current limitations and future hooks

A1 intentionally does not implement analytic cylinder, sphere, cone, torus, ruled/swept, or B-spline ray intersections. It also does not reconstruct features, integrate volume, change DisplayIR, or add modeling capabilities.

CIR/evaluable intersection remains the next architectural hook once an admitted ray API exists for map workloads. Torus should be prioritized in a later truth pass because tessellated torus and trimmed-loop display paths have historically been fragile. Suggested future milestone:

```text
ANALYZE-MAP-A3: analytic cone + torus truth pass
```


## Forward link

ANALYZE-MAP-A2 extends this analytic tier from planes to cylinders and spheres; see `docs/development/implementation/analyze-map-a2-cylinder-sphere-analytic-hits.md`.
