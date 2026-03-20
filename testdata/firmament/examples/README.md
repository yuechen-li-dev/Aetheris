# Firmament Example Pack

This directory contains small canonical Firmament examples for the current v1 surface.

These examples represent the currently supported boolean subset.
Other combinations may fail and are intentionally not supported yet.

- `box_basic.firmament` — minimal single-box golden-path example; helper export path: `testdata/firmament/exports/box_basic.step`.
- `cylinder_basic.firmament` — minimal single-cylinder export example; helper export path: `testdata/firmament/exports/cylinder_basic.step`.
- `cone_frustum_basic.firmament` — minimal single frustum-cone export example using the v1 `cone` primitive; helper export path: `testdata/firmament/exports/cone_frustum_basic.step`.
- `cone_pointed_top_zero.firmament` — minimal single pointed-cone export example using the same v1 `cone` primitive with `top_radius: 0`; helper export path: `testdata/firmament/exports/cone_pointed_top_zero.step`.
- `sphere_basic.firmament` — minimal single-sphere export example; helper export path: `testdata/firmament/exports/sphere_basic.step`.
- `torus_basic.firmament` — minimal single-torus export example using the canonical `major_radius` / `minor_radius` surface; helper export paths: `testdata/firmament/exports/torus_basic.step` and the milestone proof artifact `testdata/firmament/exports/m10g2-torus.step`.
- `box_add_basic.firmament` — minimal supported box-plus-box boolean add example; helper export path: `testdata/firmament/exports/box_add_basic.step`.
- `boolean_add_basic.firmament` — canonical proven boolean add example for the current box/box single-box subset; helper export path: `testdata/firmament/exports/boolean_add_basic.step`.
- `boolean_subtract_basic.firmament` — canonical proven boolean subtract example for the current box/box single-box subset; helper export path: `testdata/firmament/exports/boolean_subtract_basic.step`.
- `boolean_intersect_basic.firmament` — canonical proven boolean intersect example for the current box/box single-box subset; helper export path: `testdata/firmament/exports/boolean_intersect_basic.step`.
- `boolean_box_cylinder_hole.firmament` — the only supported box+cylinder boolean case: subtracting a Z-aligned cylinder that cleanly spans the full box height to form a single box-with-through-hole solid; helper export path: `testdata/firmament/exports/boolean_box_cylinder_hole.step`.
- `placed_primitive.firmament` — selector-based placement using `place.on` and `offset[3]`; helper export path: `testdata/firmament/exports/placed_primitive.step`.
- `schema_box_basic.firmament` — minimal schema-present export example showing schema presence without changing export body semantics; helper export path: `testdata/firmament/exports/schema_box_basic.step`.

Unsupported boolean requests are tracked as fixtures/tests, not presented in this working example pack. The only supported box+cylinder case is `subtract(box, cylinder)` where the cylinder stays axis-aligned with +Z/-Z, fully spans the box from bottom face to top face, and remains strictly inside the box footprint so the result is one connected box-with-through-hole solid. Box+cylinder `add`, box+cylinder `intersect`, rotated cylinders, partial-depth cuts, tangent/grazing cases, and outside-footprint cuts remain unsupported. Box+sphere booleans are currently unsupported in all audited forms, including `subtract(box, sphere)` with a fully contained sphere, boundary-touching sphere, or partially outside sphere, plus all current box+sphere `add` and `intersect` requests. Box+cone booleans are also currently unsupported in all audited forms, including `subtract(box, cone)` with both pointed-cone and frustum-cone tools, plus all current box+cone `add` and `intersect` requests. Box+torus booleans are currently unsupported in every audited form: `subtract(box, torus)`, `add(box, torus)`, and `intersect(box, torus)` all fail the real kernel boundary for centered, offset, fully contained, face-intersecting, and outside-footprint torus placements, so no box+torus example is included here. These files are intended to stay minimal, readable, and aligned with the implemented language surface. Export artifacts are kept separately under `testdata/firmament/exports/` so examples remain source-only and demo outputs stay easy to find.
