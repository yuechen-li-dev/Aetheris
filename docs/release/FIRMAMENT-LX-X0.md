# FIRMAMENT-LX-X0 — language experience contract

## Executive verdict

**Meaningful progression.** Aetheris now provides partial-source, compiler-owned Helix and Loft field completion through the Web SDK, plus selector candidates derived from the qualified geometry source map. This is not enough for an unfamiliar user to author representative Firmament entirely inside Helios. The editor still uses a textarea and has no Monaco completion, hover, or live language diagnostics.

## Language inventory and authority

| Area | Current authority | Available now | Gap |
| --- | --- | --- | --- |
| Top-level and canonical primitives | `FirmamentV2Parser`, feature/template expanders, specialist parsers | Grammar and diagnostics for complete input | No shared construct catalog or recovery tree for partial input |
| Helix | `WireFormAuthoring` operation admission and `WireCoilAuthoring.CreateAxis` | Radius, Turns, Pitch/Height, Handedness, StartPhase; type, default, choices, meaning, required state | No diagnostic ranges or value completion; Pitch/Height is an either-or requirement |
| Loft | `LoftAuthoringParser` | Solid/hollow field lists reused for field admission and completion | Cross-field validity and referenced profile/frame symbols are not exposed |
| Hole | `FirmamentV2Parser.ParseCanonicalHoles` and related semantic hole binders | Variant-specific field admission and named Hole lowering | No public authoring metadata or partial Hole completion |
| Feature, Concept, Interface, Pattern, Set, Span | Existing expansion, parsing, and lowering owners | Complete-source semantics and canonical fixtures | No unified authoring metadata or same-document symbol/reference index |
| Source selectors | `GeometrySourceMap`, construction correspondence, Web SDK mesh provenance | Stable qualified selectors including Box axis faces and simple Hole walls | Completion is snapshot-derived and not yet filtered by a parsed expected-selector position |
| Diagnostics | Firmament parser/binder/build results | Build diagnostics | Existing Web runtime fallback source range is zero-length at document start; no lightweight language diagnostic route |

The language service is `FirmamentLanguageService.Complete`. It asks `WireFormAuthoring` or `LoftAuthoringParser` for context/fields. It uses a bounded prefix/field-presence scan on incomplete source and does not run BRep construction. `LoftAuthoringParser` uses the same field metadata for parser admission. `WireCoilAuthoring` keeps Helix field names beside the actual binder checks. Web SDK `ModelSession.selectorCandidates` filters the existing compiler construction correspondence exposed by `geometrySourceMap()`; it does not infer source selectors from display IDs.

## Public Web SDK contract

```ts
const result = await cad.language.complete(source, cursorOffset, {
  sourceName: 'spring.firmament', sourceRevision: 'draft-7'
});
// result: { document, revision, context, replaceStart, replaceLength,
//           fields: [{ name, type, required, meaning, default?, choices? }],
//           missingRequiredFields }

const faces = model.selectorCandidates('Face');
// Each item carries selector, semanticKey, outputRole, source,
// qualification, and buildRevision from the last valid model snapshot.
```

Results are JSON-safe and deterministic for the same source and offset. The caller owns source revision tokens. A model's selector candidates carry build revision and must be discarded when their compiled source is stale relative to the editor draft.

## Coverage and Helios

| Construct | X0 completion/help status |
| --- | --- |
| Box | Existing compiled selector correspondence; no field completion |
| Helix | Partial-source field completion and required field hints |
| Hole | Existing qualified wall selector and insertion; no field completion |
| Feature | No LX metadata or completion |
| Concept | No LX metadata or completion |
| Loft | Partial-source solid/hollow field completion and required field hints |
| Other constructs | No LX field metadata or completion |

Helios currently consumes qualified picking, Go to Source, Copy Selector, Box/Hole commands, and rebuild diagnostics. It does **not** consume the new language completion API. Monaco integration, Problems markers, source hover, go-to-definition, and LX-sourced command snippets remain open. No Helios release note is claimed for this Aetheris-only substrate change.

## Verification and performance

- `dotnet build Aetheris.slnx -c Release --no-restore -m:1`: passed; existing SQLite WASM warnings remain.
- `dotnet test Aetheris.slnx -c Release --no-build --no-restore -m:1`: passed across the test projects (3,857 reported tests); the separate FrictionLab project reported no test count.
- Full Firmament suite: 1,599 passed. Targeted language service, semantic hole, and parser lane: 138 passed.
- SDK transport and source-map selection tests: 5 passed.
- Helios Vitest suite: 28 passed in 5 files. This verifies existing behavior only; Helios does not call LX yet.
- Headless Chrome using the packaged WebAssembly runtime returned `Radius: Length` for an incomplete Helix and legal remaining fields for an incomplete Loft. A compiled plain Box yielded all six axis selectors; a compiled through-Hole model yielded `face(H.Wall)` with no runtime ID suggestions.
- In one browser run after runtime initialization, first Helix completion took 33.5 ms and median of ten subsequent calls took 0.4 ms; first Loft completion took 2.5 ms and warm median took 0.6 ms. These are local observations, not an SLA. Top-level completion, hover, and language diagnostics do not yet exist to measure.
- Fresh-agent Box/Helix/Hole authoring tasks were not run. The missing Helios editor integration would make the requested in-editor tasks fail today.

| Fresh-agent task | Result | Time | Wrong turns | Missing tooling | Fix |
| --- | --- | --- | --- | --- | --- |
| Box / Helix / Hole authoring and Hole-wall reference | Not run as fresh-agent tests | Not measured | Not measured | Monaco LX providers and broader compiler metadata | Complete editor consumption and rerun independently |

## Next blocker

Most Firmament construct legality is embedded in several specialist parser/binder branches rather than a reusable authoring catalog. The current service only covers Helix and Loft. Expanding it responsibly requires each owner to expose fields/types/choices and a shared partial-source context layer before wiring Monaco. Copying those signatures into Helios would violate the single-language-authority contract. Source-range diagnostics, symbols, definitions/references, and selector-position filtering still require compiler-owned semantics. X0 acceptance criteria are not met.
