using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Geometry.Curves;
using Rhino.FileIO;
using Rhino.Geometry;
using KernelPoint = Aetheris.Kernel.Core.Math.Point3D;

namespace Aetheris.ThreeDm;

public enum RecoveryQualification { WithinSourceTolerance, Approximate, Unresolved }

public sealed record RecoveryResidual(
    double RmsMillimetres, double P95Millimetres, double MaxMillimetres,
    double EndpointMaxMillimetres, double? MaxTangentDegrees, int SampleCount);

public sealed record RecoveryCandidate(
    string Kind, string Method, RecoveryResidual Residual, RecoveryQualification Qualification,
    IReadOnlyDictionary<string, double> Parameters, string Limitation);

public sealed record RecoveryEdge(
    int EdgeIndex, bool SourceIsRational, int Degree, int ControlPointCount, int KnotCount,
    double MinimumWeight, double MaximumWeight, double RelativeWeightSpread,
    string NativeClassification, IReadOnlyList<RecoveryCandidate> Candidates,
    string? PreferredCandidate, string Status, string TopologyStatus);

public sealed record RecoveryFace(
    int FaceIndex, int SurfaceIndex, bool SourceIsRational, int DegreeU, int DegreeV,
    int ControlPointCountU, int ControlPointCountV, int KnotCountU, int KnotCountV,
    int LoopCount, int TrimCount, string NativeClassification, RecoveryCandidate? Candidate,
    string Status, string TopologyStatus);

public sealed record RecoveryBody(
    int ObjectIndex, Guid SourceId, int LayerIndex, bool SourceIsSolid, bool SourceIsManifold,
    int FaceCount, int LoopCount, int TrimCount, int EdgeCount, int VertexCount,
    int NativeAnalyticEdgeCount, int QualifiedRecoveredEdgeCount, int UnresolvedRationalEdgeCount,
    int QualifiedAnalyticFaceCount, int UnresolvedRationalFaceCount,
    ThreeDmBounds BoundsMillimetres, IReadOnlyList<RecoveryEdge> Edges, IReadOnlyList<RecoveryFace> Faces);

public sealed record RecoveryDimensionHypothesis(
    string Kind, double ValueMillimetres, double SpreadMillimetres,
    IReadOnlyList<string> SourceEntities, string Qualification);

public sealed record ThreeDmRecoveryReport(
    string SourceUnit, double MillimetresPerSourceUnit, double SourceToleranceMillimetres,
    string ManufacturingToleranceStatus, IReadOnlyList<RecoveryBody> Bodies,
    int SourceRationalEdgeCount, int NativeAnalyticRationalEdgeCount, int StudiedRationalEdgeCount,
    int QualifiedRecoveredRationalEdgeCount, int UnresolvedRationalEdgeCount,
    int SourceRationalSurfaceCount, int QualifiedAnalyticRationalSurfaceCount,
    int UnresolvedRationalSurfaceCount,
    IReadOnlyList<RecoveryDimensionHypothesis> RepeatedDimensions,
    string ProductionStatus, IReadOnlyList<string> Diagnostics);

