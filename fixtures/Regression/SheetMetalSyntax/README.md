# Shared mechanical / sheet-metal Profile qualification

Run `python fixtures/Regression/SheetMetalSyntax/run.py` after a Release CLI build.
The runner validates, builds and flattens the current canonical sheet-metal corpus,
the local shared-profile specimens, and the complete new documentation examples.
It requires matching acceptance/rejection and byte-identical formed STEP plus flat
area/bounds for the modern and legacy bracket pair. It never rewrites a fixture or
a geometry reference. Output belongs in ignored `artifacts/local/sheetmetal-syntax/`.

`unsupported-xz.firmament` must reject explicitly. New `Slot2` cuts are capsules;
legacy sheet `Slot` cuts retain their rectangular geometry. Unit tests additionally
check exact rounded area, retained region identities, reusable cut identities,
Template specialization, unsupported curves and unknown-field admission.
