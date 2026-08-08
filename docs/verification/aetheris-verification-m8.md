# AETHERIS-VERIFICATION-M8

> Aetheris does not call an artifact correct merely because it exported, reimported, or looked plausible in one viewer. Construction, topology, serialization, and external display are separate verification layers.

`aetheris verify model.step` is the artifact-level verifier. It writes a hash-tied report under `artifacts/verification/<fixture>/<step-sha256>/` by default, including a copied STEP artifact and `verification-report.json`.

## M8-HOLE-LOOP-X1 topology policy

Directed topology uses are resolved exclusively through `DirectedEdgeUse.Resolve`: edge start/end establish topology direction and a coedge reverses it once. Curve `same_sense` and face `SameSense` do not participate in loop connectivity. Ordered endpoints, including the closing pair, are required; geometrically coincident periodic seam vertices are admitted only by binding-aware validation. See [M8 hole-loop policy](m8-hole-loop-x1.md).

## Independent B-rep mass properties

The evaluator consumes a materialized `BrepBody`, never fixture expected-volume values, AIR, CIR, or `StepAnalyzer`'s legacy planar signed-shell routine. It first gates the body on one body, closed cyclic loops, two uses per edge (with the closed parametric-sphere exception), connected shells, face bindings, and coincident geometric endpoints where the kernel represents periodic seam vertices separately.

Supported materialized surface families are planes, cylinders, cones, spheres, and tori. The evaluator refines its verification mesh twice. At each resolution it corrects each curved triangle to the face's bound `SameSense`, sums triangle area, and integrates oriented tetrahedra `(0,a,b,c)`. Thus `V = sum dot(a, cross(b,c))/6`; the first moment is each tetrahedron's signed volume times `(a+b+c)/4`. The centroid is that first moment divided by signed volume.

The surface mesh is display-derived diagnostic evidence and never becomes authoritative geometry or occupied-volume evidence. Results are `NumericalConverged`, `NumericalWithBound`, or `Unavailable`; no tessellated value is called exact. The reported envelope is the larger of the coarse/refined delta and `4 × sampled-area × chord-tolerance`. M2 proved that this envelope can contain a systematic trim-domain bias even when refinement appears stable, so the generic result is explicitly a non-authoritative sanity estimate. Face-level signed-volume, area, triangle-count, surface-family, and orientation evidence remain useful for debugging.

## M8B bounded trimmed-cone repair

`Frustum<Hollow>` has two separate single-coedge, closed circular loops on each conical face. The generic periodic UV trim processor correctly projects each loop to a full angular ring, but then interprets the second ring as a hole. In a periodic domain those two full rings cancel the fill mask, producing zero triangles. This was a trim-role error, not a cone equation, apex, or material orientation error.

The admitted repair is intentionally narrow. For exactly two closed, full-period `Circle3` trim edges on a cone, it verifies coaxial circle normals, center-on-axis residual, radius against `v × tan(semiAngle)`, four support samples per circle, non-apex levels, and a nondegenerate generator interval. The ordered trim levels come from actual face topology and edge geometry. It then evaluates the kernel convention `S(u,v) = Apex + v Axis + v tan(alpha) (cos(u) X + sin(u) Y)` on a deterministic structured strip with periodic `u in [0, 2π]`; the seam rings coincide numerically. The grid is oriented to the support normal, while the mass evaluator applies the binding's `SameSense` once for curved faces. Thus the outer cone uses `SameSense=true` and the inner cone uses `false` while sharing the same geometric convention.

The direct and STEP-reimported `Frustum<Hollow>` both now have five participating faces. At the declared maximum 512 angular/generator segments per cone, the refined artifact result is volume `47273.485526 mm³`, area `47817.126229 mm²`, centroid approximately `(0,0,41.687779) mm`, and a conservative bound `19.126850 mm³`. Its analytic witness is `47274.672097 mm³`; delta `-1.186572 mm³` is within the bound. Cone patches contain 524,288 triangles each, have machine-evaluation support residuals (the focused test requires `<= 1e-8 mm`), and have opposite material-facing senses.

The older CLI `analyze volume` planar signed-shell path remains a separate, exact planar-only route and is not used by `verify`. Its four historical Box/derivation/PMI failures were caused by applying projected-loop orientation after the planar triangulator had already oriented the triangles; the duplicate sign inverted valid faces. That second sign is removed and the affected regression filter now passes. `verify` / `BrepMassProperties` may report the generic curved estimate, but callers must treat it as a sanity cross-check rather than occupied-volume authority.

## External CAD Assistant observation

`aetheris verify model.step --cad-assistant` resolves CAD Assistant in this order: `--cad-assistant-path`, `AETHERIS_CAD_ASSISTANT_PATH`, then two documented Program Files locations. It launches only the resolved executable with the one requested artifact, observes a fresh owned process, waits for a stable responsive main window, captures native PNG evidence where a visible Windows desktop permits it, then requests a clean close only for that owned process.

The report records executable path, artifact SHA-256, start/end times, arguments, timeout outcome, responsive-window observation, stability time, raw unavailable progress/display fields, screenshot paths, and diagnostics. It deliberately records `DisplayedWithWarnings` rather than a clean visual pass because CAD Assistant does not expose a stable import-progress/display-ready automation contract here. No visual geometric correctness is inferred from process or screenshot success. A secondary screenshot is an observation only; camera rotation is not synthesized without a stable public automation control.

Statuses are explicit: `Unavailable`, `LaunchFailed`, `ImportFailed`, `TimedOut`, `Displayed`, `DisplayedWithWarnings`, and `InspectionCompleted`. Normal CI does not request CAD Assistant; `--require-external` makes unavailability return exit code 2 for opt-in external jobs.

## Admission

The JSON report keeps producer evidence, independent B-rep evidence, STEP reimport analysis, external observation, and an overall admission separately. Artifact-only verification cannot reconstruct compiler preflight evidence, so it reports that honestly. CAD Assistant unavailability is `ExternalInspectionPending`, not a failure or a full pass.

The 2026-08-04 canonical run recorded all ten artifacts as topology-enclosed, orientation-consistent, `NumericalWithBound`, and STEP-reimport valid. Their exact hashes and external states are in `m8-regression-manifest.json`; CAD Assistant discovery returned `Unavailable` for each, so the correct overall state is `ExternalInspectionPending`, not visual admission. Current limitations are deliberately bounded: manifest orchestration remains shell-driven, compiler-originated producer evidence aggregation is artifact-only, CAD Assistant semantic status/control automation is unavailable, and arbitrary conic holes/partial angular trims remain unsupported. The next geometry milestone should add only the next bounded primitive family after external CAD availability is established.
