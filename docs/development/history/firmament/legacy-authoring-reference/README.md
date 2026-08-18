# Historical Firmament authoring reference

> Historical only. This material predates the canonical Preview 3 language and is retained for engineering provenance and the language-inconsistency inventory. It is not an authoring contract. Use [`docs/public/firmament`](../../../../public/firmament/overview.md) for current supported syntax.

This folder was an implementation-grounded reference for an earlier low-level Firmament surface.

Goal: let a fresh model write correct Firmament without guessing hidden semantics.

## Reading order

1. `01-language-overview-and-pipeline.md`
2. `02-file-shape-and-sections.md`
3. `03-primitives-and-local-frames.md`
4. `04-placement-semantics.md`
5. `05-booleans-safe-families-patterns.md`
6. `06-supported-vs-deferred.md`
7. `07-semantic-mismatches-and-gotchas.md`
8. `08-worked-examples.md`

## Scope

This reference was anchored to repository behavior at the time it was written. Its TOON-style examples, placement rules, and supported/deferred claims are not current Preview 3 guidance.

Integration boundary note: this corpus focuses on file-authoring semantics, not full caller-facing compiler API contracts.  
Integrators should inspect `Aetheris.Kernel.Firmament/FirmamentCompileResult.cs`, `Aetheris.Kernel.Firmament/FirmamentCompilationArtifact.cs`, and integration tests under `Aetheris.Kernel.Firmament.Tests`.
