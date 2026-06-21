# INLINE-STEP-A0 — inline STEP and strangler-fig semantic migration architecture audit

## 1. Purpose

This audit evaluates how Firmament could support an inline/imported STEP body that remains raw geometry while semantic PMI, recognition labels, and eventually semantic replacement features are added incrementally. It is an architecture milestone only: it does not implement inline STEP syntax, parser behavior, STEP import/export behavior, Firmament lowering, BRep/materializer behavior, or product behavior.

The motivating migration pattern is intentionally smaller than full STEP-to-Firmament decompilation. A STEP body should first be carried and re-exported as an imported body, then annotated, then partially recognized, and only later have selected regions replaced with semantic Firmament features after verification.

## 2. Existing STEP import/export inventory

Commands used for inventory included:

```bash
git grep -n -i "Step242Importer\|Step242Exporter\|ExportBody\|Import.*Step\|STEP.*Import\|AP242\|ADVANCED_FACE\|VERTEX_POINT" -- .
git grep -n -i "entity id\|entityId\|step entity\|source entity\|FaceId\|EdgeId\|VertexId\|topology id\|provenance\|originating" -- Aetheris.Kernel.Core Aetheris.Kernel.Firmament Aetheris.*.Tests docs
git grep -n -i "semanticPmi\|PMI\|ShapeAspect\|ShapeDimensionRepresentation\|PROPERTY_DEFINITION\|datum\|diameter" -- .
git grep -n -i "reimport\|roundtrip\|round-trip\|fixture.*step\|analyze volume" -- Aetheris.*.Tests docs fixtures
```

Key findings:

| Area | Current capability | Evidence |
| --- | --- | --- |
| STEP import entry point | `Step242Importer.ImportBody(string)` imports through `ImportOrchestrator.CreateDefault()` and returns a `KernelResult<BrepBody>`. | `Aetheris.Kernel.Core/Step242/Step242Importer.cs` |
| Exact BRep import lane | The exact lane rejects missing rigid roots and assembly-like multi-root exact BReps, then reads one `MANIFOLD_SOLID_BREP` or `BREP_WITH_VOIDS` root. | `Aetheris.Kernel.Core/Step242/Step242Importer.cs` |
| Supported topology entities | Import reads `ADVANCED_FACE`, `FACE_BOUND`/`FACE_OUTER_BOUND`, `EDGE_LOOP`, `ORIENTED_EDGE`, `EDGE_CURVE`, and `VERTEX_POINT` through helper decoders. | `Aetheris.Kernel.Core/Step242/Step242Importer.cs` |
| Export entry point | `Step242Exporter.ExportBody(BrepBody, Step242ExportOptions?)` delegates to an overload that can also receive semantic PMI. | `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` |
| Export root planning | Export supports a single body and chooses `MANIFOLD_SOLID_BREP` or `BREP_WITH_VOIDS` through `StepSolidRootExportPlanner`. | `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` |
| Exported topology | Export emits generated STEP entities for vertices, edges, loops, bounds, faces, closed shells, product context, representation context, and shape representation. | `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` |
| Semantic PMI | `Step242SemanticPmiHole`, `Step242SemanticPmiDatum`, and `Step242SemanticPmiNote` exist, and the exporter emits `SHAPE_ASPECT`, `PROPERTY_DEFINITION`, and `SHAPE_DIMENSION_REPRESENTATION`-style semantic PMI records. | `Aetheris.Kernel.Core/Step242/Step242SemanticPmi.cs`, `Aetheris.Kernel.Core/Step242/Step242Exporter.cs` |
| CLI roundtrip path | The CLI `canon` command reads STEP, imports it, exports it, and writes canonical AP242. | `Aetheris.CLI/CliRunner.cs` |
| Roundtrip tests | `Canon_Command_RoundTrips_Supported_Step_Through_Importer_Exporter_Path` verifies an existing supported STEP fixture can pass through importer/exporter and re-import. | `Aetheris.CLI.Tests/CliBaselineTests.cs` |
| V2 STEP pipeline tests | Firmament V2 tests build fixtures, assert `ADVANCED_FACE`/`VERTEX_POINT`, re-import exported STEP, and verify volume/topology evidence. | `Aetheris.CLI.Tests/FirmamentV2*StepPipelineTests.cs` |
| AP242 PMI tests | STEP-V2-X7 tests assert semantic PMI markers and verify re-import/volume; graphical PMI is explicitly absent. | `Aetheris.CLI.Tests/FirmamentV2SemanticPmiStepPipelineTests.cs` |

