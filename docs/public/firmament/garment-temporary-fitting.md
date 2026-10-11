# Temporary fitting from Concept geometry

Follow-up: the [armhole investigation](garment-armhole-experiment.md) combines
these guides with T-pose sewing and A-pose wearing. Final strain improves to
9.23%, while startup checkpoint strain remains an explicit failed gate.

A fitting plane belongs to an ordinary Concept Struct. It is construction
geometry, erased from the garment mesh and material scene. A `Fitting` binding
selects named material boundaries and compiles the plane to a bounded, explicit
solver command. It does not add permanent pins, alter the flat pattern, weld
seams or change the cloth content key.

```firmament
Concept Struct fitting {
  FigureFit body {
    figure: Anatolia;
    pose: Rest;
    chestHeight: 1280mm;
    hipHeight: 960mm;
  }
  collarFloor: Plane {
    origin: [fitting.body.centerX, fitting.body.centerY,
      fitting.body.shoulderHeight + 45mm]
    normal: [0, 0, 1]
  }
}

// Inside the Garment, alongside Panels, stitch Interfaces and Drape:
Fitting collar {
  on: fitting.collarFloor;
  edges: [front.edges.collar, back.edges.collar];
  compliance: 0.00000001;
  releaseAfter: 20;
}
```

The body measurement pass and existing Concept plane resolver own construction
geometry. Coordinates use the existing compile-time scalar evaluator, including
dimensional arithmetic; origins are in Firmament millimetres and compile to
metres. Normals must be finite unit vectors. Selected vertices obey
`dot(normal, position) >= offset`, with unilateral XPBD multipliers reset per
substep and accumulated across iterations. Compliance is an authoring parameter,
not a calibrated textile modulus. Body contact runs after guide projection so
a conflicting guide cannot override body clearance; its residual stays visible.

`releaseAfter` counts gravity-driven settling frames **after** the 30 sewing
frames. Zero supports sewing only. Every guide requires an explicit integer
release frame, from 0 through 585. There are at most 32 guides, 64 edge references
per binding and 32,768 total selected vertex entries. A drape budget leaving
fewer than 15 frames after the final release fails before stepping with
`garment-fitting-release-budget`. Early settling cannot succeed while a guide
is active or during those first 15 unsupported frames. The existing 15% strain,
10mm seam gap and 5mm sampled penetration limits stay unchanged.

CLI evidence retains the Concept reference, compiled vertex IDs, numeric plane,
compliance, release frame and per-sample active status/residual. It writes the
last supported snapshot as `guided-garment.obj` and `guided-body.obj`; the usual
`garment.obj` is the unsupported final snapshot. Neither OBJ nor USDA exports
the construction plane. A `build` success establishes compilation only, not a
supported or unsupported body fit.

This uses the existing CPU cloth solver. The Vulkan solver and shared GPU header
builder reject guides with `AUR-CLOTH-005` before GPU work; they must not silently
drop constraints. There is no new GPU fitting, friction, CCD or self-contact
qualification.

## Analytic witness

`fixtures/Canonical/Garment/temporary-fitting.firmament` uses the same mechanism
on a small horizontal sheet. Real draping supports its boundaries at one metre,
releases them after five settling frames and shows downward motion during 15
unsupported frames with exact snapshot replay. Tests also check XPBD compliance
without artificial iteration stiffening, contact conflicts, unchanged material
topology, invalid bindings, unit errors and atomic invalid-command rejection.

## Anatolia experiment

The known raised GarmentCode dress is reused without changing its flat cut,
material, stitches, initial placement or body. Only measured Concept geometry
and the fitting binding are added. The retained source and CLI evidence live
under `artifacts/local/garment/fitting-round/`.

Both trials use the same cloth key
`42C204F0BE495216B6CB3C2C3087EC8C786135D3EE71FE2D465BCFB4D468BA8B`,
611 vertices, 992 triangles and zero permanent pins. The body is
`antonia.gameplay-candidate.v1`, artifact SHA-256
`0a0ad03e86447fc56448940c7aa56b54ea6683e8a874f833e01fd56a65a2d0e5`.
Only the four named front/back collar boundaries are selected. Each run uses
30 sewing frames, 60 settling frames, eight substeps and 24 iterations. Guides
release after settling frame 20, leaving 40 unsupported frames.

