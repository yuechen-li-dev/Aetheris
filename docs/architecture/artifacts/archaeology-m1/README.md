# AETHERIS-ARCHAEOLOGY-M1

Status: audit complete; architecture frozen for phased migration. Repository evidence was inspected on 2026-08-13. Runtime architecture is unchanged; two narrow compatibility-test/formatter inconsistencies found by the audit were corrected.

## Decisions

1. Firmament V2 is the sole canonical engineering authoring language. Its `.firmament` general profile and `.firmasm` Assembly profile are current.
2. The older `firmament/model/ops/schema/pmi` document has two accepted codecs, TOON-style and JSON-shaped. Both normalize to `FirmamentParsedDocument`; the surrounding validator, lowerer, executor, and `expect` machinery are legacy execution semantics, not serialization.
3. Legacy JSON `.firmasm` is a third, unrelated format with its own `FirmasmManifest` model. It remains a deprecated compatibility input. Read, migration, direct execution, and outbound STEP-package export are live Preview 3 compatibility paths; same-format JSON writing is absent.
4. `BrepBoolean` is frozen as a compatibility facade over bounded recognized families. It is not a general intersection-to-topology engine. New surface/tool families require a bounded migration or critical regression justification.
5. Future topology ownership is split three ways:
   - geometry/contact/intersection queries produce evidence;
   - recognized construction recipes decide expected topology;
   - a small BRep Surgery layer realizes explicit topology and validates it.

## Artifact index

| Artifact | Question answered |
|---|---|
| [firmament-v1-map.md](firmament-v1-map.md) | What V1 source, model, semantics, callers, fixtures, and tests exist? |
| [legacy-firmasm-map.md](legacy-firmasm-map.md) | What does legacy JSON `.firmasm` own and what compatibility remains live? |
| [boolean-callers.md](boolean-callers.md) | Who calls `BrepBoolean`, with what intent and migration risk? |
| [boolean-internals-map.md](boolean-internals-map.md) | Which Boolean components are recognition, policy, history, recipes, validation, or low-level mechanics? |
| [surgery-candidates.md](surgery-candidates.md) | What bounded Surgery Kit surface is justified by existing repetition? |
| [boolean-lessons.md](boolean-lessons.md) | What did the implemented families teach, and why does central expansion not scale? |
| [migration-plan.md](migration-plan.md) | What are M2-M6, their compatibility risks, rollback boundaries, test classes, and freeze policy? |
| [validation-report.md](validation-report.md) | What was inspected and what validation passed? |

## Ownership boundary

```text
Firmament V2 source (canonical intent)
              |                           legacy input only
              |                     +------------------------+
              v                     | V1 TOON / V1 JSON      |
     recognized construction        | legacy JSON .firmasm   |
              |                     +-----------+------------+
              v                                 |
       construction recipe                 versioned codec
              |                                 |
              +--------------+------------------+
                             v
                   explicit BRep Surgery
                             |
              +--------------+------------------+
              v                                 v
       topology + bindings               validation/provenance

Geometry queries (side/closest/intersection/contact) feed evidence into
recognition and validation; they do not decide or author result topology.
```

## Direct verdict

Firmament V1 TOON/JSON and legacy JSON `.firmasm` can be reclassified as versioned serialization/compatibility formats as an architectural destination, while V2 remains sole canonical authoring. They are **not yet codec-only in implementation**: V1 build fallback and legacy `.firmasm` direct execution/export are live dependencies that M2 must wrap and name explicitly before semantic code can be retired.

The current Boolean stack can be decomposed without losing working knowledge. Repeated topology mechanics are extractable; family recognition, expected-topology policy, and `SafeBooleanComposition` history must remain above that substrate as explicit recipes/state. The safest first implementation milestone is M2, the serialization boundary, because it can remove language/format ambiguity without changing geometry or topology.