Inventory verdict: Aetheris already has a real AP242 subset import/export path, a CLI canonicalization path, semantic PMI emission for Firmament-derived features, and pipeline tests that re-import emitted STEP. The current path is a canonicalizing BRep roundtrip, not a byte-preserving imported STEP wrapper.

## 3. Imported topology identity

Current identity behavior:

- `Step242Importer` reads STEP entity numbers while decoding, including `ADVANCED_FACE` entity IDs and edge/vertex references.
- During import it builds a local `faceEntityToFaceId` map, but that map is local to `ImportExactBrepCore` and is not stored on `BrepBody`.
- Edges and vertices are similarly mapped from STEP entity IDs to internal `EdgeId` / `VertexId` during import, but the maps are local importer implementation details.
- `BrepBody` currently contains topology, geometry, bindings, vertex points, and shell representation, but no imported-source provenance or source STEP entity identity map was found.
- Internal topology IDs are stable only inside the constructed in-memory `TopologyModel`; `VertexId`, `EdgeId`, and `FaceId` are described as stable in-memory topology IDs, not stable source IDs.
- Export regenerates STEP entity IDs in writer order. There is no evidence that exporter attempts to preserve original source entity numbers such as `#847`.
- Tests can refer to internal analyzer IDs such as `faceId`, `edgeId`, or `vertexId`, but no current test refers to a face/edge/vertex by imported source STEP entity ID.
- `Step242Exporter.ExportBody(body, semanticPmi)` can emit PMI records, but current semantic PMI targets are string metadata on PMI records rather than typed references to imported topology entities.

Answers:

| Question | Answer |
| --- | --- |
| Does `Step242Importer` preserve AP242 entity numbers like `#123`? | It reads them during decoding, but does not preserve them on the returned `BrepBody`. |
| Does imported `BrepBody` preserve source STEP face/edge/vertex entity IDs? | No durable source-ID metadata was found. |
| Are topology objects assigned stable internal IDs? | Yes, internal in-memory IDs exist for vertices, edges, coedges, loops, faces, shells, and bodies. |
| Are imported topology handles stable after re-export? | No. Re-export regenerates STEP entities; source IDs are not preserved as handles. |
| Is there a mapping from STEP entity ID to `BrepFace` / `BrepEdge` / `BrepVertex`? | Only transient importer dictionaries; no public/durable imported topology identity map was found. |
| Can tests currently refer to imported topology by source ID? | Not with current public model support. Analyzer tests use internal IDs. |
| Can AP242 exporter preserve or emit references to imported source topology? | Not currently. It can emit semantic PMI strings, but not typed topology references to imported source entities. |

Smallest metadata addition recommended later: introduce an optional import provenance object outside the core topology IDs, for example `ImportedStepProvenance`, containing file path, content hash, source schema/header summary, unit assumptions, and maps such as `StepEntityRef("#847") -> FaceId`, `StepEntityRef("#300") -> EdgeId`, and reverse maps. This should be attached either to a wrapper model (`ImportedStepBody`) or an optional `BrepBody` metadata envelope, not by redefining `FaceId`/`EdgeId`/`VertexId` semantics.

## 4. Pure STEP wrapper/re-export feasibility

Feasible today for supported single-part AP242 exact BRep subset inputs:

- The CLI `canon` command already imports a STEP file and immediately exports it through `Step242Exporter`.
- Tests verify at least one supported fixture can roundtrip through this path and be imported again.
- Firmament V2 STEP pipeline tests repeatedly export AP242, re-import, and verify volume/topology evidence.

Not feasible today as an unchanged wrapper:

