# Panel meshing and settling experiment

> **Experimental.** Garment work is research, not part of the supported product. Its APIs, syntax and results may change or be withdrawn. See [experimental modules](../README.md).

Experiment date: 2026-10-11. This implements the first two experiments from
[the draping research plan](draping-research.md), using the unchanged
GarmentCode dress cut and retained raised Anatolia arrangement. Mesh quality and
inspection improve; the dress still fails qualification. Temporary fitting
guides, material-space strain and friction are not implemented in this round.

## Reusable capabilities

`PlanarMeshQuality.FillInterior` inserts a bounded triangular lattice inside an
existing constrained XY triangulation, then uses the existing Lawson flips.
Original boundary points, directed segments and the polygon domain are retained.
The garment mesher uses this shared kernel path before its existing conforming
maximum-edge refinement. It does not replace analytic Profiles or stitch truth.

Boundary sampling uses length plus geometric error: lines are subdivided by
length, arcs by length and sagitta, and cubics by bounded de Casteljau subdivision.
The error target is the smaller of 1mm and 2% of `meshSize`. There is no
unconditional minimum sample count on short curves. Authored endpoints remain;
legitimate short features are not discarded. Named boundary, fill, flip and
vertex budgets stop with garment diagnostics before excessive allocation.

This is deterministic point-distribution and size control, **not a general
minimum-angle guarantee**. Acute authored features can still produce thin faces.
The known-dress test checks its observed quality separately from the general
concave-domain, boundary, area, determinism and budget tests.

`GarmentMeshInspection.Measure` reports unique-edge length statistics, minimum
triangle angle, triangles below 10 degrees, unreferenced vertices and the
maximum/minimum area-lumped vertex ratio. CLI `inspect`, `build` and `drape`
include `meshQuality` in their evidence. These measure the undeformed material
mesh, including pinned vertices, rather than the posed arrangement.

`GarmentDrapeSettings` exposes bounded substeps and structural iterations.
Defaults remain `(8,24)`, with the existing sewing schedule and materials.
`--until-settled` requires all of the following at three consecutive sampled
checkpoints, after at least 30 settling frames:

- Maximum vertex speed at most 0.01m/s.
- Finite state and maximum edge tensile strain at most 15%.
- Maximum seam gap at most 10mm.
- Maximum sampled body penetration at most 5mm.

Samples are retained every five sewing/settling frames and at the final frame.
Evidence records requested budget, completed settling ticks, settings and stop
reason. Low velocity with excessive residuals cannot produce an early success.
`GarmentDrapeLimits` centralizes the unchanged CLI checkpoint thresholds.
Pinned analytic controls test both accepted and stationary-but-stretched cases;
these do not qualify an unsupported garment or every intermediate surface.

## Measured controls

The cut, seam orientation/ease, fabric and 60mm mesh request are unchanged.
Only mesh construction changes in the first comparison. All new drapes share
cloth content key
`42C204F0BE495216B6CB3C2C3087EC8C786135D3EE71FE2D465BCFB4D468BA8B`.
The body artifact SHA-256 is
`0a0ad03e86447fc56448940c7aa56b54ea6683e8a874f833e01fd56a65a2d0e5`.

| Rest mesh measurement | Previous mesh | Interior-fill mesh |
| --- | ---: | ---: |
| Vertices / triangles | 1,234 / 2,244 | 611 / 992 |
| Minimum edge length | 1.409mm | 6.135mm |
| Median edge length | 41.434mm | 48.000mm |
| Minimum triangle angle | 1.790° | 10.328° |
| Triangles below 10° | 554 | 0 |
| Maximum/minimum lumped area | 110.74 | 26.32 |

The independent analytic curve oracles and seam tests still pass. Per-panel
mesh areas remain within 250mm² of the exact cut area; approximation error is
from the sampled curved boundary, not interior fill changing the domain.

All controls below use 30 gravity-free sewing frames. The previous control's
metrics are retained historical evidence; new runs use the real CLI and body
contact solver. Blender only presents their exported OBJ meshes.

| Mesh / solver budget | Settling frames | Peak strain | Seam gap | Sampled penetration | Replay |
| --- | ---: | ---: | ---: | ---: | --- |
| Previous mesh, 8 × 24 | 30 | 50.82% | 1.28mm | <0.001mm | Exact |
| Interior fill, 8 × 24 | 30 | 111.18% | 5.60mm | <0.001mm | Exact |
| Interior fill, 8 × 24 | 120 | 111.24% | 5.62mm | <0.001mm | Exact |
| Interior fill, 32 × 6 | 30 | 41.67% | 3.18mm | <0.001mm | Exact |

