# Aetheris FTC-11 AP242 PMI Injection Demo

This product-facing demo shows a conservative Aetheris workflow for adding editable semantic PMI to a public NIST AP242 STEP model:

```text
public NIST AP242 STEP model in
  -> Aetheris-canonical AP242 STEP
  -> editable Firmament InlineStep PMI overlay
  -> enriched AP242 STEP out
  -> PMI evidence present
  -> STEP import/reimport pipeline remains valid
```

The demo does **not** claim exact FTC-11 volume equality. For FTC-11, the current exact volume analyzer reports unsupported curved trimmed-shell integration on cylinder faces, so the demo verifies import/reimport validity and PMI evidence instead.

## Fast path on Windows / PowerShell

Recommended human-friendly demo path:

```powershell
.\demos\Aetheris.PmiInjectionDemo\Run-PmiInjectionDemo.ps1 -Open
```

Custom PMI example:

```powershell
.\demos\Aetheris.PmiInjectionDemo\Run-PmiInjectionDemo.ps1 -PmiValue 33.0 -PmiLabel demoInnerDiameter33 -Open
```

The script writes to `demo-output/pmi-injection` by default, prints the absolute output path, lists the generated files, and shows the exact `explorer` command to open the folder. A repo-root convenience wrapper is also available:

```powershell
.\tools\Run-PmiInjectionDemo.ps1 -Open
```

`dotnet run --project demos/Aetheris.PmiInjectionDemo` still works and keeps the existing demo behavior. The PowerShell script is the recommended path for people running the demo locally.

## Bundled FTC-11 STEP asset

The bundled STEP file is copied from the repository's vendored NIST PMI FTC-11 test data. The original NIST filename is intentionally preserved so the demo input is easy to trace and compare.

* Demo asset: `demos/Aetheris.PmiInjectionDemo/assets/nist_ftc_11_asme1_ap242-e2.stp`
* Vendored source: `testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp`
* Original filename preserved: `nist_ftc_11_asme1_ap242-e2.stp`

## Run from the repository root

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo
```

Default outputs are written to `demos/Aetheris.PmiInjectionDemo/out`. Without `--keep`, the demo removes only its known generated files before the run and then overwrites them deterministically. With `--keep`, existing files are preserved where possible, while the current run still writes the expected output paths.

A successful default run prints a receipt with the input STEP, canonical STEP, Firmament overlay, enriched AP242 output, checks, and report path.

If you want the more obvious repo-root output location for local demo use, prefer:

```powershell
.\demos\Aetheris.PmiInjectionDemo\Run-PmiInjectionDemo.ps1
```

That script passes `--out demo-output/pmi-injection` to the demo without changing the executable's own default behavior.

## Change the PMI value or label

Use `--pmi-value` and `--pmi-label` to generate a different semantic PMI overlay:

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo -- --pmi-value 33.0 --pmi-label demoInnerDiameter33
```

The generated overlay will contain the requested label and a millimeter value such as `33mm` or `33.0mm` depending on numeric formatting. Labels must be simple Firmament identifiers: letters, digits, or underscores, not starting with a digit. Values must be positive finite numbers.

## Edit the Firmament overlay manually

After a default run, edit:

```text
demos/Aetheris.PmiInjectionDemo/out/ftc11-pmi-overlay.firm
```

Then run with the edited overlay:

```bash
dotnet run --project demos/Aetheris.PmiInjectionDemo -- --firm demos/Aetheris.PmiInjectionDemo/out/ftc11-pmi-overlay.firm --keep
```

Or use the PowerShell runner with a visible repo-root output directory:

```powershell
.\demos\Aetheris.PmiInjectionDemo\Run-PmiInjectionDemo.ps1 -Firm .\demo-output\pmi-injection\ftc11-pmi-overlay.firm -Keep
```

For an external overlay path, the demo validates that the file exists and copies it into the output directory before building. It does not overwrite or delete the user-provided source overlay.

## Output files

A default run writes:

* `out/nist_ftc_11_asme1_ap242-e2.stp` — copied FTC-11 input with the original NIST filename.
* `out/nist_ftc_11_asme1_ap242-e2.canonical.step` — Aetheris-canonical AP242 generated from the bundled input.
* `out/ftc11-pmi-overlay.firm` — editable Firmament InlineStep PMI overlay.
* `out/ftc11-with-aetheris-pmi.step` — enriched AP242 STEP output.
* `out/demo-report.json` — machine-readable receipt with import statuses, PMI label/value, PMI evidence, and exact-volume caveat fields.

## What to inspect in the overlay

Open `out/ftc11-pmi-overlay.firm`. The important parts are:

* `solid ftc11: InlineStep` points at the canonicalized STEP file, not the raw input.
* `diameter <label>` is editable text.
* `value: <number>mm` changes the emitted semantic PMI value.
* `target: ftc11.face("#304")` is a stable imported canonical face reference selected from the canonicalized FTC-11 demo asset.

## What to inspect in the enriched AP242 output

Open `out/ftc11-with-aetheris-pmi.step` and search for semantic PMI evidence such as:

* `SHAPE_DIMENSION_REPRESENTATION('diameter:ftc11.<label>'`
* `PROPERTY_DEFINITION('diameter:ftc11.<label>'`
* `MEASURE_REPRESENTATION_ITEM('diameter',<value>`

The demo reimports the enriched AP242 output and writes `pmiEvidenceFound: true` to `out/demo-report.json` when all expected semantic PMI evidence strings are present.

## Report semantics and exact volume caveat

The report intentionally separates STEP/import validity from exact volume analysis:

* `inputStepImported`, `canonicalStepImported`, and `outputStepImported` indicate that the input, canonicalized, and enriched STEP files import through the Aetheris AP242 path.
* `geometryRoundTripOk` means those import/reimport checks succeeded for the demo pipeline. It does **not** mean exact FTC-11 volume equality was verified.
* `volumeCheckSupported` is currently `false` for FTC-11.
* `volumeCheckStatus` is currently `unsupported-curved-trimmed-shell` for FTC-11.
* `volumeCheckMessage` explains that exact curved trimmed-shell integration is not currently supported for this model.

If a future simpler input supports exact volume analysis, the report structure is ready to carry `inputVolume`, `outputVolume`, `volumeDelta`, and `volumeWithinTolerance`. For FTC-11, those fields remain `null` rather than faking exact volume preservation.

## Limitations

This is a short AP242 semantic PMI enrichment proof, not a broad MBD or CAD replacement. It intentionally does not add or claim:

* graphical PMI;
* drawing views;
* automatic decompilation;
* automatic recognition;
* SolidWorks MBD replacement behavior;
* general vendor STEP support through InlineStep directly;
* direct raw STEP wrapping without canonicalization.

Canonicalization happens first, and the Firmament InlineStep overlay references the canonical AP242 output.
