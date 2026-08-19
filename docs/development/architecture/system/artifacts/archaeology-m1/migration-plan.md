# Phased migration, freeze, tests, and documentation plan

## Freeze policy effective after M1

Allowed:

- fixes for regressions in already-supported bounded families;
- diagnostics, validation, and tests that make existing bounds explicit;
- extraction work required by M2-M5 with parity evidence;
- new product geometry through V2 Template/construction primitive/recognized recipe, using Surgery where necessary;
- a new central-dispatch case only when it is an unavoidable bounded migration bridge or critical compatibility regression, documented with an owner and removal plan.

Not allowed:

- adding a surface/tool family to `BrepBoolean` merely to make the generic API appear broader;
- treating numerical intersection as topology authority;
- new V1 Concepts/Templates/Modules/authoring features;
- new transform-first legacy `.firmasm` semantics;
- arbitrary topology surgery in `Forge.Host`.

Working families, fixtures, and tests remain supported during migration.

## M2 - serialization boundary (first implementation milestone)

| Item | Plan |
|---|---|
| goal | remove format/language ambiguity with no geometry change |
| scope | name `FirmamentV1Toon`, `FirmamentV1Json`, `LegacyFirmasmJson`; split parse DTO/codecs from V1 semantics/I/O; add explicit compatibility route and diagnostics; document CLI asymmetry |
| movement | initially namespaces/facades inside `Aetheris.Kernel.Firmament`; no assembly move. `FirmamentTopLevelParser` codec portions and `FirmasmManifestLoader.Parse` become explicitly versioned readers. Keep old public facades as adapters. |
| compatibility | preserve V1 build fallback, legacy JSON `.firmasm` inspect/display/export, and current V2 `.firmasm`; make Forge/server legacy admission explicit rather than accidental |
| risk | low/medium: format detection and diagnostics can change routing; protect with fixture hashes/model equivalence |
| rollback | old compiler/loader remain behind adapters; switch routing back without data migration |
| success evidence | V1 TOON/JSON normalized-model tests; deterministic TOON write; legacy JSON migration authority; V2 `.firmasm` unchanged; CLI compatibility matrix |

M2 should not build a same-format legacy `.firmasm` writer unless an external Preview 3 requirement is proven. It should first isolate live execution dependencies.

## M3 - Surgery substrate extraction

| Item | Plan |
|---|---|
| goal | extract repeated explicit topology mechanics without changing recipe policy |
| scope | oriented loop/face construction, deterministic ID/binding remap, known ring/section scaffolds, shell assembly, validation bundle |
| movement | private repeated mechanics from Boolean builders and existing Firmament stitching/remap helpers into internal `Aetheris.Kernel.Core.Brep.Surgery` |
| compatibility | `BrepBoolean` signatures, routing, `SafeBooleanComposition`, outputs and diagnostics remain stable |
| risk | medium: orientation, IDs, and STEP ordering are sensitive |
| rollback | each builder can retain or restore its old private helper; extract one primitive at a time |
| success evidence | topology counts, binding/manifold validation, deterministic STEP/hash where contractual, and legacy Boolean suites green |

## M4 - representative recipe migration

| Item | Plan |
|---|---|
| goal | prove policy/recipe/Surgery separation on representative families |
| scope | through hole first; then polygonal through cut or orthogonal union; preserve facade adapters |
| movement | recognition/history inputs become explicit recipe requests; recipe calls Surgery; old builders become adapters/worked examples until parity is established |
| compatibility | no family removal and no generic expansion |
| risk | medium: provenance/diagnostic parity and hidden composition metadata |
| rollback | per-family dispatcher switch returns to old builder |
| success evidence | old/new differential tests across canonical, tolerance-boundary, invalid, and STEP reimport cases |

Add architecture comments at migrated recipe entry points and publish the two kernel teaching docs in this phase.

## M5 - production caller migration and advanced boundary

