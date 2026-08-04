# Profile arrangement X1

Proper overlap is a planar problem. Aetheris resolves each active Base/Add/Remove slab as one bounded analytic arrangement, then emits the resulting section topology once.

> Compose material regions first. Emit topology once.

`ProfileArrangement2D` preserves source curve identity (`operation`, `Profile`, loop, named segment, parameter interval, and profile provenance). Lines use a normalized endpoint-distance parameter; circular arcs use a normalized traversal over their signed angular sweep. Pairwise analytic line-line, line-circle, and circle-circle intersections are filtered to those bounded domains. Source curves are split in parameter order and receive stable `.partN` identities.

Each atomic fragment is classified by two deterministic normal-offset samples under:

```text
IsMaterial(p) = (InsideBase(p) or InsideAnyAdd(p)) and not InsideAnyRemove(p)
```

Only a fragment whose sides differ is retained; it is oriented material-left. Coincident same-side support collapses to the lowest stable source ID, removing Add/Add and Remove/Remove internal walls without losing provenance. Directed boundary fragments are then walked using the deterministic angular successor around every vertex. A valid production result has exactly one CCW outer loop, zero or more nested inner loops, no dangling fragment, and no zero-area loop. Point-only connections reject as disconnected/non-manifold instead of being healed.

CTC-BLOCKOUT-X2 completes the material-policy ladder. Exact pre-emission guards now reject contradictory coincident Add/Remove support, bounded line/arc tangency between distinct operations, non-manifold boundary incidence, zero-width contacts, dangling incidence, unresolved angular ordering, and more than one final outer material loop. These are rejection policies, not tolerance-healing hooks. Horizontal transition differences may contain several disconnected cap patches even though each open slab and the final body remain one connected material region; the transition carries those exact patches as a bounded region set.

The arrangement result is the existing `PrismaticSectionRegion`. Adjacent regions use the same machinery for exact horizontal differences. The emitter additionally splits section edges at global arrangement vertices so a changed section boundary shares directed edges with the neighboring slab and transition face. It remains one `PrismaticSectionStackBrepPlan` and one analytic BRep/STEP body, with planes, cylinders, lines, circles, and trimmed curves only.

`inspect-compose --json` reports per-slab arrangement curve, intersection, fragment, coincident-normalization, loop, perimeter, timing, diagnostic, and provenance evidence. The included overlapping-additive rectangle fixture reports a 480 mm² upper section and 6400 mm³ total volume after STEP round-trip. The overlapping-removal unit fixture reports a 304 mm² upper material section and 5520 mm³ total volume. `mixed-line-arc-additive-overlap.firmament` forces two transverse line/circle crossings: its upper section is `458.72298071147134 mm²`, with five retained lines and four retained circular arcs. Its analytic volume is `6293.614903557356 mm³`; in-memory and STEP-reimport values are within `0.01 mm³` under the existing bounded mass-property tessellation tolerance. The exported STEP contains planes and four cylindrical surfaces, with no B-spline fallback.

The completion fixtures add four exact material cases: Add overlapped by Remove (`418 mm²` final section, `4090 mm³`), overlapping Removes (`304 mm²`, one explicit `Inner`, `5520 mm³`), shared-boundary Adds (`600 mm²`, no internal face, `5000 mm³`), and a crossing removal notch (`352 mm²`, no `Inner`, `3760 mm³`). Their analytic, in-memory, STEP-reimported, and M8 volumes agree within `0.01 mm³`. Reversing Add/Remove enumeration in the first fixture preserves section areas, loop roles, and volume.

X1 remains deliberately bounded: it does not support splines, offsets, non-planar profiles, disconnected multibody output, or fuzzy tolerance healing. Coincident support with contradictory local material state and unresolved equal-angle continuations rejects before BRep emission.
