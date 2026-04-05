# What Works Reliably Today vs What Is Deferred

## Known working capabilities (confidence-high, test-backed)

- Parsing canonical TOON `.firmament` and JSON-object compatibility mode.
- Primitives: box, cylinder, cone (frustum/pointed constraints), sphere, torus.
- Validation ops: expect_exists, expect_selectable, expect_manifold.
- Selector-shaped target/anchor references with per-kind port contracts.
- Placement subset (`on`/`offset` + semantic keys) with runtime anchor extraction.
- Pattern subset: linear and circular expansion from subtract sources.
- Narrow boolean families validated by execution tests/examples:
  - box/box add/intersect/subtract within proven subset examples
  - box minus cylinder/cone hole families under safe-family constraints
  - box minus contained sphere cavity (narrow)
  - sequential cylinder/cone safe subtract continuation (non-overlap/tangent)

## Deferred / unsupported / unsafe-to-assume

- General unrestricted boolean support across arbitrary primitive combinations.
- Unbounded rotated/tilted tool guarantees across all families.
- General semantic placement mating/alignment language (current is anchor/axis heuristic subset).
- Multi-hop/chained selector traversal (`a.top_face.edges` style).
- Full counterbore/countersink dedicated language constructs.
- Guaranteed support for all box+torus boolean forms (audited as unsupported set in tests/docs).
- Guaranteed support for every box+sphere / box+cone / box+cylinder geometric arrangement outside audited subset.

## Authoring rule for LLMs

If requirement lies outside explicitly proven subset (examples + tests + safe-family rules), treat it as deferred and avoid speculative source generation.
