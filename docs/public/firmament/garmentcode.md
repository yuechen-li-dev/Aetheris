# Starting from GarmentCode sewing patterns

Garment authoring can begin with an actual programmed sewing pattern rather
than an approximate garment silhouette. Aetheris imports resolved GarmentCode
JSON into ordinary `schema Garment` source: pure profile `Feature`s, arranged
`Panel`s and explicit `Interface<Stitch>` declarations. The existing profile
resolver, analytic seam inspector, panel mesher and cloth solver retain authority.
There is no Python dependency in the application runtime.

Credit: Maria Korosteleva and Olga Sorkine-Hornung,
[*GarmentCode: Programming Parametric Sewing Patterns*](https://igl.ethz.ch/projects/garmentcode/),
SIGGRAPH Asia 2023. The [upstream code](https://github.com/maria-korosteleva/GarmentCode)
is MIT licensed. See `THIRD_PARTY_NOTICES.md` and the complete preserved license
in `fixtures/Canonical/Garment/GarmentCode/LICENSE.txt`.

## Import, inspect, modify

```powershell
dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment import-garmentcode `
  fixtures/Canonical/Garment/GarmentCode/dress.json `
  --out-dir artifacts/local/garment/imported-dress --json

dotnet Aetheris.CLI/bin/Release/net10.0/aetheris.dll garment build `
  artifacts/local/garment/imported-dress/garment.firmament `
  --out-dir artifacts/local/garment/imported-dress/build --json
```

Import emits `garment.firmament`, the retained `specification.json`, an analytic
`patterns.svg` preview and `import-evidence.json`. It does **not** claim to mesh,
simulate or fit a new figure. Evidence records the input hash, original panel
names, edge-index mapping, boundary reversals and measured seam lengths.
Keep the original specification's license with imported derivatives.

The importer admits resolved relative curves, explicit units and panel transforms,
one closed loop per panel, line/circular/quadratic/cubic edges and two-sided
stitches. Quadratic curves are elevated exactly to cubic Beziers. Circular
arcs retain radius, side and major/minor selection. Unsupported curves, unresolved
parameter programs, invalid references and reused seam spans stop with diagnostics.

The source's local XY coordinates remain the flat material pattern. Upstream
Y-up placement maps to Aetheris Z-up; Euler rotation order is retained. Clockwise
loops are normalized to Firmament's counterclockwise outer boundary and seam
correspondence is adjusted by the same reversal parity. `right_wrong` stitches
retain their upstream same-direction pairing. Numeric edge IDs remain in the
names even when semantic armhole/collar labels are present.

Upstream JSON does not carry Aetheris fabric properties or a material grain
direction. The generated source exposes **Aetheris defaults** explicitly for
review and replacement. Upstream unequal seam lengths become an explicit
measured `ease` value; mismatches above Aetheris's 25% budget are rejected.
Analytic cut lengths govern admission. Polygonal chord errors during meshing
do not redefine authored ease.

## Known templates

`fixtures/Canonical/Garment/garmentcode-dress.firmament` is the imported
non-fitted `Shirt` block with sleeves disabled and upstream length multiplier
2.2. It has four half-torso panels and six seams, with real curved armholes
and different front/back collar cuts. `GarmentCode/shirt.json` retains the
original sleeved shirt control with eight panels and sixteen seams.

`garmentcode-dress-template.firmament` adds a native `hemDrop: Length` parameter
to those same pure profile recipes:

```firmament
Feature left_ftorsoCut = left_ftorsoPattern(hemDrop: 100mm);
```

Apply the same adjustment to all four cuts. Zero produces the same compiled
cloth as the reference import; a positive value lowers the hem without changing
armhole, collar or shoulder construction. Existing profile deltas remain the
owner for other named-span edits. This is a resolved starter block, **not** a
port of the entire upstream measurement-driven configurator.

## Reproduce the research controls

The external checkout is pinned to
`d449629979028123a5c4dc9e732a2ec19b7fce31`.
`scripts/research-garmentcode.py --upstream <checkout> --out-dir <ignored-directory>`
calls the real upstream `Shirt` program and its SVG/JSON exporter. The checked-in
designs, measurement YAML, provenance and independently sampled curve oracles
document the inputs. Canonical presentation ordering makes generated JSON repeat
byte-for-byte despite upstream component discovery using a Python set.

Pattern-only Python dependencies used in the isolated research venv:
`numpy<2`, `pyyaml`, `scipy`, `svgwrite`, `svgpathtools`, `psutil`, `matplotlib`,
`CairoSVG`. The venv and checkout remain under `artifacts/local/garment/research/`.

For a matching **external** unrigged reference mesh, the shared
`GarmentDraping.SettleOnSurface` API runs the same sewing/settling/replay path
using `IClothBodySurface3D`, without inventing skeletons or skin weights.
It rejects named figure/pose bindings and measured fits; those must use their
actual humanoid binding. The bounded research harness accepts upstream Y-up,
metre, triangle-only OBJ input:

```powershell
dotnet run --file scripts/qualify-garmentcode-reference.cs -c Release -- `
  artifacts/local/garment/imported-dress/garment.firmament `
  <external-checkout>/assets/bodies/mean_all.obj `
  artifacts/local/garment/reference-body
```

The harness retains body/specification hashes, actual solver metrics, replay,
contact/stretch outliers and OBJ artifacts. A failure returns code 2. These are
discrete one-way CPU contact experiments without friction, CCD or self-contact;
they do not qualify animation or production cloth. Body meshes and the separate
GarmentCodeData dataset are not included or relicensed by this source adoption.

## Current experiment results

Follow-up: [garment draping research](garment-draping-research.md) audits the
retained mesh and strain distribution, compares primary research, and defines
the next controlled experiments. It does not relax these qualification gates.
The [mesh and settling implementation](garment-meshing-settling-experiment.md)
records the new mesh, residual histories and solver split experiment separately
from these historical controls. The dress still fails qualification.

The real upstream shirt and dress patterns match their independently generated
curve oracles (five samples and length per edge, 0.00001mm tolerance), then build
through the normal CLI mesher. A 100mm native hem extension keeps collar and
armhole geometry unchanged; zero reproduces the reference compiled cloth.

The first drape controls use the same 60mm mesh request, cotton defaults,
30 sewing ticks and 30 gravity ticks. Blender renders only the retained solver
OBJ output. These controls **fail** the existing 15% peak strain budget:

| Control | Peak stretch | Maximum seam gap | Maximum surface penetration | Replay |
|---|---:|---:|---:|---|
| Original upstream sizing/arrangement on Anatolia | 42.35% | 3.51mm | <0.001mm | Exact |
| Original cut on matching upstream `mean_all` reference body | 85.62% | 11.30mm | <0.001mm | Exact |
| Matching reference body, arrangement raised 100mm, cut unchanged | 154.72% | 4.20mm | 1.49mm | Exact |
| Anatolia, same raised arrangement and unchanged cut | 50.82% | 1.28mm | <0.001mm | Exact |

The unraised dress slips below the shoulders on both bodies. Raising arrangement
improves seam closure but worsens peak strain, so an arrangement offset alone is
not a qualified fix. The raised control's largest outlier is a 1.48mm rest link
near the front neckline, despite the 60mm mesh request. This isolates mesh quality
and sewing/body-contact convergence as the next investigation; it does not prove
that short links alone caused the failure. Neither changing the reference cut
nor relaxing the acceptance budget is justified by these results.

Retained evidence: `artifacts/local/garment/garmentcode-dress/` contains imports,
sources, `antonia-reference-sizing/`, `reference-body/`, `raised-reference-body/`
and their metric/outlier reports. The upstream simulator was not run. No
production garment fit, animation or new-body automatic measurement mapping is
claimed by these pattern/interchange controls.

The raised arrangement stays on the shoulders in the reference-body render;
its improved silhouette does not override the failed strain check. The same
arrangement is retained as `anatolia-raised.firmament` and `anatolia-raised/`,
with an unchanged flat cut and no hidden supports. Both still need solver/mesh
convergence work. Both Release solution builds passed; the full Aetheris suite
passed 4,479 tests, and the Aurelian consumer suite passed 1,217 with its installed
Naga selected through `AURELIAN_NAGA`.
