# LANG-BURN-X1 witnesses

These 45 sources preserve the September 12, 2026 burn-in, including failed first attempts and successful workarounds. They are regression and diagnostic evidence, **not canonical examples or a claim that every source is valid**. Initially authored under ignored local artifacts, they were promoted only after command and artifact observations were recorded. Sources with name collisions intentionally retain them. `*.first-attempt.txt` preserves the initial imported-source spelling.

See the [burn-in report](../../../docs/release/LANG-BURN-X1-FIRMAMENT-BURN-IN.md) for intent, verdicts, wrong-artifact cases, and deferred milestones. `manifest.json` records provenance and the final domain command. Public-doc authoring and fixture parameter variations are distinguished; no independent agent success rate is claimed.

Reproduce CLI evidence from the repository root:

```powershell
./scripts/Invoke-FirmamentBurnIn.ps1
./scripts/Invoke-FirmamentBurnIn.ps1 -NoBuild -Case stock-block,mount-counterbore,import-modify,sheet-tab
```

The runner copies sources to ignored `artifacts/local/lang-burn-x1-replay/`, builds stock before dependent InlineStep cases, and supplies the qualified imported FEA STEP input from `testdata/`. It preserves each command's output and exit status. Negative cases intentionally fail; the collector is not a release gate. Existing STEP output after a later failed build must never be treated as a new successful artifact. Browser screenshots, large STEP files, and per-run logs remain local. For visual review, import successful single-body STEP outputs in Cadmata and render their `iso.svg` files. Structural/Piping assembly extraction uses `asm import-step`; the single-part upload UI rejects those multi-root files.
