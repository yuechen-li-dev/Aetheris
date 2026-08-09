using System.Diagnostics;
using Aetheris.Continuum.Boundaries;
using Aetheris.Continuum.Cir;
using Aetheris.Continuum.Lattice;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Continuum.Regions.Analytic;

public sealed record RootFilletContactValidation(
    bool Passed,
    int TorusFaces,
    int PlaneContacts,
    int CylinderContacts,
    double MaximumPositionResidual,
    double MaximumTangencyErrorDegrees,
    IReadOnlyList<string> Evidence);

/// <summary>
/// Analytic CIR fixture for a shoulder, exact concave quarter-torus root blend, and shaft. The
/// supplied faces come from the production ConcaveFilletConstruction BRep materializer; CIR owns
/// occupancy while those exact trimmed faces own boundary identity and differential geometry.
/// </summary>
public sealed class BrepTorusRootFilletContinuumRegion : IContinuumRegion, IBoundsClassificationCapability,
    IBoundaryReferenceCapability, IBoundaryOffsetMapCapability
{
    private readonly Transform3D _inverse;
    private readonly IReadOnlyList<BrepTorusBoundarySupport> _supports;

    public BrepTorusRootFilletContinuumRegion(RegionId id, BrepBody body, IReadOnlyList<FaceId> torusFaces,
        double majorRadius, double minorRadius, double outerRadius, double headThickness, double shaftLength,
        Transform3D transform)
    {
        if (torusFaces.Count == 0) throw new ArgumentException("At least one exact torus face is required.", nameof(torusFaces));
        if (!(majorRadius > minorRadius && minorRadius > 0d && outerRadius >= majorRadius
            && headThickness > 0d && shaftLength > minorRadius)) throw new ArgumentOutOfRangeException(nameof(majorRadius));
        Id = id; BrepBody = body; TorusFaceIds = torusFaces; MajorRadius = majorRadius; MinorRadius = minorRadius;
        OuterRadius = outerRadius; HeadThickness = headThickness; ShaftLength = shaftLength; Transform = transform;
        _inverse = transform.Inverse();
        Bounds = TransformBounds(new(new(-headThickness, -outerRadius, -outerRadius), new(shaftLength, outerRadius, outerRadius)), transform);
        _supports = torusFaces.OrderBy(face => face.Value).Select(face =>
        {
            var reference = new BoundaryReference("brep", $"{id}:root-fillet-face-{face.Value}", face.Value.ToString(), "concave-root-fillet");
            // ConcaveFilletConstruction deliberately orients this torus support normal into material.
            return new BrepTorusBoundarySupport(reference, new ExactBrepBoundaryQuery(body, face, transform), double.Pi, 1.5d * double.Pi,
                exactFaceNormalIsMaterialSide: true);
        }).ToArray();
        ContactValidation = ValidateContacts(body, torusFaces);
        ValidateMaterialSide();
    }

    public RegionId Id { get; }
    public BoundingBox3D Bounds { get; }
    public BrepBody BrepBody { get; }
    public IReadOnlyList<FaceId> TorusFaceIds { get; }
    public Transform3D Transform { get; }
    public double MajorRadius { get; }
    public double MinorRadius { get; }
    public double OuterRadius { get; }
    public double HeadThickness { get; }
    public double ShaftLength { get; }
    public double ShaftRadius => MajorRadius - MinorRadius;
    public double ExactRootFilletArea => (double.Pi * double.Pi * MajorRadius * MinorRadius) - (2d * double.Pi * MinorRadius * MinorRadius);
    public double ExactVolume => (double.Pi * OuterRadius * OuterRadius * HeadThickness)
        + double.Pi * ((MajorRadius * MajorRadius * MinorRadius) - (0.5d * double.Pi * MajorRadius * MinorRadius * MinorRadius) + ((2d / 3d) * double.Pow(MinorRadius, 3d)))
        + (double.Pi * ShaftRadius * ShaftRadius * (ShaftLength - MinorRadius));
    public RootFilletContactValidation ContactValidation { get; }

    public ContinuumPointClassification Classify(Point3D point, double tolerance = 1e-9d)
    {
        var p = _inverse.Apply(point);
        if (p.X < -HeadThickness - tolerance || p.X > ShaftLength + tolerance) return ContinuumPointClassification.Outside;
        var rho = double.Sqrt((p.Y * p.Y) + (p.Z * p.Z));
        var radius = ProfileRadius(p.X);
        if (rho > radius + tolerance) return ContinuumPointClassification.Outside;
        return double.Abs(rho - radius) <= tolerance || double.Abs(p.X + HeadThickness) <= tolerance || double.Abs(p.X - ShaftLength) <= tolerance
            ? ContinuumPointClassification.Boundary : ContinuumPointClassification.Inside;
    }

    public ContinuumBoundsClassification ClassifyBounds(BoundingBox3D bounds, double tolerance = 1e-9d)
    {
        var interval = LocalIntervals(bounds);
        if (interval.MaxX < -HeadThickness - tolerance || interval.MinX > ShaftLength + tolerance) return ContinuumBoundsClassification.Outside;
        if (interval.MinX < -HeadThickness + tolerance || interval.MaxX > ShaftLength - tolerance) return ContinuumBoundsClassification.Cut;
        var (minimumProfile, maximumProfile) = ProfileRange(interval.MinX, interval.MaxX);
        if (interval.MinRho > maximumProfile + tolerance) return ContinuumBoundsClassification.Outside;
        if (interval.MaxRho < minimumProfile - tolerance) return ContinuumBoundsClassification.Inside;
        return ContinuumBoundsClassification.Cut;
    }

    public IReadOnlyList<BoundaryReference> BoundaryCandidates(BoundingBox3D cellBounds) =>
        IsRootFilletCandidate(cellBounds) ? SelectSupports(cellBounds).Select(s => s.Reference).ToArray() : [];

    public IReadOnlyList<IAnalyticBoundarySupport> BoundarySupports(BoundingBox3D cellBounds) =>
        IsRootFilletCandidate(cellBounds) ? SelectSupports(cellBounds).Cast<IAnalyticBoundarySupport>().ToArray() : [];

    public bool IsRootFilletCandidate(BoundingBox3D cellBounds)
    {
        if (ClassifyBounds(cellBounds) != ContinuumBoundsClassification.Cut) return false;
        var i = LocalIntervals(cellBounds);
        if (i.MaxX < -1e-10d || i.MinX > MinorRadius + 1e-10d || i.MaxRho < ShaftRadius - 1e-10d || i.MinRho > MajorRadius + 1e-10d) return false;
        var x0 = double.Clamp(i.MinX, 0d, MinorRadius); var x1 = double.Clamp(i.MaxX, 0d, MinorRadius);
        var outer = ProfileRadius(x0); var inner = ProfileRadius(x1);
        return i.MinRho <= outer + 1e-10d && i.MaxRho >= inner - 1e-10d;
    }

    private IReadOnlyList<BrepTorusBoundarySupport> SelectSupports(BoundingBox3D bounds)
    {
        if (_supports.Count == 1) return [_supports[0]];
        var center = Center(bounds); var uv = _supports[0].Query.RecoverParameters(_supports[0].Query.Project(center));
        var seamDistance = double.Min(double.Abs(double.Sin(uv.U)), double.Abs(double.Sin(uv.U - double.Pi)));
        if (seamDistance < 0.08d) return _supports;
        return [uv.U < double.Pi ? _supports[0] : _supports[1]];
    }

    private void ValidateMaterialSide()
    {
        foreach (var support in _supports)
        {
            foreach (var u in new[] { 0.37d, 2.1d, 4.7d })
            foreach (var v in new[] { double.Pi + 0.15d, 1.25d * double.Pi, 1.5d * double.Pi - 0.15d })
            {
                var point = support.Query.Evaluate(u, v);
                var material = support.MaterialSideNormal(point);
                var plus = Classify(point + (material * 1e-6d));
                var minus = Classify(point - (material * 1e-6d));
                if (plus == ContinuumPointClassification.Outside || minus != ContinuumPointClassification.Outside)
                    throw new InvalidOperationException($"CIR/BRep material-side disagreement on root-fillet face {support.Query.FaceId.Value}: u={u:R}, v={v:R}, point={point}, material={material}, plus={plus}, minus={minus}.");
            }
        }
    }

    private double ProfileRadius(double x)
    {
        if (x <= 0d) return OuterRadius;
        if (x >= MinorRadius) return ShaftRadius;
        var dx = x - MinorRadius;
        return MajorRadius - double.Sqrt(double.Max(0d, (MinorRadius * MinorRadius) - (dx * dx)));
    }

    private (double Minimum, double Maximum) ProfileRange(double minX, double maxX)
    {
        var values = new List<double> { ProfileRadius(minX), ProfileRadius(maxX) };
        if (minX < 0d && maxX >= 0d) { values.Add(OuterRadius); values.Add(MajorRadius); }
        if (minX < MinorRadius && maxX >= MinorRadius) values.Add(ShaftRadius);
        return (values.Min(), values.Max());
    }

    private (double MinX, double MaxX, double MinRho, double MaxRho) LocalIntervals(BoundingBox3D bounds)
    {
        var local = Corners(bounds).Select(_inverse.Apply).ToArray();
        var minY = local.Min(p => p.Y); var maxY = local.Max(p => p.Y); var minZ = local.Min(p => p.Z); var maxZ = local.Max(p => p.Z);
        var dy = minY <= 0d && maxY >= 0d ? 0d : double.Min(double.Abs(minY), double.Abs(maxY));
        var dz = minZ <= 0d && maxZ >= 0d ? 0d : double.Min(double.Abs(minZ), double.Abs(maxZ));
        var minRho = double.Sqrt((dy * dy) + (dz * dz));
        var maxRho = double.Sqrt(double.Max(minY * minY, maxY * maxY) + double.Max(minZ * minZ, maxZ * maxZ));
        return (local.Min(p => p.X), local.Max(p => p.X), minRho, maxRho);
    }

    private static RootFilletContactValidation ValidateContacts(BrepBody body, IReadOnlyList<FaceId> torusFaces)
    {
        var plane = 0; var cylinder = 0; var residual = 0d; var angle = 0d; var evidence = new List<string>();
        var torusSupport = body.GetFaceSurface(torusFaces[0]).Torus!.Value;
        var faceEdges = body.Topology.Faces.ToDictionary(f => f.Id, f => body.GetEdges(f.Id).ToHashSet());
        foreach (var torusFace in torusFaces)
        foreach (var adjacent in body.Topology.Faces.Where(f => f.Id != torusFace && faceEdges[f.Id].Overlaps(faceEdges[torusFace])))
        {
            var surface = body.GetFaceSurface(adjacent.Id);
            if (surface.Kind == SurfaceGeometryKind.Plane) plane++;
            else if (surface.Kind == SurfaceGeometryKind.Cylinder) cylinder++;
            else continue;
            var shared = faceEdges[torusFace].Intersect(faceEdges[adjacent.Id]).ToArray();
            evidence.Add($"torus:{torusFace.Value}->{surface.Kind}:{adjacent.Id.Value}:edges={string.Join(',', shared.Select(e => e.Value))}");
            foreach (var edgeId in shared)
            {
                var edge = body.Topology.GetEdge(edgeId);
                foreach (var vertexId in new[] { edge.StartVertexId, edge.EndVertexId }.Distinct())
                {
                    if (!body.TryGetVertexPoint(vertexId, out var point)) continue;
                    var deltaFromTorus = point - torusSupport.Center;
                    var axialTorus = deltaFromTorus.Dot(torusSupport.Axis.ToVector());
                    var tx = deltaFromTorus.Dot(torusSupport.XAxis.ToVector()); var ty = deltaFromTorus.Dot(torusSupport.YAxis.ToVector());
                    var tu = double.Atan2(ty, tx); var tv = double.Atan2(axialTorus, double.Sqrt((tx * tx) + (ty * ty)) - torusSupport.MajorRadius);
                    var torusNormal = torusSupport.Normal(tu, tv).ToVector();
                    if (surface.Plane is { } p)
                    {
                        residual = double.Max(residual, double.Abs((point - p.Origin).Dot(p.Normal.ToVector())));
                        angle = double.Max(angle, double.Acos(double.Clamp(double.Abs(torusNormal.Dot(p.Normal.ToVector())), -1d, 1d)) * 180d / double.Pi);
                    }
                    if (surface.Cylinder is { } c)
                    {
                        var d = point - c.Origin; var axial = d.Dot(c.Axis.ToVector()); var radial = d - (c.Axis.ToVector() * axial);
                        residual = double.Max(residual, double.Abs(radial.Length - c.Radius));
                        if (radial.TryNormalize(out var cylinderNormal))
                            angle = double.Max(angle, double.Acos(double.Clamp(double.Abs(torusNormal.Dot(cylinderNormal)), -1d, 1d)) * 180d / double.Pi);
                    }
                }
            }
        }
        var passed = plane >= torusFaces.Count && cylinder >= torusFaces.Count && residual <= 1e-8d && angle <= 1e-5d;
        return new(passed, torusFaces.Count, plane, cylinder, residual, angle, evidence);
    }

    private static Point3D Center(BoundingBox3D b) => new((b.Min.X + b.Max.X) * 0.5d, (b.Min.Y + b.Max.Y) * 0.5d, (b.Min.Z + b.Max.Z) * 0.5d);
    private static Point3D[] Corners(BoundingBox3D b) => [new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z), new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z)];
    private static BoundingBox3D TransformBounds(BoundingBox3D b, Transform3D t)
    {
        var p = Corners(b).Select(t.Apply).ToArray();
        return new(new(p.Min(x => x.X), p.Min(x => x.Y), p.Min(x => x.Z)), new(p.Max(x => x.X), p.Max(x => x.Y), p.Max(x => x.Z)));
    }
}

