# Semantic Mismatches, Traps, and Caution Areas

This file records behavior that is either surprising, path-dependent, or easy to misuse.

## 1) Primitive default anchors are not uniform

- Box/cylinder/cone use explicit default publish shift (`+height/2` or equivalent) and behave like bottom-on-Z0 defaults.
- Sphere/torus are origin-centered by default **unless placement is present**, then placement adds primitive-specific +Z correction.

Trap: assuming every primitive uses the same anchor convention.

---

## 2) Sphere/torus placement correction only happens when `place` exists

No `place` block:
- sphere center remains at origin
- torus center remains at origin

With `place` block (including `place.on: origin` with zero offset):
- sphere gets extra `+radius Z`
- torus gets extra `+minor_radius Z`

Trap: thinking these corrections apply globally even without placement, or assuming `place.on: origin` == no placement block.

---

## 3) `offset` is world-frame, not face-local frame

`offset[3]` is always world XYZ translation after anchor resolution.

Trap: treating `offset` as local UVN displacement on selected face.

---

## 4) `on_face` / `centered_on` are anchor-point semantics, not full mating

Both currently resolve selector anchor points with centroid-style extraction.
No automatic orientation/frame alignment is applied.

Trap: assuming `centered_on` aligns normals/axes automatically.

---

## 5) Boolean placement timing is path-dependent

Some subtract-cylinder semantic paths place tool before boolean; other paths place result after boolean.
Pattern-generated booleans place tool first in dedicated path.

Trap: assuming one universal placement timing rule.

---

## 6) Boolean field reference vs placement anchor are independent

`from`/`to`/`left` pick boolean base feature.
`place.on*` picks anchor source for translation.

Trap: assuming anchor selector must equal primary boolean source ID.

---

## 7) Pattern ops are directives, not directly referenceable geometry

`pattern_linear` / `pattern_circular` do not create bodies themselves.
They expand into synthesized booleans.

Trap: attempting selector/validation targets against pattern op IDs.
Use generated IDs (`__linN`, `__cirN`) instead.

---

## 8) Pattern instances are chained, not independent root clones

Each generated boolean references previous generated instance as primary source.

Trap: assuming instance `N` subtracts from original source body directly.

Also: linear pattern `step` accumulates on top of source offset (`source_offset + i*step`), and circular pattern angle accumulates on top of source angle (`baseAngle + i*angularStep`).

---

## 9) Safe-family acceptance is stricter than syntax validity

Syntactically valid boolean chains can still fail feature-graph safety guards.

Trap: assuming parser/required-field success implies executable support.

---

## 10) Validation op `target` support differs by op kind

- `expect_exists`: bare feature-id or selector-shaped target.
- `expect_selectable`: selector target only at execution.
- `expect_manifold`: bare feature-id only at execution.

Trap: using one target style uniformly for all validation ops.

`expect_selectable.count` is exact-match and `count: 0` means "expect none" (passes only on zero resolved elements). Failures produce warnings, not execution aborts.

⚠️ Severity trap: all three validation ops (`expect_exists`, `expect_selectable`, `expect_manifold`) are diagnostic-only at execution. Failed validations produce warning diagnostics but do not by themselves fail compile or block export.

---

## 11) `model.units` is required but currently unconstrained as an enum

Parser requires presence, but current implementation does not restrict to a fixed token list.
Corpus convention uses `mm`.

Trap: assuming units string changes internal scaling. Current pipeline exports coordinates as-authored numeric values.

---

## 12) Export body selection ignores validation ops

Export always chooses the last successfully executed primitive/boolean body by source order.
Validation ops affect diagnostics, not exported body selection.

Trap: expecting final validation op target to become export body.

⚠️ Partial execution trap: if a later boolean fails, earlier successful geometry is still exported. This can look like success while silently truncating intended features.

Validation failures can still appear alongside successful compile/export because they are warning-only diagnostics.

---

## 13) Source-of-truth hierarchy

Some older human-readable docs may lag implementation status.
For semantic disputes, trust parser/validation/lowering/execution code and tests over stale module summaries.


## 14) `with.place` is ignored

Boolean tool objects do not support nested placement semantics.

Trap: writing `with.place` expecting tool motion. Placement must be authored at boolean top-level `place`.

## 15) Boolean selector ports are heuristic

Boolean result ports (for example `top_face`) rely on classification heuristics (such as normal-direction filters), not guaranteed topological identity.

Trap: relying on these ports for rotated or complex boolean outcomes.

## 16) `pmi` is currently no-op metadata

`pmi` is parsed as section presence but currently does not change execution behavior or STEP export payload.

Trap: expecting PMI entries to affect geometry/export semantics in current implementation.

## 17) `intersect` is implemented but narrow

`intersect` uses `left` as its primary reference field (`left` role mirrors `from`/`to` in other boolean ops) and executes through the general boolean path.
Positive behavior: it computes geometric overlap (`left ∩ with`) when that overlap is representable inside the currently supported bounded subset.

Trap: assuming it participates in safe subtract continuation semantics. It does not; treat as narrow subset behavior unless test-backed.

## 18) Pointed-cone cap selectors can legally parse but resolve empty

Cone selector ports include `top_face`/`bottom_face`, but runtime counts actual topology.

- `top_radius: 0` pointed cone ⇒ no top cap face ⇒ `top_face` resolves to zero elements.
- `bottom_radius: 0` pointed cone ⇒ no bottom cap face ⇒ `bottom_face` resolves to zero elements.

Trap: assuming compile-time port allowance guarantees runtime non-empty selector results.

## 19) Enclosed-void policy is export-blocking in non-allowing modes

For default/no schema, `cnc`, and `injection_molded`, enclosed void detection is a hard compile failure (error diagnostic), not a warning.

Trap: treating "void rejected" as advisory. A fully enclosed cavity (for example an internal spherical cavity) prevents successful compile/export in those modes. `additive` is the allowing mode.

## 20) Pattern marker substrings are effectively reserved in generated IDs

Pattern expansion synthesizes IDs with `__linN` and `__cirN`, and executor logic treats feature IDs containing `__lin`/`__cir` as pattern-generated behavior cues.

⚠️ Avoid manually authoring feature IDs that contain `__lin` or `__cir` substrings unless you intentionally want that pattern-generated classification behavior.
