# Concept Path M1 design note

Ordered guide adjacency removes repeated endpoint declarations for ordinary outlines while preserving the existing exact guide/Profile pipeline. The new construct is deliberately a Path rather than a Loop: it is an open-capable construction scaffold, and `Profile` alone continues to own closure, winding, material-region, and self-intersection policy.

`Line` and `Arc` are geometry declarations, not `Vector2` actions. `Vector2` stays a value; compiler-local state only exists while the Path lowers. `Turn` is relative to the current heading, whereas `Heading` is absolute in the enclosing local 2D frame. This keeps local editing readable without offering a world-frame bypass.

The lowering emits `Path.Start`, `Path.Step`, and `Path.Step.End` identities into the same maps consumed by normal profile authoring. No path-specific resolved profile, materializer, STEP route, role-Match map, or automatic winding correction is introduced. Branches, cursor moves, arbitrary arc constructions, and role inference remain deferred.

## Preview 2 consolidation contract

`Concept Path` is the typed, ordered planar construction described above. It is
not general object-member navigation, raw BRep traversal, mesh selection,
reflection, or a string lookup API. Its exposed semantic members are the start
point plus each named guide and endpoint. A consuming `Profile` proves closure,
winding, and region validity and normalizes the path to `ResolvedProfile2D`.

That resolved Profile is the capability boundary. Extrude and Compose consume
the same representation, and Selection may consume its named profile segments.
No downstream BRep/section-stack backend receives Concept Path syntax. Segment
provenance retains `concept-path:<Path>.<Step>` together with the Profile stable
ID and source span. `aetheris inspect <file> --json` reports path capabilities,
exposed members, consumers, and provenance.

Template expansion occurs before path binding. Consequently a Template-authored
Concept Path remains ordinary path source after scalar or typed Record/Table
substitution. The document retains Template specialization and Record/Table
provenance even when the result takes the Profile/Compose adapter route.

Firmament's bounded `Instance.Member` Concept IR expressions, recognized STEP
regions, Forge capability output descriptors, and FEA `SemanticRegionBinding`
are distinct contracts. They are not silently treated as Concept Paths. See
`docs/preview2/consolidation-gap-inventory.md` for the admitted and deferred
matrix.

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
