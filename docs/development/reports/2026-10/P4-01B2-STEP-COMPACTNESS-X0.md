# P4-01B2 — STEP compactness X0

B2 began after [B1 acceptance](P4-01B1-THREAD-SHOWCASE-REPAIR.md). The audited artifact is the normal Firmament showcase, including its full modeled groove and engraved CODEX mark. Geometry representation repairs belong to B1; the B2 comparison uses identical repaired geometry.

## Baseline and result

| Artifact | Bytes | Lines | Entities |
| --- | ---: | ---: | ---: |
| Original engraved X2 witness | 9,028,303 | — | — |
| Repaired B1, before exporter optimization | 6,991,354 | 83,644 | 83,635 |
| Repaired B1, exact geometry sharing | 4,539,892 | 50,115 | 50,106 |

The bounded exporter change saves **2,451,462 bytes (35.064%)** against repaired geometry, or 49.715% against the original engraved witness. The original unmarked X1 witness was 8,798,446 bytes. The latter comparison includes physical construction and legal spline representation changes, so it is not attributed entirely to exporter optimization.

Both repaired exports have one body, 293 faces, 689 edges, and 406 vertices. Pitch is 1.25 mm, span 47.5 mm, with 38 turns and two maker-mark counters.

## Entity and byte attribution

`scripts/profile-step-entities.py` profiles actual UTF-8 entity lines. Complex entities are listed by their component type names joined with `+`. The JSON reports include the complete histogram, required types including zero counts, numeric literal lengths, exact duplicate definitions, and normalized graph digest.

| Type | Before count / bytes | After count / bytes |
| --- | ---: | ---: |
| CARTESIAN_POINT | 74,693 / 5,736,970 | 43,824 / 3,385,829 |
| B_SPLINE_SURFACE_WITH_KNOTS | 114 / 484,742 | 114 / 484,617 |
| B_SPLINE_CURVE_WITH_KNOTS | 380 / 428,841 | 304 / 411,575 |
| ORIENTED_EDGE | 1,378 / 56,498 | 1,378 / 56,498 |
| DIRECTION | 1,217 / 40,562 | 58 / 2,811 |
| DEFINITIONAL_REPRESENTATION | 618 / 36,462 | 618 / 36,462 |
| EDGE_CURVE | 689 / 33,072 | 689 / 33,072 |
| VECTOR | 1,057 / 32,964 | 42 / 1,207 |
| LINE | 981 / 30,411 | 605 / 18,675 |
| PCURVE | 618 / 19,796 | 618 / 19,796 |
| EDGE_LOOP | 301 / 16,870 | 301 / 16,870 |
| SURFACE_CURVE | 275 / 16,691 | 275 / 16,691 |
| ADVANCED_FACE | 293 / 13,627 | 293 / 13,627 |

Exact immutable duplication totaled **33,529 definitions / 2,443,384 serialized line bytes** before optimization. This includes 30,869 Cartesian points / 2,351,024 bytes, 1,159 directions, 1,015 vectors, 376 lines, 76 spline curves, 19 placements, 9 circles, and 6 planes. Duplicate line bytes exclude the additional effect of shorter renumbered references. After sharing, the audited immutable types have zero exact duplicates.

The dominant repetition was control points shared mathematically by adjacent spline supports and boundary curves but serialized independently. Topology itself is modest. Distinct thread turns have different placed supports: none of the 114 spline surfaces is an exact duplicate. The remaining 6 hyperbolas, 76 linear-extrusion surfaces and 22 trimmed curves also have zero exact duplicates. Sharing leaf/control geometry addresses the evidence without merging face trims or changing the helical realization.

## Bounded implementation and cleanup

`Step242TextWriter` owns one ordinal dictionary per export. A whitelist admits immutable points, directions, vectors, placements, analytic supports/curves, and ordinary polynomial spline definitions. Keys include the complete serialized type, name, arguments, and already-resolved child references. First encounter determines numbering. No approximate equality, tolerance welding, global state, thread/bolt condition, alternate serializer, or persistent cache is introduced. Memory is bounded by the unique geometry in that export.

Topology, pcurve/surface-curve associations, representation/product entities, and raw complex entities retain independent identities. Rational/raw entities are deliberately outside this bounded interner. No AP242 association or valid pcurve is removed. Repeated qualified pcurve geometry benefits from ordinary point/line sharing while all 618 pcurve and definitional-representation associations remain.