- The current path canonicalizes through `BrepBody` and `Step242TextWriter`; it does not preserve original entity numbering, ordering, comments, header values beyond exporter defaults, or unsupported AP242 payload.
- Assembly-like multi-root exact BRep input is explicitly rejected by the single-part import path with route guidance.
- Import length units are not yet preserved in at least one analyzer diagnostic path; current exporter emits millimetre units.
- The importer/exporter subset is strong enough for known supported test fixtures and some NIST/CTC-style single rigid-root paths, but not a guarantee for arbitrary AP242 files, broad healing, or all commercial STEP variants.

Stage-0 inline STEP wrapper implication:

```text
Firmament wrapper around imported STEP body -> export AP242 canonicalized body
```

This is plausible as INLINE-STEP-X1 if scoped as “supported exact BRep canonical roundtrip,” not as “byte-identical STEP preservation.” X1 should record the distinction in diagnostics and provenance.

## 5. Semantic PMI on imported topology

Existing STEP-V2-X7 context:

- Firmament V2 can emit semantic PMI for authored semantic features and datum aliases.
- Exported PMI is semantic AP242-style metadata, not graphical PMI; tests assert absence of `DRAUGHTING_CALLOUT` and `ANNOTATION_PLANE`.
- PMI records currently use feature IDs and string target fields; they do not require the target to be a topology entity created by a semantic feature.

Feasibility:

- Conceptually, the same semantic PMI model can target imported topology if target references become typed and resolvable.
- The smallest useful target reference is probably not an internal `FaceId` alone, because users need stable references in source. A source-facing reference like `step.face("#123")` should resolve through `ImportedStepProvenance` to an internal `FaceId`, with a fallback diagnostic if missing.
- Geometric signatures can help detect drift, but should be verification evidence rather than the primary authored handle for X2. A signature-only target would be fragile and hard to review.
- AP242 exporter support would need a typed PMI target model that can express “target imported face/entity” and, later, emit AP242 shape-aspect relationships that are more explicit than the current descriptive strings.

Smallest path for “existing STEP + semantic PMI -> enriched AP242”:

1. Import STEP into an `ImportedStepBody` wrapper that carries `BrepBody` plus source entity maps.
2. Resolve semantic PMI targets such as source STEP face IDs to internal topology handles.
3. Emit canonical AP242 geometry via current exporter.
4. Emit semantic PMI records whose target descriptions include both user-facing source references and resolved internal handles.
5. Add exporter support for stronger AP242 topology association only after target identity is durable and tested.

This should precede replacement work. Semantic PMI attachment exercises source identity, provenance diagnostics, and AP242 enrichment without requiring topology surgery.

## 6. Inline STEP wrapper architecture

Recommended conceptual architecture:

```text
Firmament source declaration
  inline/imported STEP declaration
    -> ImportedStepBody symbol
       -> BrepBody body
       -> ImportedStepProvenance provenance
       -> ImportedTopologyIdentityMap identityMap
       -> semantic annotations / PMI overlays
       -> recognized regions
       -> replacement plan overlays
```

Design recommendations:

- Treat inline STEP as a source-level imported body declaration, not as parser-level decompilation into Firmament primitives.
- The declaration should materialize to an `ImportedStepBody` envelope. That envelope owns the imported `BrepBody`; it should not pretend the imported body was authored by Firmament semantic primitives.
- Units should be explicit in provenance. If import-unit preservation is incomplete, X1 should record the source header/unit evidence and the effective normalized unit used by Aetheris, with a warning when assumptions are made.
- Store source path, normalized full path, content hash, STEP schema/header summary, import timestamp/build context if appropriate, and importer diagnostics.
- Store source AP242 entity IDs as source references (`#123`) mapped to internal topology IDs, including reverse maps and entity kind.
- `aetheris build` should re-export a canonicalized AP242 representation of the body for X1, not rematerialize from semantic Firmament features.
- Semantic PMI declarations should attach to `ImportedStepBody` topology through resolvable imported topology references.
- Later semantic replacement features should be overlays that consume a declared imported region and emit a replacement plan; they should not mutate original provenance or erase the ability to audit what was replaced.

