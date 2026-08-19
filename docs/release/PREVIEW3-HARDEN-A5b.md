# PREVIEW3-HARDEN-A5b — repository information architecture

## Executive verdict

**Pass.** A human or agent can now determine where current docs, historical engineering docs, Firmament fixtures, external/reference data, consumer samples, runnable demos, test support, and generated output belong from the repository map and owning READMEs. The structural guard passes across 3,579 tracked files.

The apparent million-line desktop diff is a rename-detection artifact. Git identifies 2,002 renames; the staged content delta is 2,364 added and 30,246 removed lines. Most deletion volume is the retired 25,631-line generated PMI demo STEP file, not rewritten source.

## Before/after map

| Before | After | Rule |
| --- | --- | --- |
| `docs/<topic>/`, loose `docs/*.md`, `docs/firmament-v2/`, `docs/language/`, `docs/mvp/`, `docs/step242/` | `docs/public/`, `docs/development/`, `docs/release/`, `docs/legal/`, `docs/roadmap/` | Documentation is organized by authority/kind, then topic. |
| `references/firmament/` | `docs/development/history/firmament/legacy-authoring-reference/` | Preserved only as visibly historical material. |
| `fixtures/FirmamentV2/`, `fixtures/Firmament/`, `testdata/firmament/fixtures/` | `fixtures/` | One Firmament-family fixture kingdom. |
| `testdata/firmament/exports/` | `testdata/step242/golden/firmament-v1/` | Deliberate STEP regression goldens remain tracked. |
| `tests/` support projects | `test-support/` | Real `Aetheris.*.Tests` projects remain in place. |
| `demo-output/`, live verification/evidence defaults | `artifacts/local/` | Per-run output is ignored and never a documentation default. |

The compact durable map is in `CONTRIBUTING.md`; placement rules are also in `AGENTS.md` and the README at each content root.

## Documentation authority

`docs/public/` is the only canonical current Preview 3 user-documentation surface. Its examples and links remain executable-test qualified. `docs/development/README.md` explicitly labels the 1,202 historical/engineering files beneath it as non-canonical. Release, legal, and roadmap material retain distinct ownership.

Ambiguous Firmament, V2, language, milestone, architecture, and scope files were classified into development architecture, milestones, implementation, history, or scope-contract areas. Historical syntax was preserved rather than modernized.

## Removed competing authority

All nine tracked files under `references/firmament/` were reviewed and moved to `docs/development/history/firmament/legacy-authoring-reference/`. The retained README labels them historical and points to current public documentation; the old authority root is gone and the CI guard prevents its return. Current public docs and parser-backed qualification already covered the useful supported behavior, so no obsolete authoring form was promoted as Preview 3 syntax.

## Fixture consolidation

| Old root | Files classified/moved | Destination |
| --- | ---: | --- |
| `fixtures/FirmamentV2/` | 335 | Direct domain/status folders under `fixtures/`. |
| `fixtures/Firmament/` | 27 | `fixtures/Speculative/<domain>/`; metadata semantics preserved. |
| `testdata/firmament/fixtures/` | 256 | `fixtures/Compatibility/LegacyV1/Corpus/{valid,invalid,deferred}/`, classified from manifests and expected outcomes. |

Additional authored legacy examples, friction-lab inputs, reconstruction/verification sources, manifests, and `.firmasm` compatibility inputs moved under `fixtures/Compatibility/LegacyV1/` and `fixtures/Compatibility/Firmasm/LegacyAssembly/`. Seven loose demo regression sources moved to `fixtures/Regression/DemoRegression/`. The final fixture root contains 530 `.firmament`, 173 `.firmfixture`, and 10 `.firmasm` files. Extension semantics remain distinct; the demo-specific `.firm` file was generated output and was not generalized into the language.

Tests, projects, manifests, scripts, CI, and documentation were repaired to the new paths. A repository-wide stale-path scan and local Markdown-link scan pass.

## Generated-output cleanup

Four tracked files under `demo-output/pmi-injection/` were removed: 2,026,717 bytes and 28,224 text lines, including the 25,631-line source STEP input copied into the run directory. The demo generator remains and now defaults to `artifacts/local/demos/pmi-injection/`. CLI verification defaults to `artifacts/local/verification/`.