public sealed class BrepTorusBoundarySupport : IAnalyticBoundarySupport
{
    private static readonly int[] AdmittedResolutions = [4, 8, 16, 24];
    private readonly double _minimumMinorParameter;
    private readonly double _maximumMinorParameter;
    private readonly bool _exactFaceNormalIsMaterialSide;

    public BrepTorusBoundarySupport(BoundaryReference reference, ExactBrepBoundaryQuery query, double minimumMinorParameter, double maximumMinorParameter,
        bool exactFaceNormalIsMaterialSide)
    {
        Reference = reference; Query = query; _minimumMinorParameter = minimumMinorParameter; _maximumMinorParameter = maximumMinorParameter;
        _exactFaceNormalIsMaterialSide = exactFaceNormalIsMaterialSide;
    }
    public BoundaryReference Reference { get; }
    public ExactBrepBoundaryQuery Query { get; }
    public double ExactArea => (2d * double.Pi * Query.MinorRadius) * ((Query.MajorRadius * (_maximumMinorParameter - _minimumMinorParameter))
        + (Query.MinorRadius * (double.Sin(_maximumMinorParameter) - double.Sin(_minimumMinorParameter))));
    public Point3D Project(Point3D point) => Query.Project(point);
    public Vector3D MaterialSideNormal(Point3D boundaryPoint)
    {
        var parameters = Query.RecoverParameters(boundaryPoint);
        var faceNormal = Query.ExactFaceNormal(parameters.U, parameters.V);
        return _exactFaceNormalIsMaterialSide ? faceNormal : -faceNormal;
    }

