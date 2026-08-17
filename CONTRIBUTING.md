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
- Put current user-facing behavior under [`docs/public`](docs/public/README.md). Architecture, experiments, and development evidence belong elsewhere under `docs/` and must not silently become the public contract.

## Rights and provenance

Submit only code, documentation, models, and assets that you have the right to contribute. Record third-party source, author, license, and redistribution constraints in the pull request and, when bundled, in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). Tool-assisted or AI-assisted work is acceptable, but the contributor remains responsible for the right to submit it and for reviewing its correctness and provenance.

## Contributor license agreement status

Aetheris intends to use a contributor-friendly CLA: contributors keep copyright, while granting the project owner enough non-exclusive rights to distribute contributions under AGPL-3.0 and alternative licenses. In return, the intended bargain commits Aetheris to continuing AGPL availability rather than using the CLA to make the community version proprietary-only.

The current [`CLA-CANDIDATE.md`](docs/legal/CLA-CANDIDATE.md) is **draft preparation for human attorney review**. It is not a final legal agreement and this repository does not yet define click-through, pull-request, or signature acceptance. Until counsel and the project owner publish approved terms and acceptance mechanics, maintainers should not represent a contribution as having accepted that draft.

Legal reviewers should also see the [`CLA counsel checklist`](docs/legal/CLA-COUNSEL-QUESTIONS.md).
