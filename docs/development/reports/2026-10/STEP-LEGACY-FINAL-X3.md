# STEP-LEGACY-FINAL-X3

Verdict: **Meaningful progression. Corpus acceptance withheld.** The four ball reimports are repaired, the actual Cadmata viewport now supports matched-camera trim inspection, and the oversized gear sleeves have a bounded display fix. General trimmed spline threads still show poor triangulation. The rectangular quad path is implemented and demonstrated on the spring; it is not a general trimmed-surface remesher.

## Ball reimports

Isolated normalized bearing roots **1044, 4327, 4420, 4460** each retain two faces, two edges, and two vertices. The ambiguity was not harmless symmetry: an equatorial boundary centroid gives a nearly perpendicular radial support normal. Taking the sign of that rounding-level dot product could choose incompatible hemisphere orientations after serialization, collapse the displayed shell, and cancel signed volume.

`Step242Importer.TryDeriveSupportAlignmentFromBoundaryNormal` now declines the sphere hint when the normalized dot product has magnitude at most `1e-8`. The existing winding, shared-edge adjacency, and signed-volume derivation resolve orientation. Source `SAME_SENSE` remains provenance, and no topology or engineering tolerance changes.

All four original normalized files now reimport as **Qualified**, with complete valid pcurves and both hemisphere orientations aligned. Three synthetic translated-sphere tests verify three repeated export/reimport cycles, outward triangle winding, preserved counts, and positive near-analytic volume. Removing the guard made two tests fail; restoring it made all three pass. CLI JSON and before/after diagnostic captures are under `artifacts/local/step-final-x3/balls*`.

## Surface and trim inspection

The production Cadmata viewport has Normal, Surfaces only, BRep wire only, Translucent + BRep wire, and Face colors + BRep wire modes. Mode changes and face isolation preserve the camera. Each selectable patch carries occurrence identity, kernel face ID, STEP entity ID, support family, and boundary edge IDs. Red lines are actual kernel edge projections. Legacy STEP uses BRep mesh display; these specimens have no SDF field.

`scripts/inspect-step-legacy-cadmata.mts` uploads the real files into the current server, captures all modes through Edge/WebGPU, verifies equal camera states, verifies mesh/edge visibility, and checks isolated faces retain exactly one mesh and their boundary curves. All 26 passed these diagnostic assertions without browser runtime errors. This is automated browser evidence plus Codex pixel review, not human signoff or a new orbit/selection qualification. The earlier all-26 product selection scan had six sparse-hit misses and one missing analytic-only edge overlay; those failures are retained in the earlier artifacts and are not reclassified as passes.

The analytic-only route now exposes topology lines even without a mesh fallback. When a fallback exists, its denser authoritative edge samples are reused rather than replacing them with the coarse wire-patch samples. Geometry and selection authority remain in the existing kernel/Telos path.

## Bounded gear repair

On `mcm-04`, root **4926**, face **21 / STEP #5885** previously filled a cylindrical region outside its toothed boundary. Face **38 / STEP #1863** filled a complete cylinder although its five boundary edges enclose a narrow sector. The same sleeve defect appears in `mcm-05` through `mcm-07`.

Complete single-loop cylinder pcurve polygons now use the existing trim materializer. Reused-seam/full-circle families retain their dedicated route. Neighboring cylinder/cone pcurves are translated by integral angular turns at each coedge join, preserving the authored sweep within each coedge. This repairs chart discontinuities without changing stored pcurves or forcing a full cylinder. The synthetic equivalent-chart sector test checks bounded positions and immutable bindings; the existing periodic closed-cylinder test remains green. Actual before/after isolated views show the spurious sleeves removed and surfaces following their trim wires.

## Quad cells before triangles

The existing `QuadCell` surface IR is reused by `SplineRectangleTessellator`. Native knot-aware chord refinement runs first. Physical span lengths then balance the grid, aiming at about 4:1 or better across most cells rather than treating arbitrary U/V units as equal physical distances. Nearly coincident grid lines are merged only when the parameter spacing is tiny and their sampled separation is at most one ten-thousandth of the display chord budget. Kernel knots remain unchanged.

Cells are retained on `DisplayFaceMeshPatch.Cells` and are lowered only afterward through the same shorter-valid-diagonal helper used by SurfaceMeshIR. A stretched planar spline test checks cell proportions, complete area, and two triangles per retained quad. Axis/triangle budgets remain bounded. This applies to the existing dense rectangular spline route; general trim loops still require boundary-conforming clipping, and their bounding boxes are never treated as valid filled quads.

The real spring has **12,000 retained quads**, **24,000 triangles** on its two spline faces, plus **44 planar end triangles**. Its sampled cell edge ratios are:

| Face | Quads | Median | 95th percentile | Worst |
|---|---:|---:|---:|---:|
| 1 | 6,144 | 2.962 | 3.997 | 5.542 |
| 2 | 5,856 | 2.548 | 4.119 | 6.762 |

This is sampled display evidence, not an exact rational-surface error bound. It removes numerically vanishing grid rows and gives predictable interior cells without claiming every curved cell is square.

## Review of all 26

Review scope: current-source fitted surface/wire/overlay/face-color captures, with isolated gear and thread patches. **Visually correct** means no obvious mismatch in the reviewed views; it does not promote an engineering `Degraded` specimen. **Suspicious** means a local artifact remains unresolved. **Broken** means visible display damage remains. These are Codex assessments. Current result: **23 visually correct, 2 suspicious, 1 broken**.

