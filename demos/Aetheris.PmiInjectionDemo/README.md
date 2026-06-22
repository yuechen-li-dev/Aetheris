# Aetheris PMI Injection Demo

This demo takes a public NIST AP242 PMI test model, wraps it in Firmament InlineStep, injects editable semantic PMI, and emits an enriched AP242 STEP file.

Aetheris demonstrates a lightweight, scriptable AP242 PMI enrichment workflow:

```text
Existing AP242 STEP in
  -> Aetheris canonicalizes it
  -> Firmament wraps it as InlineStep
  -> editable PMI overlay adds semantic PMI
  -> enriched AP242 STEP out
  -> geometry remains unchanged
  -> PMI evidence is present
```

## Why FTC-11 is bundled

The bundled STEP file is copied from the repository’s vendored NIST PMI FTC-11 test data and preserves the original filename so users can compare it against the public NIST distribution.

* Demo asset: `assets/nist_ftc_11_asme1_ap242-e2.stp`
* Vendored source: `testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp`

## Run the demo

From the repository root:

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo
```

By default, outputs are written to `demos/Aetheris.PmiInjectionDemo/out`.

## Change the PMI value or label

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo -- --pmi-value 33.0 --pmi-label demoInnerDiameter33
```

You can also provide an edited overlay:

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo -- --firm path/to/edited-overlay.firm --keep
```

## Output files

A default run writes:

* `nist_ftc_11_asme1_ap242-e2.stp` — copied FTC-11 input with the original filename.
* `nist_ftc_11_asme1_ap242-e2.canonical.step` — Aetheris-canonical AP242.
* `ftc11-pmi-overlay.firm` — editable Firmament InlineStep PMI overlay.
* `ftc11-with-aetheris-pmi.step` — enriched AP242 STEP output.
* `demo-report.json` — receipt with paths, PMI evidence, and geometry preservation status.

## What to inspect in the overlay

Open `out/ftc11-pmi-overlay.firm`. The important parts are:

* `solid ftc11: InlineStep` points at the canonicalized STEP file, not the raw input.
* `diameter <label>` is editable text.
* `value: <number>mm` changes the emitted semantic PMI value.
* `target: ftc11.face("#304")` is a stable imported canonical face reference selected from the canonicalized FTC-11 demo asset.

## What to inspect in the AP242 output

Open `out/ftc11-with-aetheris-pmi.step` and search for semantic PMI evidence such as:

* `SHAPE_DIMENSION_REPRESENTATION('diameter:ftc11.<label>'`
* `PROPERTY_DEFINITION('diameter:ftc11.<label>'`
* `MEASURE_REPRESENTATION_ITEM('diameter',<value>`

The demo also reimports the output STEP and writes `pmiEvidenceFound: true` to `demo-report.json` when the expected semantic PMI evidence is present.

## Limitations

This is a product-facing PMI enrichment demo, not a full MBD replacement. It does not claim graphical PMI, drawing views, automatic recognition, automatic decompilation, full GD&T authoring, or universal raw vendor STEP support. Raw STEP is canonicalized before InlineStep is used.
