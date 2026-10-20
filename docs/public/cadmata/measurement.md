# Measure in Cadmata and Helios

Open **Measure** in the viewport, choose a measurement mode, then click two visible
faces. The first pick becomes **A**, the second **B**. Further clicks replace B;
click the A or B button to replace that reference. Orbit between picks to reach
the other side of a part. Dragging does not select a measurement point.

**New measurement** clears both references. **Esc** closes the tool. **M** toggles
it while the viewport workspace has keyboard focus. Switch between millimetres
and inches without recalculating geometry. **Copy result** copies the underlying
millimetre values, reference identities, witnesses and interpretation as JSON.

| Mode | What the number means | Typical use |
| --- | --- | --- |
| Surface points | Straight-line distance between the actual picked points | Check an offset; inspect signed world ΔX/ΔY/ΔZ |
| Support planes | Perpendicular spacing between parallel infinite planes supporting the selected planar faces | Plate thickness or planar seat spacing |
| Axes | Shortest separation between infinite axes of cylindrical, conical or toroidal faces | Hole/shaft centre spacing; cylinder diameter is also shown |

Nonparallel support planes show their acute angle instead of inventing a parallel
spacing. Axis mode also reports the acute angle. Plane/axis witnesses may lie
outside the finite faces because these modes measure their supporting geometry.
None of these modes claims general minimum clearance between arbitrary trimmed
faces or bodies. Pick a cylindrical hole wall for axis spacing, rather than the
surrounding planar face.

Gold witness crosses and a connecting line show the measured world-space segment.
The panel retains occurrence and face identities in the A/B button tooltips, so
two instances of the same part remain distinguishable. Zero distance is allowed;
it does not prove that the selected faces or bodies coincide.

## Geometry and revisions

The kernel resolves references against the displayed build's BRep, including rigid
assembly occurrence placements. Analytic surface picks use the kernel's existing
trim admission policy. An unsupported face produces an explanation. In Surface
points mode, **Allow approximate display points** explicitly permits a mesh-based
pick; the entire result is then labeled **Approximate display**. Such results
are unsuitable for checking manufacturing tolerances. Display precision is not
an accuracy or tolerance certification.

Measurements are read-only queries. They do not author Firmament dimensions, PMI,
constraints, or assertions. A source edit, rebuild, document transform, or loading
transition clears or disables the result. Late replies from an older context
are discarded. Helios retains its last valid model after a failed build, but
measurement stays disabled until the current source builds successfully.

This pass supports Helios Parts and static Assemblies, and Cadmata document bodies
and imported STEP assemblies, through both Telos and WebGL rendering. Return a
Helios motion clip to its built pose before measuring. Scene/mesh-only builds and
Cadmata's stateless Firmament gallery previews do not yet retain a BRep query
context; the tool explains that boundary. Import the preview's STEP into Cadmata
when a retained document measurement context is needed.

The browser SDK exposes `ModelSession.measure(request)`. Cadmata exposes
`POST /api/v1/documents/{documentId}/measure` with `{ revision, measurement }`;
use the document summary's revision. Both adapters call the same geometry query
owner and preserve named errors such as `measurement-stale-revision`,
`measurement-surface-unsupported`, `measurement-plane-required`, and
`measurement-axis-required`.