The longer `(8,24)` trace already has 111.33% strain at sewing frame 5. After
60 settling frames maximum speed is 0.00259m/s; after 120 it is 0.000294m/s,
while strain stays near 111%. It correctly exhausts its budget rather than
reporting settled. The worst link is a 27.856mm front-neckline link, so the new
failure is not explained by the previous tiny-edge outlier. Longer settling
does not resolve this sewing/contact conflict; a 600-frame repetition is not
justified by this stationary plateau.

Smaller steps reduce peak strain at the same 192 structural passes per frame,
but the garment drops farther down the body. This is **not improved fit**, and
does not justify changing the default budget. Both meshes expose an initialization
problem: the collar/shoulder placement is not retained during sewing. Temporary
semantic fitting guides, released before unsupported qualification, are the next
controlled experiment. Their benefit remains unproven; neither permanent pins
nor a strain-threshold relaxation is substituted for that missing capability.

Observed total CLI times were 99.24s for the new 30-frame `(8,24)` control,
241.02s for 120 frames, and 105.00s for `(32,6)`. These are local observations,
not a controlled performance benchmark; the split run overlapped regression
tests. Equal structural pass counts do not imply equal contact cost.

## Reproduction and retained evidence

Run from the repository root with the actual gameplay body artifact. Recreate
the previously retained raised arrangement without changing the cut:

```powershell
$output = 'artifacts/local/garment/mesh-round'
New-Item -ItemType Directory -Force $output | Out-Null
$pattern = Get-Content fixtures/Canonical/Garment/garmentcode-dress.firmament -Raw
$pattern = $pattern.Replace('545.535950181744mm', '645.535950181744mm')
$pattern = $pattern.Replace('545.089908235091mm', '645.089908235091mm').TrimEnd()
$pattern = $pattern.Substring(0, $pattern.Length - 1) + "`n  Drape { figure: Anatolia; pose: Rest; }`n}"
$source = Join-Path $output 'anatolia-raised.firmament'
Set-Content -LiteralPath $source -Value $pattern -Encoding utf8
$body = 'artifacts/local/humanoid-production/antonia.gameplay-body.json'

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment build $source `
  --out-dir "$output/build" --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape $source `
  --body $body --ticks 30 --out-dir "$output/drape-30" --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape $source `
  --body $body --ticks 120 --until-settled --out-dir "$output/drape-120" --json
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment drape $source `
  --body $body --ticks 30 --substeps 32 --iterations 6 `
  --out-dir "$output/drape-32x6" --json
```

Retain the JSON report even when a drape fails. The CLI qualification exit code
is 2 (the experiment log's PowerShell host may return 1); runtime exceptions
return 1. The three drapes remain under the ignored output directories above,
with source/body identities, actual solver snapshots, OBJ, USDA and SVG.
Front/side/back renders are under
`artifacts/local/garment/renders/mesh-round-{30,120,32x6}/`, with input hashes.
The read-only rest-mesh comparison is
`artifacts/local/garment/mesh-round/mesh-comparison.png`. Final CLI inspection
reproduces the same content key and is retained as `final-inspection.json`.

The Release Aetheris solution build passed. Its complete idle/serial test lane
passed 4,498 tests, including 1,195 kernel and 2,093 Firmament tests. An earlier
overlapping run was stopped: two CAD display tests hit their five-second display
budget, and the new oversized-panel test exposed late fill-budget admission.
Admission now occurs before seed triangulation; its focused test passes. Both
display cases passed in isolation and in the final complete idle run. The
generated STEP test output was restored after validation.
The Release Aurelian consumer solution also built with no warnings or errors;
all 1,217 consumer tests passed with the installed Naga selected through
`AURELIAN_NAGA`. This is consumer compatibility evidence, not a new native
garment animation or GPU body-contact qualification.

Validation logs are retained as
`artifacts/local/garment/mesh-round-final-build.log` and
`mesh-round-full-tests-idle.log`; the Copeland checkout retains
`artifacts/aurelian-beacon3d/garment-mesh-round-{build,tests}.log`.

The existing one-way CPU body contact limitations still apply: no friction,
CCD or garment self-contact. No NVIDIA research-only solver was copied, no new
third-party package was installed, and this experiment does not qualify GPU
body contacts, garment animation or production fit.
