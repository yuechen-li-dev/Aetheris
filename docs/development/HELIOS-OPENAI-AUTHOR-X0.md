# Helios OpenAI author: first-provider integration

Date: 2026-10-20 (America/Los_Angeles)

## Result and qualification boundary

The first provider path is implemented against the maintained Dominatus source
checkout, replacing Leviathan's active Dominatus 0.3 package references. The model
choices are GPT-6 Astra (`gpt-6-astra`), GPT-6.1 Sol (`gpt-6.1-sol`, default) and
GPT-6 Luna (`gpt-6-luna`). The author confirmed the Luna spelling after checking
the official model catalog.

**Success for the bounded offline integration.** The user explicitly requested
offline qualification. No paid/live inference, account model-access verification,
or generated-part quality claim is made. Authenticated HTTP tests use a synthetic
provider transport through the real endpoint/adapter; the UI reports missing
credentials and disables Run rather than fabricating a response.

This is not completion of the entire durable-agent/cloud-checkpoint proposal.
Local recovery works; server-backed multi-file history remains an explicit next
boundary.

## Ownership

| Owner | Responsibility |
| --- | --- |
| Dominatus.Llm.OptFlow | New explicit conversation/provider contract; OpenAI Responses function calls; original provider items and usage; incomplete/error handling |
| Dominatus.Llm.Context | Current workspace manifest, content hashes and engineering requirements assembled into a budgeted packet; required-context omission rejects |
| Leviathan | Authenticated, CSRF-protected gateway, active Helios installation check, fixed provider endpoint, server credential, registered tool schemas |
| Helios | Explicit tool loop, candidate source state, cancellation/deadline, working-revision comparison, review/automatic apply, local checkpoints |
| Aetheris public SDK/compiler | Profile/domain/syntax admission, actual candidate builds and measurement queries; no separate UI validator |

The conversation adapter is additive. Existing `ILlmClient`, text/decision helpers
and cassette formats are unchanged. Parameterless HttpClient transport construction
is preserved; hosts can additionally inject an owned/factory HttpClient.
The wire-level integration test exposed an existing content-header defect:
`StringContent`'s default text/plain survived a declared application/json header
because adding a second single-value Content-Type silently failed. The shared
transport now replaces declared content headers. This was reproduced as a failing
gateway test before repair and is covered in both the library and host tests.
Opaque reasoning items are retained for stateless Responses continuation.
`store: false` is explicit. A provider response cannot execute tools itself.

The old vendored RustSimulator sample script remains linked in Leviathan as a
legacy sample, but its active Core/OptFlow/Ariadne/LLM runtime comes from current
Dominatus. No broad sample/game migration was attempted. Cross-version restore
of archived 0.3 agent checkpoint files was not qualified by these tests.

## Author workflow

The agent chooses its engineering strategy. It can read project files, replace a
unique exact source occurrence, create documents, inspect schema/outline, build
the candidate and measure its built model. There is no per-edit permission prompt
or arbitrary edit-count limit. It may author existing Firmament Assert statements;
those are proposed checks rather than proof of the underlying assumptions.

Tool edits affect a separate project copy and a separate SDK compiler worker.
An invalid build is recorded as rejection. Changes must build before application,
and application compares the captured working-source revision. Source changes by
the human invalidate application. Changing engineering requirements clears an
outstanding candidate. A normal workspace rebuild remains authoritative after
application and retains the last valid geometry on failure.

The default is automatic application after a successful build. Review mode shows
before/after source per changed document and requires Apply candidate. Model
switching is explicit; provider failure never selects another model silently.
Task budget defaults to five minutes, adjustable in the panel. Cancellation stops
HTTP work and terminates the isolated compiler worker. An applying transaction
finishes rather than reporting that already-applied source was cancelled.

The panel remains mounted while another utility tab is inspected. Closing the
workspace cancels pending agent work. Conversation state is explicit but currently
in memory; it is not a durable Dominatus HFSM task or a resumable server session.

## Local checkpoint contract

IndexedDB checkpoints capture all open workspace document strings, the root and
the panel's engineering requirements. They are immutable entries. Recovery is
saved before an AI task and before restore. Restoring creates a new state with a
restored-from identity and retains later states; it never deletes the forward
history. Invalid drafts can be stored and restored independently of build status.

The real browser witness saved the original 50 x 40 x 8 mm mounting plate with
its hole, changed it to an 80 x 40 x 8 mm plain box and changed its requirements,
then restored the original source/requirements and the later state. Each restore
ran the normal compiler/build path. History also survived a fresh editor reload.
Switching Inspector/AI Author retained the restored requirements.

