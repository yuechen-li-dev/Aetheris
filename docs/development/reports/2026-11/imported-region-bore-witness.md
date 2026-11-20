# Local bore insertion on imported stock

The kernel experiment inserts a fresh round through-bore in an imported solid
between two explicitly selected, outward-facing Z-normal planes. The region
is the bore radius plus clearance, across the thickness between the supports.
Admission checks the trimmed material domain at both ends. Bounding boxes supply
the first exclusion test. An overlapping line segment or trimmed circle is
checked against the round footprint, with import uncertainty added to the
clearance. A cylinder can also be excluded by radial separation if its tilt
cannot close the gap across the stock thickness. Other overlapping faces still
cause a refusal.

The existing through-hole recipe builds a temporary coupon at the requested
diameter and stations. The splice takes its cylindrical wall and two opening
loops. It adds them to the imported body, with fresh IDs and freshly constructed
face-local trims. The shell gains a face and each support gains an inner loop.
Existing faces keep their surface bindings and prior loops. Existing edges,
vertices, coedges and pcurves retain their identities and geometry.

## Evidence

- A STEP-imported 40 × 30 × 8 mm plate acquired a 6 mm bore. The test checks
  retained topology and bindings, the unchanged source export, and the bore
  center and radius after a strict STEP export and reimport.
- The reimported plate accepted a second bore whose envelope overlaps the first
  bore's bounds. The actual openings and walls clear each other. The same stock
  refused an intersecting bore, and blank stock refused breakout, tangency and
  placement outside the support.
- The FreeCAD cylinder accepted a concentric bore and exported back to STEP
  with both analytic radii intact. A separate placement cleared the rim by
  0.05 mm and was refused against the required 0.1 mm clearance.

The first two-bore attempt failed during pcurve recovery. Recovery was
rechecking retained trims using its own assumptions about parameter sense.
Constructing pcurves only for new uses and checking the complete result with
the standard pcurve validator admitted the separated bore while preserving
every retained trim.

Run the witness and the original move/remove regressions together:

```powershell
dotnet test Aetheris.Kernel.Core.Tests -c Release --filter 'FullyQualifiedName~ThroughHoleInsertionTests|FullyQualifiedName~BrepThroughHoleRevisionTests'
```

The original EditableV8 mesh-order mismatch reproduced at parent `7e335f15`.
EditableV8 now uses a traced Concept Struct profile for concentric annular
stock. The valve stem and retainer share a nominal stem diameter, with the
retainer bore derived from its radial running clearance. The assembly test
checks these dimensions and the two open ends directly on the compiled BRep.
The mesh archive remains available as historical evidence. Firmament rejected
unary negation of a named length in a template argument. The lower station
uses the admitted dimensional subtraction `0mm - halfThickness`.

The full demo also exposed a scope boundary. Its standalone valve stack
reported `firmament-v2-static-scalar-consumer-unsupported` when the stem
diameter lived in the construction module imported by the wireform. The
stem and retainer now import the diameter from their own module. The standalone
valve stack has a test for both spring occurrences.

All five demo stages built. `aetheris asm inspect` accepted the emitted baseline
project. Comparison against parent `7e335f15` found identical AP242 data sections
for all four STEP exports and zero difference in the solved rest matrices for
the three motion exports, including all 171 bindings in the eight-cylinder build.

A straight chamfer needs a patch with two trimmed support faces and endpoint
faces that can close the replacement. The bore witness stays internal to Core.

The Windows Release build and final serial solution run passed with 6,787
tests passed, two skipped, and zero failures. That includes 13 focused bore
cases and all four EditableV8 tests. An earlier parallel run hit seven display
tessellation timeouts. All seven passed when replayed one project at a time.
Verification logs are under `artifacts/local/imported-region/`.
