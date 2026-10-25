# Cadmata browser-first packaging

Cadmata now opens the user's existing browser by default. The Windows convenience window reuses shared Evergreen WebView2; normal publication does not download or package a browser engine. Geometry code and modeling tolerances are unchanged by this pass.

## Changes and ownership

- `Cadmata.exe` is the console server/launcher; blank startup opens the packaged UI in the default browser. Close its console or press Ctrl+C to stop it. Closing a browser tab alone does not stop the server.
- `Cadmata.Desktop.exe` is the optional Windows window. Its existing form lifetime still owns the backend. It selects stable Evergreen, checks availability before backend startup, and presents Microsoft's download-page link plus Retry when unavailable. No installer runs automatically.
- Default browser sessions reuse the existing loopback/session-cookie protection. Embedded test hosts do not open browser tabs. Explicit `--no-browser --urls ...` hosting remains available.
- Publication retains executable names and merges shared dependencies with the existing compatibility guard. CLI discovery selects the platform's executable name. An explicitly supplied, hash-checked CAB remains an offline desktop packaging option; historical fixed-runtime packages remain readable.
- Public launch, bundle and platform documentation describes browser-first operation and the Windows-only wrapper.

## Qualification evidence

Evidence root: `artifacts/local/release-2-readiness/evergreen-webview/` (ignored).

- Clean CLI plus desktop publication succeeded. A first attempt caught incompatible stale `Aetheris.Cloth3D.dll` output; a clean rebuild resolved it without weakening the shared-file guard.
- Actual packaged `Cadmata.exe` launched Edge at an ephemeral loopback address with a blank, connected workspace. The test tab was closed and Ctrl+C stopped the owning server.
- Production package HTTP smoke served three UI assets and imported the canonical through-hole STEP. The same check passed after extracting the final ZIP into a fresh directory (`zip-http-smoke/`).
- Actual optional Evergreen window passed the existing interactive qualifier: gallery generation, download naming, standard views/orbit, composed-part generation, STEP import/export, invalid-import retention, and clean server shutdown following native close. `native-ui-qualified/qualification.json` records no frontend errors and `closeStoppedServer: true`. Runtime version observed: 155.0.4283.62.
- An empty runtime override, scoped to the test process, exercised the missing-runtime panel and Retry. Both checks stopped before backend creation; the native log records runtime unavailable. No system runtime was removed and no installer was launched.
- Linux WSL (`archlinux`), using .NET 10.0.13, ran the framework-dependent server from an unrelated working directory, served three production assets, imported the STEP as `Qualified`, and exited cleanly on SIGINT (`linux-smoke.json`). This is backend/asset evidence, not a Linux GPU or native-window qualification.
- Solution build: zero errors, 36 warnings. Fast kernel lane: 1,101 passed. Full serial solution gate: 6,028 passed, zero failed, two optional external PDF-renderer tests skipped. This includes the Firmament, complete STEP/kernel corpus, server startup/discovery and V8 geometry qualification. No test, compiler-server or Cadmata host remained after qualification.
- `git diff --check` passed. Modified launch, publication, diagnostic and discovery paths were reviewed for duplicate host copies, implicit browser downloads, authority duplication and unmanaged child lifetimes.

## Candidate and size

Local candidate: `browser-bundle/Aetheris-2.0.0-win-x64-browser-first.zip`.

| Payload | Previous local candidate | Browser-first candidate |
|---|---:|---:|
| ZIP bytes | 416,028,500 | 97,258,682 |
| Extracted bytes | 943,088,652 | 243,275,482 |

The ZIP is 76.6% smaller. Removing the private browser saves 667.4 MiB unpacked. The remaining payload includes self-contained .NET, the compiler, UI assets, optional Windows wrapper, existing Forge host, examples and notices; it contains no `webview2` directory or duplicate server publish directory. The unchanged Forge host/examples were copied from the preceding local candidate; application binaries were freshly published and current public docs copied from source.

SHA-256: `d7ac3770814dfb9763c7a374e8153487b7bb50c323dd9d6a789cfdbfe9c2415c`.

This is a locally qualified modified-source candidate, not a published release. No remote CI/publication was performed. macOS execution has not been tested. The browser/server separation permits a macOS-targeted server build without WebView2, but no macOS desktop package is claimed. The optional desktop edition still requires shared WebView2 on Windows; browser mode does not.

## Verdict

**Success.** The real default launch opens the existing browser; the optional Windows wrapper works with a shared runtime and explains a missing prerequisite without installing anything. The lean extracted package and full regression gate passed, including existing geometry qualifications. Linux server/asset/import behavior passed; macOS execution remains explicitly unqualified.
