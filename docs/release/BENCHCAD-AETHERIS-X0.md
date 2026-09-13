# BENCHCAD-AETHERIS-X0 — open-book reconstruction smoke qualification

## Status and executive answer

This document reports an **open-book BenchCAD reconstruction score**. It is not an official BenchCAD Vision2Code score, an official CodeEdit score, a blind result, or a leaderboard-comparable result.

The full requested experiment covers 18,648 records. This X0 run has completed the repository/scorer audit and a source-assisted, eight-record smoke qualification over the STEP targets committed in BenchCAD's `test_data/`. It is therefore a meaningful progression checkpoint, not the full experiment result.

On the four committed Vision2Code targets, GPT-5.6 Sol Codex plus Aetheris produced three exact voxel matches after bounded refinement. Mean IoU rose from 0.7479 to 0.7581; the remaining axis-rotated washer scored 0.0325 because public Firmament profile extrusion is fixed to XY/+Z. On the four committed CodeEdit targets, raw target IoU averaged 0.2180 and normalized IoU averaged 0.0102. The dominant failures require semantic geometry Aetheris does not yet admit: connected sphere/shaft or revolved-profile construction and bounded blind side-cylinder removal.

The run also found and fixed a general inspection blocker: Aetheris now unwraps STEP `SEAM_CURVE` exactly like its `SURFACE_CURVE` sibling and follows the authoritative 3D curve. That change made ordinary CadQuery STEP topology inspectable and has a focused regression test.

## Reproducibility

| Item | Value |
| --- | --- |
| BenchCAD repository | `https://github.com/BenchCAD/BenchCAD-main.git` |
| BenchCAD commit | `52087ef4b08811c21c09b8b16d0ae36c70844865` |
| Aetheris commit at start | `4605ac2355b5ff5aeca334225eb18c3df8e3d19b` plus this report's working-tree changes |
| Model | GPT-5.6 Sol Codex |
| Mode | Open book; STEP inspected first, public GT CadQuery subsequently used; final authority remains independent Firmament |
| Scorer | `benchcad_core/scoring/iou.py::iou_step_vs_step`, normalized 64-cube voxel IoU |
| Local artifacts | `artifacts/local/benchcad-aetheris/` |
| Harness | `scripts/benchcad-aetheris-x0.py` |

Reproduce the qualified smoke run from the Aetheris root with an adjacent BenchCAD checkout:

```powershell
$aetherisRoot = (Get-Location).Path
$benchCadRoot = (Resolve-Path ..\BenchCAD-main).Path
uv run --project $benchCadRoot python scripts\benchcad-aetheris-x0.py `
  --aetheris-root $aetherisRoot `
  --benchcad-root $benchCadRoot
```

Each record directory contains `target-summary.json`, `reconstruction.firmament`, `reconstructed.step`, `inspect-target.json`, `inspect-reconstructed.json`, `score.json`, `notes.md`, and GT/reconstruction preview PNGs. The harness rebuilds every Firmament source before scoring. None contains `inlineSTEP`, an imported target path, target vertices, or hand-authored STEP entities.

## Upstream repository and scoring audit

Vision2Code target records are sourced by `Vision2Code/tools/download_codegen_bench.py` from Hugging Face config path `code_gen/data/*.parquet`. The current schema supplies `stem`, `family`, CadQuery `code`, and render images, but no shipped GT STEP. The downloader executes each GT CadQuery program through `benchcad_core/scoring/exec_cq.py::execute_cq_to_step` and writes `Vision2Code/data/steps/<record>.step`. The committed four-record qualification corpus is under `Vision2Code/test_data/`.

CodeEdit target records are sourced by `CodeEdit/tools/download_edit_bench.py` from `edit-bench/*`. Each row supplies original and target CadQuery plus `orig_step` and `gt_step` bytes. The downloader writes `CodeEdit/data/steps/<record>_{orig,gt}.step`; the committed four-record corpus is under `CodeEdit/test_data/`.

