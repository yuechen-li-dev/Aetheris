# Through-hole parity

The canonical box/cylinder through-hole is routed by existing Boolean policy to
`ThroughHoleRecipeRequest`, then `ThroughHoleConstructionRecipe`. The retained
`BuildRecognizedThroughHoleLegacy` seam permits direct comparison.

Canonical result: 12 vertices, 15 edges, 7 faces, 9 loops, 30 coedges, 15
curves, and 7 surfaces (six planar, one cylindrical). Entry and exit faces each
own one inner circular loop; the wall owns its rings and periodic seam.

Direct recipe, legacy seam, and `BrepBoolean.Subtract` have identical topology
counts, bindings, analytic supports, orientations as serialized, construction
history object, and exact STEP text/SHA-256. STEP reimport passes binding and
manifold preflight. No historical hash was fabricated; equality is measured
against the retained implementation on every test run.

The real Firmament source was also rebuilt and inspected with Aetheris CLI. Its
STEP SHA-256 was `9183b3ed6747b0d754910bec5cd60c0affce08f0f43310e0623477ab7fc1fbe4`;
CLI reported 12 vertices, 15 edges, 7 faces, six planes, one cylinder, and
`enclosed-manifold`.
