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

With `place` block:
- sphere gets extra `+radius Z`
- torus gets extra `+minor_radius Z`

Trap: thinking these corrections apply globally even without placement.

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

---

## 11) `model.units` is required but currently unconstrained as an enum

Parser requires presence, but current implementation does not restrict to a fixed token list.
Corpus convention uses `mm`.

Trap: assuming units string changes internal scaling. Current pipeline exports coordinates as-authored numeric values.

---

## 12) Export body selection ignores validation ops

Export always chooses the last executed primitive/boolean body by source order.
Validation ops affect diagnostics, not exported body selection.

Trap: expecting final validation op target to become export body.

---

## 13) Source-of-truth hierarchy

Some older human-readable docs may lag implementation status.
For semantic disputes, trust parser/validation/lowering/execution code and tests over stale module summaries.
