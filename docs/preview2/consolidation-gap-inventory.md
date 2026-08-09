# Preview 2 consolidation gap inventory

This is the current cleanup source of truth. Historical milestone documents are
kept as history; current claims are classified here and in the Preview 2 feature
manifest (`docs/preview2/feature-manifest.json`).

## Closed in M1

| Gap | Result |
| --- | --- |
| Concept Path -> Profile | Already valid; contract documented as validated `ResolvedProfile2D`. |
| Concept Path-derived Profile -> Compose | Closed through one resolved-profile operand dictionary. |
| Path Profile -> Selection | Supported through ordinary named Profile segments; native fixture covers it. |
| Template-authored path | Expansion precedes path binding; no template-specific lowering. |
| Table/Record-derived Template path | Exact native fixture builds; input and specialization provenance retained. |
| Capability inspection | CLI JSON reports path capabilities, members, consumers, stable IDs, and provenance. |
| Stale Preview 1 guard/fixture | Invalid fixture promoted to valid; capability manifest and roadmap updated. |
| CIR/FRep authority wording | Historical roadmap marked superseded and linked to settled architecture. |
| SurfaceMeshIR M1-era status | Current M2-M7 capability progression called out explicitly. |

## Still Experimental

- Whole-loop mixed line/arc Profile Fillet: exact shell executes, but curved-trim
  volume verification remains too loose for promotion.
- Forge trusted extensions: deterministic and validated, but trusted/in-process,
  explicitly registered, and not a sandbox/plugin marketplace.
- Bounded recognition and recovery routes where diagnostics label them as such.

## Intentionally unsupported

- Dynamic reflection, arbitrary object walking, and string-based
  `Resolve("A.B")` language semantics.
- Raw BRep topology traversal or mesh IDs as Concept Path.
- Arbitrary CIR/SDF -> exact BRep and arbitrary BRep -> authoritative CIR.
- Spline/NURBS support surfaces in SurfaceMeshIR.
- Unsupported Modify targets without existing exact identity/topology contracts.
- Untrusted Forge extension execution and implicit assembly scanning.

## Deferred post-Preview2

- One typed semantic-reference/value layer spanning Concept IR exposure,
  recognized imported regions, Forge outputs, Selection, Modify, and AnalysisIR.
- Profile-capable and boundary-region-capable semantic value kinds for that layer.
- General imported exact-profile recognition (no new recognition was invented).
- Curved FEA boundary constraints/loads, nonlinear mechanics, contact, dynamics,
  and higher-order recovery.
- General Forge loft/multi-body assembly, production source generation, nested
  host Record construction, and broader native InlineStep parameter ergonomics.
- Rational spline trim policy and general trimmed-support meshing.

## Candidate Preview 2 follow-ups

| Rank | Gap | Friction | Risk | Cost | Evidence | Preview 2 relevance |
| ---: | --- | ---: | ---: | ---: | ---: | ---: |
| 1 | Typed semantic reference shared by Selection/FEA/Recognize | 5 | 5 | 5 | 3 | 5 |
| 2 | Preserve exact recognized-region capability metadata in common binder | 5 | 4 | 4 | 3 | 5 |
| 3 | Compose/Profile source-span modernization | 3 | 3 | 3 | 4 | 4 |
| 4 | Forge semantic output/exposure contract | 4 | 4 | 5 | 2 | 4 |
| 5 | Curved-trim certified volume for mixed Fillet | 3 | 4 | 5 | 5 | 3 |
| 6 | Forge production source generator | 2 | 2 | 3 | 3 | 2 |

Scores are ordinal 1-5. No competing runtime strategy is selected here, so a
JudgmentEngine integration would add machinery without improving the audit.
