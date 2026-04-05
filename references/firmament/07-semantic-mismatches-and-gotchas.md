# Semantic Mismatches, Suspected Mismatches, and LLM Gotchas

This file is intentionally explicit about places where intended language semantics and current behavior may diverge.

## Known mismatch: module README vs actual implementation

`Aetheris.Kernel.Firmament/README.md` still describes pre-M0 scaffold status, but parser/validators/lowering/execution/tests are implemented.
Treat code/tests as source of truth, not that README status text.

## Known mismatch: section-order guidance vs parser behavior

Human docs prescribe fixed top-level order, but parser checks required sections/shape and unknown sections rather than strict ordering.
Use canonical order anyway to avoid future breakage.

## Known/suspected mismatch: primitive placement anchor expectations

`ApplyDefaultLocalFrame` already shifts box/cylinder/cone upward by half-height.
Placement resolver then adds extra primitive corrections for sphere/torus only.
Resulting default anchors are not uniform across primitives.

LLM trap: assuming every primitive anchors at geometric center or at Z=0 plane identically.

## Known mismatch risk: boolean placement timing differs by path

For some subtract-cylinder semantic cases, tool is placed before boolean; in other paths boolean result is translated after operation.
These are not equivalent for many geometries.

LLM trap: assuming `place` always means “move the tool before operation.”
Current behavior is path-dependent.

## Known/suspected mismatch: `centered_on` name implies richer behavior than implemented

Runtime currently resolves `centered_on` by same selector-anchor centroid extraction style as `on_face`/`on` selector anchors.
No full frame alignment or orientation matching is applied.

LLM trap: assuming automatic orientation alignment when using `centered_on`.

## Known/suspected mismatch: semantic placement selector ports vs runtime extraction

Compile-time selector contracts permit certain ports by feature kind.
Runtime anchor extraction then uses heuristic geometric filters (e.g., plane normal Z tests, cylindrical/conical detection).
Port legality does not guarantee intuitive spatial result on complex bodies.

## Known mismatch risk: patterns chain booleans, not independent clones

Pattern expansion rewrites each generated boolean to reference previous generated feature (chain), not always the original source root.
This affects geometry accumulation and safe-family validation.

LLM trap: assuming each pattern instance subtracts independently from original base.

## Known mismatch risk: safety-family state is stricter than op availability

Even if parser accepts a boolean op and tool kind, safe-family graph rules may reject composition order or family re-entry.

LLM trap: assuming syntactically valid subtract chains are always executable.

## Placement gotchas

- `radial_offset` without `around_axis` is invalid.
- selector must reference earlier feature id.
- selector port token must be legal for referenced feature kind.
- around-axis currently expects cylindrical/conical side-face axis extraction.

## Boolean gotchas

- `with` supports primitive tool definitions only.
- broad mixed-primitive boolean families remain intentionally narrow/guarded.
- some unsupported requests fail late at execution with NotImplemented diagnostics despite passing early shape checks.

## What to verify before relying on advanced semantics

Before generating high-stakes geometry, verify against current tests/examples for:

1. target primitive default-frame anchor assumptions,
2. boolean placement pre/post application path,
3. safe-family continuation state transitions,
4. pattern chaining expectations,
5. selector runtime anchor behavior on expected feature kind.
