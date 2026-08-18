# Preview 2 release-hardening inventory

Audited 2026-08-10 against current code, tests, `docs`, `references`, release
metadata, TODO/FIXME evidence, and Preview 1 release records.

| Item | Disposition | Current evidence |
| --- | --- | --- |
| Concept Path Profile + Compose semantics | Closed | `ConceptPathAuthoringTests.ProfileFromPath_IsAnOrdinaryComposeOperand_WithPathProvenance` and consolidation M1 evidence. |
| Generic Compose unresolved-profile diagnostic | Fixed in hardening | Renamed to `ConceptPathProfileNotAdmittedByCompose`; regression assertion updated. |
| Canonical parser source spans | Experimental but acceptable | Normal-user V2 AST nodes for Templates, Require, Module/Panel/Assembly/Drawing/Review-facing declarations carry authored `FirmamentV2SourceSpan`; parser-wide legacy string diagnostics remain bounded debt. |
| CIR/FRep whole-loop volume authority | Partially closed | Shared constructive intent and BRep ownership now provide supported authority; mixed line/arc whole-loop fillet remains Experimental because curved-trim certified volume envelopes are release-loose. Universal authority is not claimed. |
| Parasolid chamfer interop | External/vendor issue | Aetheris keeps validated AP242 output authoritative; no importer-specific workaround was introduced. Vendor reproduction remains external. |
| `FirmamentTemplateHostBridge.Expand` duplicate-key exception | Fixed in hardening | Duplicate enumerations convert to `firmament-host-argument-duplicate`; raw `ArgumentException` no longer leaks for this caller misuse. |
| Forge enum/symbol typed value | Fixed in hardening | `ForgeEnumCase` is typed by declared enum, validates identifiers, emits a bare symbol, and never string-coerces. |
| Forge extension default trust | Fixed in hardening | Safe default plus explicit `UNSAFE` declaration and `AllowUnsafeExtensions` host consent gate. |
| Old `Aetheris.Forge.Sdk` package | Obsolete | Migration documentation points to Host/KernelSDK; no current project/package uses the old name. |
| General NURBS, SheetMetal, SubD/SDF, G1/G2 Panels | Deferred post-P2 | Feature-freeze non-goals; status documented as Future/bounded. |
| Mixed-profile whole-loop fillet | Experimental but acceptable | Exact shell exists; measurement certification boundary remains explicit. |
| Implicit plugin discovery / hostile-code sandbox | Deferred post-P2 | No implicit scanning; in-process CLR isolation is not falsely claimed. |

Historical milestone documents remain evidence of their then-current boundaries.
The current contract is the feature manifest, Preview 2 notes, and this inventory.
