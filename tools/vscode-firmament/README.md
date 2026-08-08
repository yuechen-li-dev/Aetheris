# Aetheris Firmament for VS Code

Firmament is Aetheris's canonical, intent-oriented language for mechanical design. This Preview 1 extension recognizes `.firmament` files, adds TextMate syntax highlighting, comments/brackets, and a focused set of canonical snippets, and delegates validation, STEP build, Cadmata viewing, and verification to the real Aetheris CLI.

## Requirements

Install the Aetheris CLI so `aetheris` is on `PATH`, or set `aetheris.executablePath` to the executable. The extension never implements or guesses compiler semantics.

Current development builds are installed from the supplied VSIX: in VS Code run **Extensions: Install from VSIX...** and select `aetheris-firmament-0.1.0-preview.1.vsix`.

## Commands

- **Aetheris: Validate Firmament** runs `aetheris validate <file> --json`.
- **Aetheris: Build STEP** runs `aetheris build <file> --json` and reports the artifact.
- **Aetheris: View in Cadmata** runs `aetheris view <file> --json`; the CLI owns Cadmata discovery and launch.
- **Aetheris: Verify Model** runs `aetheris verify <file> --json`.

Structured CLI diagnostics populate VS Code Problems. Validate and build remain distinct: unsupported materialization regimes can be build-time diagnostics even when syntax and semantics validate.

`aetheris.validateOnSave` defaults to `true`; it launches a short-lived CLI process only when a Firmament file is saved. The extension activates without starting the CLI and creates no daemon. All CLI execution is disabled in untrusted workspaces.

## Snippets

Use `model`, `box`, `cylinder`, `concept-path`, `profile-from-path`, `hole-shaft`, `hole-counterbore`, `hole-countersink`, `slot-capsule`, `edgefinish-chamfer`, `edgefinish-fillet`, `require`, `pmi-projection`, `assert-volume`, and `inline-step`.

## Preview 1 limits

Syntax highlighting is not capability proof. This extension has no LSP, completion engine, semantic tokens, formatter, hover, navigation, or embedded CAD view. Respect build diagnostics and the frozen capability matrix in the [Aetheris manual](https://yuechen-li.github.io/aetheris/).

The minimum supported VS Code version is 1.90. This baseline supports the APIs used here without requiring a current editor release.