Numeric formatting remains the existing invariant `0.#############` engineering formatter, including its cross-platform last-bit suppression contract. Actual literals were already compact; arbitrary decimal truncation would introduce a different precision policy. B2 changes no numeric strings. Product/context boilerplate is small and not accidentally multiplied per face; no metadata reduction was justified.

The dedicated review covered cache ownership, exact equality, deterministic insertion order, names/dimensionality, excluded associations, memory lifetime, formatter ownership, and absence of showcase conditions. Temporary B1 phase probes are removed. The final SDK snapshot is built from unmodified production transport/runtime sources.

## Correctness, determinism and external import

The profiler's identity-independent graph digest recursively resolves references, retains every topology/association/metadata multiplicity, and counts equal immutable geometry once. Before and after share digest `F88C6A7B3B58E97CF242A1DCC7B74AB04FA7BD8B600A07ECB2F3EFA7183B05A4`. Thus names, geometry arguments, numeric text, topology, pcurves and semantic structures match after legal immutable sharing. This evidence complements actual reimport rather than replacing it.

Four exports of the same runtime body compare byte-for-byte with the normal compiler result, each followed by import and topology-count checks. Final STEP SHA256 is `2376F5E53F715716F4F0823C3787059B0E69337B22EFA39F93357F4951BE89E8`; the repository CLI independently produces the same bytes.

FreeCAD 1.0.2's normal native importer accepts the optimized STEP as one valid, closed solid/shell: 293 faces, 693 edges, 408 vertices. Native bounds and volume are exactly equal to the pre-optimization repaired artifact: 2938.769841889224 mm³, X [-5.3, 52], Y/Z approximately ±7.505553499465 mm. Translator seam splits explain its four extra edges and two extra vertices. No custom healing or geometry rewrite is applied.

The ordinary unthreaded, unmarked HexBolt improves from 22,476 to 17,825 bytes (20.69%). Its normalized graph digest is unchanged; both native imports are valid/closed with one solid, 21 faces, 48 edges, 28 vertices, identical bounds and 3381.1925048367398 mm³ volume. This is a general exporter regression, independent of thread machinery.

Existing semantic PMI and corpus tests exercise associated exports. The established snapshot updater refreshes 16 canonical serialization hashes; a separate comparison verifies every corpus success/failure, diagnostic and topology snapshot field remains identical. Cone checks now compare support values and every face's angle rather than requiring duplicate support definitions. The three surgery parity hashes are refreshed while binding, roundtrip, enclosure and orientation assertions remain.

## Performance and remaining size

Warm native .NET runs use one discarded warm-up plus three timed repeats on the same body; assertions and file writes are outside export/import timers. Milliseconds below are medians, not statistical performance guarantees.

| Path | Export before → after | Import before → after |
| --- | ---: | ---: |
| Modeled/engraved bolt | 118.913 → 125.960 ms | 793.705 → 650.693 ms |
| Ordinary bolt | 0.311 → 0.306 ms | 14.151 → 10.229 ms |

The measured export increase is 5.9% (~7 ms), with 18.0% faster import and 35.1% smaller output. Native FreeCAD import measured 0.920 s before and 0.728 s after in separate single runs; startup/cache variance limits that comparison. No large CPU penalty is observed.

Remaining size is primarily unique Cartesian control data (3,385,829 bytes, 74.6%), spline surfaces (484,617), and spline curves (411,575). These three consume 94.3% of output. They express the current certified piecewise helical and engraved geometry; a smaller representation would require a separate geometric realization/tolerance project. This audit does not claim a mathematically minimal STEP encoding. It establishes that obvious exact duplication is eliminated without weakening the artifact.

## Evidence and verdict

Ignored artifacts under `artifacts/local/p4-01b/compactness/` include `repaired-before.step`, `before-profile.json`, `after-profile.json`, `ordinary-before-profile.json`, `ordinary-after-profile.json`, `before/` and `after/` timed exports/imports, and independent native reports. `showcase/bolt-cli.step` is the final deliverable. Final build/test and product qualification are recorded in [the closeout](P4-01B-CLOSEOUT.md).

**B2 verdict: Accepted.** Exact exporter sharing materially removes avoidable bloat while preserving full modeled thread, maker mark, topology, numeric precision and associations.
