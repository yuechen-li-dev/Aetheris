# Imported STEP edge finish plans in Firmament

The same pinned import path used for other exact edits now binds single-edge
and outer-loop finishes through Modify and EdgeFinish. A STEP edge or face
entity must resolve exactly once in the current body. Each operation receives
the previous operation's qualified body and retains the pinned source.

JudgmentModel delegates policy selection to JudgmentEngine. It admits the three
geometry plans only after their own incidence, section and clearance proofs.
Admitted plans rank by the share of original faces whose boundaries they retain.
The score cannot make a failed proof eligible. On failure the compiler reports
the planner's reasons at the authored feature. The selected plan builds new
pcurves only where it changes topology. The export gate checks the whole body
and reimports AP242 at the original import tolerance.

| Plan | Geometry | Replacement |
| --- | --- | --- |
| straight-edge | Convex straight edge between planar faces with perpendicular end caps | One plane or constant-radius cylinder, with line or circle ends |
| circular-rim | One whole outer circle on a cap adjoining an exterior cylinder with one axial seam | One cone or ring torus |
| polygon-rim | Full convex planar polygon rim with perpendicular sides and straight axial corners | Planar chamfer strips or cylindrical fillet strips with exact elliptical miters |

The finish trims reside on the imported body. All three plans share an assembler
that preserves unaffected bindings and trims. The polygon plan checks every
offset side and the remaining material under the cap, and tests each strip
against existing trims and unrelated faces. A drilled plate retains its bore
when clearance is proved. A cut that comes within 0.05 mm of the bore fails
against the required 0.1 mm clearance. The same plan also builds on a rotated
convex pentagon with non-right corners.

Current admission requires one qualified solid, a pinned source and explicit
geometry-only import. Circular rims use one complete edge. Polygon loops use
mitered joins on strictly convex caps. Reentrant edges, oblique straight-edge
caps, split circular rims, arbitrary open chains and intersecting edits receive
refusals. Conservative support bounds can refuse some close configurations.

The public examples are in fixtures/Regression/InlineStep/production. CLI
validation, AP242 samples and test logs are under
artifacts/local/imported-edge-finish/.

The Windows Release build passed. The full serial suite recorded 6,807 passes,
two skips and zero failures. All six CLI combinations validated, built and
reopened as enclosed manifolds. The focused Core and Firmament runs passed
23 and 34 cases respectively, including the imported-hole regressions.
