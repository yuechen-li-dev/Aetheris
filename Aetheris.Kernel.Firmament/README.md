# Aetheris.Kernel.Firmament

This project owns the Firmament compiler-facing implementation: the canonical Firmament V2 frontend and assembly profile, AIR/materialization bridges, STEP build/export orchestration, plus frozen V1 and legacy `.firmasm` compatibility paths.

Current architecture:

- Firmament V2 is the sole canonical engineering authoring language. `.firmament` is its general document profile and `.firmasm` is its current Assembly profile.
- `FirmamentV2/` contains the current parser, typed/static authoring, Concepts/Templates, validation, and lowering integration.
- `Assembly/` contains current Assembly compilation/interchange and the deprecated JSON-shaped `.firmasm` compatibility reader/migration path.
- `Parsing/`, `ParsedModel/`, `Validation/`, `Lowering/`, the public `FirmamentCompiler`, and related `op`/`expect` execution represent the older V1 TOON/JSON path. They remain for build compatibility and historical regression, not for new language features.
- `Materializer/` and parts of `Execution/` are shared implementation substrate; do not assume every file in those folders is V1-owned.
- `FirmamentBuildAndExport` routes recognized V2 documents through current paths and retains a V1 export fallback for unrecognized historical documents.

The V1 parser accepts both TOON-style and JSON-shaped documents and normalizes them into `FirmamentParsedDocument`. Its formatter emits deterministic canonical TOON, not lossless source preservation and not JSON. The broad V1 test suite is opt-in through `AETHERIS_RUN_LEGACY_TESTS=1`.

The authoritative ownership and migration audit is [AETHERIS-ARCHAEOLOGY-M1](../docs/development/architecture/system/artifacts/archaeology-m1/README.md). The current V2 language reference is [docs/development/history/firmament/preview2-reference/language-reference.md](../docs/development/history/firmament/preview2-reference/language-reference.md).