/// <summary>
/// Deterministic, read-only geometry recovery evidence. Candidates are support-geometry
/// hypotheses; no face/trim topology is changed and no candidate is canonicalized here.
/// </summary>
public static class ThreeDmRecovery
{
    public static ThreeDmRecoveryReport Analyze(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var file = File3dm.Read(Path.GetFullPath(path)) ?? throw new InvalidDataException("OpenNURBS could not read the 3DM file.");
        var unit = file.Settings.ModelUnitSystem.ToString();
        var factor = unit switch
        {
            "Millimeters" => 1d, "Centimeters" => 10d, "Meters" => 1000d,
            "Microns" => 0.001d, "Inches" => 25.4d, "Feet" => 304.8d,
            _ => throw new NotSupportedException($"UnitConversionIssue: unsupported 3DM model unit {unit}.")
        };
        var tolerance = file.Settings.ModelAbsoluteTolerance * factor;
        var angleToleranceDegrees = file.Settings.ModelAngleToleranceDegrees;
        if (!double.IsFinite(tolerance) || tolerance <= 0)
            throw new InvalidDataException("UnitConversionIssue: source tolerance is not positive and finite.");

        var bodies = new List<RecoveryBody>();
        var diagnostics = new List<string>();
        var objectIndex = 0;
        foreach (var item in file.Objects)
        {
            if (item.Geometry is not Brep brep)
            {
                diagnostics.Add($"Unsupported3dmObjectType: object {objectIndex} is {item.Geometry.GetType().Name}; recovery is BRep-only.");
                objectIndex++;
                continue;
            }
            var edges = new List<RecoveryEdge>(brep.Edges.Count);
            foreach (var edge in brep.Edges)
                edges.Add(RecoverEdge(edge, factor, tolerance, angleToleranceDegrees));
            var faces = new List<RecoveryFace>(brep.Faces.Count);
            foreach (var face in brep.Faces)
                faces.Add(RecoverFace(face, brep.Surfaces[face.SurfaceIndex], factor, tolerance, angleToleranceDegrees));
            var box = ThreeDmBoundsSampler.Sample(brep);
            bodies.Add(new RecoveryBody(objectIndex, item.Attributes.ObjectId, item.Attributes.LayerIndex,
                brep.IsSolid, brep.IsManifold, brep.Faces.Count, brep.Loops.Count, brep.Trims.Count,
                brep.Edges.Count, brep.Vertices.Count,
                edges.Count(e => e.SourceIsRational && e.NativeClassification != "Unclassified"),
                edges.Count(e => e.Status == "Qualified support candidate; topology pending"),
                edges.Count(e => e.SourceIsRational && e.Status == "Unresolved"),
                faces.Count(f => f.SourceIsRational && f.Status == "Qualified support candidate; topology pending"),
                faces.Count(f => f.SourceIsRational && f.Status == "Unresolved"),
                new ThreeDmBounds(
                    new(box.Min.X * factor, box.Min.Y * factor, box.Min.Z * factor),
                    new(box.Max.X * factor, box.Max.Y * factor, box.Max.Z * factor)),
                edges, faces));
            objectIndex++;
        }
        var allEdges = bodies.SelectMany(b => b.Edges).ToArray();
        var allFaces = bodies.SelectMany(b => b.Faces).ToArray();
        return new ThreeDmRecoveryReport(unit, factor, tolerance,
            "No manufacturing tolerance was supplied; source tolerance is geometric evidence only.",
            bodies, allEdges.Count(e => e.SourceIsRational),
            allEdges.Count(e => e.SourceIsRational && e.NativeClassification != "Unclassified"),
            allEdges.Count(e => e.SourceIsRational && e.NativeClassification == "Unclassified"),
            allEdges.Count(e => e.SourceIsRational && e.NativeClassification == "Unclassified" && e.Status == "Qualified support candidate; topology pending"),
            allEdges.Count(e => e.SourceIsRational && e.Status == "Unresolved"),
            allFaces.Count(f => f.SourceIsRational),
            allFaces.Count(f => f.SourceIsRational && f.Status == "Qualified support candidate; topology pending"),
            allFaces.Count(f => f.SourceIsRational && f.Status == "Unresolved"),
            RepeatedDimensions(bodies, tolerance),
            "No canonical BRep or production STEP is emitted. All candidates require trim/topology qualification.", diagnostics);
    }

