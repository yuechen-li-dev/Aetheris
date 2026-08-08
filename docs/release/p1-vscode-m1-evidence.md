# P1-VSCODE-M1 evidence

## Package

- Extension: `tools/vscode-firmament`
- VSIX: `artifacts/vscode/aetheris-firmament-0.1.0-preview.1.vsix`
- Bytes: 12,091
- SHA-256: `4895602940BFAAD8A9C427EE63819CA063140CFD25EEE266CFA7A7B98E457A71`
- Archive: 10 entries. It contains the bundled extension, package metadata, grammar, snippets, language configuration, README, changelog, and license. It contains no `node_modules`, source tree, environment file, private key marker, API-key marker, or absolute developer path.

## Automated extension evidence

- TSPack sync/check/typecheck/test/build/package: pass.
- Unit and grammar tests: 13 passed. Coverage includes PATH/configured/development CLI invocation, spaces and Unicode, missing/malformed executables, nonzero exits, JSON envelopes, malformed JSON, diagnostics, one-based line/column and zero-based offset spans, deterministic ordering, build artifacts, view, verify, validate-on-save gating, and critical TextMate scopes.
- The official `@vscode/vsce` tool is the narrow packaging boundary. TSPack owns dependencies and run targets.

## Release-like smoke

The VSIX installed into an isolated extensions directory as `aetheris.aetheris-firmament@0.1.0-preview.1`. A published Release CLI and a source file under `%LOCALAPPDATA%/Temp/aetheris-vscode-m1-smoke`, outside both repositories, proved the command routes:

- validate: valid; first cold run 558 ms, five warm runs 118-135 ms
- build: success; adjacent STEP emitted; 372 ms
- verify: `ExternalInspectionPending`; artifact hash tied; 384 ms
- view: success; Cadmata discovered and launched by the CLI; 605 ms

The installed package could not be opened in a second isolated VS Code window because the editor's external `vscode-updating` mutex remained held across two 30-second launch attempts. VS Code logged `Code is currently being updated. Please wait for the update to complete before launching.` Package installation, isolated extension location, and all underlying command paths were still verified. Visual checks of editor language selection, Problems navigation, comment behavior, and snippet insertion remain blocked on that external editor update completing.

## Invalid-fixture smoke

| Case | Stage | Exit | Diagnostic |
| --- | --- | ---: | --- |
| unknown `Hole` variant | validate | 1 | `firmament-v2-hole-variant-unknown` |
| missing `CounterboreDepth` | validate | 1 | `firmament-v2-hole-counterbore-invalid` |
| `ConvexSmall` chamfer | build | 1 | `ProfileBoundaryChamferConvexArcRadiusTooSmall` message with corrective guidance |
| projected PMI value override | validate | 1 | `firmament-v2-pmi-projected-field-must-not-override-source-constraint` |
| unknown datum | validate | 1 | `firmament-v2-pmi-unknown-datum` |

Materialization policy remained build-time. Current build diagnostics do not always expose a separate code, and current validate/build envelopes generally omit source spans. The extension preserves every structured field that exists and uses a minimum one-character range when no span is supplied.

## Fresh-model editorfooding

A context-free frontier-model run used only the public manual, extension README/snippets/command labels, and structured CLI responses. It authored a plate and Shaft hole, corrected one deliberate dimensional error, built and viewed the part, added projected PMI, and respected an unsupported three-segment EdgeFinish diagnostic without compiler-source archaeology.

- invented commands: 0
- invented syntax: 0
- source archaeology: 0
- intentional error correction retries: 1
- unsupported-policy retries: 0
- Cadmata handoff: success

The run exposed one upstream diagnostic-quality advisory: `Diameter: 8deg` produced `firmament-v2-canonical-modify-malformed` and `firmament-v2-parse-failed` as warnings while validate returned `valid`. The extension reports those structured warnings faithfully; changing compiler severity/status is outside the editor adapter and should be corrected in the authoritative CLI.

## Repository gates

- `dotnet restore Aetheris.slnx`: pass
- `dotnet build Aetheris.slnx -f net10.0 --no-restore /m:1`: pass, zero warnings/errors
- Kernel Core: 994 passed
- Kernel Firmament: 1,023 passed
- CLI: 348 passed
- Server: 45 passed
- Cadmata frontend: ESLint pass, 58 tests pass, production build pass
- Public site TSPack docs-sync/check/format/typecheck/build/docs-check: pass; 22 routes validated
- `git diff --check`: pass in both repositories
