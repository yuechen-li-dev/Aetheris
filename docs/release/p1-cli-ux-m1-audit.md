# P1-CLI-UX-M1 CLI audit

## Baseline

The previous root help mixed public authoring work with internal/lab commands
(`trace`, `canon`, `asm`, `experimental`, and specialized inspection routes).
`build` advertised `--out`, while its implicit output searched for an
`Aetheris.slnx` checkout and wrote beneath `testdata/step242/golden/firmament-v1`.
That made a normal user directory fail despite the compiler otherwise being
file-oriented. `verify` accepted only an existing STEP artifact. There was no
public `inspect` or `view` command.

## Preview 1 public surface

The root help now presents only `validate`, `build`, `view`, `inspect`,
`analyze`, and `verify`. Existing specialized commands and `build --out` stay
available as compatibility/internal routes but are intentionally absent from
primary discovery. `--output` is the documented spelling.

Input policy is explicit: Firmament is accepted by validate/build/inspect/
verify/view; STEP (`.step` or `.stp`) is accepted by inspect/analyze/verify/
view. `analyze` rejects Firmament with a build-first hint. Build defaults to a
same-directory `.step` file and deterministically replaces compiler-generated
output; `--output` can select another path and its parent is created.

`validate` remains parse/bind/static-semantic validation and does not perform
materialization. `build` materializes, exports STEP, and evaluates existing
build-time assertions. `verify source.firmament` builds first, then uses the
existing STEP reimport verification route. `inspect` reports parsed Firmament
semantics or delegates STEP input to topology analysis.

## View and packaging boundary

`view source.firmament` builds its adjacent STEP artifact, and `view artifact.step`
opens directly. The narrow handoff starts the configured CAD Assistant/Cadmata
executable with the STEP path. Resolution uses `--cad-assistant-path`, then
`AETHERIS_CAD_ASSISTANT_PATH`, then established CAD Assistant install paths.
There is no existing-instance IPC: Preview 1 starts a new process. No executable
was available on the development machine for a GUI smoke, so packaging/Cadmata
delivery remains the P1-CADMATA-M1 / P1-PACKAGE-M1 boundary.

## Evidence

The clean-directory smoke uses the built CLI DLL from a temporary directory
containing only `plate.firmament`. Help, version, validate, build, inspect,
verify, and analyze succeeded; build emitted adjacent `plate.step`. `view`
correctly stopped with the actionable missing-launcher diagnostic.
