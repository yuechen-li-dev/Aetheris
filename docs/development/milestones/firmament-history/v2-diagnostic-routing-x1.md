# Firmament V2 diagnostic routing X1

## Fixed control flow

Previously `FirmamentBuildAndExport.ExportSource` ran the Firmament V2 parser, but only returned its fatal diagnostics when a build-layer leading-token heuristic guessed that the source was V2. A recognized `Concept`/`Struct` document could therefore produce useful V2 diagnostics and still fall through to the V1 TOON/JSON parser, which reported the unrelated `FIRM-PARSE-0001` canonical-TOON/JSON-root error.

## Parser recognition contract

`FirmamentV2ParseResult` now carries `FirmamentV2ParseDisposition`:

- `NotRecognized`: no V2 grammar entry point admitted the source. V1 fallback remains permitted.
- `RecognizedInvalid`: a V2 grammar entry point admitted the source and fatal V2 diagnostics prevented a valid V2 document.
- `RecognizedValid`: V2 admitted and produced a valid document.

Recognition is decided at the V2 parser boundary from the grammar route that is actually selected (including Concept/Struct, construction-policy, rounded-box, Phase 3, and generic V2 model/template admission). It is not decided by `FirmamentBuildAndExport` scanning for raw source keywords.

## Build and validate agreement

When the disposition is `RecognizedInvalid`, build returns the parser's fatal V2 diagnostics in deterministic parser order after its existing ordinal deduplication. Their diagnostic messages remain the original V2 codes and their source remains `FirmamentV2.Parse`; no generic validation wrapper replaces them. The CLI build JSON keeps every returned diagnostic in its `diagnostics` array. `aetheris validate` continues to report the same codes as fatal V2 validation diagnostics.

Once Firmament V2 recognizes a document and produces fatal diagnostics, those diagnostics must never be hidden by legacy-parser fallback.

`NotRecognized` is deliberate: a valid legacy V1 TOON document continues to use the V1 parser, and arbitrary non-V2 text deterministically reaches the V1 parser and receives its normal legacy parse diagnostic. This avoids treating speculative generic V2 failures as authoritative for JSON or V1 input.

## Regression fixture

`fixtures/Language/invalid/concept-struct-diagnostic-routing-x1.invalid.firmfixture` is a minimized Concept/Struct document with a malformed construction-plane `Hole<Shaft>` center and an omitted required Concept member. Both `validate` and `build` surface `HoleLocalCenterInvalid` and `firmament-concept-missing-member:BracketConcept.RequiredExpose`; build does not surface `FIRM-PARSE-0001` or the canonical TOON/JSON-root wording.

## Remaining boundary

This change routes diagnostics; it does not unify the existing Firmament V2 dialects, change Hole or EdgeFinish syntax, or materialize combined Hole + EdgeFinish export. The next milestone remains combined Hole + EdgeFinish export.
