# Bounded concave fillet profiles

The existing concave fillet kernel admits `BoundedFilletProfile.Circular` (default) and `BoundedFilletProfile.CurvatureContinuous`. Both reuse the same trusted-source preflight, selected edges, setback/contact points, source loop, shared edges, and shell construction. The smooth mode replaces the circular quarter-section with a fixed degree-five polynomial and its straight extrusion surface.

The size remains the existing `radius` argument for compatibility. In smooth mode it means the equal setback on both planar supports, not constant curvature radius. No author control cage or equation interpreter is involved. Three collinear controls at each endpoint give zero endpoint normal curvature; the straight axial direction has zero curvature as well. Thus the blend matches both planar supports with G2 continuity along its full contact edges. Its top/bottom termination edges remain sharp.

This mode currently applies to the kernel's existing one/two-edge, orthogonal concave selections on trusted planar source bodies. It does not add support for curved rails, arbitrary face pairs, or circular termination contexts. Unsupported circular termination with smooth mode reports `BoundedFilletSmoothCylindricalTerminationUnsupported`.

The existing explicitly versioned V1 file adapter exposes the mode on `op: fillet` as `profile: CurvatureContinuous`; see [the regression witness](../../../fixtures/Regression/Fillet/concave-curvature-continuous.firmament). This is compatibility-path qualification, not a new recommended V1 authoring surface. Invalid mode names and numeric enum values fail.

The canonical V2 Profile `EdgeFinish` contact-shell path still admits only its circular profile. An explicit `Profile: CurvatureContinuous` request fails with `ProfileBoundaryFilletProfileModeUnsupported:CurvatureContinuous:polynomial-contact-shell-required`. It does not silently perform a circular fillet. [The isolated smooth-loop fixture](../../../fixtures/Invalid/Fillet/smooth-loop-contact-shell.firmament) records the still-unsupported implicit EdgeFinish form.

For a raised region on parallel planar supports, use [Plateau](plateaus.md). That operation supplies explicit height, width, and footprint semantics and reuses the same quintic quarter law with polynomial contacts, support-face trims, and closed-loop topology. The implicit EdgeFinish form above remains rejected; it lacks those two-support inputs.
