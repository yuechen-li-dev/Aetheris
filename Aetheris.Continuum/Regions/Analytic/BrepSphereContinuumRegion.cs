using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Judgment;
using System.Diagnostics;

namespace Aetheris.Continuum.Regions.Analytic;

/// <summary>CIR owns occupancy; a real BRep sphere face owns exact boundary identity and evaluation.</summary>
public sealed class BrepSphereContinuumRegion : IContinuumRegion, IBoundsClassificationCapability,
    IBoundaryReferenceCapability, IBoundaryOffsetMapCapability
{
    private readonly SdfContinuumRegion _cir;
    private readonly BrepSphereBoundarySupport _support;

    public BrepSphereContinuumRegion(RegionId id, double radius, Transform3D transform)
    {
        Id = id;
        var result = BrepPrimitives.CreateSphere(radius);
        if (!result.IsSuccess || result.Value is null) throw new InvalidOperationException("Kernel sphere BRep construction failed.");
        BrepBody = result.Value;
        FaceId = BrepBody.Topology.Faces.Single().Id;
        Transform = transform;
        var root = new SdfTransformNode(new SdfSphereNode(radius), transform);
        _cir = new SdfContinuumRegion(id, root);
        Bounds = _cir.Bounds;
        BoundaryReference = new BoundaryReference("brep", $"{id}:sphere-face-{FaceId.Value}", FaceId.Value.ToString(), "closed-spherical-boundary");
        _support = new BrepSphereBoundarySupport(BoundaryReference, new ExactBrepBoundaryQuery(BrepBody, FaceId, transform));
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public BrepBody BrepBody { get; }
    public FaceId FaceId { get; }
    public Transform3D Transform { get; }
    public BoundaryReference BoundaryReference { get; }
    public ExactBrepBoundaryQuery ExactQuery => _support.Query;
    public double ExactVolume => (4d / 3d) * double.Pi * double.Pow(_support.Query.Radius, 3d);
    public double ExactArea => 4d * double.Pi * _support.Query.Radius * _support.Query.Radius;

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d) => _cir.Classify(point, tolerance);
    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d)
    {
        // Exact interval for the CIR sphere under the fixture's rigid transform.
        var center = ExactQuery.Center;
        var dx = center.X < bounds.Min.X ? bounds.Min.X - center.X : center.X > bounds.Max.X ? center.X - bounds.Max.X : 0d;
        var dy = center.Y < bounds.Min.Y ? bounds.Min.Y - center.Y : center.Y > bounds.Max.Y ? center.Y - bounds.Max.Y : 0d;
        var dz = center.Z < bounds.Min.Z ? bounds.Min.Z - center.Z : center.Z > bounds.Max.Z ? center.Z - bounds.Max.Z : 0d;
        var minimumDistance = double.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        if (minimumDistance > ExactQuery.Radius + tolerance) return ContinuumBoundsClassification.Outside;
        var maximumDistance = Corners(bounds).Max(point => (point - center).Length);
        if (maximumDistance < ExactQuery.Radius - tolerance) return ContinuumBoundsClassification.Inside;
        return ContinuumBoundsClassification.Cut;
    }
    public IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds) =>
        ClassifyBounds(cellBounds) == ContinuumBoundsClassification.Cut ? [BoundaryReference] : [];
    public IReadOnlyList<IAnalyticBoundarySupport> BoundarySupports(BoundingBox3D cellBounds) =>
        ClassifyBounds(cellBounds) == ContinuumBoundsClassification.Cut ? [_support] : [];

    private static Point3D[] Corners(BoundingBox3D b) =>
    [
        new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z),
        new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z),
    ];
}

public sealed class BrepSphereBoundarySupport : IAnalyticBoundarySupport
{
    public BrepSphereBoundarySupport(BoundaryReference reference, ExactBrepBoundaryQuery query)
    {
        Reference = reference;
        Query = query;
    }

    public BoundaryReference Reference { get; }
    public ExactBrepBoundaryQuery Query { get; }
    public double ExactArea => 4d * double.Pi * Query.Radius * Query.Radius;
    public Point3D Project(Point3D point) => Query.Project(point);
    public Vector3D MaterialSideNormal(Point3D boundaryPoint) => -Query.SupportNormalAt(boundaryPoint);

