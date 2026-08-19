# Firmament fixture corpus

This is the single repository root for Firmament-family test source. Choose a category before copying anything:

- `Canonical/` contains current Firmament V2 examples. These are qualified and safe to copy.
- `Invalid/` contains focused current diagnostic expectations.
- `Compatibility/` contains older accepted formats and spellings. Do not copy them into new source.
- `Speculative/` contains future or not-implemented language corpus entries. It is not public syntax.
- `Regression/` contains bug-specific and implementation witnesses. It is not a general example library.

Extensions remain semantically distinct:

- `.firmament` is executable Firmament source.
- `.firmfixture` is a self-describing corpus entry and may describe expected failure or future syntax.
- `.firmasm` is a compatibility assembly input.

Required STEP inputs live under `testdata/`; generated STEP, SVG, solver, and report output belongs under ignored `artifacts/local/`.

Start authoring from the [Canonical cookbook](Canonical/README.md). Run `scripts/Test-CanonicalFixtures.ps1` to qualify every canonical example through its real CLI path.