    public IBoundaryOffsetMap CreateOffsetMap(CellIndex cellIndex, BoundingBox3D cellBounds, int resolution, BoundaryOffsetMapErrorPolicy policy, BoundaryEvaluationCache? cache = null) =>
        Build(cellIndex, cellBounds, resolution, resolution, policy, cache, true);

    public SampledBoundaryOffsetMap Build(CellIndex cellIndex, BoundingBox3D cellBounds, int resolutionU, int resolutionV,
        BoundaryOffsetMapErrorPolicy policy, BoundaryEvaluationCache? cache, bool runOracle, BoundaryMapBuildCosts? costs = null)
    {
        var stage = Stopwatch.GetTimestamp();
        var frame = CreateFrame(Center(cellBounds));
        if (costs is not null) costs.LocalFrameMilliseconds += Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var projected = Corners(cellBounds).Select(p => p - frame.Origin).ToArray();
        var raw = new BoundaryMapDomain(projected.Min(p => p.Dot(frame.TangentU)), projected.Max(p => p.Dot(frame.TangentU)),
            projected.Min(p => p.Dot(frame.TangentV)), projected.Max(p => p.Dot(frame.TangentV)));
        var domain = AddMargin(raw, 0.015d);
        stage = Stopwatch.GetTimestamp();
        var certificate = Certificate(frame, domain, resolutionU, resolutionV, policy);
        if (costs is not null) costs.RuntimeCertificateMilliseconds += Stopwatch.GetElapsedTime(stage).TotalMilliseconds;
        var frameKey = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Reference.SourceId}:{frame.Origin.X:R}:{frame.Origin.Y:R}:{frame.Origin.Z:R}:{frame.Normal.X:R}:{frame.Normal.Y:R}:{frame.Normal.Z:R}");
        ExactBoundaryEvaluation Exact(double u, double v) => ExactEvaluation(frame, u, v);
        var map = RuntimeBoundaryMapBuild.Build(cellIndex, Reference, frame, domain, resolutionU, resolutionV, policy, Exact,
            (u, v) => new(frameKey, resolutionU, resolutionV, BitConverter.DoubleToInt64Bits(u), BitConverter.DoubleToInt64Bits(v)), cache, certificate, costs,
            point =>
            {
                var minor = ExactBrepBoundaryQuery.UnwrapPeriodic(Query.RecoverParameters(point).V, 0.5d * (_minimumMinorParameter + _maximumMinorParameter));
                return double.Min(minor - _minimumMinorParameter, _maximumMinorParameter - minor) * Query.MinorRadius;
            });
        return runOracle ? Validate(map, policy) : map;
    }

    public SampledBoundaryOffsetMap Validate(SampledBoundaryOffsetMap map, BoundaryOffsetMapErrorPolicy policy) =>
        CertifiedBoundaryMapValidation.Validate(map, (u, v) => ExactEvaluation(map.LocalFrame, u, v), policy);

    public (int U, int V) ChooseResolution(BoundingBox3D bounds, BoundaryOffsetMapErrorPolicy policy)
    {
        var frame = CreateFrame(Center(bounds));
        var p = Corners(bounds).Select(x => x - frame.Origin).ToArray();
        var domain = AddMargin(new(p.Min(x => x.Dot(frame.TangentU)), p.Max(x => x.Dot(frame.TangentU)), p.Min(x => x.Dot(frame.TangentV)), p.Max(x => x.Dot(frame.TangentV))), 0.015d);
        var candidates = (from u in AdmittedResolutions where u <= policy.MaximumResolution
                          from v in AdmittedResolutions where v <= policy.MaximumResolution
                          let certificate = Certificate(frame, domain, u, v, policy)
                          select (U: u, V: v, Certificate: certificate)).ToArray();
        var lookup = candidates.ToDictionary(c => $"torus-resolution-{c.U}x{c.V}");
        var choices = candidates.Select(c => new JudgmentCandidate<BoundaryMapDomain>($"torus-resolution-{c.U}x{c.V}",
            _ => c.Certificate.Decision == BoundaryMapCertificateDecision.Acceptable, _ => -(c.U * c.V),
            _ => $"engineering certificate requires {c.Certificate.Decision}", (c.U * 100) + c.V)).ToArray();
        var decision = new JudgmentEngine<BoundaryMapDomain>().Evaluate(domain, choices);
        return decision.IsSuccess ? (lookup[decision.Selection!.Value.Candidate.Name].U, lookup[decision.Selection.Value.Candidate.Name].V)
            : (policy.MaximumResolution, policy.MaximumResolution);
    }

    public EngineeringBoundaryMapCertificate Certificate(BoundaryLocalFrame frame, BoundaryMapDomain domain, int nu, int nv, BoundaryOffsetMapErrorPolicy policy)
    {
        if (nu < 2 || nv < 2) return new(BoundaryMapCertificateDecision.Invalid, double.PositiveInfinity, 180d, 0, "invalid torus map resolution");
        var uv = Query.RecoverParameters(frame.Origin);
        var curvature = Query.PrincipalCurvatures(uv.U, uv.V);
        var extentU = double.Max(double.Abs(domain.MinimumU), double.Abs(domain.MaximumU));
        var extentV = double.Max(double.Abs(domain.MinimumV), double.Abs(domain.MaximumV));
        var kU = double.Abs(curvature.CurvatureU) * (1d + (2d * extentV / Query.MinorRadius));
        var kV = (1d / Query.MinorRadius) * (1d + (2d * extentV / Query.MinorRadius));
        if (extentV >= Query.MinorRadius * 0.85d) return new(BoundaryMapCertificateDecision.Invalid, double.PositiveInfinity, 180d, 0, "torus graph approaches a minor-circle tangent horizon");
        var du = (domain.MaximumU - domain.MinimumU) / (nu - 1); var dv = (domain.MaximumV - domain.MinimumV) / (nv - 1);
        var position = 0.25d * ((kU * du * du) + (kV * dv * dv));
        var normalRadians = 0.25d * ((kU * kU * du * du) + (kV * kV * dv * dv));
        var normalDegrees = normalRadians * 180d / double.Pi;
        var decision = position <= policy.MaximumPositionError && normalDegrees <= policy.MaximumNormalAngleDegrees
            ? BoundaryMapCertificateDecision.Acceptable : BoundaryMapCertificateDecision.RefineMap;
        return new(decision, position, normalDegrees, 0, "conservative local principal-curvature and second-order torus variation engineering bounds");
    }

    private ExactBoundaryEvaluation ExactEvaluation(BoundaryLocalFrame frame, double u, double v)
    {
        var basePoint = frame.Origin + (frame.TangentU * u) + (frame.TangentV * v);
        var w = 0d;
        for (var iteration = 0; iteration < 16; iteration++)
        {
            var point = basePoint + (frame.Normal * w); var delta = point - Query.Center;
            var axial = delta.Dot(Query.Axis); var radialVector = delta - (Query.Axis * axial); var rho = radialVector.Length;
            if (rho <= 1e-14d) throw new InvalidOperationException("Torus graph query reached its axis.");
            var radialOffset = rho - Query.MajorRadius;
            var f = (radialOffset * radialOffset) + (axial * axial) - (Query.MinorRadius * Query.MinorRadius);
            if (double.Abs(f) <= 1e-14d) break;
            var gradient = (radialVector * (2d * radialOffset / rho)) + (Query.Axis * (2d * axial));
            var derivative = gradient.Dot(frame.Normal);
            if (double.Abs(derivative) <= 1e-12d) throw new InvalidOperationException("Torus graph Newton solve reached a tangent horizon.");
            w -= f / derivative;
            if (!double.IsFinite(w) || double.Abs(w) > Query.MinorRadius) throw new InvalidOperationException("Torus graph Newton solve left the local branch.");
        }
        var boundary = basePoint + (frame.Normal * w);
        var parameters = Query.RecoverParameters(boundary);
        var minor = ExactBrepBoundaryQuery.UnwrapPeriodic(parameters.V, 0.5d * (_minimumMinorParameter + _maximumMinorParameter));
        // A Cartesian cell touching a trim curve needs a modest analytic-support halo; the owning
        // experiment classifies that contact cell separately and does not attribute halo area to the trim.
        if (minor < _minimumMinorParameter - 3d || minor > _maximumMinorParameter + 3d)
            throw new InvalidOperationException("Local map queried outside the bounded root-fillet torus sector.");
        return new(w, MaterialSideNormal(boundary));
    }

    private BoundaryLocalFrame CreateFrame(Point3D nearPoint)
    {
        var origin = Query.Project(nearPoint);
        var parameters = Query.RecoverParameters(origin);
        var normal = MaterialSideNormal(origin);
        var principalU = Query.PrincipalCurvatures(parameters.U, parameters.V).DirectionU;
        var tangentU = principalU - (normal * principalU.Dot(normal));
        if (!tangentU.TryNormalize(out tangentU)) throw new InvalidOperationException("Torus principal direction is degenerate.");
        var tangentV = normal.Cross(tangentU);
        if (!tangentV.TryNormalize(out tangentV)) throw new InvalidOperationException("Could not construct torus principal frame.");
        return new(origin, normal, tangentU, tangentV);
    }

    private static BoundaryMapDomain AddMargin(BoundaryMapDomain d, double f) { var u = (d.MaximumU - d.MinimumU) * f; var v = (d.MaximumV - d.MinimumV) * f; return new(d.MinimumU - u, d.MaximumU + u, d.MinimumV - v, d.MaximumV + v); }
    private static Point3D Center(BoundingBox3D b) => new((b.Min.X + b.Max.X) * 0.5d, (b.Min.Y + b.Max.Y) * 0.5d, (b.Min.Z + b.Max.Z) * 0.5d);
    private static Point3D[] Corners(BoundingBox3D b) => [new(b.Min.X,b.Min.Y,b.Min.Z), new(b.Max.X,b.Min.Y,b.Min.Z), new(b.Min.X,b.Max.Y,b.Min.Z), new(b.Max.X,b.Max.Y,b.Min.Z), new(b.Min.X,b.Min.Y,b.Max.Z), new(b.Max.X,b.Min.Y,b.Max.Z), new(b.Min.X,b.Max.Y,b.Max.Z), new(b.Max.X,b.Max.Y,b.Max.Z)];
}