    public IBoundaryOffsetMap CreateOffsetMap(CellIndex cellIndex, BoundingBox3D cellBounds, int resolution,
        BoundaryOffsetMapErrorPolicy policy, BoundaryEvaluationCache? cache = null) =>
        Build(cellIndex, cellBounds, resolution, resolution, policy, cache, runOracle: true);

    public SampledBoundaryOffsetMap Build(CellIndex cellIndex, BoundingBox3D cellBounds, int resolutionU, int resolutionV,
        BoundaryOffsetMapErrorPolicy policy, BoundaryEvaluationCache? cache, bool runOracle, BoundaryMapBuildCosts? costs = null)
    {
        var stage = Stopwatch.GetTimestamp();
        var center = Center(cellBounds);
        var frame = Query.CreateMaterialSideFrame(center, materialInside: true);
        if (costs is not null) costs.LocalFrameMilliseconds += Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var corners = Corners(cellBounds);
        var raw = new BoundaryMapDomain(
            corners.Min(point => (point - frame.Origin).Dot(frame.TangentU)),
            corners.Max(point => (point - frame.Origin).Dot(frame.TangentU)),
            corners.Min(point => (point - frame.Origin).Dot(frame.TangentV)),
            corners.Max(point => (point - frame.Origin).Dot(frame.TangentV)));
        var domain = AddMargin(raw, 0.02d);
        stage = Stopwatch.GetTimestamp();
        var certificate = Certificate(domain, resolutionU, resolutionV, policy);
        if (costs is not null) costs.RuntimeCertificateMilliseconds += Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var frameKey = string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{Reference.SourceId}:{frame.Origin.X:R}:{frame.Origin.Y:R}:{frame.Origin.Z:R}:{frame.Normal.X:R}:{frame.Normal.Y:R}:{frame.Normal.Z:R}");
        ExactBoundaryEvaluation Exact(double u, double v) => ExactEvaluation(frame, u, v);

        var map = RuntimeBoundaryMapBuild.Build(cellIndex, Reference, frame, domain, resolutionU, resolutionV, policy,
            Exact, (u, v) => new BoundaryEvaluationKey(frameKey, resolutionU, resolutionV,
            BitConverter.DoubleToInt64Bits(u), BitConverter.DoubleToInt64Bits(v)), cache, certificate, costs);
        return runOracle ? CertifiedBoundaryMapValidation.Validate(map, Exact, policy) : map;
    }

    public SampledBoundaryOffsetMap Validate(SampledBoundaryOffsetMap map, BoundaryOffsetMapErrorPolicy policy) =>
        CertifiedBoundaryMapValidation.Validate(map, (u, v) => ExactEvaluation(map.LocalFrame, u, v), policy);

    public (int U, int V) ChooseResolution(BoundingBox3D cellBounds, BoundaryOffsetMapErrorPolicy policy)
    {
        var frame = Query.CreateMaterialSideFrame(Center(cellBounds), materialInside: true);
        var corners = Corners(cellBounds);
        var domain = AddMargin(new BoundaryMapDomain(
            corners.Min(p => (p - frame.Origin).Dot(frame.TangentU)), corners.Max(p => (p - frame.Origin).Dot(frame.TangentU)),
            corners.Min(p => (p - frame.Origin).Dot(frame.TangentV)), corners.Max(p => (p - frame.Origin).Dot(frame.TangentV))), 0.02d);
        var candidates = new List<(int U, int V, EngineeringBoundaryMapCertificate Certificate)>();
        for (var u = 2; u <= policy.MaximumResolution; u++)
        for (var v = 2; v <= policy.MaximumResolution; v++) candidates.Add((u, v, Certificate(domain, u, v, policy)));
        var byName = candidates.ToDictionary(candidate => $"resolution-{candidate.U}x{candidate.V}");
        var judgmentCandidates = candidates.Select(candidate => new JudgmentCandidate<BoundaryMapDomain>(
            $"resolution-{candidate.U}x{candidate.V}",
            _ => candidate.Certificate.Decision == BoundaryMapCertificateDecision.Acceptable,
            d => -(candidate.U * candidate.V)
                - (1e-3d * double.Abs(((d.MaximumU - d.MinimumU) / (candidate.U - 1)) - ((d.MaximumV - d.MinimumV) / (candidate.V - 1)))),
            _ => $"engineering certificate requires {candidate.Certificate.Decision}",
            (candidate.U * 100) + candidate.V)).ToArray();
        var decision = new JudgmentEngine<BoundaryMapDomain>().Evaluate(domain, judgmentCandidates);
        return decision.IsSuccess
            ? (byName[decision.Selection!.Value.Candidate.Name].U, byName[decision.Selection.Value.Candidate.Name].V)
            : (policy.MaximumResolution, policy.MaximumResolution);
    }

