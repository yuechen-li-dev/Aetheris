# Authored geometry reasoning

Status: AETHERIS-GEOMETRY-REASONING-M2

## Architecture and authority

Aetheris separates four layers:

1. authored bounded geometry, including parameter domain, first/second jets, identity, and provenance;
2. predicates that inspect that geometry and return explicit evidence;
3. optional realization into a supported surface representation;
4. BRep topology and STEP serialization.

`Aetheris.Geometry.BoundedParametricCurve3` and `BoundedParametricPatch3` are sibling public objects at layer 1. Neither owns B-rep topology or CAD intent. Curves and patches share identity, provenance, representation, first/second-jet interfaces, and explicit singularity states while retaining dimension-appropriate `CurveJet1`, `CurveJet2`, `SurfaceDifferential`, and `PatchJet2` results. `SupportsSecondJet` is a capability statement: expression geometry and supported analytic/non-rational spline adapters return raw second derivatives; first-jet-only procedural geometry does not silently finite-difference them.

Patch domains are rectangular and curve domains are one dimensional. All bounds are finite and strictly increasing. A reversed native curve support is mapped into an increasing public domain whose increasing parameter follows the authored edge direction. Curvature is computed from geometric differential forms, so parameter scaling and reversal do not change unsigned curve curvature, Gaussian curvature, or principal-curvature magnitudes; signed surface curvature follows orientation.

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

## Differential queries

`CurvatureQuery` consumes public second jets. Curve curvature uses `|P' × P''| / |P'|³`. Patch curvature forms the first and second fundamental forms, then reports oriented normal, Gaussian and mean curvature, ordered principal curvatures (`K1 >= K2`), stable principal directions where distinct, and normal curvature in an arbitrary tangent direction. `DifferentialPolicy` owns magnitude, metric-conditioning, and curvature tolerances.

Finite analytic evaluation returns `ToleranceBounded`, never `Certified` merely because the source is an expression. Singular tangents, tiny normals, ill-conditioned metrics, unavailable second jets, and indeterminate principal directions remain explicit. See [differential-geometry.md](differential-geometry.md).

## Limitations

M2 does not add contact order, a generic tangency certificate, arbitrary trimmed parameter domains, generic curve/surface intersection, CAS, rational splines/NURBS, or theorem-specific vocabulary. Section and boundary procedural patches remain first-jet-only unless an honest second evaluator is supplied. No differential query materializes intersection curves or mutates topology.

## Differential-geometry ladder

- M0 established bounded patches and evidence-aware signed-side reasoning.
- M1 adds bounded curves and a shared first-jet vocabulary.
- M2 adds curve `P''`, patch `Puu/Puv/Pvv`, curvature, and sampled Panel G1/G2 evidence.
- M3 pressure is closest point/distance. Certified intersection and generic contact classification/order remain later independent query milestones; chronology may change with product pressure.

M1 adds no generic curve intersection service. The constructive transverse-cone/world-Z hyperbola remains an admitted construction-specific analytic result; adapting its bounded trim does not grant it topology-authoring authority.
