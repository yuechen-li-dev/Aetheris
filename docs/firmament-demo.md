# Firmament Demo Script

Use this when you need to demo Firmament v1 quickly and without improvising.

## Step 1 — Open an example

Open `testdata/firmament/examples/box_with_hole.firmament`.

Point out that it is a tiny ordered feature program:
- first a `box`
- then a `subtract` with a cylinder tool
- and that v1 preserves its current export-selection behavior instead of pretending subtraction is a new capability

## Step 2 — Run export

Run:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test Aetheris.Kernel.Firmament.Tests/Aetheris.Kernel.Firmament.Tests.csproj --filter "FullyQualifiedName~FirmamentBuildAndExportTests.Run_BoxWithHoleExample_Writes_Default_Export_Artifact"
```

That test calls `FirmamentBuildAndExport.Run(...)`, which reads the `.firmament` file, compiles it through the current pipeline, exports STEP, and writes:

- `testdata/firmament/exports/box_with_hole.step`

## Step 3 — Open the generated STEP file

Open `testdata/firmament/exports/box_with_hole.step` in a STEP viewer or CAD tool that reads AP242.

## Step 4 — What to point out

- exact geometry: the generated STEP body is the exported rectangular base box for this example under the current contract
- boolean cut: the source clearly shows a subtract-with-cylinder feature, which lets you explain the authored intent without changing runtime behavior
- deterministic result: rerunning the same source produces the same STEP text
- export metadata: the export contract keeps the exported feature id and source op index, so you can explain why the selected body is `base` at op index `0`

## Optional talking points

- machine-first DSL
- deterministic output
- STEP AP242
