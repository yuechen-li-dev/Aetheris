# Aetheris.CLI baseline (C3)

## Supported commands

```bash
aetheris build <file.firmament> [--out <path>] [--json]
aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]
```

## Contract notes

- `build` runs `FirmamentBuildAndExport.Run`; no parallel compile/export path is introduced.
- `build` default output path remains deterministic: `<repo>/testdata/firmament/exports/<source-filename>.step`.
- `analyze` imports STEP with `Step242Importer.ImportBody` and reports imported topology/geometry facts.
- At most one detail selector is allowed on `analyze`: `--face`, `--edge`, or `--vertex`.
- `analyze --json` is the canonical machine-readable mode.

## `analyze --json` shape

```json
{
  "stepPath": "...",
  "summary": {
    "bodyCount": 1,
    "shellCount": 1,
    "faceCount": 6,
    "edgeCount": 12,
    "vertexCount": 8,
    "boundingBox": { "min": { "x": 0, "y": 0, "z": 0 }, "max": { "x": 10, "y": 5, "z": 2 } },
    "structuralAssessment": "enclosed-manifold",
    "structuralAssessmentBasis": "derived from imported topology edge-to-face coedge incidence counts",
    "lengthUnit": "mm",
    "lengthUnitBasis": "assumed; STEP import length units not yet preserved",
    "surfaceFamilies": {
      "plane": 6,
      "cylinder": 0,
      "cone": 0,
      "sphere": 0,
      "torus": 0,
      "bspline": 0,
      "other": 0
    },
    "faceIds": { "min": 1, "max": 6, "count": 6, "contiguous": true },
    "edgeIds": { "min": 1, "max": 12, "count": 12, "contiguous": true },
    "vertexIds": { "min": 1, "max": 8, "count": 8, "contiguous": true }
  },
  "face": null,
  "edge": null,
  "vertex": null,
  "notes": []
}
```

### Structural assessment enum semantics

- `enclosed-manifold`: every imported edge is used by exactly two face coedges.
- `leaky-or-open`: at least one imported edge is used by only one face coedge.
- `non-manifold`: no leaky edges, but one or more edges have coedge incidence different from 2.

### Unit semantics (honesty contract)

- `lengthUnit` is currently emitted as `mm`.
- `lengthUnitBasis` communicates trust level of that value.
- Current C3 truth: `lengthUnit` is an Aetheris analyzer assumption/normalization output, not preserved STEP source-unit provenance.

## Face detail contract (`--face <id>`)

Common fields:

- `faceId`, `surfaceType`, `surfaceStatus`, `boundingBox`, `representativePoint`, `adjacentEdgeIds`

Surface binding semantics:

- Bound face: `surfaceStatus: "bound"`, `surfaceType` is a concrete kind (`Plane`, `Cylinder`, `Cone`, `Sphere`, `Torus`, `BSplineSurfaceWithKnots`, ...).
- Missing surface binding: `surfaceStatus: "binding-missing"`, `surfaceType: null`.

Kind-specific fields:

- Plane: `anchorPoint`, `planarNormal`
- Cylinder: `anchorPoint`, `axis`, `radius`
- Cone: `anchorPoint`, `apex`, `axis`, `semiAngleRadians`, `placementRadius`
- Sphere: `anchorPoint`, `radius` (no `axis` field is emitted; spheres have no intrinsic axis)
- Torus: `anchorPoint`, `axis`, `majorRadius`, `minorRadius`

## Edge detail contract (`--edge <id>`)

Fields:

- `edgeId`, `curveType`, `startVertexId`, `startVertex`, `endVertexId`, `endVertex`, `adjacentFaceIds`, `parameterRange`, `arcLength`, `arcLengthStatus`

`arcLengthStatus` enum semantics:

- `computed`: current curve-kind support computed a numeric `arcLength`.
- `unsupported-for-curve-kind`: `arcLength` is null because current analyzer does not implement that curve kind.
- `unavailable-no-trim-interval`: edge is present but has no trim interval.
- `unavailable-curve-missing`: curve binding exists but curve geometry is unresolved.
- `unavailable-binding-missing`: edge has no curve binding.
- `unavailable`: generic fallback when no narrower status applies.

## Analyze failure JSON shape

When `--json` is requested and analyze fails:

```json
{
  "success": false,
  "stepPath": "<resolved-path>",
  "error": "<message>"
}
```

## Truthfulness and scope boundaries

- Structural classification is derived from imported edge-to-face coedge incidence counts.
- Missing coordinate-dependent fields are emitted as null and accompanied by notes.
- C3 does not add arbitrary source-unit preservation/export redesign.
- C3 does not add renderer/viewer scope, language redesign, or broad CLI framework redesign.