Both tasks call `benchcad_core/scoring/iou.py::iou_step_vs_step`. Each STEP is tessellated independently, translated so its bounding-box center is `(0.5, 0.5, 0.5)`, uniformly scaled so its longest bounding-box side is 1, voxelized at pitch 1/64, filled, centered in a padded 68-cube grid, and compared without rotational search or post-score alignment. Orientation therefore remains significant.

CodeEdit additionally computes:

```text
normalized IoU = clip((model IoU - baseline IoU) / (1 - baseline IoU), 0, 1)
```

The current BenchCAD errata documents 26 historical Vision2Code GT timeouts. It explicitly classifies them as valid programs, not broken targets: all 185 members of the slow `double_simplex_sprocket` family were rebuilt successfully in the pinned environment, and the default timeout was raised from 90 to 300 seconds. There are no current per-record invalid-target entries. Until the full GT materialization is executed on this machine, the auditable expected inventory is 17,900 eligible Vision2Code records and 748 eligible CodeEdit records; this report does not pretend they were locally reconstructed.

## Qualified target inventory

Exact bounds, volumes, body counts, and topology below come from CadQuery/OCP over the authoritative STEP. Aetheris inspection is retained separately so importer failures are not hidden.

| Record | Task | Bounds size (mm) | Volume (mm³) | Bodies | Surface families | Faces | Edges | Status |
| --- | --- | ---: | ---: | ---: | --- | ---: | ---: | --- |
| `bolt_000008_s20260505` | Vision2Code | 27.71 × 24 × 120 | 27014.4819 | 1 | plane 15, cylinder 1 | 16 | 33 | Eligible |
| `hex_nut_000006_s20260505` | Vision2Code | 6.35 × 5.499 × 2.4 | 45.8920 | 1 | plane 8, cylinder 1 | 9 | 21 | Eligible |
| `spacer_ring_000007_s20260505` | Vision2Code | 23.95 × 24 × 0.8 | 212.4207 | 1 | plane 4, cylinder 2 | 6 | 12 | Eligible |
| `washer_000001_s20260505` | Vision2Code | 64.8 × 5 × 64.8 | 11087.9560 | 1 | plane 2, cylinder 2, cone 2 | 6 | 10 | Eligible |
| `ball_knob_cylinder_radius_f130` | CodeEdit | 25 × 25 × 75 | 13556.8886 | 2 | plane 2, cylinder 1, sphere 1 | 4 | 4 | Eligible |
| `battery_holder_box_y_f130` | CodeEdit | 48.5 × 39 × 8.8 | 9817.6798 | 1 | plane 10, cylinder 2 | 12 | 24 | Eligible |
| `topup_ball_knob_axial_hole` | CodeEdit | 25 × 25 × 74.63 | 9251.7970 | 2 | plane 2, cylinder 3, sphere 1 | 6 | 10 | Eligible |
| `topup_ball_knob_axial_hole_rev` | CodeEdit | 25 × 25 × 75 | 11362.0934 | 2 | plane 2, cylinder 1, sphere 1 | 4 | 4 | Eligible |

The smoke corpus spans six part families. Its target topology contains 11 solids, 63 faces, and analytic plane/cylinder/cone/sphere surfaces; it contains no thread target. Thread was therefore not pre-implemented.

## First pass

Every qualified record received a buildable current-capability attempt before benchmark-driven geometry changes. The first-pass failure cause was recorded before refinement.

| Task | Records | Mean raw IoU | Median raw IoU | IoU ≥ 0.99 | Build success | Mean normalized IoU |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Vision2Code | 4 | 0.7479 | 0.9795 | 2 | 4/4 | n/a |
| CodeEdit | 4 | 0.2180 | 0.0668 | 0 | 4/4 | 0.0102 |

## Capability and refinement pass

