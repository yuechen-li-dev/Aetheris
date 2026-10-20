# AI author in Helios

The AI Author panel connects to OpenAI, Claude and Gemini through Leviathan.
Choose a provider, then a model. GPT-6.1 Sol remains the default. The server supplies
the model catalog and reports credential configuration separately for each provider.
A failed request never silently changes providers or models.

| Provider | Models | Server environment variable |
| --- | --- | --- |
| OpenAI | GPT-6 Astra, GPT-6.1 Sol, GPT-6 Luna | `OPENAI_API_KEY` |
| Claude | Opus 5.5, Sonnet 5.5, Haiku 5.5 | `ANTHROPIC_API_KEY` |
| Gemini | Gemini 4 Argon, Gemini 3.8 Flash | `GEMINI_API_KEY` |

The model IDs follow the [OpenAI catalog](https://developers.openai.com/api/docs/models),
[Claude catalog](https://platform.claude.com/docs/en/models/overview), and
[Gemini catalog](https://ai.google.dev/gemini-api/docs/models), checked October 20, 2026.
Account access and quota still depend on each provider. These choices use the
providers' APIs, independently of consumer chat subscriptions.

## Connect

Run Helios with its Leviathan backend and sign in. The account needs an active
Helios installation. Configure the selected provider's key in the server environment
before starting Leviathan. Only configure the providers you need; a missing key
disables Run for that provider without disabling the others. The panel reports
missing configuration explicitly. Never put keys in Firmament source or chat.
This integration uses the operator's server credentials; it does not implement
per-user key custody or consumer subscription sign-in. Requests consume the
selected provider's server account API usage.

The source workspace requires adjacent current Dominatus and Leviathan checkouts.
Leviathan's `DominatusSourceRoot` build property can select another explicit
Dominatus source location. Its old vendored snapshot is not the active runtime.
For a Docker build from Leviathan, use
`docker build --build-context dominatus=../Dominatus .`. The new Dominatus
conversation adapters must ship together with this integration; they are not yet a
published dependency version.

## Work with the agent

For specifications, catalog lookup, spreadsheet parameter proposals and draft
review ZIPs, see [Engineering references and review packages](engineering-assistance.md).

Changing provider or model starts a fresh conversation and discards any unapplied
candidate. The current project, engineering requirements, unsent prompt and
Version Control history remain. Switching utility tabs alone preserves the
conversation. This keeps provider-specific reasoning signatures with the model
that created them.

Describe the part or change and enter any engineering requirements. The agent
chooses its approach and can read project files, edit a separate candidate,
request the compiler schema/outline, build, and query measurements. It can write
Assert statements where the compiler supports them. An agent's proposed assertion
does not independently establish that its assumptions are correct.
Free-text requirements guide the agent; this pass does not automatically prove
their satisfaction. Executable assertions and measurement results provide the
checks that the compiler/kernel can actually perform.

The initial tools edit exact source occurrences. They can create new project
documents; they have no shell or arbitrary disk access. Measurements use the same
SDK/kernel authority as the UI. The model must use actual measurement evidence;
successful compilation does not prove manufacturability.

By default, successfully built changes apply automatically. Turn off **Apply
changes after a successful build** to review the candidate first. Each changed
file shows its previous and proposed source. A rejected candidate never replaces
the working project. If the human edits during a task, application rejects the
stale revision. If the normal workspace rebuild subsequently fails, the last valid
geometry remains and the source/draft failure is reported.

The task time budget defaults to five minutes and can be changed. **Stop agent**
cancels further provider/tool work and discards unapplied candidate changes. A
transaction that has started applying is allowed to finish; checkpoints provide
recovery. Token usage reports completed provider turns, not a billing guarantee
for failed or cancelled requests.

## Version Control

Open the separate **Version Control** utility tab to save or restore a project state.
AI Author stays focused on the conversation; switching tabs retains the conversation
and any running task. Recovery
checkpoints are also saved before an AI task and before restoring another state.
They capture the root document, all document strings in the open workspace, and
the current engineering requirements. Imported content already embedded in those
documents is preserved. This is not an independent capture of arbitrary external
filesystem assets.

Restoring copies the saved state into a new current checkpoint and retains later
checkpoints, so another state can be restored again. Invalid drafts can be saved
and restored; saving is distinct from compiling successfully. Restoring rebuilds
the source rather than pretending an old mesh proves the restored source valid.
Restoring also restores engineering requirements and clears stale agent context
and candidates. Save and restore controls wait for running agent tasks and project
operations to finish.

**These checkpoints live in this browser's IndexedDB.** Clearing browser storage
removes them. They are not cloud backups. Local mode currently uses one local
workspace history; signed-in projects have separate histories. Conversation
transcripts are session state and are not durable resumable agent tasks.

The existing cloud Save endpoint still persists a single source document. Helios
refuses that operation for a multi-file workspace instead of marking a partial
save as complete. Server-backed project manifests/checkpoint history, binary asset
pinning, checkpoint export/import, durable agent replay/resume, streamed assistant
text and local CLI-agent adapters remain follow-up work.
