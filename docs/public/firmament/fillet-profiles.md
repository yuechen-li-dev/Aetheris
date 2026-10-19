# Fillets and chamfers

Use `EdgeFinish<Fillet>` with `radius`, or `EdgeFinish<Chamfer>` with `distance`,
for ordinary CAD edge finishing. The existing `kind: Fillet` / `kind: Chamfer`
spelling has the same meaning. Name the engineering boundary and its support;
the planner selects a qualified construction from that source intent. Authors
do not need to replace a simple rim finish with a general sculpting blend.

For an extruded Profile, `target: Section.Outer; on: Top` selects its whole top
rim; `on: Bottom` selects its bottom rim. Qualified cases include straight
polygon rims, tangent line/arc rims, and a circular segment made from one arc
and its closing chord. The [D-profile fillet](../../../fixtures/Regression/EdgeFinishRobustness/d-profile-fillet.firmament)
and [D-profile chamfer](../../../fixtures/Regression/EdgeFinishRobustness/d-profile-chamfer.firmament)
are complete executable examples. [The angled plate](../../../fixtures/Regression/EdgeFinishRobustness/polygon-rim-fillet.firmament)
shows convex corners that are not right angles. These rim finishes preserve
the sharp vertical stock corners; they do not round every edge of the body.

The Profile/Compose materializer currently admits one `EdgeFinish` operation
per compiled construction. Requesting multiple finishes, including separate top
and bottom finishes, rejects with `ProfileBoundaryEdgeFinishSequenceUnsupported`.
It never compiles only the first requested finish. Uncalled Feature/Template
definitions are recipes, not requested operations. Primitive Box finish
combinations retain their separate admission rules.

The circular-segment cap comes from the inset circle and inset chord. Its finish
size must be strictly smaller than the segment's inradius, as well as the stock
thickness. For a semicircle of radius 10 mm, the inradius is 5 mm. A size of 5 mm
collapses the cap and rejects. The two finish surfaces share their real miter
curve: an exact quadratic parabola for Chamfer, and a certified cubic
representation within the kernel's 0.000001 mm linear tolerance for Fillet.
The support surfaces remain analytic planes, cones, cylinders and tori. The
finite edge representation is not an unqualified freeform fallback.

`build --json` exposes `edgeFinishPlanning` for the circular-segment construction:
the selected strategy, candidate admission/rejection traces, policy version and
maximum curve-deviation bound. Judgment chooses among supported constructions;
it cannot relax a failed clearance or support condition. Other sharp line/arc
junction families still reject until their shared intersection is qualified.
Non-right-angle reflex fillets and interacting/collapsing finishes are not
silently approximated. `endClearance` applies to an open chain, and is rejected
on a closed loop, which has no endpoints.

General surface blending remains a low-level sculpting operation with explicit
supports, trims and continuity contracts. It is not a prerequisite for these
ordinary CAD finishes. See [sculpting](sculpting.md) for that separate authority.

## Bounded concave fillet section laws

The existing concave fillet kernel admits `BoundedFilletProfile.Circular` (default) and `BoundedFilletProfile.CurvatureContinuous`. Both reuse the same trusted-source preflight, selected edges, setback/contact points, source loop, shared edges, and shell construction. The smooth mode replaces the circular quarter-section with a fixed degree-five polynomial and its straight extrusion surface.

The size remains the existing `radius` argument for compatibility. In smooth mode it means the equal setback on both planar supports, not constant curvature radius. No author control cage or equation interpreter is involved. Three collinear controls at each endpoint give zero endpoint normal curvature; the straight axial direction has zero curvature as well. Thus the blend matches both planar supports with G2 continuity along its full contact edges. Its top/bottom termination edges remain sharp.

This mode currently applies to the kernel's existing one/two-edge, orthogonal concave selections on trusted planar source bodies. It does not add support for curved rails, arbitrary face pairs, or circular termination contexts. Unsupported circular termination with smooth mode reports `BoundedFilletSmoothCylindricalTerminationUnsupported`.

The existing explicitly versioned V1 file adapter exposes the mode on `op: fillet` as `profile: CurvatureContinuous`; see [the regression witness](../../../fixtures/Regression/Fillet/concave-curvature-continuous.firmament). This is compatibility-path qualification, not a new recommended V1 authoring surface. Invalid mode names and numeric enum values fail.

The canonical V2 Profile `EdgeFinish` contact-shell path still admits only its circular profile. An explicit `Profile: CurvatureContinuous` request fails with `ProfileBoundaryFilletProfileModeUnsupported:CurvatureContinuous:polynomial-contact-shell-required`. It does not silently perform a circular fillet. [The isolated smooth-loop fixture](../../../fixtures/Invalid/Fillet/smooth-loop-contact-shell.firmament) records the still-unsupported implicit EdgeFinish form.

For a raised region on parallel planar supports, use [Plateau](plateaus.md). That operation supplies explicit height, width, and footprint semantics and reuses the same quintic quarter law with polynomial contacts, support-face trims, and closed-loop topology. The implicit EdgeFinish form above remains rejected; it lacks those two-support inputs.
