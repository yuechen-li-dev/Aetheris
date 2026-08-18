# STEP-NIST-SNAPSHOT-HOTFIX-A0 triage

## Failing tests

The broad validation run exposed hash-only failures in `Aetheris.Kernel.Core.Tests.Step242.Step242NistAuditHarnessTests`:

- `NistCorpus_PerFile_AuditReport_IsStable_AndMatchesSnapshot` for the exact NIST AP242 BRep fixtures in `testdata/step242/nist/CTC`, `FTC`, and `STC`.
- `NistCorpus_AggregateAuditReport_IsByteStableAcrossConsecutiveRuns_AndMatchesSnapshot` for `testdata/step242/manifests/nist.v0.report.json`.

The failures did not change import status, diagnostics, exception state, file sizes, or topology counts. Only the exported canonical STEP SHA-256 values changed.

## Root cause

The stale snapshot hashes predated this hotfix and were inconsistent with the current importer/exporter behavior after the STEP orientation/canonicalization fixes, especially the preserved `ADVANCED_FACE` same-sense path covered by `Step242Ftc06SameSenseRegressionTests`.

The current NIST audit harness still imports each fixture, validates BRep bindings, exports canonical AP242, and proves consecutive runs are byte-stable before comparing the snapshot.

## Why the snapshot update is safe

The updated snapshot keeps the same corpus entries and the same semantic audit expectations:

- all exact BRep NIST fixtures still report `success`;
- diagnostics remain `Audit.None` / `No diagnostics.`;
- topology counts are unchanged;
- the tessellation-only FTC08 TG fixture remains explicitly classified as unsupported;
- current canonical exports contain AP242 topology markers such as `ADVANCED_FACE` and `VERTEX_POINT`; the NIST audit contract remains import, BRep binding validation, export, and byte-stability rather than canonical reimport.

This hotfix updates the canonical SHA-256 values in the NIST audit snapshot and preserves the existing parser-backed fixture source directory when trace validation reparses V2 fixtures. The trace-path fix is limited to resolving existing relative fixture assets, including the existing InlineStep canonical-box fixture; it does not expand inline STEP accepted syntax or STEP semantics. It does not expand Firmament syntax, imported topology semantics, recognized regions, replacement/residual accounting, or display/frontend behavior.

## Validation performed

- `dotnet restore Aetheris.slnx`
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`
- `dotnet test Aetheris.Kernel.Core.Tests/Aetheris.Kernel.Core.Tests.csproj -f net10.0 --no-build --filter Step242NistAuditHarnessTests`
- per-fixture `Aetheris.CLI canon` plus marker checks for `ADVANCED_FACE` and `VERTEX_POINT` on exact NIST BRep canonical exports.
- `dotnet test Aetheris.CLI.Tests/Aetheris.CLI.Tests.csproj -f net10.0 --no-build --filter FirmamentV2DesignFixtures_AreNotTreatedAsV1ParseFailures`
