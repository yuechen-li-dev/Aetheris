# ENGINE-X0 — the editable V8

The bounded CAD and prescribed-kinematics demonstration works through the real Firmament assembly, AP242, mesh-export and browser paths. It builds a single slider-crank, a valve-equipped cylinder, one bank, and baseline/revised V8s. The browser consumes exported geometry and solved occurrence frames. A stroke change regenerates the source, exact geometry, interfaces, mesh and motion together.

## Reproduce

```powershell
./demos/Aetheris.EditableV8/Run.ps1
```

Open `http://127.0.0.1:8765/`; Ctrl+C stops the foreground server. Use `-NoServe` for headless generation and evaluator qualification. Variant input and CLI inspection are documented in the [demo README](../../demos/Aetheris.EditableV8/README.md) and [public mechanism guide](../public/firmament/editable-mechanisms.md).

Generated evidence stays under `artifacts/local/demos/editable-v8/`. That includes source, STEP, definition meshes, motion and assembly manifests, numerical reports, timings, hashes and screenshots. No generated engine mesh is tracked.

## Authority and reusable results

- A typed C# specification authors ordinary dimensioned Firmament templates. Circle2, Rect2, Polygon2, TraceLoop, hole features and WireForm build the actual parts. Identical components share definitions.
- Named datum interfaces solve the indexed assembly. The V8 has 171 physical occurrences, one assembly root, 170 mates, 32 diameter-fit results and 186 materialized constraint residuals. Rod/pin/head/sleeve relationships are explicit. Motion rest matrices come from the solved assembly.
- `SliderCrank` supplies exact closure in an arbitrarily oriented XY bank; `PrescribedMotion` supplies the angular ratio and idealized valve-lift law. A browser projection is checked against the C# evaluator.
- `AssemblyDisplayMeshExporter` and `mesh --format assembly-json` export shared local geometry, outward normals, triangle indices, stable occurrence IDs and world transforms. Engine geometry is not recreated in Three.js.
- WireForm `AxisCoil` operations expose compiler-derived winding-axis and seating-frame semantics. `SpringOnStem` constrains the actual coil axis to the valve stem, fixes the seat/roll, and checks clear diameter. This corrected the visible spring misalignment identified during review. Compression now acts about the solved winding axis, not source-local Z.

The spring defect was a useful authority test: `StartFrame.Up` and the part origin were not the helix axis. The compiler-derived axis was both offset and tilted to preserve the wire's starting tangent. The fix exposes the actual winding datum from the same AIR used for geometry. The canonical [coil-on-stem fixture](../../fixtures/Canonical/Assembly/coil-on-stem.firmament) proves free/compressed pitches and rejects contradictory axis constraints.

Other bounded product fixes cover template-local closed boundaries, distinct inner-loop names, correct full-circle/ellipse seam vertices, consumer mesh winding, double-precision placement composition and stable near-zero angular residual calculation. Small regression fixtures exercise the generic capabilities independently of the V8.

## Verified results

| Check | Baseline | Revised |
|---|---:|---:|
| Bore / stroke / rod length, mm | 90 / 88 / 145 | 90 / 94 / 145 |
| Crank radius, mm | 44 | 47 |
| Shared definitions / physical occurrences | 21 / 171 | 21 / 171 |
| Unique definition triangles | 178,716 | 178,716 |
| Cylinder samples, 0–720° at 1° | 5,768 | 5,768 |
| Maximum rod closure error, mm | 5.69e-14 | 8.53e-14 |
| Conservative rod/sleeve clearance, mm | 4.701 | 2.527 |
| Head / valve clearance, mm | 11 / 7 | 11 / 7 |
| Spring-axis samples | 11,536 | 11,536 |
| Maximum spring/stem axis separation, mm | 3.56e-14 | 5.60e-14 |
| Maximum spring-axis angular error, rad | 9.26e-17 | 2.33e-16 |
| Fixed spring-seat drift, mm | 0 | 0 |

Cam rotation is 360° per 720° crank cycle. Firing order 1–5–7–3–6–8–4–2 is checked against the actual cylinder TDCs. Both browser variants compare 27,360 matrix entries against C# samples, with maximum error 2.85e-14 and tolerance 1e-9.

Two complete generations produced identical hashes for all 30 deterministic source, STEP, mesh, assembly, motion, sample and validation artifacts. Per-run timings are kept separately. Source → assembly → AP242 occurrence → mesh definition → motion binding → browser object traces cover Piston3, Crankpin1, IntakeValve3 and IntakeSpring1 in both variants.

Release solution build: zero warnings/errors. Serial solution tests: **3,404 passed, zero failed, zero skipped** across 18 populated suites. The legacy-gated FrictionLab test project exposes no tests by default. An earlier parallel run during competing generation/build work exceeded existing tessellation wall-clock budgets; the serial run passed without changing those budgets. Repository layout and whitespace checks passed. Remote CI was not run.

A freshly packed/installed CLI was run from a temporary directory outside the checkout to inspect the generated Firmament assembly, export its mesh and AP242, and re-import the STEP. Definition and physical occurrence counts are preserved. A fresh agent, using only public instructions and reusable entry points, generated a 92 mm bore / 84 mm stroke / 150 mm rod / 8 mm lift variant and its +6 mm revision. Both mechanical sweeps, spring-axis sweeps and browser-evaluator parity passed without editing internals or repairing placements. The agent's report is under `artifacts/local/demos/engine-x0-fresh-agent/report.md`.

Representative local baseline timings: assembly compilation 1.15 s, mesh export 1.77 s, STEP export 0.25 s, mechanical sweep 5.4 ms. Baseline mesh JSON is 10,632,049 bytes; revised is 10,632,671 bytes. These are observations, not a performance guarantee or optimization target.

## Visual review

Review artifacts: [full engine](../../artifacts/local/demos/editable-v8/baseline-hero.png), [front](../../artifacts/local/demos/editable-v8/baseline-front.png), [one-bank cutaway](../../artifacts/local/demos/editable-v8/baseline-bank-cutaway.png), [isolated cylinder at maximum intake lift](../../artifacts/local/demos/editable-v8/baseline-cylinder.png), [corrected spring axes](../../artifacts/local/demos/editable-v8/spring-axis-fixed.png), and [revised engine](../../artifacts/local/demos/editable-v8/revised-hero.png). Playback, scrubbing, selection, isolation, transparency and variant switching were exercised in the real browser.

## Modeling and language boundaries

The engine is a simplified engineering demonstration. Heads, crankcase supports and timing collars are simplified; valve actuation is an idealized law without physical cam contact. Coil compression is display deformation. The clearance report explicitly limits its scope to the checked planes, clipped rod envelope, floor envelope and axial slabs. It does not certify every pairwise interference. There is no pressure, torque, combustion, thermal, contact-dynamics, fatigue or FEA result.

Closed boundaries and reusable templates materially reduced profile authoring. Finite bank/station tables in C# emit repeated assembly occurrences using the existing public assembly language; no general loop syntax, second CAD IR or general dynamic mate solver was added. Set/Pattern/Feature/Span were not extended to assembly occurrence generation in this milestone. Symmetry would primarily simplify paired bank seats, heads and crank webs; the present rigid placements preserve handedness without introducing generic reflection.

Remaining language friction: scalar template substitution can collide with property labels such as `Radius`, so these templates use unambiguous parameter names; extrusion limits are supplied as explicit typed Zmin/Zmax values because that lane does not admit general arithmetic expressions. These boundaries are recorded rather than hidden behind browser geometry or placement repairs.
