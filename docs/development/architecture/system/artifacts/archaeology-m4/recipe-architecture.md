# Recipe architecture

```text
recognition / Judgment / SafeBooleanComposition
    -> ThroughHoleRecipeRequest | PolygonalThroughCutRecipeRequest
    -> ThroughHoleConstructionRecipe | PolygonalThroughCutRecipe
    -> BrepLoopBuilder / BrepFaceBuilder / BrepShellAssembler / validation
    -> BrepBody -> STEP validation
```

Requests contain recognized roots, feature descriptors/ordered footprints,
bounded span, tolerance, and only the history needed by the construction. They
never accept two arbitrary bodies. Results are `KernelResult<BrepBody>` and
direct-contract failures are typed `KernelDiagnostic` values.

`IntersectionQuery` and other geometry queries remain observational. No new
Boolean family, generic intersection topology, public Surgery API, reflection,
or general recipe framework was added. M1's M4/M5 sequencing remains current.
