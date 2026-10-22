# Rehearse a code-native part build

This local demo builds one parametric machined L bracket, exports STEP AP242,
GLB, a dimensioned PDF, SVG and PPTX, and reconstructs the nominal solid from
the PDF alone. It does not push to GitHub or modify a demo repository.

The small C# entry point calls `MakeBracket(dimensions, outputDirectory)` for
two specifications. A typed Firmament Record and Template own the part: two
`Rect2` guides, a six-edge trace loop, an extrusion and a countersunk through
hole. The C# wrapper calls the existing compiler and exporters. There is no
Assembly wrapper or alternate geometry implementation.

## Run locally

From the Aetheris repository root, with .NET 10 and Python's `pypdf` installed:

```powershell
dotnet build Aetheris.CLI -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet build demos/Aetheris.CodeNativeBracket -c Release -m:1 -nr:false -p:UseSharedCompilation=false
dotnet demos/Aetheris.CodeNativeBracket/bin/Release/net10.0/Aetheris.CodeNativeBracket.dll
python fixtures/Regression/ComposeHoleFamilies/reconstruct_bracket_pdf.py --pdf artifacts/local/demos/code-native-bracket/nominal/Bracket.pdf --reference-step artifacts/local/demos/code-native-bracket/nominal/Bracket.step --out-dir artifacts/local/demos/code-native-bracket/nominal/pdf-reconstruction --cli Aetheris.CLI/bin/Release/net10.0/aetheris.dll
python fixtures/Regression/ComposeHoleFamilies/reconstruct_bracket_pdf.py --pdf artifacts/local/demos/code-native-bracket/variant/Bracket.pdf --reference-step artifacts/local/demos/code-native-bracket/variant/Bracket.step --out-dir artifacts/local/demos/code-native-bracket/variant/pdf-reconstruction --cli Aetheris.CLI/bin/Release/net10.0/aetheris.dll
```

The same commands work in a Linux shell. Alternatively run
`pwsh demos/Aetheris.CodeNativeBracket/Run-Rehearsal.ps1`.
The default output root is ignored `artifacts/local/demos/code-native-bracket`;
the executable accepts an explicit output directory as its only argument.

| Input | Nominal | Resized variant |
| --- | ---: | ---: |
| Overall X / Y / Z (mm) | 80 / 60 / 12 | 100 / 75 / 15 |
| Vertical / horizontal leg widths (mm) | 20 / 20 | 25 / 24 |
| Hole X / Y from O (mm) | 60 / 10 | 70 / 12 |
| Shaft / countersink entry diameter (mm) | 6 / 12 | 8 / 14 |
| Countersink included angle | 90° | 90° |

Each output directory contains the instantiated geometry source, the authored
drawing source, exact STEP, display GLB with supported semantic PMI, the final
PDF/SVG/PPTX, Drawing IR and a compact qualification/hash report. The intermediate
`drawing-base` sheets are the compiler's projections before this family's authored
dimension layout; use the final `Bracket.pdf` for review.

AP242 and GLB carry the supported semantic datum and shaft-diameter PMI. The
outline dimensions, countersink entry/angle and their additional drawing limits
are authored on the PDF; this demo does not claim unsupported STEP PMI kinds were
projected or that all drawing requirements are encoded in AP242.

## What the drawing proves

Five controlling dimensions define the six straight outline edges and uniform
thickness. The marked origin O establishes X0/Y0; the hole schedule states its
Cartesian coordinates and limits. A separate schedule specifies through diameter,
countersink diameter and included angle. The drawing identifies the top entry
plane A and normal hole axis. Nominal reconstruction does not require measuring
the picture, consulting the source or opening the STEP.

The independent reader consumes only printed PDF text. It freezes a new Path-based
Firmament model before opening the reference STEP. Qualification then compares
bounds, topology counts, vertices, cylinder/cone supports and independently
evaluated volumes. Missing size, origin, coordinate limits, hole row, countersink
angle/limits, through extent, axis orientation or outline-angle definition must
make the reader reject (nine negative mutations per sheet). This is a
bounded reconstruction witness for this drawing convention, not general PDF/OCR.

The linear limits are ±0.1 mm; specified and otherwise nominally right angles
have ±0.5° limits. A conservative screen rejects limits permitting entry breakout
or eliminating the shaft, using Cartesian/size limits and hole-axis lean on
ideal orthogonal stock. It does not qualify stock form, all angular stacks,
machining process capability or inspection. Material is explicitly 6061-T6
aluminium. Drawn-by credits Codex (AI); checked/approved names and dates remain
blank. These are engineering-review artifacts, not signed manufacturing release.

The automatic drawing planner still supports its documented rectangular-plate
family. This bracket uses authored dimensions over compiler-projected views;
successful bracket export does not expand automatic drawing recognition.

## Boundary before a public CI demo

Use current source or a subsequently qualified release: published Preview 3
predates composed-profile countersinks. Pin the Aetheris revision and .NET SDK,
run admission/tests, generate artifacts, run both PDF reconstruction witnesses,
then upload the STEP/GLB/PDF and qualification report. Parameter changes should
produce reviewable source diffs and rebuilt artifacts. A hosted GitHub Actions
run is a separate qualification step; this rehearsal does not claim one occurred.
