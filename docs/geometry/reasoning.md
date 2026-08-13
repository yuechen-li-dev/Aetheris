# Authored geometry reasoning

Status: AETHERIS-GEOMETRY-REASONING-M0

## Architecture and authority

Aetheris separates four layers:

1. authored bounded geometry, including parameter domain, evaluation, first jet, identity, and provenance;
2. predicates that inspect that geometry and return explicit evidence;
3. optional realization into a supported surface representation;
4. BRep topology and STEP serialization.

`Aetheris.Geometry.BoundedParametricPatch3` is the public object at layer 1. It is not a Panel and carries no material, thickness, fabrication, or material-side meaning. `Aetheris.Surfacing.PanelIr` remains the manufacturing/CAD wrapper and exposes its source through `AuthoredPatch`. Expression-authored patches evaluate `P(u,v)`, `dP/du`, `dP/dv`, and a normal where the first jet is non-singular. The derivatives come from forward automatic differentiation; users do not author them independently.

The M0 domain is rectangular: `u in [u0,u1]` and `v in [v0,v1]`, with finite, strictly increasing limits. Arbitrary trim-domain topology and second jets are deferred.

## Representation is not evidence

`GeometryRepresentationKind` describes the object being evaluated: `AnalyticExpression`, `ProceduralParametric`, `CertifiedApproximation`, `SampledApproximation`, `MaterializedBRep`, or `ImportedGeometry`.

`PredicateEvidenceKind` describes one conclusion: `Structural`, `Certified`, `ToleranceBounded`, `Sampled`, `Heuristic`, or `Unknown`.

These are deliberately separate. `AnalyticExpression` means a finite-numerical expression representation, not algebraically exact arithmetic. `Certified` is reserved for a conclusion supported by conservative, outward-rounded interval bounds. A procedural patch can in principle receive certified evidence from a future certified evaluator; its representation does not predetermine the evidence class.

## SignedSideQuery

`SignedSideQuery.Query(patch, plane, policy)` reduces the question to

```text
g(u,v) = dot(plane.normal, P(u,v) - plane.origin)
```

and returns a `SignedSideResult` containing classification, evidence kind, policy/tolerance, queried domain, identity and provenance, observed or certified bounds, ordered witnesses, subdivision/sample statistics, and typed diagnostics.

The classifications mean:

- `Positive`: the reported evidence supports the whole domain being strictly above the tolerance band.
- `Negative`: the analogous whole-domain negative conclusion.
- `Crossing`: strict positive and strict negative points or certified regions have been established. The authored expression patch is continuous, so the intermediate value theorem establishes a zero between them. The evidence kind states how the strict-side facts were obtained.
- `Touching`: contact without crossing has been established. M0 exposes the contract but does not manufacture this conclusion from samples and does not certify tangency.
- `Unknown`: valid geometry did not yield enough evidence. This is a normal result, not malformed geometry.

### Sampled policy

The sampled policy uses a deterministic row-major tensor grid including all domain boundaries. It reports observed minimum/maximum distance and at most one ordered negative, contact-candidate, and positive witness. A near-zero sample is only a `contact-candidate`; without both strict signs the classification remains `Unknown`. This policy always returns `Sampled` evidence and can never return `Certified`.

### Certified interval policy

The bounded evaluator supports constants, parameters, addition, subtraction, multiplication, division whose denominator excludes zero, integer powers, sine, and cosine. Arithmetic bounds are expanded with adjacent floating-point values. Trigonometric bounds include every enclosed extremum.

The query evaluates the whole root domain, then subdivides each inconclusive rectangle into four children in deterministic southwest, southeast, northwest, northeast order. It stops at explicit depth and leaf budgets. All resolved positive leaves certify `Positive`; all resolved negative leaves certify `Negative`. One strict positive region plus one strict negative region certifies `Crossing` by continuity. Unsupported expressions and exhausted budgets return `Unknown`; the certified policy never silently samples.

M0 does not certify touching. A tangent patch therefore normally returns `Unknown` with a budget diagnostic, while a separate sampled query can locate a contact candidate.

## Semantic obligations

`SignedSideExpectation` is a generic compile-time-style obligation over a query result and minimum evidence strength. It validates the result and reports a rejection reason. It is not a realization request and cannot create trims, curves, faces, or BRep topology.

## Intersection authority rule

Generic numerical intersection is a query/evidence operation. Its numerical points or curves do not author semantic topology, trimming authority, or a surprise engineering object.

Constructive intersection is allowed inside an admitted materializer only when authored construction intent already fixes the construction family, expected topology, and expected analytic intersection family. Numerical work may validate that topology; it does not invent it.

This distinction preserves bounded routes such as the signed-permutation transverse cone/world-Z plane hyperbola. The restricted-field/marching-squares prototype remains contained: numerical contours are explicitly `NumericalOnlyNotExportable`, analytic snaps are candidate-only, and its records state that STEP export and BRep topology emission were not performed.

## Public use

```csharp
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Math;

var patch = new BoundedParametricPatch3(
    "fixture-clearance",
    new ParametricDomain(new(-1, 1), new(-1, 1)),
    new SurfacePointExpression(
        SurfaceExpression.Multiply(SurfaceExpression.Length(50), SurfaceExpression.U),
        SurfaceExpression.Multiply(SurfaceExpression.Length(30), SurfaceExpression.V),
        SurfaceExpression.Add(SurfaceExpression.Length(8),
            SurfaceExpression.Multiply(SurfaceExpression.Length(1), SurfaceExpression.Power(SurfaceExpression.U, 2)))),
    "cad:mold-tooling-clearance");

var plane = new Plane3(Point3D.Origin, Direction3D.Create(new Vector3D(0, 0, 1)));
var result = SignedSideQuery.Query(patch, plane, SignedSidePolicy.Certified());
```

Normal Aetheris code references `Aetheris.Geometry` directly. Forge extension authors may do the same or call `Aetheris.Forge.KernelSDK.KernelSdk.QuerySignedSide`; KernelSDK is an exposure surface, not the owner.

## Limitations

M0 has no second jets, curvature, contact order, generic tangency certificate, arbitrary trimmed parameter domains, generic curve/surface intersection, CAS, or theorem-specific vocabulary. Procedural patches have public first-jet evaluation but require a future certified evaluator before the interval policy can certify them. No generic predicate materializes intersection curves or mutates topology.
