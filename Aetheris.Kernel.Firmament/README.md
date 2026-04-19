# Aetheris.Kernel.Firmament

This project is the future home of Firmament in Aetheris.

Current status: pre-M0 scaffold only.

Implemented scaffolding:
- compile boundary contracts (`FirmamentSourceDocument`, `FirmamentCompileRequest`, `FirmamentCompileResult`)
- Firmament diagnostic taxonomy placeholders and code-family conventions
- source location contract placeholders (`FirmamentSourcePosition`, `FirmamentSourceSpan`)
- deterministic compiler facade stub (`FirmamentCompiler`) that reports not implemented
- tiny pre-M0 curated corpus scaffold (`testdata/firmament/`) with a manifest and placeholder `.firmament` fixtures consumed by tests

Not implemented yet: parser, semantic validation, selectors, lowering, or STEP behavior. The corpus scaffold is infrastructure-only and will be expanded by future parser/validator/lowering work.

The top-level lanes (`Connectors`, `ParsedModel`, `Lanes`, `Mapping`, `Diagnostics`) are intentionally separated to guide future implementation work.

Pre-M0 composition seam note: `ImportOrchestrator.CreateDefault(...)` now supports additive registration while keeping STEP/AP242 as the default import composition, so Firmament can later register its own source-family connector/lane without reshaping STEP-specific wiring.

Canonical corpus note: repository `.firmament` fixtures under `testdata/firmament/fixtures/` are maintained only in canonical TOON-style Firmament syntax; JSON-shaped source text is supported by parser compatibility but is not canonical corpus form.


FORGE-M0c note: reusable external/library parts are routed through `Connectors/FirmamentPartLibraryConnector.cs` (currently `standard_library/*`), while operation/schema parsing/validation/lowering remains lane-owned.