| ID | Part | Visual result | Evidence / remaining concern |
|---|---|---|---|
| mcm-01 | Bevel pinion | Visually correct | Tooth and bore outlines correspond in overlay. |
| mcm-02 | Bevel gear | Visually correct | Flanks and hub correspond; some fitted-view faceting. |
| mcm-03 | Taper-lock sprocket | Visually correct | Outer teeth, bore, and tapped opening correspond. |
| mcm-04 | Long spur gear, no threads | Visually correct | Chart/sector repair removes sleeves; isolated #5885 and #1863 reviewed. |
| mcm-05 | Spur gear, no threads | Visually correct | Oversized cylindrical tooth sleeves removed. |
| mcm-06 | Spur gear | Visually correct | Repaired tooth sectors follow wires. |
| mcm-07 | Spur gear | Visually correct | Repaired tooth sectors follow wires. |
| mcm-08 | Internal gear, fusion | Visually correct | Ring and internal tooth contours correspond. |
| mcm-09 | Internal gear | Visually correct | Ring and internal tooth contours correspond. |
| mcm-10 | Two-piece shaft collar | Suspicious | Small chip-like patch beside exposed screw threads remains. |
| mcm-11 | Steel worm | Suspicious | Isolated face 28 / #2647 retains long fan facets at refinement limit. |
| mcm-12 | Ball bearing | Visually correct | Assembly capture corresponds; four ball reimports independently qualified. |
| mcm-13 | Pawl | Visually correct | Perimeter, thickness, and hole correspond. |
| mcm-14 | Ratchet gear | Visually correct | Teeth and bore correspond. |
| mcm-15 | U-joint, no threads | Visually correct | Visible joint and ends correspond; engineering degradation remains. |
| mcm-16 | Miter gear | Visually correct | Tooth flanks and bore correspond. |
| mcm-17 | Signal-power connector | Visually correct | Tabs, stem, and tip correspond. |
| mcm-18 | Mounted ball bearing | Visually correct | Housing, mounting holes, and bearing contours correspond. |
| mcm-19 | Hex nut, no threads | Visually correct | Hex perimeter and bore correspond. |
| mcm-20 | Threaded hex nut | Visually correct | Visible bore thread and perimeter correspond in fitted view. |
| mcm-21 | Threaded hex screw | Broken | Flake-like triangular damage remains along shaft; faces 143–145 reach refinement limit. |
| mcm-22 | Hex screw, no threads | Visually correct | Shaft, head, and marking correspond. |
| mcm-23 | Lathe center point | Visually correct | Tip pattern and stepped shaft correspond. |
| mcm-24 | Compression spring | Visually correct | Coil/ends correspond; retained quad cell metrics above. |
| mcm-25 | Slotted spring pin | Visually correct | Slot and end chamfers correspond; analytic-only wire now present. |
| mcm-26 | Clevis pin | Visually correct | Shaft/head and visible cross-hole correspond. |

## Remaining blocker and acceptance boundary

The next bounded problem is **quad interiors with correctly clipped boundary cells on nonrectangular spline trims**. Worm face **28 / #2647** has one loop with **13 coedges**, reaches the approximately 65k triangle budget (65,538 final triangles), and still has conspicuous fan facets. Screw face **143 / #10869** has one loop with **87 coedges**, finishes with 65,537 triangles and a `RefinementIncomplete` diagnostic; faces **144 / #3300** and **145 / #2345** have the same budget limitation. The corresponding isolated browser captures and sampled UV loops are retained. Raising the triangle cap or filling the UV rectangle would not establish correct trim materialization.

No unconditional corpus or Preview 4 acceptance is granted. The sphere failure and cylinder chart/sector blocker are removed; irregular spline trim materialization is now isolated with source identities and actual pixel evidence. Engineering totals remain **8 Qualified / 18 Degraded**, **473 missing pcurves**, despite complete displayed face counts. Engineering rational recovery remains bounded at the user's **0.1 mm** allowance with sampled evidence; the base pcurve policy remains **0.001 mm**.

## Validation and artifacts

- Current solution build passed. Known WebAssembly SQLite calling-convention warnings remain.
- Fast core lane: **1,047 passed**.
- Full serial .NET lane: **4,371 passed, zero failures/skips** across 20 suites.
- Frontend typecheck/build passed; **89 tests passed**.
- A final test-only cleanup removed ignored tuple names and an unused local; its project build passed without warnings and all four cylinder materialization tests passed afterward. Production code is unchanged from the full gate.
- Telos: **12 tests passed**.
- Frozen Zig lanes skipped because no Zig compiler/build/test infrastructure changed.
- Frozen corpus: **26 imports, 58 rigid roots, 5,552/5,552 faces displayed**; all hashes verified.
- Qualified-root production normalization: **32 attempts, 32 passed**; engineering status is checked independently of display.

All generated/vendor-bearing evidence stays ignored under `artifacts/local/step-final-x3/`:

- `review-current/corpus-summary.json`: final import, display, and normalization evidence.
- `inspection-current/`: all 26 matched-camera captures, source-face inventories, and seven reviewed contact sheets.
- `isolation-cylinder/` and `isolation-current/`: before/after gear patch evidence.
- `threads-current/`, `screw-isolation-current/`, `worm-face-2647/`, `screw-face-10869/`: isolated remaining blockers.
- `balls/`, `balls-fixed/`: isolated roundtrip diagnostics, CLI evidence, and deterministic two-sided mesh captures; these captures are not GPU witnesses.
- `quad-spring/cell-metrics.json`: retained quad metrics.
- `final-build.log`, `final-fast.log`, `final-full.log`, frontend and Telos logs: final checks.

Earlier failed iterations are retained separately, including the regression from widening the rectangle route and the interrupted test/build overlap. They do not supply final passing evidence. The widening was reverted and the final build/full gate completed afterward. Existing uncommitted milestone work is preserved; no commit, service, or external file change was made.
