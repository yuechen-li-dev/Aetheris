# Concept Path M1 design note

Ordered guide adjacency removes repeated endpoint declarations for ordinary outlines while preserving the existing exact guide/Profile pipeline. The new construct is deliberately a Path rather than a Loop: it is an open-capable construction scaffold, and `Profile` alone continues to own closure, winding, material-region, and self-intersection policy.

`Line` and `Arc` are geometry declarations, not `Vector2` actions. `Vector2` stays a value; compiler-local state only exists while the Path lowers. `Turn` is relative to the current heading, whereas `Heading` is absolute in the enclosing local 2D frame. This keeps local editing readable without offering a world-frame bypass.

The lowering emits `Path.Start`, `Path.Step`, and `Path.Step.End` identities into the same maps consumed by normal profile authoring. No path-specific resolved profile, materializer, STEP route, role-Match map, or automatic winding correction is introduced. Branches, cursor moves, arbitrary arc constructions, and role inference remain deferred.

The matched rectangle comparison is materially shorter without concealing connectivity:

| Measure | Low-level rectangle | Concept Path rectangle |
| --- | ---: | ---: |
| repeated endpoint references | 8 | 0 |
| authored point scalar coordinates | 8 | 2 |
| boundary guide declarations | 4 | 4 ordered steps |
| normalized Profile segments | 4 | 4 |
| exact extrusion volume | 400 mm³ | 400 mm³ |

The L-bracket similarly removes coordinate-coincidence arithmetic while retaining locally meaningful turns. The low-level form remains clearer when a profile selects only portions of scaffolding or needs non-path guide composition. For the matched rectangle, canonical STEP hashes are identical as well as topology, bounds, and exact volume.

M1 readability check: the start, initial travel direction, each turn, arc, closure, and consuming Profile are visible in source order. The intentionally open fixture demonstrates that a Path is valid until a Profile consumes it. Focused authoring tests cover first-attempt rectangle, L/arc state transitions, low-level fallback, and typed invalid mixes without parser archaeology.