Local mode has one local workspace history; authenticated project IDs separate
their histories. Checkpoints are browser-local, not cloud backups. Embedded import
text is retained with source; arbitrary external/binary resources are not newly
captured by this implementation.

The existing cloud Save endpoint accepts one source string. Once AI editing can
create module files, calling that a complete save would lose intent. Helios now
refuses cloud save for a multi-file workspace or an active non-root document.
The next persistence task is a versioned complete project manifest in Leviathan
and compatible open/save/publication/fork contracts, rather than another parallel
save store.

## Evidence

| Lane | Result |
| --- | --- |
| Dominatus.Release.slnx, serial | 1,401 passed across 18 target suites; 8 external/live payment smoke cases skipped |
| Dominatus.Llm.OptFlow.Tests | 694 passed on net8.0 and 694 on net10.0 |
| Dominatus.Llm.Context.Tests | 70 passed on net8.0 and 70 on net10.0 |
| Leviathan full solution | 54 passed, including authenticated provider fixtures and existing lifecycle/persistence tests |
| Helios build:fast | Passed; existing bundle-size advisory |
| Helios complete unit suite | 113 passed in 25 files |
| Browser | Model picker, honest unconfigured state, source/requirements rollback and forward restore, reload persistence and utility-tab retention |

The new tests cover preserved provider items, all three model IDs, rejected
incomplete/unknown/duplicate/malformed tool calls, credential-safe errors, failed
candidate admission, stale measurement refusal, isolated edits, cancellation of an
in-flight compiler, multi-file checkpoints and refusal of partial cloud saves.

No Aetheris compiler/kernel implementation changed in this pass. Its full kernel
suite was not rerun for this provider/UI integration; this report does not reuse
the previous sketcher pass's kernel totals as new evidence.

Detailed logs and screenshot remain ignored local artifacts:

- `artifacts/local/author-dominatus-release-tests.log`
- `artifacts/local/author-dominatus-llm-tests.log`
- `artifacts/local/author-dominatus-context-tests.log`
- `artifacts/local/author-content-type-before.log` (intentional failing wire witness before repair)
- `artifacts/local/author-server-tests.log`
- `artifacts/local/author-helios-build.log`
- `artifacts/local/author-helios-tests.log`
- `artifacts/local/author/openai-panel-checkpoints.png`

## Deployment and remaining boundaries

Leviathan now requires the matching Dominatus source changes beside its checkout,
or an explicit `DominatusSourceRoot`. There is a clear build error for an absent
conversation API. Its Dockerfile accepts a named Dominatus build context:
`docker build --build-context dominatus=../Dominatus .`. The image definition was
updated but no Docker image build/deployment was qualified here. The eventual
published package release should replace this coordinated source dependency.

This first path uses the operator's server `OPENAI_API_KEY`, not per-user BYOK or
ChatGPT-plan authentication. It is not qualification for an unrestricted public
shared-key service. Existing auth and installation boundaries remain required.
Requests are nonstreaming, with 8,192 maximum output tokens per provider turn, a
1,000,000-character gateway request budget and a 2,000,000-character candidate
source budget. Exceeding a budget fails explicitly; no source/context is silently
truncated. Reported token counts cover completed turns and are not a billing
guarantee for cancellation or failed requests.

Still pending: live model access and engineering dogfood, server project manifests
and history, export/import of checkpoints, durable conversation cassettes/task
resume, streamed assistant output, per-user credential custody and other provider
or local CLI-agent adapters. No deterministic live model replay is claimed.

Public workflow: [AI author guide](../public/firmament/ai-author.md).

## Version Control tab follow-up

Checkpoint controls now live in a separate Version Control utility tab. The shared
workspace owner retains one IndexedDB checkpoint store; moving its code out of
the author module leaves existing browser histories intact. AI Author retains
conversation state when switching tabs and still saves pre-task and applied-change
recovery checkpoints. Restore rebuilds the full source workspace, restores its
engineering requirements, and clears stale agent context and candidates. Save and
restore wait for agent/project operations to finish.

Qualification: Helios frontend build and all 116 tests in 26 files passed.
Tests cover shell tab separation, normal rebuild on restore, all module sources,
requirements, invalid drafts, retained later states, conversation drafts, automatic
agent checkpoints, and exclusion of concurrent restore during an agent task.
Browser qualification saved the mounting plate, built a temporary 80 × 40 × 8 mm
box, restored the original plate with its hole, and confirmed the normal build
finished without diagnostics. Existing histories remained readable. This is a
frontend-only follow-up; no .NET code changed and no .NET hosts were launched.
Evidence: `artifacts/local/version-control-build.log`,
`artifacts/local/version-control-tests.log`, and
`artifacts/local/author/version-control-tab.png`.
