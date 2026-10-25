# Cadmata import diagnostic presentation

Result: **Success**. The user's McMaster screw now renders with a compact
diagnostic summary instead of a wall of importer explanations.

## Change

- One shared frontend component summarizes warnings and informational messages,
  keeps errors (including unfamiliar severities) visible, and downloads the
  complete original diagnostics as a text log with severity, code and source.
- The existing API client forwards structured response/failure diagnostics to
  the existing desktop session log. No new logger, service, diagnostic authority
  or importer filtering was added. Browser hosting retains the log download.
- Import qualification and display status remain visible. Public portable-app
  documentation describes both log paths. Obsolete inline-list styling was removed.

## Reproduction and evidence

The user supplied `91180A142_Medium-Strength Class 8.8 Steel Hex Head Screw.STEP`.
Input SHA-256: `a7deeaba1359ba67448d83bb8a95d8faa83b8f61bb09aaf44872bccc331818a9`.
The external file was read without modification and is not redistributed here.

The actual extracted WinForms/WebView2 application imported and rendered it.
The pane shows **32 warnings, 3 informational messages** and the log download.
All 35 original messages survived in both the downloaded log and the native
session log; none of their nonfatal message bodies appeared inline.

The screw remains **Degraded**, with 95 faces, 250 edges and 165 vertices.
Missing pcurves/recovery qualification remains unresolved; this presentation
change does not reclassify that engineering evidence. CLI `analyze --json`
before/after summaries, including bounds and qualification, compare equal.

The local candidate changes only production web assets. All 409 root executable
and DLL hashes match the qualified c279d98d bundle. Its metadata explicitly says
`modifiedSource: true`; it is a local qualification candidate, not a newly
published or hosted-qualified release.

Evidence under `artifacts/local/release-2-readiness/import-log-ux/`:

- `desktop-ui/qualification.json`, screenshot
  `desktop-ui/packaged-import-diagnostic-summary.png`, downloaded
  `desktop-ui/cadmata-diagnostics.log` and `desktop-ui/native-startup.log`.
- `binary-parity.json`, `screw-parity.json`, before/after CLI analyses and
  `local-hotfix-receipt.json`.
- The same desktop session passed canonical STEP export/reimport, invalid-file
  model retention, GPU rendering, native close and local-server shutdown, with
  no uncaught browser errors.

The existing portable qualifier accepts
`CADMATA_QUALIFICATION_DIAGNOSTIC_STEP` to run this optional noisy-import check
before its standard canonical import/export checks.

## Validation and scope

- Production TypeScript/Vite build passed; 101 frontend tests passed.
- Fast .NET lane: 1,089 passed.
- Full serialized solution lane: 6,008 passed, zero failed, two optional external
  drawing-renderer tests skipped (renderer not configured). FrictionLab has no
  discovered tests. These were the prescribed `--no-build` lanes; no C# source
  or managed binaries changed in this pass.
- Dependency-notice audit passed; `git diff --check` passed.
- Code-quality review found no duplicate diagnostic authority, new error-code
  allowlist, logger infrastructure or geometry/admission changes.

The local updated app is in `import-log-ux/Aetheris-win-x64/Cadmata.exe` beneath
the evidence directory above. The existing qualified extracted bundle was left
untouched. This pass does not publish a release or claim fresh hosted CI evidence.