The explicit policy is `docs/development/GENERATED-ARTIFACT-POLICY.md`. Nineteen evidence generators now default to `artifacts/local/evidence/` instead of writing into tracked development docs. CI rejects tracked local/retired output and new development diagnostic JSON/JSONL/CSV/log files over 20,000 lines. Nine pre-existing large historical evidence files are narrowly documented in `scripts/tracked-large-artifact-allowlist.txt`; adding another is an explicit reviewed policy change.

Deliberate deterministic STEP goldens were preserved under `testdata/step242/golden/firmament-v1/`. Generated qualification products live only under ignored `artifacts/local/a5b/`.

## Samples, demos, and test support

- `samples/` means external-consumer integration examples.
- `demos/` means runnable narrative capability demonstrations.
- `test-support/` owns the three auxiliary support projects formerly hidden under ambiguous top-level `tests/`.
- `testdata/` is non-Firmament external/reference input and deliberate goldens; NIST/OCCT provenance corpora were not reorganized.

Fresh placement questions resolve directly: an invalid unsupported-material FEA fixture goes to `fixtures/Canonical/FEA/invalid/`; an external STEP corpus goes under `testdata/`; a Python Forge Host integration example goes under `samples/`.

## Firmament language inconsistency inventory

The development-only [Firmament inconsistency register](../development/firmament-language-inconsistencies.md) records active, accepted-legacy, speculative, historical, and intentional domain differences without changing them.

High-priority later audits are the Through/extent value families, overlapping Template/Compose/Modify/derivation mechanisms, and the exact casing-acceptance matrix. Model versus Sheet Metal selectors/holes and assembly-domain documents are recorded as intentional domain distinctions unless a later runtime audit proves otherwise.

## Validation

- Repository layout policy: pass, 3,579 tracked files inspected.
- Release solution build: pass, 0 warnings, 0 errors.
- Full serial .NET suite (`-m:1`, `MaxCpuCount=1`): 3,019 passed, 0 failed, 0 skipped. The pre-existing FrictionLab test assembly still has no discoverable tests.
- Aetheris CLI ground truth: canonical fixture validation passed through the real Release CLI path.
- Cadmata: TSPack sync/check, typecheck, 16 test files / 81 tests, production build, and lint passed. The acknowledged lock-version and blocked lifecycle-script policy notices remain unchanged.
- VS Code extension: TSPack sync/check, typecheck, 13 tests, build, and VSIX packaging passed.
- NuGet: CLI plus 16 public libraries packed; isolated external restore/run passed and exercised the packaged material catalog and Forge API.
- NativeAOT Forge Host: publish passed with the existing audited trimming/AOT warnings; packaged `info` and `list` reported Protocol v1 and five templates.
- Release ZIP smoke: exact ZIP extraction, CLI help, fixture validate/build/STEP reimport (seven faces, datum plus diameter PMI), and packaged Cadmata HTTP 200 passed.
- SHA-256: all 19 staged publication entries independently reverified.
- `git diff --check`, stale-path scan, relative Markdown-link scan, and final layout guard: pass.

## Release artifact changes

A5b changes bundled public documentation/fixture paths and repository-relative source metadata, so publication artifacts were regenerated rather than reusing A5 bytes. The VSIX is byte-identical to A5; the Windows ZIP, CLI package, and 16 public library packages have new candidate bytes. The principal A5b candidate hashes are:

| Artifact | Bytes | SHA-256 |
| --- | ---: | --- |
| Windows x64 ZIP | 107,779,469 | `58bf53368bfc124c3419269b26d651a8798dbee44b906c0f36fcecdaf86e6d4d` |
| Firmament VSIX | 11,937 | `def44bc6a83aed90a67505987ec36d00aa134d900fab734c48b83d0aeb59f361` |
| CLI NuGet | 33,411,657 | `4d479f128322225a06f1e5696b6caeab60c947f6f340fc8980e34e6e748389c4` |

The complete 19-entry `RELEASE-INVENTORY.md` and `SHA256SUMS.txt` were regenerated under ignored `artifacts/local/a5b/release/`. Because A5b is not committing, tagging, publishing, or pushing, release automation must regenerate them once more from the final committed/tagged revision; staging documentation now makes that requirement explicit.

## Feature freeze

Feature freeze remains intact. No Firmament keyword, parser alias, grammar, AST, lowering, CAD capability, diagnostic contract, or runtime semantic was changed. A5b changes repository paths, documentation authority, generator destinations, and path consumers only.