The matching no-guide control (`control-60/`) uses the same 30+60 frame budget,
cloth, body and solver settings. It slips below the shoulders, with 111.24% peak
strain and 5.618mm seam gap. All three runs have exact replay, finite metrics
and no guides active in their final snapshot. Comparison values and matching
cloth/body hashes are retained in `comparison.json`:

| Trial | Final strain | Seam gap | Sampled penetration | Appearance |
|---|---:|---:|---:|---|
| No guide | 111.24% | 5.618mm | 0.0000964mm | Slips below shoulders |
| Low, soft guide | 48.50% | 5.658mm | 0.858mm | Still strapless |
| Higher, firmer guide | 87.12% | 3.144mm | 0.00872mm | Remains around shoulders |

None passes the strain gate. The higher guide improves retention without
permanent support; the low guide demonstrates why optimizing peak strain in
isolation would choose the wrong visual result.

The first trial (`anatolia-guided.firmament`, `guided-60/`) sets the plane 30mm
below the measured shoulder height, at 1.3848487m, with compliance 1e-6. Its final
strain is 48.50%, seam gap 5.658mm and sampled penetration 0.858mm. It still looks
strapless while supported and after release. A lower strain alone is not a
better fit.

The second (`anatolia-guided-high.firmament`, `high-60/`) uses shoulder height
plus 45mm, at 1.4598488m, with compliance 1e-8. Source SHA-256 is
`9b912b0e7e30de828d854d44a111c83bda95daa43a4977c756af6767384f014a`.
The collar remains around the shoulders after release; the guide violation is
zero in the last supported sample. Final seam gap is 3.144mm and sampled
penetration is 0.00872mm, with exact replay and all guides removed. However,
peak strain is 84.46% already at the last supported sample and 87.12% after
release: **the dress still fails the unchanged 15% strain limit**.

The worst final constraint is the right-back armhole diagonal 336/337, rest
length 14.275mm. Its material coordinates are retained in `stretchOutliers`.
This isolates the next investigation to sewing/contact/material strain at the
armhole rather than simply keeping the collar from falling. It does not prove
that pattern sizing alone, contact alone or a particular textile parameter is
the root cause. No acceptance threshold or pattern dimension was adjusted to
make the trial pass.

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape `
  artifacts/local/garment/fitting-round/anatolia-guided-high.firmament `
  --body artifacts/local/humanoid-production/antonia.gameplay-body.json `
  --ticks 60 --substeps 8 --iterations 24 `
  --out-dir artifacts/local/garment/fitting-round/high-60 --json

& 'C:/Program Files/Blender Foundation/Blender 5.2/blender.exe' `
  --background --python scripts/render-garment.py -- `
  --garment fitting-round-high-supported `
  --input-dir artifacts/local/garment/fitting-round/high-60 --frame guided --samples 12
```

Render the unsupported result by omitting `--frame guided` and choosing a
different `--garment` output name. All three views use the actual solver OBJ
meshes, with no Blender fitting, subdivision or cloth simulation. Each render
records its input hashes. The first trial preceded the dedicated checkpoint-body
export; its Rest body OBJ was copied to `guided-body.obj` for presentation only.
The second trial exports both checkpoint meshes directly through the CLI.

## Validation

The Release Aetheris solution build passed with the existing nullable/obsolete
and WebAssembly SQLite interop warnings. Its complete idle, serial solution
lane passed 4,517 tests, including 1,195 kernel and 2,108 Firmament tests. The
15 fitting tests also passed after making the scalar-expression test input
independent of checkout line endings. The generated STEP test output was
restored after validation. Logs are retained as
`artifacts/local/garment/fitting-round/{build-final,tests-full,tests-focused-final}.log`.

The Release Aurelian consumer solution built with zero warnings and errors.
Its full serial test lane passed 1,218 tests with the installed Naga selected
through `AURELIAN_NAGA`. Copeland logs are
`artifacts/aurelian-beacon3d/garment-fitting-{build,tests}.log`. This establishes
consumer compatibility and explicit GPU refusal, not GPU fitting support or a
new native garment animation qualification. No dependency was downloaded and
no third-party solver code was adopted.
