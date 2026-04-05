# Booleans, Bounded Families, and Pattern Continuation

## Boolean ops and required shape

Supported boolean ops:

- `add` requires `id`, `to`, `with`
- `subtract` requires `id`, `from`, `with`
- `intersect` requires `id`, `left`, `with`

`with` must be object-like with nested primitive tool `op`.

Nested tool parameter validation exists for: box/cylinder/sphere/cone/torus.

## Safe-family model (current implemented boundary)

The system enforces a bounded feature-graph model via explicit state tracking.

Key states:

- `BoxRoot`
- `CylinderRoot`
- `BoundedOrthogonalAdditiveSafeRoot`
- `BoundedOrthogonalAdditiveOutsideSafeRoot`
- `SafeSubtractComposition`
- `Other`

### Orthogonal additive unions

Add results can be recognized as bounded orthogonal safe roots.
If recognized, subtract safe-family continuation may proceed from that additive result.
If not recognized, safe-family subtract re-entry is blocked.

### Subtract re-entry rule

Subtract with safe-hole tools (`cylinder`/`cone`) may:

- start from bounded recognized roots,
- continue from already validated safe subtract composition,
- but cannot re-enter from arbitrary `Other` states.

### Hole families / continuation

Supported safe-hole continuation family is narrow:

- follow-on boolean must be `subtract`
- follow-on tool kind must remain `cylinder` or `cone`
- analytic recognition + overlap/tangent guards are enforced by boolean graph validator path

### Counterbore/blind/coaxial notes

There is no general counterbore feature op family today.
Blind/coaxial-like outcomes can appear only when they fit current subtract + placement + kernel recognition constraints; do not assume dedicated semantics.

### Independent multi-hole continuation

Pattern expansion and sequential subtracts can produce multi-hole chains, but only within current safe subtract family and non-interfering analytic constraints.

## Pattern family (P2 subset)

Supported pattern ops:

- `pattern_linear`
- `pattern_circular`

Constraints:

- `source` must reference earlier feature.
- Source currently must be a `subtract` boolean feature.

`pattern_linear`:

- requires `count > 0` integer, `step[3]` numeric.
- clones source boolean entries with synthesized IDs `source__linN`.
- chains primary boolean reference to previous instance.

`pattern_circular`:

- requires `count > 0`, `axis` selector.
- requires either `angle_degrees` (span) or `angle_step_degrees` (exclusive).
- clones source with `around_axis = axis` and adjusted `angle_degrees`, IDs `source__cirN`.
- chains primary boolean reference to previous instance.

Pattern expansion is compile-time document transformation before lowering/execution.
