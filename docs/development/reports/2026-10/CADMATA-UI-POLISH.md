# Cadmata control alignment and release UI qualification

2026-10-24. **Success** for the requested UI refinements and build-integrity fixes. No geometry or measurement-query semantics changed.

## Changes

- MachinaLayout resolves the display-options and view-actions rows from the measured toolbar height. Standard view and Measure share the same row and 34px control height. Display options wrap without overlapping the actions below; narrow header/status rows wrap without horizontal page overflow.
- Shared form grids align controls at their lower edge, regardless of label wrapping. The paperclip's fixed material is shown as read-only `304 SS`, with its full material identity in the tooltip, rather than a clipped disabled dropdown.
- Telos MeasurementTool exposes presentation variables with its prior defaults. Cadmata supplies its own palette, typography and square borders. The toggle exposes its expanded state. Query ownership, picking and measurement results are unchanged.

## TSPack and stale-renderer repair

The installed executable was `v0.1.14-m80i.1`, older than Aetheris's existing CI pin `01051b791cf83757a1c419ef7bc276d9de787a9d`. The pinned embedded-bridge build restores package-local executable resolution. `.tspack-version` now declares `v0.1.14-m80i.2` as the minimum.

Visual inspection also exposed an older locked Telos snapshot in the consumer. `sync` correctly materialized locked bytes, but those bytes differed from the newly built producer; earlier unit checks alone could therefore pass against stale code. A reviewed lock refresh changed only Telos's path snapshot; all 482 other packages stayed unchanged. `Build-CadmataFrontend.ps1` now checks every producer/consumer dist file and rejects stale snapshots before typecheck/tests/build. The negative check was reproduced, then the updated snapshot passed the full build.

Separately, a manifest frontend failure could reach patch validation with a nil graph and panic. A three-line guard in the TSPack repository preserves the original error before any materialization. A focused regression and a real invalid-manifest CLI invocation prove the diagnostic survives. This is a local TSPack source change, not a published upstream release. The local executable was updated to `v0.1.14-m80i.2+01051b7.modified`; the previous executable is backed up under the ignored evidence directory.

## Evidence

Evidence root: `artifacts/local/release-2-readiness/cadmata-ui-polish/`.

- Current TSPack sync/check, typecheck, **104 frontend tests**, production build and lint pass through the normal build script, including the producer/consumer byte guard.
- **19 Telos tests** pass. Full TSPack Go suite (`./cmd/... ./internal/... ./tools/...`) and vet pass, including the new failure regression. Real invalid-manifest `sync` exits 1 with `TSPACK_MANIFEST_INVALID_DEFAULT_EXPORT`, without a panic.
- Actual production UI inspected in the browser. Standard view and Measure both measure 34px high at the same Y coordinate; all six paperclip fields align by row. The open measurement panel uses the Cadmata palette.
- Actual packaged desktop qualification passes at **1280, 768 and 390px**, checking row clearance, field alignment and absence of horizontal page overflow. Screenshots are in `native-qualified/`, including `packaged-paperclip-controls-1280.png`, `packaged-paperclip-controls-768.png`, `packaged-paperclip-controls-390.png`, and `packaged-measurement-panel.png`.
- The desktop qualifier also passes blank startup, paperclip generation, gallery preview/download naming, Compose demo, view/orbit/picking, STEP import/export, invalid-import retention, and backend shutdown after native close. `qualification.json` records no frontend errors and `closeStoppedServer: true`.
- Final package HTTP smoke serves three production assets and imports the paperclip. All 797 ZIP entries are readable, with exactly the nine current UI assets and no stale extras. Dependency notices were regenerated. Both repositories pass `git diff --check`; owned test hosts exited.
- Managed geometry binaries reuse the preceding qualified analytic-trim candidate, whose full Windows gate passed **6,038 tests with two optional skips** and bounded Linux gate passed 41 checks. That gate was not rerun for these TypeScript/CSS/build-tool changes. No new macOS/Linux graphics qualification is claimed.

## Candidate and limits

Local candidate: `final/Aetheris-2.0.0-win-x64-ui-polished.zip`, **97,265,303 bytes**.

SHA-256: `d485b798225d010febd70ef516557287a675267e39bb0801139be10ff653f8f8`.

This is a patch of the preceding modified-source browser-first candidate, with fresh qualified UI assets and notices. It retains the analytic paperclip correction and contains no private Chromium/WebView2 runtime. Use a fresh extraction. The optional desktop wrapper still uses shared Evergreen WebView2. No tag, remote CI result or public publication is claimed.

The ignored evidence directory also includes `Cleanup-IntermediateStages.ps1` for user review. It supports `-WhatIf`, names only the three intermediate local test-bundle directories, and preserves final artifacts, evidence, source, and the tool backup. It has not been run.