    private EngineeringBoundaryMapCertificate Certificate(BoundaryMapDomain domain, int nu, int nv, BoundaryOffsetMapErrorPolicy policy)
    {
        var maximumRadiusSquared = new[]
        {
            (domain.MinimumU * domain.MinimumU) + (domain.MinimumV * domain.MinimumV),
            (domain.MinimumU * domain.MinimumU) + (domain.MaximumV * domain.MaximumV),
            (domain.MaximumU * domain.MaximumU) + (domain.MinimumV * domain.MinimumV),
            (domain.MaximumU * domain.MaximumU) + (domain.MaximumV * domain.MaximumV),
        }.Max();
        var remaining = (Query.Radius * Query.Radius) - maximumRadiusSquared;
        if (remaining <= 0d) return new(BoundaryMapCertificateDecision.Invalid, double.PositiveInfinity, 180d, 0, "sphere graph domain crosses tangent horizon");
        var hessianBound = (Query.Radius * Query.Radius) / double.Pow(remaining, 1.5d);
        var du = (domain.MaximumU - domain.MinimumU) / (nu - 1);
        var dv = (domain.MaximumV - domain.MinimumV) / (nv - 1);
        var positionBound = hessianBound * ((du * du) + (dv * dv)) / 8d;
        // Linear interpolation of exact normal samples is second-order for a sphere.
        var normalizedDiagonal = double.Sqrt((du * du) + (dv * dv)) / Query.Radius;
        var normalBound = 0.5d * normalizedDiagonal * normalizedDiagonal * 180d / double.Pi;
        var decision = positionBound <= policy.MaximumPositionError && normalBound <= policy.MaximumNormalAngleDegrees
            ? BoundaryMapCertificateDecision.Acceptable : BoundaryMapCertificateDecision.RefineMap;
        return new(decision, positionBound, normalBound, 0, "conservative sphere Hessian and normal-variation engineering bounds");
    }

    private ExactBoundaryEvaluation ExactEvaluation(BoundaryLocalFrame frame, double u, double v)
    {
        var radialSquared = (u * u) + (v * v);
        var remaining = (Query.Radius * Query.Radius) - radialSquared;
        if (remaining <= 0d) throw new InvalidOperationException("Cell patch is not a single-valued local sphere graph.");
        var offset = Query.Radius - double.Sqrt(remaining);
        var point = frame.Origin + (frame.TangentU * u) + (frame.TangentV * v) + (frame.Normal * offset);
        return new ExactBoundaryEvaluation(offset, MaterialSideNormal(point));
    }

    private static BoundaryMapDomain AddMargin(BoundaryMapDomain d, double fraction)
    {
        var u = (d.MaximumU - d.MinimumU) * fraction;
        var v = (d.MaximumV - d.MinimumV) * fraction;
        return new(d.MinimumU - u, d.MaximumU + u, d.MinimumV - v, d.MaximumV + v);
    }

    private static Point3D Center(BoundingBox3D b) => new((b.Min.X + b.Max.X) * 0.5d, (b.Min.Y + b.Max.Y) * 0.5d, (b.Min.Z + b.Max.Z) * 0.5d);
    private static Point3D[] Corners(BoundingBox3D b) =>
    [
        new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z),
        new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z),
    ];
}
