# SurfaceMeshIR M7 evidence

- `ctc-01.obj`: topology-preserving polygon OBJ.
- `ctc-01.stl`: deterministic triangle-only lowering of the same IR.
- `ctc-01-surface-mesh-ir.json`: full IR, including cell provenance and feature plans.
- `ctc-01-metrics.json`: full mesh and planar audit.
- `face-3-decomposition.json` / `face-98-decomposition.json`: focused trim, band, bridge, timing, and audit data.
- `generic-fixtures.json`: through-hole, multi-hole, slot, mixed, and close-feature fixture results.
- `before-vs-after-metrics.json`: M6-to-M7 comparison.
- `ctc-faces-3-98-provenance.png`: Plane-local provenance debug view.
- `hashes.sha256`: artifact hashes.

The design, algorithms, tradeoffs, validation, and remaining weaknesses are in
[`surface-mesh-ir-m7-feature-bands.md`](../../surface-mesh-ir-m7-feature-bands.md).
