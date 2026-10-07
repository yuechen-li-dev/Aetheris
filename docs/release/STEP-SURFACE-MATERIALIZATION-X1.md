# STEP-SURFACE-MATERIALIZATION-X1

Verdict: **Meaningful progression. Preview 4 legacy STEP fidelity is not accepted.**

The frozen McMaster corpus reproduces the inherited 39-missing-face baseline. This pass restores 38 of those faces, including all 28 missing spherical bearing faces. The gear, collar and mounted bearing now display every bound face. One U-joint face still has a collapsed recovered UV trim. The spring has coherent coils in current Cadmata captures, but one rational spline patch exhausts adaptive refinement and remains Degraded. Exact-face pcurve population, connector remapping, topology overlay and human review remain open gates.

## Scope and baseline

The specimen set and SHA-256 values in `testdata/step242/manifests/mcmaster-legacy-x0.json` are unchanged: 26 local specimens, 25 AP203 and one AP214, 58 rigid roots, 5,552 source faces and 5,552 bound faces. No vendor CAD was committed. The existing X0 manifest outcomes remain a historical baseline rather than being overwritten with X1 claims.

Before geometry edits, `artifacts/local/step-surface-x1/baseline-copy/` reproduced 5,513 displayed faces and 39 missing faces. All rigid roots remained structurally enclosed. There is no actual `NEXT_ASSEMBLY_USAGE_OCCURRENCE` hierarchy in this corpus; existing authored assembly tests qualify that separate path.

## Materialization repairs

Spherical faces previously used a rectangular longitude/elevation envelope. That discarded inner loops and rejected equatorial hemispheres because every boundary sample had the same elevation. The new bounded stereographic chart triangulates the directed outer boundary and its holes, then evaluates the exact sphere through the chart inverse. Directed vector area selects the chart interior; resolved canonical face orientation supplies material-side polarity. Explicit outer-bound metadata wins over loop order. Repeated pole samples collapse without inventing area; a true point boundary and a boundary crossing the excluded chart pole fail with named diagnostics. No bounding-box cap replaces a trim.

The native sphere convention is documented on `SphereSurface`: U is azimuth around Axis, with period 2π; V is elevation in [-π/2, π/2]. X is the projected reference axis, Y=Axis×X, and poles are Center±Radius×Axis. Retained face-local pcurves are sampled through this same evaluator when present. Source `SAME_SENSE` remains provenance; downstream winding consumes the canonical orientation authority. The obsolete sphere-envelope resolvers and their duplicate parameter projection were removed.

The spline trim path required at least three coedges even when two curved edges enclosed a finite region. It now validates the sampled polygon. Numerical display projection also reuses the existing qualified spline inverter: a sparse duplicate seed grid had selected the wrong branch on narrow native domains and long closed profiles. Native spline domains, degrees, knots and rational weights remain intact. The display projection budget is separate from pcurve qualification.

Closed spline boundary projection now unwraps with the actual native period and origin. The spring's U=0/U=1 aliases had been connected across the whole support. Source pcurves remain preferred; even line pcurves retain a midpoint so a full native-period span is not mistaken for a seam jump.

Rectangular free-form trims previously bypassed geometric refinement. When native knot spans require finer sampling than the established grid, they now use the shared boundary-conforming adaptive materializer. Ordinary low-complexity rectangles retain their established scaffold path. Native knot spans constrain refinement cells; subdivision still depends on chord/normal error. Refinement visits the region fairly and counts live triangles rather than discarded records. The options-derived work cap is bounded at 65,536 live triangles, below the existing maximum grid's triangle budget. Exhaustion emits `Viewer.Tessellation.RefinementIncomplete`; it never implies fidelity success. Display chord tolerance remains 0.05 mm and angular tolerance remains π/12. Geometrically checked removal of inversion noise is limited to one thousandth of the display chord allowance.

Mounted-bearing source face #9760 exposed a separate recovered-edge display gate. Its 31.8 mm cylinder has three recovered boundaries departing radially by up to 0.0340962 mm. Recorded curve-recovery deviations are 0.0271561–0.0381652 mm, but display projection used a fixed 0.01 mm floor. Cylinder projection now admits a face-local allowance bounded by recorded deviation, the recovery budget and the current display chord budget. Exact edges retain the existing gate. Synthetic tests reject the same departure without provenance and when the display budget is only 0.02 mm. This changes display projection, not source geometry or pcurve qualification.

## Face-level results

