# Exact face graft and explicit subdivision sculpting

- `exact-face.firmament`: a native engineering box with one bicubic graph graft.
  The top center is 8.5625 mm rather than 8 mm. All input topology, curves,
  vertices and neighboring supports remain retained; the output is exact STEP.
- `box-cage.firmament`: exact box conversion, explicit crease removal, two finite
  Catmull-Clark levels and one selected-vertex movement with a protected neighbor.
- `sphere-cage.firmament`: budgeted cube-sphere cage conversion, refinement and
  explicit scaling into an ellipsoid-like mesh. The exact source sphere remains
  retained and unchanged. The output is GLB/STL, not STEP.

`GenericSculptingTests` checks shape changes, exact preservation, stale handles,
crease succession, bounded resources and serialized replay. CLI tests validate
actual GLB/STL geometry, float precision, provenance sidecars, validation levels,
unknown fields/statements, protected vertices, local foldover, incompatible
formats and atomic rejection. No global self-intersection certification is claimed.

Generated evidence belongs under `artifacts/local/generic-sculpting/`.
