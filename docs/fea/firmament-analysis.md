# Firmament linear-elastic analysis

The M5 analysis block is declarative and erases to AnalysisIR before mechanics execution:

```firmament
analysis LinearElastic PlateWithHole {
    body: plate
    material Steel {
        youngsModulus: 200GPa
        poissonRatio: 0.3
        density: 7850kg/m3
    }
    fixed Clamp {
        region: plate.face(-X)
        components: [X, Y, Z]
    }
    force Tension {
        region: plate.face(+X)
        vector: [10000N, 0N, 0N]
    }
    results: [Displacement, Strain, Stress, ReactionForce]
    lattice: [16, 8, 2]
}
```

`youngsModulus` accepts `Pa`, `kPa`, `MPa`, or `GPa` and normalizes to Pa. Resultant force components require N. Traction components and pressure require Pa. Lengths from native Firmament bodies normalize to meters. Poisson ratio is dimensionless and must lie in `(-1, 0.5)`.

`fixed` defaults to all three translation components; components may be selected individually. `traction` is force per area. `force` is a resultant distributed by the same exact-area traction integration. `pressure` uses `-p n_outward` on exact planar semantic faces; CIR material-side classification, not BRep `SameSense`, establishes outward direction.

Orientation and lattice adaptation are backend solve options and do not appear in AnalysisIR.

## Forge invocation

The existing Forge path is extended on `ForgeInvocation`:

```csharp
var result = host.LoadModule("Plate", source)
    .ResolveTemplate("PlateAnalysisTemplate")
    .Invoke("HostPlate")
    .Bind("AppliedForce", new ForgeReal(10000))
    .Analyze();
```

`Analyze()` expands the typed Template, compiles AnalysisIR, runs native mechanics, emits Abaqus input from that same IR, and returns `ForgeAnalysisInvocationResult`. The host never sees sparse internals.

An InlineStep Template uses a typed resource parameter:

```firmament
Template < Part: ImportedStep > Model ImportedBoxAnalysis {
    analysis LinearElastic ImportedBoxPull {
        body: imported
        bodyResource: Part
        // material, constraints, loads, lattice...
    }
}
```

Bind it with `ForgeImportedStep(resource.Name)` and `AddResource(resource)`. M5 validates canonical STEP evidence and six-face exact-box admission.

Diagnostics include invalid material, unresolved/nonplanar faces, invalid local frames/trims, no owned fragments, material-side ambiguity, area/resultant/moment mismatch, rigid modes, conflicting constraints, non-convergence, missing imported resources, unsupported imported CIR lowering, and invalid Abaqus output.