| Specimen | Bound | Baseline displayed | Final displayed | Source faces restored |
|---|---:|---:|---:|---|
| mcm-05 gear | 210 | 208 | 210 | #8204, #6502 |
| mcm-10 collar | 200 | 198 | 200 | #26224, #22518 |
| mcm-12 bearing | 166 | 138 | 166 | All 28 spherical faces below |
| mcm-15 U-joint | 209 | 206 | 208 | #8211, #4515; #12119 remains empty |
| mcm-18 mounted bearing | 349 | 345 | 349 | #2996, #17004, #9668, #9760 |
| Whole corpus | 5,552 | 5,513 | 5,551 | 38 restored; one still missing |

The bearing hemisphere pairs are #3892/#5001, #3779/#444, #3346/#1654, #1639/#3888, #4722/#2379, #3951/#5071 and #758/#2790. The two-loop spherical faces are #3604, #3673, #1331, #3806, #1121, #697, #2069, #65, #373, #3148, #111, #3247, #5003 and #2284. Every one now has nonempty display geometry.

Full 26-file sweeps were recorded after the sphere repair, spline trim repair and spline interior work, and again after cleanup and the recovered-cylinder repair. Missing counts progressed 39 → 11 → 4 → 2 → 1. No unrelated specimen lost display coverage.

## Remaining blockers and qualification

The U-joint's rigid root #7851, kernel face 67, source face #12119 is supported by a degree-(1,3), 2×18 spline. Its two recovered line pcurves collapse to a polygon with signed UV area approximately -2.71e-20. The materializer reports the four input samples, U=[6.19e-16,0.0010174085], V=[0.4968757963,0.4999999148], triangulation rejection and zero retained triangles. A feature-aware trim approximation is needed at the pcurve owner; adding triangles to this collapsed trim would fabricate area. The body remains Degraded.

Spring source face #2370, kernel face 1, retains a rational degree-(3,3), 7×142 support with native U/V domains [0,1], closed U and nonuniform V knots. It still reaches the 65,536 live-triangle limit before all refinement criteria pass. The whole spring mesh has 77,883 triangles versus 924 at baseline. This is a substantial interior improvement, not a completed fidelity qualification. Its exact support, sampled boundaries, normalized trim and isolated triangulation are saved in `face-debug/spring-final/`. Further work should improve knot-span seeding and refinement conditioning rather than repeatedly increasing the cap. This is the bounded spline-interior stop for this pass.

Machine qualification is deliberately independent of counts. Existing shell, pcurve and orientation reasons remain; newly exposed incomplete-refinement reasons can downgrade a previously silent candidate. Human visual review is pending for all 26 specimens. Complete display coverage does not establish manufacturing fidelity.

## Pcurve population and normalization

The importer still invokes population only when curve/support recovery provenance is present. This exact/non-recovery population gap was audited but not changed after the spline-interior stop. Existing explicit pcurves remain retained and preferred. The base pcurve allowance is still 0.001 mm; geometry recovery is still 0.1 mm, with existing measured local qualification allowances. No global budget was loosened.

All currently mechanically qualified body/display candidates were retried through AP242 export/reimport. Pawl root #2 passes with 22 faces, 54 edges, bounds deviation 5.03e-15 mm and sampled edge deviation 1.02e-12 mm. Connector root #1060 **fails normalization**: spline-support faces 43, 44, 48 and 49 reject Circle3/Polyline pcurve lift residuals of 0.00864634, 0.00699945, 0.00699945 and 0.00864634 mm against 0.001 mm. The serialization/remapping cause was not repaired or newly root-caused in this pass. Interior equivalence across normalization is not claimed. X0's five passing body candidates remain historical evidence, not an X1 pass for candidates that no longer qualify under current display evidence.

## Product and visual evidence

Current-source Cadmata served its production frontend to local Edge WebGPU. Bearing, U-joint, spring, gear, collar, connector and the repaired mounted bearing all passed actual file upload, framing, orbit pixel change and selection-hit checks, with zero page errors. The final `product-current/` contains seven initial captures, seven orbit/selection captures, HTTP/UI evidence and `product-summary.json`; `product-direct/` retains the preceding six-case run. Rendering telemetry includes submitted empty patches; it is not the displayed-face metric. The corpus runner counts only patches containing triangles.

The browser harness initially failed on the large U-joint/collar replies when relaying them as base64 through DevTools. Direct browser HTTP fixed that harness issue; no product geometry or renderer workaround was introduced. Each specimen now runs in its own browser lifecycle. The successful final product run used normal Edge settings, not the optional software-GPU diagnostic run.

