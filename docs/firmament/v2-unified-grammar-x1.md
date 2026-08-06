# FIRMAMENT-V2-UNIFIED-GRAMMAR-X1 reconciliation log

## Decision

Firmament V2 has a canonical `Model { Units: mm ... }` surface for ordinary mechanical parts. Its parser produces the existing `FirmamentV2Document` normalized AST: solids, Modify blocks, semantic holes, edge finishes, and optional Concept IR. Export routing consumes that normalized semantic inventory; it does not receive a parser-lane identifier.

| Concern | Previous forms | Canonical form | Compatibility |
| --- | --- | --- | --- |
| Root and units | `Model X mm`, `model X { units mm }` | `Model X { Units: mm }` | Existing forms remain compatibility routes. |
| Primitive | `Box Base`, `solid Base : Box` | `Box Base { Size: [...] }` | Lowercase record bindings remain compatibility inputs. |
| Point2 | `[x,y]`, `[xmm,ymm]`, `Point2(...)` | `Point2(xmm, ymm)` | Canonical rejects bracket points with a specific diagnostic. |
| Hole | `hole<Shaft>`, face selectors, assorted ends | `Hole<Shaft> { On, Center, Diameter, End }` | Existing semantic-hole and construction-plane forms remain supported through their adapters. |
| Edge finish | phase-only `EdgeFinish` | `EdgeFinish { Face, Target, Kind, Distance/Radius }` inside any `Modify` | Existing phase source remains a compatibility route. |

## Parser inventory at the start of X1

| Historical trigger | Resulting parser/AST | Capability boundary |
| --- | --- | --- |
| lowercase `model` | `FirmamentV2Document` direct parser | records, regions, semantic holes; previously no canonical edge vocabulary |
| leading `Model X mm` | `ParsePhase3ModelingDocument` | one primitive, required Modify, one to three EdgeFinishes |
| `Concept`, `Concept Struct`, or `Struct` | `ConceptIrResolver` then `FirmamentV2Document` | construction planes, profiles, compose, semantic geometry |
| `RoundedBox` / construction-policy token shape | bounded special parser | one historical bounded primitive shape |

The canonical root is recognized before those compatibility adapters. It parses the whole balanced document and builds a normal `FirmamentV2Document`; no edge/hole backend is chosen while source spelling is being recognized. `NotRecognized`, `RecognizedInvalid`, and `RecognizedValid` are retained so only true non-V2 inputs can fall back to V1.

## Proven route

`box-hole-chamfer.firmament` is 8 lines including its root and closing braces (compared with the 22-line Concept/Struct reconstruction that motivated X1). It parses to one Box, one `Modify`, one semantic shaft ThroughAll hole, and one edge finish. The existing semantic route selector chooses `CombinedHoleEdgeFinish`; its authoritative plan is exported to STEP and reimport-verified by the focused regression test.

The former Concept/Struct solution is still appropriate when placement requires construction-plane or composed-host semantics. The ordinary part does not need that ceremony.

## Deliberate limits and next work

This change establishes the canonical ordinary grammar and its normalized-AST boundary. The legacy Concept IR and profile/composition syntaxes still have dedicated compatibility parsers, so a complete single lexical grammar for every advanced declaration remains the next release blocker. Slot and semantic-selection declarations also need first-class canonical productions before the compatibility lanes can be retired. This is intentionally not hidden: today, the canonical parser gives specific diagnostics rather than silently switching an author into a different dialect.
