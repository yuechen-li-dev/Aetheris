# M3 validation

M3A remains covered by
`AssemblyM0Tests.TemplateAssembly_ReusesDefinitionAndExposesOnlyIntentionalSurface`.

M3B focused coverage includes:

- `AssemblyM3Tests`: cached local solve, local Mate/assertion ownership, repeated
  occurrence transform composition, world Axis/Plane/Point, public summary edge,
  expanded contributors, parent assertion, hidden-path rejection, and internal
  underconstraint rejection.
- `AssemblyM2InteropTests.TemplateAssembly_Ap242RoundTripPreservesNestedReuseAndComposedTransforms`:
  reusable nested AP242 definition, logical occurrence expansion, transforms,
  and exact shared child geometry.
- `KernelApiIntegrationTests.AssemblyDisplay_TemplateSubassemblyPublishesNestedTreeSelectionAndPublicSurface`:
  nested Cadmata packet, isolated selection groups, public inspector, Mate public
  endpoint, and tolerance details.

The canonical source is
`fixtures/Regression/Assembly/bearing-module-family-with-legacy-placement.firmament`; the persisted AP242 round-trip
artifact is `docs/development/milestones/assembly/artifacts/m3/bearing-module-ap242.step`.
