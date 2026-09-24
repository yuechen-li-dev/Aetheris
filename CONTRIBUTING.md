# Contributing to Aetheris

Thank you for improving Aetheris. Keep changes focused, explain the engineering intent, and preserve the rule that supported semantics are emitted truthfully while unsupported semantics fail with actionable diagnostics.

## Preview 3 status

`2.0.0-preview.3` is feature-frozen. Release-hygiene, documentation, tests, narrowly scoped correctness fixes, and post-Preview work on an explicitly agreed branch are appropriate; new Preview 3 CAD, Sheet Metal, PMI, FEA, Forge, material, platform, or Cadmata capability is not.

## Build and test

Install the .NET SDK selected by [`global.json`](global.json), then use the canonical solution:

```powershell
dotnet restore Aetheris.slnx
dotnet build Aetheris.slnx -c Release -m:1
dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1
```

### Two test lanes

The command above is the **full corpus** lane and is what has to be green before you commit.
It imports the whole NIST and OCCT corpus, so it is the lane that actually protects the
interchange behaviour.

For the edit-run-edit inner loop there is a **fast lane** that skips the corpus-driven tests:

```powershell
dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"
```

On a two-core machine that is roughly 9s against 31s for the full lane, over 959 of the 1092
tests. Use it while iterating; never use it as the gate.

A test belongs in `[Trait("Category", "SlowCorpus")]` when its cost is dominated by importing a
large corpus file rather than by the behaviour it is checking. Keep at least one cheap test per
behaviour outside the trait, so the fast lane still fails when something is broken rather than
merely running fewer tests. `Aetheris.Kernel.Firmament.Tests` has the trait too, but its time is
spread evenly across real geometry work rather than concentrated in corpus I/O, so the split
buys much less there.

### Importing corpus files in tests

Do not call `File.ReadAllText` + `Step242Importer.ImportBody` directly in a test. Use the shared
memoized fixture:

```csharp
var body = Step242Corpus.Body("testdata/step242/nist/CTC/nist_ctc_02_asme1_ap242-e2.stp");
var import = Step242Corpus.Import(relativePath);   // when the diagnostics matter
var text = Step242Corpus.Text(relativePath);       // when the file text itself matters
```

The corpus has 48 distinct files and the test project reads from it at roughly two hundred call
sites. Importing every file exactly once costs about ten seconds; doing it per call site cost
about ninety-eight. Parsing is under a second of that, so caching the parsed document would buy
almost nothing - the body is what is expensive to build, so the body is what is cached. A
`BrepBody` is read-only once constructed, which is what makes sharing it safe.

Two cases must still import for themselves and should take only `Step242Corpus.Text`: tests that
install a `Step242Importer.CaptureLoopRole*Diagnostics` scope, because it collects while the
import runs, and tests under `Step242Importer.PreserveRationalSurfaces()`, because it changes
what the import produces.

Cadmata and the VS Code extension use TSPack and their checked-in lock files:

```powershell
Push-Location aetheris.client
tspack sync
tspack check
tspack run typecheck
tspack run test
tspack run build
tspack run lint
Pop-Location

Push-Location tools/vscode-firmament
tspack sync
tspack check
tspack run typecheck
tspack run test
tspack run build
Pop-Location
```

Use `Aetheris.CLI` as the ground-truth inspection surface while developing:

```powershell
dotnet run --project Aetheris.CLI -c Release -- --help
dotnet run --project Aetheris.CLI -c Release -- analyze path/to/part.step --json
```

The shell entry points under `scripts/` remain useful for targeted Linux/framework automation. The release qualification commands above are the authoritative Windows path for Preview 3.

## Contribution expectations

- Keep pull requests small enough to review and test as one engineering claim.
- Add tests for behavior changes and run the affected real CLI, package, or UI path.
- Use `ToleranceContext` and `ToleranceMath`; do not introduce ad hoc geometry epsilon constants.
- State whether display work changes STEP semantics, BRep topology, Firmament/AIR/CIR lowering, DisplayIR authority, or frontend presentation only.
- Update public documentation whenever a user-visible command, boundary, diagnostic, or example changes.
- Put current user-facing behavior only under [`docs/public`](docs/public/README.md). Architecture, experiments, and development evidence belong under [`docs/development`](docs/development/README.md) and must not silently become the public contract.

## Repository content map

| Location | Durable purpose |
| --- | --- |
| `docs/public/` | Authoritative current Preview 3 user documentation. |
| `docs/development/` | Historical engineering, architecture, milestones, experiments, and evidence. |
| `docs/release/` | Release qualification, staging, claims, and contracts. |
| `docs/legal/` | Legal-review candidates and counsel questions. |
| `fixtures/` | All Firmament-family test source and corpus metadata, including `.firmament`, `.firmfixture`, and `.firmasm`. |
| `testdata/` | Non-Firmament external/reference inputs and deliberate golden exports. |
| `samples/` | External consumer integration examples. |
| `demos/` | Runnable narrative demonstrations of Aetheris capabilities. |
| `test-support/` | Auxiliary projects and assets used by tests. |
| `artifacts/local/` | Generated local output; ignored by Git. |

Before adding content, use an existing location above. Do not create another top-level documentation, fixture, example, or generated-output bucket without an explicit architectural reason. Generated output is governed by the [generated-artifact policy](docs/development/GENERATED-ARTIFACT-POLICY.md): tools default to `artifacts/local/`, and only bounded, reviewed evidence or goldens may be promoted into source control. Run `./scripts/Test-RepositoryLayout.ps1` before submitting structural changes.

## Rights and provenance

Submit only code, documentation, models, and assets that you have the right to contribute. Record third-party source, author, license, and redistribution constraints in the pull request and, when bundled, in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). Tool-assisted or AI-assisted work is acceptable, but the contributor remains responsible for the right to submit it and for reviewing its correctness and provenance.

## Contributor license agreement status

Aetheris intends to use a contributor-friendly CLA: contributors keep copyright, while granting the project owner enough non-exclusive rights to distribute contributions under AGPL-3.0 and alternative licenses. In return, the intended bargain commits Aetheris to continuing AGPL availability rather than using the CLA to make the community version proprietary-only.

The current [`CLA-CANDIDATE.md`](docs/legal/CLA-CANDIDATE.md) is **draft preparation for human attorney review**. It is not a final legal agreement and this repository does not yet define click-through, pull-request, or signature acceptance. Until counsel and the project owner publish approved terms and acceptance mechanics, maintainers should not represent a contribution as having accepted that draft.

Legal reviewers should also see the [`CLA counsel checklist`](docs/legal/CLA-COUNSEL-QUESTIONS.md).