Syntax should remain unsettled in A0. The eventual syntax may resemble `inline step importedPart from "./part.step"`, but this audit should not lock grammar or AST shape.

## 7. Strangler-fig semantic migration model

Recommended hybrid model:

```text
ImportedStepBody
  original imported BrepBody
  source file/hash/provenance
  topology identity map
  semantic annotations
  recognized regions
  replaced regions
  residual body/accounting
```

Stages:

| Stage | Meaning | Output |
| --- | --- | --- |
| Wrapper | STEP body is carried as imported BRep with provenance. | Canonical AP242 roundtrip and import diagnostics. |
| Annotation | Semantic PMI and labels attach to imported topology. | Enriched AP242 metadata without geometry replacement. |
| Recognition | Regions of topology are identified as likely features. | Candidate regions with evidence, confidence, and rejection reasons. |
| Replacement | A user-selected or policy-selected region is suppressed/replaced by a semantic Firmament feature after verification. | Hybrid body plan with replacement feature plus residual imported body. |
| Residual | Unreplaced imported topology remains traceable. | Progress metrics and unresolved topology inventory. |

What “replace” should mean:

- A replacement is not a text edit to the original STEP file and not an assertion that the whole body has been decompiled.
- It is a controlled hybrid operation: a declared imported topology region is claimed by a semantic feature, checked against the original region, and then omitted/suppressed from the residual representation when the hybrid body is emitted.
- The original imported body remains available as audit evidence. Replaced regions should be recorded as overlays with source entity references, semantic feature IDs, verification status, and tolerances.
- For early implementation, replacement should be restricted to bounded patterns where residual handling is simple and verifiable, such as a through-hole in a box-like host. Broad topology surgery should remain a non-goal.

Residual strategy options:

1. **Overlay-only accounting**: mark faces/edges as replaced but still export original geometry. This is safest for X3 labels but is not true replacement.
2. **Suppress-and-rebuild bounded region**: omit selected imported topology and add semantic feature-generated topology if residual closure can be proven. This is the likely first real replacement path.
3. **Full residual Boolean surgery**: subtract/patch arbitrary selected topology from the residual and union/compose the semantic feature. This is powerful but too broad for early milestones.

Recommended replacement semantics for X4: suppress-and-rebuild only for one bounded feature family with a strict verifier. If region overlap, residual invalidity, or boundary mismatch is detected, the replacement must remain planned/unverified and export should fall back to the unreplaced body or fail explicitly depending on command mode.

Progress metrics:

- original face count;
- replaced face count;
- residual face count;
- recognized feature count;
- unresolved topology count;
- replacement verification status;
- source entity coverage percentage;
- PMI target resolution count and unresolved target count.

## 8. Replacement verification model

A semantic replacement should be evidence-driven. It must not merely assert that selected faces are a hole, slot, pocket, fillet, chamfer, or datum feature.

Recommended checks:

- Selected topology surface families match the expected feature:
  - cylindrical/conical/planar stack for shaft, counterbore, countersink, blind, and stepped holes;
  - cylindrical/planar patterns for fillet/chamfer-like edge finishes where applicable;
  - planar/cylindrical patterns for pockets and slots.
- Generated semantic feature geometry matches the selected original region within tolerance.
- Volume delta matches expected feature volume or bounded region volume within tolerance.
- Boundary loops align within tolerance and have compatible orientation.
- Face adjacency matches the expected pattern for the feature family.
- Replacement does not overlap already replaced or reserved residual topology.
- Residual body remains valid, closed where required, and exportable.
- Re-exported hybrid STEP re-imports and matches expected volume/topology metrics.

Recommended verification states:

| State | Meaning |
| --- | --- |
| `recognized` | A candidate region has evidence but no user-approved replacement plan. |
| `replacement-planned` | A semantic feature is associated with a selected imported region, but verification has not passed. |
| `replacement-verified` | Geometry, topology, adjacency, and tolerance checks passed for the selected region. |
| `residual-emitted` | The hybrid residual plus semantic replacement was emitted. |
| `hybrid-step-verified` | The emitted hybrid AP242 re-imported and passed volume/topology/PMI checks. |

