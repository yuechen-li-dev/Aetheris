# Surfacing Module

## Surfacing is not synonymous with NURBS

Aetheris authors a surface using the relationship that explains it. The intended ladder is:

1. named mathematical construction;
2. explicit unit-aware parameter equation;
3. bounded non-rational B-spline materialization or local refinement;
4. SubD later;
5. SDF/FRep later.

There is no rational weight in `BSplineSurfaceWithKnots`, no rational surface API, and no NURBS authoring path. The existing STEP rational recovery lanes remain import-only analytic recovery and are not surfacing authoring facilities.

## M1 surface authority

`ParametricSurfaceIr` owns a stable identity, rectangular `u/v` domain, construction kind, source provenance, and a `Point(u,v)` expression whose three outputs must have Length dimension. The deliberately small expression tree supports addition, subtraction, multiplication, division, integer powers, `sin`, and `cos`. `u` and `v` are dimensionless. Forward automatic differentiation evaluates `dP/du` and `dP/dv` from the same tree; oriented normals use `dP/du × dP/dv`, and a zero cross product produces an observable singular result.

The runtime API remains the mathematical construction authority. A representative explicit construction is:

```csharp
new ParametricSurfaceIr(
    "saddle",
    SurfaceConstructionKind.ParametricSurface,
    new(new(-1, 1), new(-1, 1)),
    new(
        Multiply(Length(40), U),
        Multiply(Length(30), V),
        Multiply(Length(12), Multiply(U, V))),
    "source identity");
```

Named constructors cover `HyperbolicParaboloid`, `ParabolicCylinder`, `EllipticParaboloid`, and `Helicoid`. The first three pressure-test polynomial evaluation; the helicoid proves that exact procedural authoring does not have to fit STEP's native analytic subset.

The Firmament V2 bridge now accepts these constructions inside an ordinary `Panel` declaration. Template expansion occurs first, then the bounded Surfacing compiler produces the same typed construction IR and wraps it as a Panel. The user-facing hierarchy is mathematical construction -> bounded Panel -> semantic edges -> Panel network. See [Panels](panels.md).

## Ruled surfaces and transitions

`RuledSurface` evaluates `S(u,v) = (1-v) C0(u) + v C1(u)`. M1 admits non-degenerate lines, arcs, full circles, and validated non-rational B-spline curves. Correspondence is explicit `SharedNormalizedNativeParameter`; there is no hidden resampling or inferred orientation repair. Degenerate inputs produce typed diagnostics. Line-line is exact degree-(1,1), and reference-aligned coaxial circle-circle remains an exact cylinder or cone. Other admitted pairs retain exact procedural evaluation and receive certified non-rational STEP support.

`RuledTransition` is the two-section construction. Aetheris does not use “Loft” as the canonical term because straight rulings and authoritative ordered sections are different semantics.

Developability is evidence, not a synonym for ruled. The classifier samples the normalized scalar triple product `C0′ · (ruling × ruling′)` and reports `Developable` or `NonDevelopable` with method, maximum value, and sample count. Section and boundary patches remain `Indeterminate` until a bounded Gaussian-curvature classifier exists.

## SectionSurface

Two sections lower through `RuledTransition`. Three or more compatible line/arc/circle/non-rational-spline sections use deterministic shared normalized native parameters and Lagrange interpolation in section order. Section identity, order, correspondence, and provenance remain on `ConstructedSurfacePatch`; lowering does not erase the source into generic spline intent. Materialization adaptively samples a tensor grid until the sampled positional residual meets the requested tolerance (or the explicit 129×129 bound).

## BoundaryPatch

`BoundaryPatch` accepts four oriented boundaries: South and North run west-to-east, while West and East run south-to-north. Corner mismatch is diagnosed; it is not silently repaired. The patch uses transfinite/Coons-style interpolation internally but keeps `BoundaryPatch` as its construction kind. G0 is implemented. G1 requests are rejected with `surfacing-tangent-constraint-unsupported` unless a future compiler supplies adjacent-support tangent evidence; M1 does not fake tangent continuity from boundary curves alone.

## STEP and BRep

`PanelIr` is the authoritative bounded engineering object. Because the current AP242 writer accepts closed manifold-solid roots only, `SurfacePatchBrepMaterializer` creates a thin closed **export envelope**: the authoritative patch and its translated mate are bounded by their exact non-rational B-spline boundary curves, and four ruled side faces close the shell. This envelope is not Panel identity. It validates topology/geometry bindings, exports AP242 `B_SPLINE_SURFACE_WITH_KNOTS`, and reimports through the production importer. Certificates retain requested tolerance, maximum sampled residual, optional sampled normal deviation, grid dimensions, sampling policy, and procedural source identity. The six gallery artifacts are first-class Panels and all export/reimport through this envelope.

Raw local control-point refinement, BlendSurface, arbitrary trimming, G2, non-rational B-spline SurfaceMeshIR support, and Panel-specific Drawing remain outside this bounded slice. A future `Refine Surface` operation should preserve its parent construction and append refinement provenance; no control-net editor has been added.

Evidence is under [`docs/modules/artifacts/surfacing-m1`](artifacts/surfacing-m1/README.md).