    private static RecoveryEdge RecoverEdge(BrepEdge edge, double factor, double toleranceMm, double angleToleranceDegrees)
    {
        var source = edge.EdgeCurve;
        var nurbs = source.ToNurbsCurve();
        var weights = Enumerable.Range(0, nurbs.Points.Count).Select(i => nurbs.Points[i].Weight).ToArray();
        var minimumWeight = weights.Min(); var maximumWeight = weights.Max();
        var relativeWeightSpread = (maximumWeight - minimumWeight) / double.Abs(weights.Average());
        var native = source.IsLinear() ? "Line"
            : source.TryGetCircle(out _) ? "Circle"
            : source.TryGetArc(out _) ? "Arc"
            : source.TryGetEllipse(out _) ? "Ellipse"
            : "Unclassified";
        if (!nurbs.IsRational || native != "Unclassified")
        {
            var nativeCandidate = nurbs.IsRational
                ? MeasureNativeEdge(edge, native, factor, toleranceMm, angleToleranceDegrees)
                : null;
            return new RecoveryEdge(edge.EdgeIndex, nurbs.IsRational, nurbs.Degree, nurbs.Points.Count,
                nurbs.Knots.Count, minimumWeight, maximumWeight, relativeWeightSpread, native,
                nativeCandidate is null ? [] : [nativeCandidate],
                nativeCandidate?.Qualification == RecoveryQualification.WithinSourceTolerance ? nativeCandidate.Kind : null,
                native == "Unclassified" ? "Non-rational source; outside rational edge study"
                    : nativeCandidate?.Qualification == RecoveryQualification.WithinSourceTolerance
                        ? "Qualified support candidate; topology pending" : "Native recognition requires further measurement",
                "Original edge and trims retained as source evidence; no canonical edge built.");
        }

        var samples = SampleEdge(edge, int.Clamp(16 * nurbs.Degree + 1, 65, 257));
        var candidates = new List<RecoveryCandidate>();
        var p0 = samples[0]; var pm = samples[samples.Count / 2]; var p1 = samples[^1];
        if (p0.DistanceTo(p1) > 1e-12)
        {
            var direction = p1 - p0;
            direction.Unitize();
            var distances = samples.Select(p => ((p - p0) - direction * ((p - p0) * direction)).Length * factor).ToArray();
            var residual = Residual(distances, samples, edge, p => direction, factor);
            candidates.Add(new RecoveryCandidate("Line", "endpoint line fit; full edge sampled", residual,
                Qualify(residual, toleranceMm, angleToleranceDegrees), new Dictionary<string, double> { ["lengthMillimetres"] = p0.DistanceTo(p1) * factor },
                "Infinite support only; trim orientation and adjacent faces unqualified."));
        }
        try
        {
            var circle = new Circle(p0, pm, p1);
            if (circle.IsValid && circle.Radius > 1e-12)
            {
                var normal = circle.Normal;
                var distances = samples.Select(p =>
                {
                    var offset = p - circle.Center;
                    var axial = offset * normal;
                    var radial = (offset - axial * normal).Length;
                    return double.Sqrt(double.Pow(radial - circle.Radius, 2) + axial * axial) * factor;
                }).ToArray();
                var residual = Residual(distances, samples, edge, p =>
                {
                    var radial = p - circle.Center;
                    radial -= (radial * normal) * normal;
                    var tangent = Vector3d.CrossProduct(normal, radial);
                    tangent.Unitize();
                    return tangent;
                }, factor);
                candidates.Add(new RecoveryCandidate("CircleSupport", "three-point circle fit; full edge sampled", residual,
                    Qualify(residual, toleranceMm, angleToleranceDegrees), new Dictionary<string, double> { ["radiusMillimetres"] = circle.Radius * factor },
                    "Circle support only; arc sweep, trim orientation and adjacent faces unqualified."));
            }
        }
        catch (ArgumentException) { /* Degenerate three-point circle has no candidate. */ }

        var polynomial = TrySameNetPolynomial(edge, nurbs, samples, factor, toleranceMm, angleToleranceDegrees);
        if (polynomial is not null) candidates.Add(polynomial);

        var admissible = candidates.Where(c => c.Qualification == RecoveryQualification.WithinSourceTolerance).ToArray();
        string? preferred = null;
        if (admissible.Length > 0)
        {
            var engine = new JudgmentEngine<IReadOnlyList<RecoveryCandidate>>();
            var selection = engine.Evaluate(admissible,
                admissible.Select((candidate, index) => new JudgmentCandidate<IReadOnlyList<RecoveryCandidate>>(
                    candidate.Kind, _ => true,
                    _ => (candidate.Kind switch { "Line" => 3d, "CircleSupport" => 2d, _ => 1d })
                         - candidate.Residual.MaxMillimetres / toleranceMm,
                    TieBreakerPriority: index)).ToArray());
            preferred = selection.Selection?.Candidate.Name;
        }
        return new RecoveryEdge(edge.EdgeIndex, true, nurbs.Degree, nurbs.Points.Count, nurbs.Knots.Count,
            minimumWeight, maximumWeight, relativeWeightSpread, "Unclassified", candidates, preferred,
            preferred is null ? "Unresolved" : "Qualified support candidate; topology pending",
            "Original edge and trims retained as source evidence; no canonical edge built.");
    }

