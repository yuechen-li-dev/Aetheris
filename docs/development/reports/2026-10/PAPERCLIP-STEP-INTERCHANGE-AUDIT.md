# Paperclip STEP interchange investigation

2026-10-24. Diagnostic phase; no production exporter or geometry changes were made during this investigation. The subsequent implementation and qualification are recorded in [Paperclip STEP export fix](PAPERCLIP-STEP-EXPORT-FIX.md).

## Verdict

**Success for diagnosis.** The supplied Aetheris AP242 has inconsistent boundary winding. Reversing its boundary walks, without changing any surface, radius or coordinate, restores the expected paperclip in SolidWorks Design Professional for Makers 2026 SP3.0. Changing only the illegal edge basis references does not restore it. This isolates winding as the immediate cause of the observed complementary torus trims.

The original also contains a separate STEP conformance defect: three advanced-face edges reference `TRIMMED_CURVE` rather than an admitted underlying curve. A successful display in another reader does not qualify either defect away. A permanent fix still needs implementation and regression qualification.

## Evidence

Inputs, preserved unchanged:

| Input | SHA256 |
| --- | --- |
| `Downloads/maximum-paperclip-ap242.step` | `51C64F91007074CAC28C500E0CFAAC014E305491A368378AD65003EF5D526E1F` |
| `Downloads/maximum-paperclip-ap214-sw.STEP` | `2CE7A4235BABFBE9EDC408206CFAD3DDEFB70B166AC1469D49C23763C5C8203D` |

Generated evidence is under ignored `artifacts/local/release-2-readiness/paperclip-interchange-audit/`: CLI analysis JSON, `audit.py`, input hashes, `occt-comparison.json`, `torus-domain-comparison.json`, and three controlled STEP variants. Independent inspection used isolated cadquery-ocp 7.7.2 installed only in that artifact directory; no production dependency was added.

| OCCT measurement | Original AP242 | Supplied SW AP214 |
| --- | ---: | ---: |
| Faces | 9 | 16 |
| Torus faces | 3 | 6 |
| Y bounds, mm | -4.5 to 20.5 | 0 to 15 |
| Signed volume integral, mm³ | +75.1619066803201 | -15.9442802737839 |
| Surface area, mm² | 302.218423048075 | 302.218423048075 |

The negative integral is an orientation warning, not a physically meaningful negative material volume. The original nominal volume independently follows from a 0.5 mm wire radius and centerline length `58 + 12π` mm, giving `75.1619066803201 mm³`.

Both files retain the same analytic torus centers, axes and radii (major radii 3, 4 and 5 mm; minor radius 0.5 mm). OCCT reconstructs the original major-angle intervals as `[0, π]`, but the SW result as `[π, 2π]`. The latter splits each torus into two minor-angle halves. At the outer bend, the major-arc midpoint changes from `(5, 20, 0.5)` to `(5, 10, 0.5)` mm. This is a different trim domain, not a missing torus implementation or a changed torus equation.

## Controlled SolidWorks experiment

Opened local diagnostic copies through the existing SolidWorks UI. Declined the optional Import Diagnostics operation for each copy; did not alter global import settings. Existing user documents and source files were not saved or changed. Diagnostic imports remain unsaved.

| Variant | Exact intervention | Observed SolidWorks result |
| --- | --- | --- |
| Existing original import | None | Bent-back, overlapping surface appearance matching the reported failure |
| `legal-basis-only.step` | Three `EDGE_CURVE` references changed to their underlying `CIRCLE` entities | Still broken |
| `winding-only.step` | Reverse each `EDGE_LOOP` order and invert each `ORIENTED_EDGE` sense | Normal paperclip, one reported solid body |
| `legal-basis-and-winding.step` | Both interventions | Normal paperclip, one reported solid body |

These are direct UI witnesses, not SolidWorks mass-property or re-export qualification. Independent OCCT inspection of every corrected variant preserves the original volume, area and bounds. No claim is made that all downstream SolidWorks engineering operations are qualified.

## Concrete defects and ownership

### Boundary winding

Original edge `#21` is a forward circle about +Y. End-cap face `#148` has a plane normal of -Y, a forward oriented edge, a true outer-bound orientation, and true face same-sense. Its boundary therefore disagrees with its declared outward normal. The other end cap has the analogous issue.

The STEP topology convention requires the cross product of the face normal with the boundary tangent to point into the face. See the [geometric and topological representation resource, face and face_surface semantics](https://www.steptools.com/stds/smrl/data/resource_docs/geometric_and_topological_representation/sys/5_schema.htm).

The relevant source owner is `Aetheris.Kernel.Firmament/Materializer/WireForm.cs`, especially side-face and cap loop construction around lines 411–419. Correct construction should agree with analytic surface normals. Do not globally reverse all exports: the isolated specimen reversal is causal evidence, not a safe general production algorithm.

### Advanced-face edge basis

Edges `#36`, `#76` and `#114` reference trimmed curves `#35`, `#75` and `#113`. The [advanced_face EXPRESS restriction](https://www.steptools.com/docs/stp_aim/html/t_advanced_face.html) admits LINE, CONIC, POLYLINE, SURFACE_CURVE and B_SPLINE_CURVE as edge geometry; TRIMMED_CURVE is outside that set.

The exporter owner is `Aetheris.Kernel.Core/Step242/Step242Exporter.cs`, around line 1048, which deliberately emits trimmed curves for partial circles. Review other trimmed-basis emissions at the same owner. Keep true analytic circles and edge orientation; use conforming surface-local pcurves/seam representations where periodic branch information is needed.

### Qualification gaps

- Both supplied files receive `Qualified` from the current Aetheris CLI analysis; OCCT also reports both valid. Neither result proves the requested shape survived interchange.
- [OCCT STEP import normally runs shape processing](https://github.com/Open-Cascade-SAS/OCCT/blob/master/dox/user_guides/step/step.md). Requesting an unconfigured post-processing sequence gave the same measurements here, but translation itself can reconstruct topology. This is not a raw, repair-free conformance check.
- The original contains no pcurves. That alone is not established as a conformance violation; explicit periodic-face trim information is an exchange robustness consideration.
- Aetheris CLI bounds for the original omit the wire-radius contribution at the bend apex (Y -4 to 20 rather than -4.5 to 20.5). Bounds in the table come from independent OCCT inspection.
- Fusion/ACIS behavior is user-reported in this investigation; it was not independently rerun. SolidWorks import and the OCCT comparison were rerun.

## Permanent repair boundary

1. Correct WireForm boundary winding at construction and add a raw exported-loop/normal witness before reimport can hide it.
2. Emit conforming advanced-face edge bases; test partial arcs, full circles and periodic seams without losing intended arc selection.
3. Qualify paperclip volume, actual bounds, topology and analytic surfaces through export/reimport and real SolidWorks import. Preserve original analytic geometry.
4. Run the relevant full .NET regression lane when production code changes. No .NET build/test rerun was warranted for this investigation-only report; no prior test totals are claimed as new evidence.

SolidWorks could diagnose malformed input more helpfully, but the immediate repair belongs in our emitted topology. This investigation does not establish a general Parasolid torus defect.
