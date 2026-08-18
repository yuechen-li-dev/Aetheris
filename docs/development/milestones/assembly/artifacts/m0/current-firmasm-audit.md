# Current assembly audit

Audit date: 2026-08-09. Ground truth: `aetheris asm --help`, production source, tests, fixtures, and current STEP docs.

| Existing piece | Classification | Evidence / M0 decision |
|---|---|---|
| `FirmasmManifestLoader` strict JSON/schema validation and path loading | legacy compatibility only | Sound validation, but its flat `parts` + `instances` schema cannot express hierarchy, semantics, Interfaces, or Mates. Retained for `asm exec/export`. |
| `FirmasmAssemblyExecutor` rigid BRep transform/composition | salvage later | Exact world-space transform application and deterministic ID remapping can serve a future AssemblyIR geometry executor, but M0 does not pretend authored transforms are Mate solving. |
| `.firmasm` transform-first authoring | architectural dead end | Every instance requires `translate`; hierarchy and relationship intent are absent. It no longer defines the canonical model. |
| `FirmasmAssemblyRoundtripExporter` | legacy compatibility only | Truthfully emits one STEP per instance plus package JSON. It is not AP242 assembly structure. |
| STEP multi-root classifier/extraction studies | salvage | Root classification and NAUO/CDSR/IDT traversal evidence remain valuable for future import, independent of native semantics. |
| Part-instance identity | migrate | Old unique string IDs become structured `AssemblyPath` plus deterministic stable IDs. Definition identity is preserved separately. |
| Nested assemblies | missing / replaced | Old execution flattens; M0 keeps explicit parent/children nodes and does not flatten authority. |
| Mates/constraints | missing / new architecture | No old Mate idea constrained the design. M0 adds relational Interface definitions and an independent Mate graph. |
| BOM | missing / seam added | Deterministic tree inspection and definition identity now provide quantity grouping inputs; ERP behavior stays out of scope. |
| AP242 assembly export | exact missing capability documented | Current exporter has single-body/per-instance support, not `PRODUCT_DEFINITION`/NAUO/mapped-item assembly authoring. No false proof was added. |
| Tests/docs/CLI | migrate | Old tests remain for compatibility. New tests and `asm inspect` cover canonical M0. `docs/development/milestones/general/assembly.md` is marked legacy. |

The existing `let` audit found typed immutable scalar/Record lets, aliases, expression dependencies, and dimensional units. The existing `tol` audit found bilateral and asymmetric syntax, `mm`/`deg` checks, declaration binding, alias preservation, Table/Template-compatible provenance, and a warning when arithmetic drops tolerance. M0 reuses the same nominal/lower/upper semantics in `TolerancedDimensionBinding`; it does not change nominal geometry.

InlineStep recognized values already produce `SemanticValue`, but current recognition does not generally prove assembly axes plus toleranced dimensions. M0 therefore documents this seam rather than inventing recognition. Forge extension output also already enters through validated `SemanticValue`; no origin branch was added, but an end-to-end Forge-authored assembly fixture was not attempted because assembly source does not yet invoke Forge/Templates.
