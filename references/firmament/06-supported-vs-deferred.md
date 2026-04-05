# What Works Reliably Today vs What Is Deferred

This file is the capability boundary for LLM authoring.

## Confidence-high, implementation-backed capabilities

### Parsing and document shape
- Canonical TOON `.firmament` parsing works.
- JSON-object parse mode works.
- Required sections/required fields are enforced.

### Model header
- `model.name` required scalar.
- `model.units` required scalar.
- Current implementation does **not** enforce a closed enum for units at parse/validation stage.
  - Corpus convention uses `mm`.

### Primitive execution
- Primitives: `box`, `cylinder`, `cone`, `sphere`, `torus`.
- Cone supports frustum and pointed variants under current radius constraints.
- Default-frame behavior is stable and test-backed.

### Placement subset
- Legacy placement (`on` + `offset[3]`) is validated.
- Semantic keys (`on_face`, `centered_on`, `around_axis`, `radial_offset`, `angle_degrees`) are validated with current constraints.
- Selector anchoring + centroid extraction path is executable.

### Boolean subset
- Core boolean ops parse/lower/execute in bounded families:
  - `add`, `subtract`, `intersect` with nested primitive tool ops.
- Safe-family validator enforces bounded continuation semantics.
- Boolean selector contracts are available for result bodies.

### Pattern subset (P2)
- `pattern_linear` and `pattern_circular` are supported.
- Source must be earlier `subtract` feature.
- Expansion produces synthesized booleans with chained references.

### Validation ops
- `expect_exists`
- `expect_selectable`
- `expect_manifold`

`target` classification supports:
- bare feature-id target (`feature_id`)
- selector-shaped target (`feature_id.port`)

Execution semantics:
- `expect_exists`: executes for feature-id and selector targets.
- `expect_selectable`: selector targets only (bare feature-id not supported at execution).
- `expect_manifold`: bare feature-id only (selector target not supported at execution).

### Export semantics
- Export body policy is fixed: last successfully executed geometric body by source op index.
- Validation ops never become export bodies.
- Output STEP text represents the selected body in world coordinates after all default-frame and placement transforms already applied.
- Firmament world coordinates map directly to STEP geometry coordinates (no additional global remap in exporter).

---

## Deferred / unsupported / unsafe-to-assume

- General unrestricted booleans across arbitrary primitive combinations.
- Unbounded rotated/tilted tool guarantees across all families.
- Full mating/alignment placement language (current semantics are anchor+offset+axis heuristics).
- Multi-hop selector traversals (`a.top_face.edges` etc.).
- Dedicated counterbore/countersink high-level ops.
- Broad support for box+sphere / box+cone / box+cylinder / box+torus across arbitrary geometric configurations.
- Any assumption that pattern instances are independent from root (they are chained).

---

## LLM authoring policy

If a requested construct is not explicitly covered by:
1. this capability list,
2. selector contracts,
3. safe-family rules,
4. current examples/tests,

then treat it as deferred and avoid speculative generation.
