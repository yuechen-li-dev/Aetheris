# Firmament fixture kingdom

`fixtures/` is the single canonical root for Firmament-family test source.

- `.firmament` is executable Firmament source.
- `.firmfixture` is a self-describing corpus entry. Its metadata may declare a passing case, an expected rejection, or speculative/not-yet-implemented syntax; do not modernize it merely because it does not parse today.
- `.firmasm` is assembly-domain document syntax and remains distinct from Model documents.

Current Preview 3 fixtures are grouped directly by domain, normally with `valid/`, `invalid/`, `future/`, or `deferred/` status folders. `LegacyV1/` preserves the older TOON-style executable regression corpus, including its manifests and expected outcomes. `Assembly/LegacyImports/` owns the older JSON assembly compatibility corpus and its local STEP dependencies.

`Speculative/` preserves proposed language shapes whose acceptance is not part of the current contract. `DemoRegression/` contains small authored demo inputs that are exercised as regression fixtures; runnable demo applications remain under `demos/`.

When adding an invalid unsupported-material FEA case, use `fixtures/FEA/invalid/`. Do not add Firmament test source under `testdata/`.
