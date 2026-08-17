# Geometry and semantic features

Preview 3 supports bounded production routes, not an unrestricted general-purpose CAD kernel. Public native examples cover Box, Cylinder, Frustum, RoundedBox, line/arc profile extrusion and composition, semantic shaft/counterbore/countersink holes, slots, patterns, selected chamfer/fillet routes, hollow bodies, and a bounded cubic lattice. Consult the [support matrix](../reference/supported-features.md) before relying on a combination.

Use named features and selectors, never raw internal B-rep IDs in native authoring. [`box-holes-pmi-chamfer.firmament`](../../../fixtures/FirmamentV2/Canonical/valid/box-holes-pmi-chamfer.firmament) demonstrates two holes, an outer-boundary chamfer, and PMI surviving the same AP242 build. [`profile-compose-l-bracket-counterbore-pmi.firmament`](../../../fixtures/FirmamentV2/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament) demonstrates a profile-composed part, pattern holes, a counterbore, a chamfer, and shaft-diameter PMI.

`HoleDiameter` on a `Hole<Counterbore>` targets the shaft `Diameter`; `CounterboreDiameter` remains a distinct feature field and has no separate public PMI record kind in Preview 3. Pattern generation supports geometry as a semantic whole; authored names for generated instances are not a stable public instance-selector family in Preview 3.