The standalone diagnostic renderer produced isometric/opposite-isometric captures for all 26 specimens and a contact sheet. Its two-sided shading aids inspection and is not product fidelity evidence. The actual Cadmata spring capture shows coherent coils; the retained refinement warning still blocks qualification. `contact-sheet/human-review.json` leaves every human decision pending with the requested choices: visually correct, suspicious or visibly broken. No vendor reference thumbnails were supplied. No human review is asserted.

Topology overlay remains unqualified. STEP compound definitions currently project face patches through `AssemblyDisplayService`; their packet does not retain the kernel edge-polylines needed for this overlay. That contract projection requires a separate closeout. No renderer face caps, imported CIR conversion or triangle-derived substitute topology were added.

## Reproduction and debug artifacts

Run from the repository root:

```powershell
pwsh -File scripts/qualify-step-legacy.ps1 -CorpusDirectory '<local McMaster directory>' -OutputDirectory artifacts/local/step-surface-x1/final
python scripts/render-step-legacy.py artifacts/local/step-surface-x1/final
dotnet test-support/Aetheris.StepLegacyCorpus/bin/Release/net10.0/Aetheris.StepLegacyCorpus.dll '<specimen STEP path>' artifacts/local/step-surface-x1/face-debug/one-face --face-step <ADVANCED_FACE entity id>
```

`--face-step` isolates the selected face in the display mesh and dumps support geometry, native domains/knots/weights, resolved orientation, directed coedges, 3D edge samples, retained pcurves and samples, normalized spline UV triangulation input, output indices/positions and rejection diagnostics. Empty faces remain empty in the isolated output. The CLI compound analysis also records the bearing's real rigid-root structure without assigning assembly semantics.

Local ignored evidence lives under `artifacts/local/step-surface-x1/`: `baseline-copy/`, `sphere/`, `spline-trim/`, `spline-interior/`, `pcurve/`, `normalization/`, `final/`, `face-debug/`, `contact-sheet/` and `product-direct/`. Exploratory stages are retained separately and are not final acceptance evidence. The corpus worker records import/materialization timing and retains its per-file 180-second containment budget.

For product reproduction, build the frontend and `Aetheris.Server` using the established X0 commands, run the server DLL in a foreground terminal, then invoke:

```powershell
node scripts/qualify-step-legacy-cadmata.mts <playwright-index.mjs> <local-corpus-directory> artifacts/local/step-surface-x1/product-direct http://127.0.0.1:5087 mcm-12,mcm-15,mcm-24,mcm-05,mcm-10,mcm-17
```

## Validation and closeout

Synthetic source tests cover hemisphere/cap boundaries through BRep coedges, spherical bands with holes, longitude seams, pole-touching trims, collapsed pole points, two-curved-edge spline lenses, non-unit native domains, repeated spline interior turning, native closed-spline periods, collapsed pcurve polygons, explicit refinement exhaustion and bounded recovered-cylinder projection. Existing NIST, orientation, assembly, recovered-bolt and authored AP242 lanes remain required.

The final sweep imports all 26 hash-matched specimens: **2 Qualified machine candidates, 13 Inspectable, 11 Degraded, 0 Failed**. The histogram is not a fidelity promotion. Import time is 0.202–8.776 s, mean 2.698 s; complete worker time is 0.292–13.190 s, mean 4.320 s on this machine. Dense adaptive meshes remain a performance limit; this is not a broad optimization or timing guarantee.

- Full Release solution build: passed, zero errors; existing compiler/platform warnings are retained in the log. Corpus runner build: passed.
- Fast Core lane: 1,037 passed, zero failures/skips. Focused synthetic materialization lane: 14 passed.
- Required full serial solution lane: **4,361 passed, zero failures/skips**, across 20 test assemblies, including NIST, canonical orientation, authored assemblies, recovered McMaster bolt and AP242 roundtrips. FrictionLab discovers no tests.
- Focused NIST/materialization regression lane: 31 passed before the final recovered-cylinder change; the final full lane includes those tests again.
- Frontend typecheck: passed. Frontend suite: 86 passed across 18 files.
- Actual Cadmata product witnesses: seven upload/frame/orbit/selection passes, zero page errors.
- Repository content-location guard and whitespace diff check: passed. No raw vendor geometry or generated meshes were staged. Frozen legacy Zig suites were skipped because no Zig compiler/build infrastructure changed.

Preview 4 acceptance remains blocked by the named trim/interior/pcurve/normalization/overlay/human-review gates; this pass ends with verified materialization progress rather than a claim of universal legacy STEP support.
