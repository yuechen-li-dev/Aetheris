# ANALYZE-MAP-A0 ray/height-map probe

`aetheris analyze map` is an LLM-oriented spatial measurement tool. It samples imported STEP/AP242/BRep bodies by shooting rays from a selected plane and returns hit heights/depths, face ids, surface families, and compact coverage summaries. It is not a topology metadata report and does not attempt CAD feature reconstruction.

## Syntax

```bash
aetheris analyze map part.step --plane xy --direction -z --resolution 32x32 --json
aetheris analyze map part.step --plane xy --direction -z --point 3,4 --json
```

Legacy view flags (`--top`, `--bottom`, `--front`, `--back`, `--left`, `--right`) are still accepted and map to plane/direction pairs.

## Modes

* **Grid**: `--resolution NxM` samples the selected plane over the imported body's bounding-box extent and returns one sample per grid point.
* **Point**: `--point u,v` shoots one ray at a plane coordinate and returns all tessellated hits sorted by ray parameter.

Each sample reports first hit, last hit, hit count, and all hits. The summary includes hit coverage, height range, and surface families hit.

## Backend approach

A0 uses imported BRep bounds to place the sampling plane, then intersects probe rays with display tessellation. This is reported honestly as `tessellated-fallback`; exact analytic ray intersection remains a future optimization for supported surface families.

Fallback diagnostics are emitted when exact intersection is unavailable for surface families such as `linear-extrusion`, `surface-of-revolution`, `bspline`, or `unknown`. Tessellation diagnostics are surfaced instead of crashing.

## Relationship to other analyze commands

* Base `aetheris analyze` reports topology and basic STEP/BRep metadata.
* `aetheris analyze volume` estimates or computes volume and keeps its unsupported-surface policy.
* `aetheris analyze map` measures what a model looks like in space as rays and height/depth samples.

## Limitations

The A0 map is tessellation-backed, so curved and swept surfaces are approximate. It does not classify solid intervals yet, and six-view output is deferred to A1; run the command once per direction for equivalent coverage.