One reusable Aetheris capability was added: `Step242Importer.DecodeCurveGeometry` now accepts `SEAM_CURVE` and follows its authoritative 3D curve. The focused regression constructs a cylindrical STEP face whose circular edge is carried by `SEAM_CURVE`. This removes `Importer.EntityFamily: EDGE_CURVE geometry 'SEAM_CURVE' is unsupported` for CadQuery STEP and is not record-ID-specific. Its direct score impact is zero; its experiment impact is that the required topology inventory can proceed through Aetheris for single-root targets.

Two record reconstructions were refined using already-general Firmament constructs:

- The spacer ring became one exact line/arc C-profile rather than a full annulus, improving IoU by 0.0394.
- The bolt's explicit six-line head profile admitted a semantic `EdgeFinish` chamfer, improving IoU by 0.0016.

No new semantic primitive was added merely to rescue one sample. In particular, this smoke corpus did not justify a Thread implementation.

## Second pass

| Task | First-pass mean IoU | Second-pass mean IoU | Delta | Second-pass normalized IoU |
| --- | ---: | ---: | ---: | ---: |
| Vision2Code | 0.7479 | 0.7581 | +0.0102 | n/a |
| CodeEdit | 0.2180 | 0.2180 | 0.0000 | 0.0102 |

| Record | First IoU | Second IoU | Delta | CodeEdit normalized IoU |
| --- | ---: | ---: | ---: | ---: |
| `bolt_000008_s20260505` | 0.9984 | 1.0000 | +0.0016 | n/a |
| `hex_nut_000006_s20260505` | 1.0000 | 1.0000 | 0.0000 | n/a |
| `spacer_ring_000007_s20260505` | 0.9606 | 1.0000 | +0.0394 | n/a |
| `washer_000001_s20260505` | 0.0325 | 0.0325 | 0.0000 | n/a |
| `ball_knob_cylinder_radius_f130` | 0.0716 | 0.0716 | 0.0000 | 0.0000 |
| `battery_holder_box_y_f130` | 0.6813 | 0.6813 | 0.0000 | 0.0410 |
| `topup_ball_knob_axial_hole` | 0.0570 | 0.0570 | 0.0000 | 0.0000 |
| `topup_ball_knob_axial_hole_rev` | 0.0620 | 0.0620 | 0.0000 | 0.0000 |

## Score distribution

| Task/pass | p10 | p25 | Median | p75 | p90 | ≥0.90 | ≥0.95 | ≥0.99 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Vision2Code first | 0.3109 | 0.7286 | 0.9795 | 0.9988 | 0.9995 | 3 | 3 | 2 |
| Vision2Code second | 0.3227 | 0.7581 | 1.0000 | 1.0000 | 1.0000 | 3 | 3 | 3 |
| CodeEdit first | 0.0585 | 0.0607 | 0.0668 | 0.2240 | 0.4984 | 0 | 0 | 0 |
| CodeEdit second | 0.0585 | 0.0607 | 0.0668 | 0.2240 | 0.4984 | 0 | 0 | 0 |

These percentiles describe four records per task and must not be generalized to the full dataset.

## Failure taxonomy

| Primary cause | Count | Mean lost raw IoU | General fix? |
| --- | ---: | ---: | --- |
| MissingSemanticPrimitive | 5 | 0.8190 | Yes: axis-aware profile placement; bounded revolved profiles or connected ball-end shafts; bounded blind side-cylinder removal |

There were no build failures, scorer failures, invalid targets, kernel failures, or STEP-export failures in the eight final attempts. The five non-near-perfect reconstructions all built successfully but could not express the target construction through currently admitted public semantics.

## Representative parts

### Easy: hex nut

The STEP showed one regular hexagonal prism and one axial cylindrical bore. A `RegularPolygon2<6>` plus `Circle2` inner loop extruded to the measured thickness scored 1.0000 on both passes.

### Medium: spacer ring