| Item | Plan |
|---|---|
| goal | stop known construction paths from pretending to be generic user Booleans |
| scope | `ThroughHoleRecoveryExecutor`, `HoleRecoveryExecutor`, StandardLibrary, recognized CIR materialization, then V2 primitive bridge; classify server endpoint |
| movement | callers invoke recipes; `FirmamentPrimitiveExecutor` retains a compatibility facade only for V1/generic legacy ops. Evaluate proven safe advanced Surgery surface for `Forge.KernelSDK`; unsafe operations require explicit unsafe tier. |
| compatibility | current artifacts and APIs retained through adapters; server reports bounded capability/rejection honestly |
| risk | medium/high for stepped/counterbore history; high for external server API semantics |
| rollback | caller-by-caller feature switches/adapters; no schema/data migration |
| success evidence | caller-specific integration suites, Forge Host/KernelSDK boundary tests, V2 build artifacts, server rejection contracts |

## M6 - compatibility cleanup

| Item | Plan |
|---|---|
| goal | retire obsolete execution surfaces only after telemetry/repository usage is gone |
| scope | deprecate/remove accidental V1 fallback from ordinary V2 paths, old direct legacy `.firmasm` exec/export aliases if Preview permits, obsolete dispatcher routes/helpers, stale docs |
| movement | legacy codecs remain versioned compatibility package/namespace; representative regressions move to compatibility and recipe education suites |
| compatibility | announce removals; preserve readers longer than executors/writers; never reinterpret old data |
| risk | high if external consumers depend on generic APIs; requires release decision |
| rollback | compatibility package/facade can be restored independently; canonical V2 artifacts unaffected |
| success evidence | full active + compatibility suites, package/API diff, documented deprecation window, no unclassified callers |

## Test classification and preservation

| Test/docs group | Classification | Destination |
|---|---|---|
| V1 parser/formatter and legacy `.firmasm` loader/migration | compatibility contract | M2 serialization compatibility suite |
| V1 validator/executor/`expect` | historical regression / legacy behavior contract | opt-in legacy suite; promote only current behavior |
| Core Boolean happy-path families | behavior contract | recipe parity tests |
| stepped root cause, continuation graph, overlap/tangency, rotated/conic, mixed continuation | historical regression + educational example | preserve permanently; reorganize under recipe/Surgery lessons |
| generic CIR and strategy labs | educational experimental evidence | FrictionLab, linked from textbook docs |
| exact diagnostics tied only to obsolete private implementation | obsolete implementation detail | remove only when equivalent invariant/rejection assertion exists |
| `docs/development/milestones/general/boolean-deferred.md` and milestone notes | historical record | retain; add forward links/context rather than rewriting outcomes |

## Documentation cleanup map

| Documents | Classification/action |
|---|---|
| `docs/development/history/firmament/preview2-reference/language-reference.md` | canonical/current; conflicting `.firmasm` sentence corrected in M1 |
| `Aetheris.Kernel.Firmament/README.md` | severely stale pre-M0 scaffold claim; update in M1 |
| `docs/development/milestones/general/firmament-overview.md`, `firmament-language-shape.md`, `firmament-build-workflow.md`, `firmament-demo.md`, `firmament-author-rules.md`, `firmament-selectors.md`, `firmament-placement-semantics.md` | historical V1 docs that often say “current” or “canonical”; add a shared historical banner in M2, retain content for compatibility |
| `docs/development/milestones/general/air-firmament-a1-firmament-fixture-corpus.md` | directionally correct but says V1 remains valid; update wording to explicit compatibility/serialization destination in M2 |
| `docs/development/milestones/general/build-test-policy-net10-and-legacy-v1.md` | current and consistent; retain |
| `docs/development/milestones/assembly/artifacts/m0/*`, assembly milestone docs | retain historical; link to current profile distinction |
| `fixtures/Compatibility/Firmasm/LegacyAssembly/*/README.md` | update to say legacy JSON compatibility fixture |
| `docs/development/milestones/general/boolean-deferred.md` | current truthful scope statement; retain and link to freeze policy |
| stepped/CIR recovery/FrictionLab Boolean docs | retain historical/educational, including failures and later fixes |

## Rollback doctrine shared by every phase

Each phase must:

- keep `dotnet restore Aetheris.slnx` and `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1` green;
- run active tests plus the relevant explicitly enabled legacy suite;
- compare existing canonical artifacts where byte stability is contractual and topology/semantic equivalence otherwise;
- preserve old facade/adapters until the new path proves parity;
- avoid combining namespace/package movement with behavior expansion;
- end with an independently revertible commit boundary.
