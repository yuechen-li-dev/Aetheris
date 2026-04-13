# Aetheris.CLI baseline (C1)

## Supported commands

```bash
aetheris build <file.firmament> [--out <path>] [--json]
aetheris analyze <file.step> [--face <id>] [--edge <id>] [--vertex <id>] [--json]
```

### Contract notes

- `build` runs the existing `FirmamentBuildAndExport.Run` pipeline; it does not implement a parallel compiler/export path.
- `build` default output path remains deterministic: `<repo>/testdata/firmament/exports/<source-filename>.step`.
- `analyze` imports STEP using `Step242Importer.ImportBody` and reports facts from imported topology/geometry bindings.
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
    "surfaceFamilies": {
      "plane": 6,
      "cylinder": 0,
      "cone": 0,
      "sphere": 0,
      "torus": 0,
      "other": 0
    },
    "structuralAssessmentBasis": "derived from imported topology edge-to-face adjacency counts"
  },
  "face": null,
  "edge": null,
  "vertex": null,
  "notes": []
}
```

## Truthfulness and deferrals

- Structural classification is based on edge-to-face adjacency counts from imported topology.
- If bounding boxes or points cannot be determined from available vertex coordinates, output is `null` (JSON) / `unknown` (text), and a note is emitted.
- No renderer/viewer, no schema redesign, and no generalized command framework are included in C1.