The verifier should return structured diagnostics, not booleans only. When multiple bounded interpretations compete, use `JudgmentEngine` so admissibility, scoring, tie-breaking, and rejection reasons are explicit. Do not use it for deterministic reference resolution or simple ID lookup.

## 9. Relationship to FeatureWork

The analogy is direct:

```text
FeatureWork / strangler refactoring:
  legacy code remains while new code replaces one feature at a time.

Inline STEP:
  imported BRep remains while semantic Firmament features replace one topology region at a time.
```

This makes STEP decompilation incremental, reviewable, reversible, and testable. The original STEP body is the legacy system. Semantic Firmament features are the new implementation. The migration should show progress through coverage metrics and verification evidence, not through broad one-shot conversion claims.

## 10. Recommended inline STEP roadmap

Recommended sequence:

```text
INLINE-STEP-X1:
  import STEP as inline body wrapper and re-export AP242 through the existing canonical importer/exporter path.

INLINE-STEP-X2:
  attach semantic PMI to imported STEP face/feature references and export enriched AP242.

INLINE-STEP-X3:
  add semantic labels / recognized regions over imported topology, with evidence and rejection reasons, but no geometry replacement.

INLINE-STEP-X4:
  implement first semantic replacement for one simple through-hole family with strict verification.

INLINE-STEP-X5:
  add residual body accounting, progress metrics, and hybrid STEP verification reports.
```

X1 and X2 should come before replacement. They force the project to solve provenance, source-ID resolution, unit diagnostics, imported-topology targeting, and AP242 enrichment before adding topology surgery.

Recommended INLINE-STEP-X1 acceptance criteria:

- One source declaration or command path imports a supported STEP file as an imported body wrapper.
- Provenance includes source path, content hash, schema/header evidence, effective units, and import diagnostics.
- The body re-exports as canonical AP242 through `Step242Exporter`.
- The output re-imports successfully.
- Diagnostics explicitly state that the output is canonicalized, not byte-identical and not source-entity-ID-preserving.
- No semantic replacement is attempted.

## 11. Risks and open questions

Risks:

- Source STEP entity IDs are stable within the imported file but are not guaranteed to survive arbitrary external re-export.
- Current importer maps source IDs only transiently; retrofitting provenance after import will require careful API design.
- Current exporter regenerates entity numbers, so AP242 PMI topology references need an explicit source-to-output mapping if strong topology association is required.
- Unit preservation appears incomplete; inline STEP must not silently reinterpret non-millimetre files.
- Assembly-like STEP and broad commercial AP242 variants exceed the current single-part exact BRep path.
- Replacement can easily become generic topology surgery; early milestones must stay bounded.
- A hybrid body may contain both imported and Firmament-generated topology, requiring clear diagnostics for ownership, overlap, and residual validity.

Open questions:

- Should imported topology provenance live directly on `BrepBody`, in a separate `ImportedStepBody`, or in a higher-level build artifact graph?
- What AP242 construct should be used for durable PMI association to imported topology once source IDs resolve to newly emitted output entity IDs?
- Should X1 support only exact BRep roots or also tessellated import lanes?
- How should file-relative paths and content hashes be represented for reproducible builds?
- Should unresolved `step.face("#123")` targets be hard errors or warnings in annotation-only mode?
- What is the first replacement feature: plain through-hole, counterbore hole, chamfer, or slot? A plain through-hole has the best bounded verification profile.

## 12. Non-goals

- No full automatic decompiler.
- No arbitrary STEP-to-Firmament conversion.
- No generic topology surgery in A0.
- No implementation in A0.
- No parser syntax lock-in.
- No graphical PMI.
- No broad STEP healing.
- No guarantee that source STEP entity IDs are stable across arbitrary external re-export.
- No change to STEP import/export behavior in this milestone.
- No change to Firmament lowering, BRep/materializer behavior, or product behavior in this milestone.