    private static RecoveryFace RecoverFace(BrepFace face, Surface surface, double factor, double toleranceMm, double angleToleranceDegrees)
    {
        var nurbs = surface.ToNurbsSurface();
        var kind = "Unclassified";
        Func<Point3d, (double Distance, Vector3d Normal)>? evaluate = null;
        IReadOnlyDictionary<string, double> parameters = new Dictionary<string, double>();
        if (surface.TryGetPlane(out var plane))
        {
            kind = "Plane";
            evaluate = p => (double.Abs((p - plane.Origin) * plane.Normal), plane.Normal);
        }
        else if (surface.TryGetCylinder(out var cylinder))
        {
            kind = "Cylinder";
            parameters = new Dictionary<string, double> { ["radiusMillimetres"] = cylinder.Radius * factor };
            evaluate = p =>
            {
                var offset = p - cylinder.Center;
                var axial = offset * cylinder.Axis;
                var radial = offset - axial * cylinder.Axis;
                var normal = radial; normal.Unitize();
                return (double.Abs(radial.Length - cylinder.Radius), normal);
            };
        }
        else if (surface.TryGetCone(out var cone) && double.Abs(cone.Height) > 1e-12)
        {
            kind = "Cone";
            parameters = new Dictionary<string, double> { ["baseRadiusMillimetres"] = cone.Radius * factor };
            evaluate = p =>
            {
                var offset = p - cone.ApexPoint;
                var axial = offset * cone.Axis;
                var radial = offset - axial * cone.Axis;
                var normal = radial; normal.Unitize();
                return (double.Abs(radial.Length - double.Abs(axial * cone.Radius / cone.Height)), normal);
            };
        }
        else if (surface.TryGetSphere(out var sphere))
        {
            kind = "Sphere";
            parameters = new Dictionary<string, double> { ["radiusMillimetres"] = sphere.Radius * factor };
            evaluate = p =>
            {
                var radial = p - sphere.Center;
                var normal = radial; normal.Unitize();
                return (double.Abs(radial.Length - sphere.Radius), normal);
            };
        }
        else if (surface.TryGetTorus(out var torus))
        {
            kind = "Torus";
            parameters = new Dictionary<string, double>
            {
                ["majorRadiusMillimetres"] = torus.MajorRadius * factor,
                ["minorRadiusMillimetres"] = torus.MinorRadius * factor
            };
            evaluate = p =>
            {
                var offset = p - torus.Plane.Origin;
                var axial = offset * torus.Plane.Normal;
                var radial = offset - axial * torus.Plane.Normal;
                var r = radial.Length;
                var tube = double.Sqrt(double.Pow(r - torus.MajorRadius, 2) + axial * axial);
                var normal = r > 1e-12 ? radial * ((r - torus.MajorRadius) / r) + torus.Plane.Normal * axial : Vector3d.Zero;
                normal.Unitize();
                return (double.Abs(tube - torus.MinorRadius), normal);
            };
        }

        RecoveryCandidate? candidate = null;
        if (evaluate is not null)
        {
            const int countPerAxis = 17;
            var domainU = surface.Domain(0); var domainV = surface.Domain(1);
            var distances = new List<double>(countPerAxis * countPerAxis);
            var normalAngles = new List<double>();
            for (var i = 0; i < countPerAxis; i++)
            for (var j = 0; j < countPerAxis; j++)
            {
                var u = domainU.T0 + (domainU.T1 - domainU.T0) * i / (countPerAxis - 1d);
                var v = domainV.T0 + (domainV.T1 - domainV.T0) * j / (countPerAxis - 1d);
                var point = surface.PointAt(u, v);
                var result = evaluate(point);
                distances.Add(result.Distance * factor);
                var sourceNormal = surface.NormalAt(u, v);
                if (sourceNormal.IsValid && result.Normal.IsValid && sourceNormal.Length > 1e-12 && result.Normal.Length > 1e-12)
                    normalAngles.Add(double.Acos(double.Clamp(double.Abs(sourceNormal * result.Normal / (sourceNormal.Length * result.Normal.Length)), 0, 1)) * 180 / double.Pi);
            }
            var measured = Distances(distances);
            candidate = new RecoveryCandidate(kind, "Rhino analytic probe plus independent 17x17 support-domain residual",
                new RecoveryResidual(measured.Rms, measured.P95, measured.Max, 0,
                    normalAngles.Count == 0 ? null : normalAngles.Max(), distances.Count),
                measured.Max <= toleranceMm &&
                (normalAngles.Count == 0 || normalAngles.Max() <= angleToleranceDegrees)
                    ? RecoveryQualification.WithinSourceTolerance : RecoveryQualification.Approximate,
                parameters, "Full support domain sampled; trims, loops and orientation are not qualified.");
        }
        var qualified = candidate?.Qualification == RecoveryQualification.WithinSourceTolerance;
        return new RecoveryFace(face.FaceIndex, face.SurfaceIndex, nurbs.IsRational,
            nurbs.OrderU - 1, nurbs.OrderV - 1, nurbs.Points.CountU, nurbs.Points.CountV,
            nurbs.KnotsU.Count, nurbs.KnotsV.Count, face.Loops.Count,
            face.Loops.Sum(loop => loop.Trims.Count), kind, candidate,
            qualified ? "Qualified support candidate; topology pending" : "Unresolved",
            "Source loops/trims are inventory only; no canonical face or pcurve built.");
    }

