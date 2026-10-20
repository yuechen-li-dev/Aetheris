# Helios engineering capabilities and Dominatus async actuation

Date: 2026-10-20

## Result

The first native capability pass works through real Helios uploads, authenticated
Leviathan endpoints, Microsoft Semantic Kernel schema generation/typed invocation,
and the existing compiler-backed candidate workflow. It adds practical reference
lookup, parts-catalog filtering, typed parameter proposals and draft review ZIPs.
No Semantic Kernel agent, planner or orchestration loop was introduced.

**Success for the bounded native pass.** Live supplier services and Microsoft 365
delivery are not implemented or claimed. Their remaining boundary is connected
account authorization/token custody plus concrete provider adapters, rather than
another change to the authoring or async execution model. No live LLM calls or
account model-access checks were made; the earlier offline qualification decision
remains in force.

## Ownership and async roles

| Layer | Responsibility |
| --- | --- |
| OptFlow Ai.Act / Ai.Await | Agent workflow dispatch, logical suspension and completion evidence |
| C# async / await | Implementation of HTTP, storage and plugin operations |
| Dominatus AsyncActuationHandler | Shared nonblocking operation contract; immutable invocation identity and cancellation |
| ActuatorHost | Existing policy admission; task ownership; owner-tick terminal delivery; in-flight bookkeeping; shutdown |
| Semantic Kernel | Native plugin metadata, nested DTO binding and function invocation |
| Leviathan | Account/installation admission, reference storage and exact registered function grant |
| Helios | Selected reference IDs, candidate state, source-revision checks, builds, review download and checkpoints |
| Aetheris | Source legality, compiled geometry, measurements and format-specific export admission |

An agent state issues Ai.Act and yields Ai.Await; its operation handler awaits I/O.
An ASP.NET request awaits that same handler directly after its own admission, without
inventing an AiWorld or polling ticks. A direct call does not silently execute
ActuatorHost policies. The per-request registered grant still bounds SK invocation.

Already-completed successful ValueTasks retain immediate completion semantics.
Pending operations return immediately. Their observed results cross a queue and
publish typed/untyped completion on the owning tick, once, in dispatch-ID order
within the observed batch. Continuations cannot access the world through the new
context. Cancellation wins over late success; removed agents receive no callbacks.
Dispose cancels and suppresses publication; DisposeAsync additionally awaits owned
observers. Shutdown still depends on cooperative cancellation; it cannot terminate
arbitrary managed code or reverse an external effect.

Tasks/continuations are not checkpoint data. Logical IDs and existing recorded
completion/replay evidence remain the runtime contract. No durable live-call resume
is claimed. The detailed convention is in Dominatus
docs/actuators/ASYNC-ACTUATION.md.

## Useful capability surface

- Reference import: TXT/Markdown lines, PDF pages, DOCX paragraphs, CSV records and
  XLSX worksheet cells. Original bytes and extracted data remain in the account's
  existing object store, with SHA-256 identity. Another account cannot read them.
- Native SK tools: search_references, read_reference, find_parts, bind_parameters
  and prepare_review. Function schemas come from SK metadata; there is no second
  independently maintained DTO schema or SK-controlled tool loop.
- Parts filtering returns source rows and cell addresses. It does not certify fit,
  infer units, fetch live stock/prices or place orders.
- Parameter bindings require explicit units and finite literal numeric cells.
  Unknown columns, duplicate/invalid parameter names, unsupported units and formulas
  reject. Cached spreadsheet formula values are not recalculated evidence.
- Review preparation validates reference locators and captures source hashes,
  requirements, exact cited evidence and open issues. It remains an unapproved draft.
- export_review requires a current successful candidate build and a matching review
  manifest. It packages actual admitted SDK exports, source and review.json.
  Export failure or stale source produces no new package; existing export admission
  remains authoritative, including missing drawing requirements.

Engineering references live under Files, keeping Version Control and AI Author
separate. A checkpoint captures selected reference IDs; server documents remain
server-owned. Import/task/restore actions exclude one another. Upload cancellation
belongs to the component lifetime. Changing attachments invalidates stale agent
context and unapplied candidates.

## Qualification

| Check | Result |
| --- | --- |
| Dominatus Release.slnx | 1,409 passed, 8 live-payment cases skipped, 18 target suites |
| Dominatus Llm.OptFlow.Tests | 708 passed on net8.0 and 708 on net10.0 |
| Dominatus Actuators.SemanticKernel.Tests | 70 passed on net8.0 and 70 on net10.0 |
| Leviathan full solution | 64 passed |
| Helios frontend build/full tests | Build passed; 123 tests in 28 files passed |
| New source witness through CLI | validate/build succeeded; STEP analysis confirmed bounds and topology |
| Actual browser | Uploaded requirements/catalog; attached both; built witness; detached catalog; restored checkpoint and both selections |
| Review ZIP production packager | Bundled actual CLI STEP and source; round-trip verified unchanged STEP hash |

The fixture is fixtures/Regression/EngineeringEvidence. Its requirements and catalog
are explicitly synthetic. The built witness has bounds X/Y [-28,28] mm and Z [0,6]
mm, one body, seven faces (six planes and one cylinder), 15 edges and 12 vertices.
No tolerances or approval were invented. The STEP bytes retained in the review ZIP
have SHA-256 da56f7401354c092cb7079cc749c92a3724abe16c28081d97bffd3158968914f.

Detailed logs and the ZIP remain under ignored artifacts/local: sk-dominatus-full.log,
sk-llm-full.log, sk-adapter-tests.log, sk-leviathan-full.log, sk-helios-tests.log,
sk-plate-validate.json, sk-plate-build.json, sk-plate-analyze.json,
sk-review-artifact.log, engineering-review.zip and author/engineering-references.png.

The ZIP artifact is an offline packaging witness, not a live model-authored review.
Gateway tests use synthetic provider HTTP responses through the real adapters.
Document-format tests generate real PDF, DOCX and XLSX bytes in memory. The full
Aetheris kernel suite was not rerun: no compiler/kernel/exporter C# changed. The
actual CLI and Web SDK paths were used for the new source witness.

## Repairs found during qualification

- Nested JSON previously failed before SK could perform its typed binding. Cloned
  JsonElements now survive the input document lifetime and bind through SK itself.
- Structured SK results previously depended on ToString; DTOs now serialize as JSON.
- The archive library's typed-array identity checks could treat cross-realm source
  buffers as directories. The production packager normalizes views; tests assert
  extracted source text, export bytes, citations and hashes.
- Files used a full-height project explorer, burying references below blank space.
  The explorer now takes its content height.
- SK's transitive Pkcs 9.0.0 dependency triggered vulnerability diagnostics.
  The owner pins patched 9.0.21. Dependency guards now inspect actual XML references
  and Graph name segments, avoiding the false match in Cryptography.

## Remaining explicit boundaries

This is not a repository-wide async migration. SemanticKernelActuationHandler and
LlmTextActuationHandler use the new contract. HTTP, process, HomeAssistant,
streaming and synchronous LLM-scoring adapters still have legacy blocking paths.
Streaming requires owner-thread progress delivery; model-backed scoring should
obtain evidence asynchronously before deterministic scoring. Those migrations have
a defined destination now; no additional async model is needed.

PDF OCR/layout interpretation, Excel formula evaluation, XLSX named-table range
selection, vector search, external supplier adapters, Graph/OAuth document access,
SharePoint/OneDrive publication and external review synchronization remain outside
this first native pass. The document library and review manifests are evidence
surfaces, not automatic standards compliance or manufacturing certification.

Public workflow: [Engineering assistance](../public/firmament/engineering-assistance.md).
