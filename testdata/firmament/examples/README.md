# Firmament Example Pack

This directory contains small canonical Firmament examples for the current v1 surface.

- `box_basic.firmament` — minimal single-box golden-path example; helper export path: `testdata/firmament/exports/box_basic.step`.
- `cylinder_basic.firmament` — minimal single-cylinder export example; helper export path: `testdata/firmament/exports/cylinder_basic.step`.
- `cone_frustum_basic.firmament` — minimal single frustum-cone export example using the v1 `cone` primitive; helper export path: `testdata/firmament/exports/cone_frustum_basic.step`.
- `cone_pointed_top_zero.firmament` — minimal single pointed-cone export example using the same v1 `cone` primitive with `top_radius: 0`; helper export path: `testdata/firmament/exports/cone_pointed_top_zero.step`.
- `sphere_basic.firmament` — minimal single-sphere export example; helper export path: `testdata/firmament/exports/sphere_basic.step`.
- `torus_basic.firmament` — minimal single-torus export example using the canonical `major_radius` / `minor_radius` surface; helper export paths: `testdata/firmament/exports/torus_basic.step` and the milestone proof artifact `testdata/firmament/exports/m10g2-torus.step`.
- `box_add_basic.firmament` — minimal supported box-plus-box boolean add example; helper export path: `testdata/firmament/exports/box_add_basic.step`.
- `placed_primitive.firmament` — selector-based placement using `place.on` and `offset[3]`; helper export path: `testdata/firmament/exports/placed_primitive.step`.
- `schema_box_basic.firmament` — minimal schema-present export example showing schema presence without changing export body semantics; helper export path: `testdata/firmament/exports/schema_box_basic.step`.

Unsupported boolean requests are tracked as fixtures/tests, not presented in this working example pack. These files are intended to stay minimal, readable, and aligned with the implemented language surface. Export artifacts are kept separately under `testdata/firmament/exports/` so examples remain source-only and demo outputs stay easy to find.
