# Firmament linear-elastic analysis

> Semantic Value M1: boundary syntax is structurally bound to a
> `BoundaryRegionCapability` value and normalized to `SemanticRegionBinding`
> before AnalysisIR. Native analytic faces, canonical InlineStep faces, and
> exposed Recognize/Forge faces use the same normalizer. AnalysisIR retains
> stable ID, capability/binding evidence, and provenance; solver/mesh code does
> not receive the compiler semantic object or mesh IDs.

The M5 analysis block is declarative and erases to AnalysisIR before mechanics execution:

```firmament
Analysis LinearElastic PlateWithHole {
    body: plate
    material Steel {
        youngsModulus: 200GPa
        poissonRatio: 0.3
        density: 7850kg/m3
    }
    Fixed Clamp {
        region: plate.face(-X)
        components: [X, Y, Z]
    }
    Force Tension {
        region: plate.face(+X)
        vector: [10000N, 0N, 0N]
    }
    results: [Displacement, Strain, Stress, ReactionForce]
    lattice: [16, 8, 2]
}
```

`youngsModulus` accepts `Pa`, `kPa`, `MPa`, or `GPa` and normalizes to Pa. Resultant force components require N. Traction components and pressure require Pa. Lengths from native Firmament bodies normalize to meters. Poisson ratio is dimensionless and must lie in `(-1, 0.5)`.

`Fixed` defaults to all three translation components; components may be selected individually. `Traction` is force per area. `Force` is a total resultant distributed by the same exact-area traction integration. `Pressure` uses `-p n_outward` on exact planar semantic faces; CIR material-side classification, not BRep `SameSense`, establishes outward direction. Legacy lowercase semantic constructs remain accepted.

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
    Analysis LinearElastic ImportedBoxPull {
        body: imported
        bodyResource: Part
        // material, constraints, loads, lattice...
    }
}
```

Bind it with `ForgeImportedStep(resource.Name)` and `AddResource(resource)`. X1 also admits general closed BReps through kernel containment and the shared CIR/cut-cell path; six-face exact boxes retain their faster analytic lowering.

Diagnostics include invalid material, unresolved/nonplanar faces, invalid local frames/trims, no owned fragments, material-side ambiguity, area/resultant/moment mismatch, rigid modes, conflicting constraints, non-convergence, missing imported resources, unsupported imported CIR lowering, and invalid Abaqus output.