The initial annulus captured most material and scored 0.9606. STEP/source inspection showed that a rectangular radial cut joins the bore to the exterior. Re-authoring the part as one closed boundary containing major outer and inner arcs plus two cut lines scored 1.0000 without adding a primitive.

### High score: bolt

An axial circular Base plus a six-line semantic Boss recovered the shaft and head. Adding the supported top-boundary chamfer raised 0.9984 to 1.0000. Bounds and volume match to reported precision. The Aetheris result has 19 faces/42 edges versus the GT's 16/33 because its chamfer route partitions three cylindrical support faces; this is an accepted topological partition difference, not hidden geometric drift.

### Hard/failure: ball-knob edits

The targets are two-solid STEP compounds containing a spherical knob and cylindrical stem; one variant also carries an axial bore. Public Firmament can emit the sphere or cylinder independently but deliberately has no arbitrary Union. The bounded sphere witness therefore scores only 0.0570–0.0716. A general revolved-profile or ball-end-shaft semantic contract is the appropriate next design question; importing or embedding the GT compound is not.

Local GT and Aetheris preview pairs are stored as `target-preview.png` and `reconstructed-preview.png` in each record artifact directory.

## Tooling friction and next isolated blockers

1. CadQuery writes `SEAM_CURVE`; Aetheris previously rejected it. This run fixed and tested that general importer gap.
2. The three ball-knob targets are multi-root compounds representing one benchmark part. `aetheris analyze` classifies them as assembly-like and refuses single-part analysis. The artifact retains this failure while CadQuery/OCP supplies exact aggregate inventory. A part-compound inspection route is needed; silently choosing one root would be wrong.
3. Aetheris reports incomplete bounds for the spacer ring because extrema of a major trimmed circular arc are lost. CadQuery/OCP reports the authoritative full bounds. Trim-aware analytic bounds are the next concrete inspection-correctness blocker.
4. Exact `aetheris analyze volume` is intentionally narrow. The harness uses the same pinned OCP geometry behind BenchCAD for exact inventory volume and retains Aetheris inspection separately.
5. Firmament has no public axis placement for ordinary Profile extrusion, so the washer cannot preserve the target's Y-axis orientation even though its 2D annular section is trivial.
6. The battery-holder target requires two half-depth +X cylindrical channels open through the top. The current construction-plane hole is through-all only.

## Capability amortization

Within this small corpus, solving early records did make later work easier: the SEAM_CURVE fix removed a repeated CadQuery inspection failure, and the explicit bounded line/arc profile technique used for the bolt and ring produced two exact matches without kernel special cases. The evidence is not yet sufficient to claim reduced iterations or higher first-attempt scores across the 18,648-record full split.

## Validation ledger

Completed for this checkpoint:

- BenchCAD environment synchronized with the upstream lock; all eight GT-vs-GT scorer sanity checks returned IoU 1.0.
- All eight committed Firmament reconstructions build through the real Aetheris CLI.
- All eight reconstructed STEP files score through BenchCAD's scorer.
- Second-pass score files and aggregate result JSON regenerated deterministically.
- SEAM_CURVE focused importer test passed as part of the full suite.
- All eight final STEP files reimport through the locally published Aetheris CLI.
- Representative bolt and hex-nut packaged-CLI rebuilds are byte-identical to the scored STEP files.
- `dotnet build Aetheris.slnx -c Release -m:1` passed with zero warnings and zero errors.
- `dotnet test Aetheris.slnx -c Release --no-build -m:1` passed all 3,405 discovered tests; the intentionally empty FrictionLab assembly reported no tests.
- Python harness lint and the repository information-architecture guard passed.
- BenchCAD's focused Vision2Code/CodeEdit scorer suites passed 13 tests (upstream dependency deprecation warnings only).

Not completed, and therefore not claimed:

- materialization/inventory/reconstruction of all 17,900 Vision2Code and 748 CodeEdit records;
- a dataset-wide first or second pass;
- Thread tests, because no thread occurs in the qualified corpus;
- a public full-dataset score.