    private static IReadOnlyList<Point3d> SampleEdge(BrepEdge edge, int count)
    {
        var domain = edge.Domain;
        return Enumerable.Range(0, count)
            .Select(i => edge.PointAt(domain.T0 + (domain.T1 - domain.T0) * i / (count - 1d))).ToArray();
    }

    private static RecoveryQualification Qualify(RecoveryResidual residual, double toleranceMm, double angleToleranceDegrees) =>
        residual.MaxMillimetres <= toleranceMm &&
        (residual.MaxTangentDegrees is null || residual.MaxTangentDegrees <= angleToleranceDegrees)
            ? RecoveryQualification.WithinSourceTolerance : RecoveryQualification.Approximate;

    private static RecoveryCandidate? TrySameNetPolynomial(
        BrepEdge edge, NurbsCurve nurbs, IReadOnlyList<Point3d> samples,
        double factor, double toleranceMm, double angleToleranceDegrees)
    {
        // OpenNURBS omits one end knot at each end relative to the full vector
        // carried by Aetheris/STEP. This is a candidate calculation, never a
        // rewrite of the source curve or its edge uses.
        try
        {
            var controls = Enumerable.Range(0, nurbs.Points.Count)
                .Select(i => nurbs.Points[i].Location)
                .Select(p => new KernelPoint(p.X * factor, p.Y * factor, p.Z * factor)).ToArray();
            var knots = new double[nurbs.Knots.Count + 2];
            knots[0] = nurbs.Knots[0];
            for (var i = 0; i < nurbs.Knots.Count; i++) knots[i + 1] = nurbs.Knots[i];
            knots[^1] = nurbs.Knots[^1];
            var values = new List<double>(); var multiplicities = new List<int>();
            foreach (var knot in knots)
            {
                if (values.Count > 0 && knot == values[^1]) multiplicities[^1]++;
                else { values.Add(knot); multiplicities.Add(1); }
            }
            var curve = new BSpline3Curve(nurbs.Degree, controls, multiplicities, values,
                "UNSPECIFIED", nurbs.IsClosed, false, "UNSPECIFIED");
            var domain = edge.Domain;
            if (domain.T0 < curve.DomainStart - 1e-10 || domain.T1 > curve.DomainEnd + 1e-10)
                return null;
            var distances = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var parameter = domain.T0 + (domain.T1 - domain.T0) * i / (samples.Count - 1d);
                var point = curve.Evaluate(parameter);
                distances[i] = double.Sqrt(double.Pow(point.X - samples[i].X * factor, 2) +
                    double.Pow(point.Y - samples[i].Y * factor, 2) +
                    double.Pow(point.Z - samples[i].Z * factor, 2));
            }
            var measured = Distances(distances);
            var maxTangent = 0d;
            for (var i = 0; i < samples.Count; i++)
            {
                var parameter = domain.T0 + (domain.T1 - domain.T0) * i / (samples.Count - 1d);
                var sourceTangent = edge.TangentAt(parameter);
                var target = curve.EvaluateTangent(parameter);
                var targetLength = double.Sqrt(target.X * target.X + target.Y * target.Y + target.Z * target.Z);
                if (!sourceTangent.IsValid || sourceTangent.Length < 1e-12 || targetLength < 1e-12) continue;
                var cosine = double.Clamp(double.Abs((sourceTangent.X * target.X + sourceTangent.Y * target.Y + sourceTangent.Z * target.Z) /
                    (sourceTangent.Length * targetLength)), 0, 1);
                maxTangent = double.Max(maxTangent, double.Acos(cosine) * 180 / double.Pi);
            }
            var residual = new RecoveryResidual(measured.Rms, measured.P95, measured.Max,
                double.Max(distances[0], distances[^1]), maxTangent, distances.Length);
            return new RecoveryCandidate("NonRationalSameNet", "source controls and knots; weights set to one; full edge sampled",
                residual, Qualify(residual, toleranceMm, angleToleranceDegrees),
                new Dictionary<string, double> { ["degree"] = nurbs.Degree, ["controlPointCount"] = controls.Length },
                "Candidate only. Trims, face support, and topology are unqualified; no curve is promoted to canonical geometry.");
        }
        catch (ArgumentException) { return null; }
    }

    private static RecoveryCandidate? MeasureNativeEdge(
        BrepEdge edge, string native, double factor, double toleranceMm, double angleToleranceDegrees)
    {
        var samples = SampleEdge(edge, 129);
        Circle circle;
        var hasCircularSupport = edge.EdgeCurve.TryGetCircle(out circle);
        if (!hasCircularSupport && native == "Arc" && edge.EdgeCurve.TryGetArc(out var arc))
        {
            circle = new Circle(arc.Plane, arc.Radius);
            hasCircularSupport = true;
        }
        if (native is "Circle" or "Arc" && hasCircularSupport)
        {
            var normal = circle.Normal;
            var distances = samples.Select(p =>
            {
                var offset = p - circle.Center;
                var axial = offset * normal;
                var radial = (offset - axial * normal).Length;
                return double.Sqrt(double.Pow(radial - circle.Radius, 2) + axial * axial) * factor;
            }).ToArray();
            var residual = Residual(distances, samples, edge, p =>
            {
                var radial = p - circle.Center;
                radial -= (radial * normal) * normal;
                var tangent = Vector3d.CrossProduct(normal, radial);
                tangent.Unitize();
                return tangent;
            }, factor);
            return new RecoveryCandidate(native, "Rhino analytic probe plus independent support residual", residual,
                Qualify(residual, toleranceMm, angleToleranceDegrees),
                new Dictionary<string, double> { ["radiusMillimetres"] = circle.Radius * factor },
                "Analytic support only; arc sweep, trims and topology remain unqualified.");
        }
        if (native == "Ellipse" && edge.EdgeCurve.TryGetEllipse(out var ellipse))
        {
            var plane = ellipse.Plane;
            var distances = samples.Select(p =>
            {
                var offset = p - ellipse.Center;
                var x = offset * plane.XAxis;
                var y = offset * plane.YAxis;
                var z = offset * plane.Normal;
                var rho = double.Sqrt(double.Pow(x / ellipse.Radius1, 2) + double.Pow(y / ellipse.Radius2, 2));
                if (rho < 1e-12) return double.PositiveInfinity;
                // Radial projection is a point on the ellipse. Its distance is
                // a conservative upper bound on nearest-point deviation.
                return double.Sqrt(double.Pow(x - x / rho, 2) + double.Pow(y - y / rho, 2) + z * z) * factor;
            }).ToArray();
            var residual = Residual(distances, samples, edge, p =>
            {
                var offset = p - ellipse.Center;
                var theta = double.Atan2((offset * plane.YAxis) / ellipse.Radius2,
                    (offset * plane.XAxis) / ellipse.Radius1);
                var tangent = -ellipse.Radius1 * double.Sin(theta) * plane.XAxis +
                    ellipse.Radius2 * double.Cos(theta) * plane.YAxis;
                tangent.Unitize();
                return tangent;
            }, factor);
            return new RecoveryCandidate("Ellipse", "Rhino analytic probe plus radial-distance upper bound", residual,
                Qualify(residual, toleranceMm, angleToleranceDegrees),
                new Dictionary<string, double>
                {
                    ["radius1Millimetres"] = ellipse.Radius1 * factor,
                    ["radius2Millimetres"] = ellipse.Radius2 * factor
                },
                "Radial-distance residual is an upper bound; arc sweep, trims and topology remain unqualified.");
        }
        return null;
    }

    private static RecoveryResidual Residual(IReadOnlyList<double> distances, IReadOnlyList<Point3d> points,
        BrepEdge source, Func<Point3d, Vector3d> candidateTangent, double factor)
    {
        var measured = Distances(distances);
        var domain = source.Domain;
        var maxAngle = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var parameter = domain.T0 + (domain.T1 - domain.T0) * i / (points.Count - 1d);
            var tangent = source.TangentAt(parameter);
            var target = candidateTangent(points[i]);
            if (!tangent.IsValid || !target.IsValid || tangent.Length < 1e-12 || target.Length < 1e-12) continue;
            var cosine = double.Clamp(double.Abs(tangent * target / (tangent.Length * target.Length)), 0, 1);
            maxAngle = double.Max(maxAngle, double.Acos(cosine) * 180 / double.Pi);
        }
        _ = factor;
        return new RecoveryResidual(measured.Rms, measured.P95, measured.Max,
            double.Max(distances[0], distances[^1]), maxAngle, distances.Count);
    }

    private static (double Rms, double P95, double Max) Distances(IReadOnlyList<double> distances)
    {
        var ordered = distances.OrderBy(x => x).ToArray();
        return (double.Sqrt(distances.Sum(x => x * x) / distances.Count),
            ordered[(int)double.Ceiling(0.95 * ordered.Length) - 1], ordered[^1]);
    }

    private static IReadOnlyList<RecoveryDimensionHypothesis> RepeatedDimensions(
        IReadOnlyList<RecoveryBody> bodies, double toleranceMm)
    {
        var observations = new List<(double Radius, string Source)>();
        foreach (var body in bodies)
        {
            foreach (var face in body.Faces)
                if (face.Candidate?.Qualification == RecoveryQualification.WithinSourceTolerance &&
                    face.Candidate.Parameters.TryGetValue("radiusMillimetres", out var radius))
                    observations.Add((radius, $"object:{body.ObjectIndex}/face:{face.FaceIndex}"));
            foreach (var edge in body.Edges)
            {
                var preferred = edge.Candidates.FirstOrDefault(c => c.Kind == edge.PreferredCandidate);
                if (preferred is not null && preferred.Parameters.TryGetValue("radiusMillimetres", out var radius))
                    observations.Add((radius, $"object:{body.ObjectIndex}/edge:{edge.EdgeIndex}"));
            }
        }
        var ordered = observations.OrderBy(o => o.Radius).ThenBy(o => o.Source, StringComparer.Ordinal).ToArray();
        var families = new List<RecoveryDimensionHypothesis>();
        for (var i = 0; i < ordered.Length;)
        {
            var end = i + 1;
            while (end < ordered.Length && ordered[end].Radius - ordered[i].Radius <= toleranceMm) end++;
            if (end - i > 1)
            {
                var group = ordered[i..end];
                families.Add(new RecoveryDimensionHypothesis("RepeatedRadius", group.Average(o => o.Radius),
                    group[^1].Radius - group[0].Radius, group.Select(o => o.Source).ToArray(),
                    "Geometric dimension hypothesis only; not an authored constraint."));
            }
            i = end;
        }
        return families;
    }
}
