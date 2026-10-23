# AGENTS.md

- Use Aetheris.CLI to inspect and validate parts/assemblies during development as ground-truth inspection tool. If `aetheris` is not available on PATH, build/run it via dotnet (for example `dotnet run --project Aetheris.CLI -- --help` and corresponding subcommand help) before making assumptions about current capabilities.

- Update documentation opportunistically during development and surface any inconsistencies or friction immediately; do not defer doc fixes, as the system is evolving rapidly and drift must be corrected in-place.

- Follow the repository content-location policy before creating documentation, fixtures, samples, demos, or test-support assets. Current user documentation belongs only under `docs/public/`; Firmament-family test source belongs under `fixtures/`. Generated output must default to ignored `artifacts/local/` and follow `docs/development/GENERATED-ARTIFACT-POLICY.md`. Do not introduce a new top-level content bucket without an explicit architectural reason.

## Documentation rules

- **Read before writing Firmament.** `docs/public/learn/` and `docs/public/reference/language/` describe the language; the compiler (`aetheris validate --json`) is the final authority.
- **Public docs describe behavior, not milestones.** Do not put milestone codes (X0, M2, SURF-X1a), verdicts, test counts or links to development reports in `docs/public`. Say what works, what is rejected, and how to use it.
- **Public guide shape.** Title; one paragraph on what it is for; a complete, buildable example (prefer linking or quoting a file under `fixtures/`); fields and rules; limits ("rejects", "not supported"); links to related guides.
- **Lessons are fixture-backed.** Every example in `docs/public/learn/` is a file in `fixtures/Canonical/Learn/` listed in `PublicDocumentationQualificationTests.LearnLessonExamplesBuildThroughRealCli`. Change both together.
- **Work reports** go in `docs/development/reports/<yyyy-mm>/`, short: what changed, evidence, verdict. Not in `docs/release/`, which holds release notes and package readmes only.
- **History.** When an era of reports closes, summarize it in `docs/development/history/eras/`, add lasting decisions to `history/DECISIONS.md`, list the originals in `history/ORIGINALS.md`, and delete them (git keeps them). Before deleting any doc, `git grep` its file name outside Markdown: tests, scripts and module descriptors read some docs by path.
- **Experimental work** (humanoid, garments, reconstruction) is documented under `docs/public/experimental/` with an experimental banner, never presented as product.

- Aetheris introduce utility scoring as a first class concept within its JudgmentEngine to use when appropriate.
  - When a subsystem must select among multiple bounded strategies, prefer JudgmentEngine so admissibility, scoring, tie-breaking, and rejection reasons are explicit.
  - Do not use JudgmentEngine for simple deterministic transformations, ordinary enum dispatch, null checks, or reductions where no competing interpretation exists.

## Convergence rule

Every substantial task must end in exactly one of three states:

1. **Success**  
   The intended capability works in the real path and the real motivating case materially improves.

2. **Meaningful progression**  
   The capability is not complete, but one genuine blocker is removed and the next blocker is isolated with evidence.

3. **Honest stop**  
   Further work would require overbroad scope expansion, excessive debt, brittle patching, or tangled logic. Stop and report the reason with concrete evidence.

Do not continue producing patches once the work stops converging.

Do not confuse activity with progress.
A failed attempt is only acceptable if it leaves behind a narrower problem, stronger evidence, or a justified stop.

Any partial work must leave the codebase in a cleaner, more legible, and more diagnosable state than before.

## Test execution requirement

Do not skip .NET build/test validation just because `dotnet` is missing from the current environment.

If the required .NET SDK/runtime is not installed:
1. detect that explicitly,
2. install the required .NET version for this repo,
3. verify `dotnet` is available on PATH,
4. then run the full build/test commands.

Do not report .NET tests as “not run due to missing dotnet” unless installation is impossible in the current environment, and if so, explain exactly why.

## Test lanes

Iterate with the fast lane and gate on the full one:

```
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

Never report a change as verified on the fast lane alone - it omits the corpus-driven tests,
which are the ones that catch interchange regressions.

When a test needs a STEP corpus file, take it from `Step242Corpus` (`Body`, `Import` or `Text`)
rather than reading and importing it again. See CONTRIBUTING.md for when a test genuinely needs
its own import.
