# M45 Probe: STEP export edge/vertex geometric consistency

## Scope observed
- No kernel topology/geometry algorithm changes were made.
- Probe touched STEP exporter tests + documentation only.

## T1 — Exporter surface location (files/methods/call graph)

### File + method targets
- `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`
  - `ExportBody(...)`
  - `BuildEdgeCurve(...)`
  - `EnsureVertex(...)`
  - `BuildPlane(...)`

### Responsibilities
- `ExportBody(...)`
  - Walks topology (`Bodies -> Shells -> Faces -> Loops -> Coedges`) and emits STEP topological entities.
  - Delegates line/edge emission to `BuildEdgeCurve(...)`.
- `BuildEdgeCurve(...)`
  - Reads edge binding (`EdgeGeometryBinding.TrimInterval`) and curve geometry.
  - Computes `startPoint/endPoint` via `line.Evaluate(trim.Start/End)`.
  - Emits/reuses `LINE` + `VECTOR` + `DIRECTION`.
  - Emits `EDGE_CURVE(startVertex, endVertex, line, .T.)`.
- `EnsureVertex(...)`
  - Emits/reuses `CARTESIAN_POINT` and `VERTEX_POINT` per `VertexId`.
  - First computed point for a vertex id is retained and reused globally for that vertex.

### Brief call graph
- `Step242Exporter.ExportBody`
  - loops shell faces/coedges
  - `-> BuildEdgeCurve`
    - `-> EnsureVertex` (start)
    - `-> EnsureVertex` (end)
    - `-> writer.AddEntity("LINE"/"EDGE_CURVE")`
  - `-> BuildPlane`
  - `-> writer.Build(...)`

## T2 — Repro/assert test added

### New test
- `Aetheris.Kernel.Core.Tests/Step242/Step242ExporterTests.cs`
  - `Debug_ExportBody_BoxBody_EdgeCurveLineBinding_EndpointsStayOnTrimmedLine`

### What it checks
The test exports `CreateBox(4,6,8)` and parses STEP text with minimal line scanning to build:
- vertex-point map (`VERTEX_POINT -> CARTESIAN_POINT`)
- edge map (`EDGE_CURVE -> start/end vertex + curve`)
- line map (`LINE -> origin + VECTOR`)
- vector map (`VECTOR -> DIRECTION + length`)

For every `EDGE_CURVE` that references a `LINE`, it asserts:
1. start/end vertex points are collinear with the line direction,
2. projection parameter `t` is within `[-tol, len+tol]`.

Tolerance is `1e-6`.

### Expected first failure on current main (reason)
Given `CreateBox(4,6,8)` bindings, each line is built from normalized direction and trim `[0,1]`, so an edge contributes only a unit endpoint delta. Shared vertices are cached by `VertexId` from whichever edge is visited first. Subsequent incident edges can therefore bind the same vertex point to a different line whose origin/direction does not pass through that cached point. This reproduces the observed endpoint-on-line violation pattern. Based on exporter emission order, the first failing edge is expected to be the third emitted edge curve (`EDGE_CURVE #23`), where the cached vertex from the prior Y-edge does not lie on the next X-edge line.

## T3 — Hypothesis confirmation

### Outcome
- **Primary cause: B** (`curve geometry computed from endpoints that disagree with vertex-point reuse context`).
- **Contributing mechanism:** vertex caching in `EnsureVertex(...)` makes the disagreement visible across incident edges.

### Proof (short)
`BuildEdgeCurve(...)` computes per-edge endpoints from `line.Evaluate(trim)` and then calls `EnsureVertex(...)`, which persists only the first point seen for each topological vertex. For boxes with non-unit edge lengths (`CreateBox(4,6,8)`), trim `[0,1]` on normalized line directions yields per-edge unit displacements; different incident edges produce different candidate coordinates for the same topological vertex, so at least one `EDGE_CURVE`/`LINE` pair references a vertex point not lying on that line.

## Notes
- Test execution could not be run in this container because `dotnet` is unavailable (`bash: command not found: dotnet`).
- No exporter fix is implemented in this probe commit; only repro/test + analysis report were added.
