using System.Globalization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum WireKnotFamily { Trefoil, FigureEight, TorusKnot }

public sealed record WireKnotQualification(
    double MinimumNonlocalDistanceMm,
    double MinimumCurvatureRadiusMm,
    double TubeRadiusLimitMm,
    double MaximumAdmittedDiameterMm,
    double ClosestParameter1,
    double ClosestParameter2,
    double LengthIntegrationErrorMm,
    string Method);

public sealed record WireKnotFrameClosure(
    double RawClosureRotationRadians,
    double AppliedCorrectionRadians,
    double FinalClosureErrorRadians,
    string Policy);

/// <summary>A named, evaluable, inherently periodic mathematical knot centerline.</summary>
public sealed record WireKnotPathAir(
    string Name,
    int Ordinal,
    WireKnotFamily Family,
    int? P,
    int? Q,
    int ComponentCount,
    double ScaleMm,
    double? MajorRadiusMm,
    double? MinorRadiusMm,
    WireCoilHandedness Handedness,
    double PhaseRadians,
    Point3D Origin,
    Direction3D BasisX,
    Direction3D BasisY,
    Direction3D BasisZ,
    WireKnotQualification Qualification,
    WireKnotFrameClosure FrameClosure,
    WireState Input,
    WireState Output,
    double LengthMm,
    double ApproximationToleranceMm,
    int Segments)
    : WireEvaluablePathAir(Name, Ordinal, Input, Output, LengthMm, ApproximationToleranceMm)
{
    public override bool Closed => true;
    public override int ApproximationSegmentCount => Segments;
    public double SeamParameter => 0d;
    public override Point3D Evaluate(double parameter) => WireKnotGeometry.Evaluate(this, parameter);
    public override Direction3D Tangent(double parameter) => Direction3D.Create(WireKnotGeometry.Derivative(this, parameter));
}

internal static class WireKnotAuthoring
{
    private const double ApproximationTolerance = 0.01d;

    internal static KernelResult<WireKnotPathAir> Create(string name, int ordinal, string body, WireState authoredFrame, double diameter)
    {
        var familyText = WireFormAuthoring.Property(body, "Family") ?? name;
        if (!Enum.TryParse<WireKnotFamily>(familyText, false, out var family))
            return Fail("wireform-knot-parameters-invalid", name, "Family must be Trefoil, FigureEight, or TorusKnot.");
        var handText = WireFormAuthoring.Property(body, "Handedness") ?? "RightHanded";
        if (!Enum.TryParse<WireCoilHandedness>(handText, false, out var handedness))
            return Fail("wireform-knot-parameters-invalid", name, "Handedness must be RightHanded or LeftHanded.");
        var phase = 0d;
        var phaseText = WireFormAuthoring.Property(body, "Phase") ?? WireFormAuthoring.Property(body, "StartPhase");
        if (phaseText is not null && !WireFormAuthoring.TryAngle(phaseText, out phase))
            return Fail("wireform-knot-parameters-invalid", name, "Phase must be a finite angle.");

        int? p = null, q = null;
        double scale, major = 0d, minor = 0d;
        if (family == WireKnotFamily.TorusKnot)
        {
            if (!Integer(body, "P", out var pi) || !Integer(body, "Q", out var qi) || pi < 2 || qi < 2)
                return Fail("wireform-knot-parameters-invalid", name, "TorusKnot requires integers P >= 2 and Q >= 2.");
            if (Gcd(pi, qi) != 1)
                return Fail("wireform-knot-not-single-component", name, $"P={pi}, Q={qi} have gcd={Gcd(pi, qi)} and define a link, not one knot component.");
            if (!Length(body, "MajorRadius", out major) || !Length(body, "MinorRadius", out minor) || major <= 0d || minor <= 0d || major <= minor)
                return Fail("wireform-knot-parameters-invalid", name, "TorusKnot requires MajorRadius > MinorRadius > 0.");
            p = pi; q = qi; scale = major;
        }
        else
        {
            if (!Length(body, "Scale", out scale) || scale <= 0d)
                return Fail("wireform-knot-scale-invalid", name, "Scale must be a finite positive length.");
        }

        var basisX = authoredFrame.Tangent;
        var basisZ = authoredFrame.Up;
        var basisY = Direction3D.Create(WireFormAuthoring.Cross(basisZ.ToVector(), basisX.ToVector()));
        var harmonics = family switch { WireKnotFamily.Trefoil => 3, WireKnotFamily.FigureEight => 4, _ => Math.Max(p!.Value, q!.Value) };
        var segments = Math.Max(96, harmonics * 32);
        var emptyQualification = new WireKnotQualification(0d, 0d, 0d, 0d, 0d, 0d, 0d, "pending");
        var emptyClosure = new WireKnotFrameClosure(0d, 0d, 0d, "pending");
        var seed = new WireKnotPathAir(name, ordinal, family, p, q, 1, scale,
            family == WireKnotFamily.TorusKnot ? major : null, family == WireKnotFamily.TorusKnot ? minor : null,
            handedness, phase, authoredFrame.Position, basisX, basisY, basisZ, emptyQualification, emptyClosure,
            authoredFrame, authoredFrame, 0d, ApproximationTolerance, segments);
        var (length, integrationError) = WireKnotGeometry.IntegrateLength(seed);
        var startPoint = seed.Evaluate(0d); var startTangent = seed.Tangent(0d);
        var projectedUp = basisZ.ToVector() - startTangent.ToVector() * WireFormAuthoring.Dot(basisZ.ToVector(), startTangent.ToVector());
        if (projectedUp.Length < 1e-10) projectedUp = basisY.ToVector() - startTangent.ToVector() * WireFormAuthoring.Dot(basisY.ToVector(), startTangent.ToVector());
        var startState = new WireState(startPoint, startTangent, Direction3D.Create(projectedUp), authoredFrame.AccumulatedLengthMm);
        seed = seed with { Input = startState, Output = startState with { AccumulatedLengthMm = authoredFrame.AccumulatedLengthMm + length }, LengthMm = length };
        var qualification = WireKnotGeometry.Qualify(seed, integrationError);
        var closure = WireKnotGeometry.MeasureFrameClosure(seed);
        return KernelResult<WireKnotPathAir>.Success(seed with { Qualification = qualification, FrameClosure = closure });
    }

    private static bool Length(string body, string name, out double value) => WireFormAuthoring.TryLength(WireFormAuthoring.Property(body, name), out value);
    private static bool Integer(string body, string name, out int value) => int.TryParse(WireFormAuthoring.Property(body, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    private static int Gcd(int a, int b) { while (b != 0) (a, b) = (b, a % b); return Math.Abs(a); }
    private static KernelResult<WireKnotPathAir> Fail(string code, string name, string message) => KernelResult<WireKnotPathAir>.Failure([new(
        Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error,
        $"{code}:{name}: {message}", "FirmamentV2.WireForm.KnotPath")]);
}

public static class WireKnotGeometry
{
    public static Point3D Evaluate(WireKnotPathAir knot, double t)
    {
        var (p, _, _) = Jet(knot, t);
        return knot.Origin + Transform(knot, p);
    }

    public static Vector3D Derivative(WireKnotPathAir knot, double t)
    {
        var (_, d1, _) = Jet(knot, t);
        return Transform(knot, d1);
    }

    private static Vector3D SecondDerivative(WireKnotPathAir knot, double t)
    {
        var (_, _, d2) = Jet(knot, t);
        return Transform(knot, d2);
    }

    private static (Vector3D P, Vector3D D1, Vector3D D2) Jet(WireKnotPathAir knot, double t)
    {
        var a = 2d * Math.PI * t + knot.PhaseRadians;
        var w = 2d * Math.PI;
        var mirror = knot.Handedness == WireCoilHandedness.RightHanded ? 1d : -1d;
        if (knot.Family == WireKnotFamily.Trefoil)
        {
            var s = knot.ScaleMm;
            return (new((Math.Sin(a) + 2d * Math.Sin(2d * a)) * s, (Math.Cos(a) - 2d * Math.Cos(2d * a)) * s, -mirror * Math.Sin(3d * a) * s),
                new((Math.Cos(a) + 4d * Math.Cos(2d * a)) * w * s, (-Math.Sin(a) + 4d * Math.Sin(2d * a)) * w * s, -mirror * 3d * Math.Cos(3d * a) * w * s),
                new((-Math.Sin(a) - 8d * Math.Sin(2d * a)) * w * w * s, (-Math.Cos(a) + 8d * Math.Cos(2d * a)) * w * w * s, mirror * 9d * Math.Sin(3d * a) * w * w * s));
        }
        if (knot.Family == WireKnotFamily.FigureEight)
        {
            var s = knot.ScaleMm; var r = 2d + Math.Cos(2d * a); var rp = -2d * Math.Sin(2d * a); var rpp = -4d * Math.Cos(2d * a);
            var x = r * Math.Cos(3d * a); var y = r * Math.Sin(3d * a); var z = mirror * Math.Sin(4d * a);
            var xa = rp * Math.Cos(3d * a) - 3d * r * Math.Sin(3d * a); var ya = rp * Math.Sin(3d * a) + 3d * r * Math.Cos(3d * a); var za = mirror * 4d * Math.Cos(4d * a);
            var xaa = rpp * Math.Cos(3d * a) - 6d * rp * Math.Sin(3d * a) - 9d * r * Math.Cos(3d * a); var yaa = rpp * Math.Sin(3d * a) + 6d * rp * Math.Cos(3d * a) - 9d * r * Math.Sin(3d * a); var zaa = -mirror * 16d * Math.Sin(4d * a);
            return (new(x * s, y * s, z * s), new(xa * w * s, ya * w * s, za * w * s), new(xaa * w * w * s, yaa * w * w * s, zaa * w * w * s));
        }
        var p = knot.P!.Value; var q = knot.Q!.Value; var major = knot.MajorRadiusMm!.Value; var minor = knot.MinorRadiusMm!.Value;
        var radial = major + minor * Math.Cos(q * a); var radialA = -minor * q * Math.Sin(q * a); var radialAA = -minor * q * q * Math.Cos(q * a);
        var x0 = radial * Math.Cos(p * a); var y0 = radial * Math.Sin(p * a); var z0 = mirror * minor * Math.Sin(q * a);
        var x1 = radialA * Math.Cos(p * a) - p * radial * Math.Sin(p * a); var y1 = radialA * Math.Sin(p * a) + p * radial * Math.Cos(p * a); var z1 = mirror * minor * q * Math.Cos(q * a);
        var x2 = radialAA * Math.Cos(p * a) - 2d * p * radialA * Math.Sin(p * a) - p * p * radial * Math.Cos(p * a); var y2 = radialAA * Math.Sin(p * a) + 2d * p * radialA * Math.Cos(p * a) - p * p * radial * Math.Sin(p * a); var z2 = -mirror * minor * q * q * Math.Sin(q * a);
        return (new(x0, y0, z0), new(x1 * w, y1 * w, z1 * w), new(x2 * w * w, y2 * w * w, z2 * w * w));
    }

    internal static (double Length, double Error) IntegrateLength(WireKnotPathAir knot)
    {
        double Simpson(int n)
        {
            var total = Derivative(knot, 0d).Length + Derivative(knot, 1d).Length;
            for (var i = 1; i < n; i++) total += (i % 2 == 0 ? 2d : 4d) * Derivative(knot, (double)i / n).Length;
            return total / (3d * n);
        }
        var coarse = Simpson(4096); var fine = Simpson(8192);
        return (fine, Math.Abs(fine - coarse) / 15d);
    }

    internal static WireKnotQualification Qualify(WireKnotPathAir knot, double integrationError)
    {
        const int count = 1024; const double safety = 1e-4;
        var points = Enumerable.Range(0, count).Select(i => knot.Evaluate((double)i / count)).ToArray();
        // Exclude one eighth of the periodic domain in either direction. This is a
        // deliberately bounded nonlocal witness: it excludes the same smooth strand
        // and its seam-neighbourhood rather than mistaking nearby arclength for a
        // distinct crossing branch.
        var exclusion = count / 8; var best = double.PositiveInfinity; var bestI = 0; var bestJ = 0;
        for (var i = 0; i < count; i++) for (var j = i + 1; j < count; j++)
        {
            var cyclic = Math.Min(j - i, count - (j - i));
            if (cyclic < exclusion) continue;
            var distance = (points[i] - points[j]).Length;
            if (distance < best) { best = distance; bestI = i; bestJ = j; }
        }
        var chordError = 0d;
        for (var i = 0; i < count; i++)
        {
            var midpoint = knot.Evaluate((i + .5d) / count); var chordMidpoint = points[i] + (points[(i + 1) % count] - points[i]) * .5d;
            chordError = Math.Max(chordError, (midpoint - chordMidpoint).Length);
        }
        var conservativeDistance = Math.Max(0d, best - 2d * chordError);
        var minCurvature = double.PositiveInfinity;
        for (var i = 0; i < 8192; i++)
        {
            var t = (double)i / 8192; var d1 = Derivative(knot, t); var d2 = SecondDerivative(knot, t);
            var cross = WireFormAuthoring.Cross(d1, d2).Length;
            if (cross > 1e-20) minCurvature = Math.Min(minCurvature, d1.Length * d1.Length * d1.Length / cross);
        }
        var limit = Math.Min(conservativeDistance / 2d, minCurvature);
        return new(conservativeDistance, minCurvature, limit, Math.Max(0d, 2d * (limit - safety)),
            (double)bestI / count, (double)bestJ / count, integrationError,
            "Deterministic sampled conservative chord bound; approximate tube admissibility, not a formal reach proof");
    }

    internal static WireKnotFrameClosure MeasureFrameClosure(WireKnotPathAir knot)
    {
        var initial = knot.Input.Up.ToVector(); var up = initial; var previous = knot.Tangent(0d).ToVector();
        var count = knot.ApproximationSegmentCount;
        for (var i = 1; i <= count; i++)
        {
            var next = knot.Tangent((double)i / count).ToVector(); up = WireCoilGeometry.RotateFromTo(up, previous, next);
            up = Direction3D.Create(up - next * WireFormAuthoring.Dot(up, next)).ToVector(); previous = next;
        }
        var tangent = knot.Tangent(0d).ToVector(); var raw = SignedAngle(initial, up, tangent); var correction = -raw;
        var corrected = WireCoilGeometry.Rotate(up, tangent, correction); var final = Math.Abs(SignedAngle(initial, corrected, tangent));
        return new(raw, correction, final, "Rotation-minimizing transport with deterministic linear distributed holonomy correction");
    }

    internal static double SignedAngle(Vector3D from, Vector3D to, Vector3D axis)
        => Math.Atan2(WireFormAuthoring.Dot(axis, WireFormAuthoring.Cross(from, to)), WireFormAuthoring.Dot(from, to));
    private static Vector3D Transform(WireKnotPathAir knot, Vector3D local)
        => knot.BasisX.ToVector() * local.X + knot.BasisY.ToVector() * local.Y + knot.BasisZ.ToVector() * local.Z;
}

internal static class WireKnotBRepMaterializer
{
    private sealed record Station(Point3D Point, Direction3D Tangent, Direction3D Up);

    internal static KernelResult<WireFormBuildResult> Build(WireFormFeatureAir feature)
    {
        try
        {
            var knot = (WireKnotPathAir)feature.Operations.Single();
            var diagnostics = WireFormBRepMaterializer.Validate(feature);
            if (diagnostics.Count > 0) return KernelResult<WireFormBuildResult>.Failure(diagnostics.Select(Diagnostic).ToArray());
            var stations = Stations(knot); var r = feature.WireRadiusMm;
            var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>(); var curves = 1; var surfaces = 1;
            var vertices = new VertexId[stations.Count, 4]; var rings = new EdgeId[stations.Count, 4]; var ringControls = new Point3D[stations.Count, 4][];
            for (var i = 0; i < stations.Count; i++)
            {
                var right = Direction3D.Create(WireFormAuthoring.Cross(stations[i].Tangent.ToVector(), stations[i].Up.ToVector()));
                for (var q = 0; q < 4; q++) { var controls = Quarter(stations[i].Point, stations[i].Up, right, r, q); ringControls[i, q] = controls; vertices[i, q] = builder.AddVertex(); points[vertices[i, q]] = controls[0]; }
                for (var q = 0; q < 4; q++) { rings[i, q] = builder.AddEdge(vertices[i, q], vertices[i, (q + 1) % 4]); geometry.AddCurve(new(curves), CurveGeometry.FromBSpline(Bezier(ringControls[i, q]))); bindings.AddEdgeBinding(new(rings[i, q], new(curves++), new ParameterInterval(0, 1))); }
            }
            var faces = new List<FaceId>();
            for (var i = 0; i < stations.Count; i++)
            {
                var next = (i + 1) % stations.Count; var center = CenterBezier(stations[i], stations[next], knot, i); var nets = new Point3D[4][][];
                for (var q = 0; q < 4; q++)
                {
                    var net = new Point3D[4][];
                    for (var u = 0; u < 4; u++)
                    {
                        var alpha = u / 3d; var tangent = Direction3D.Create(stations[i].Tangent.ToVector() * (1d - alpha) + stations[next].Tangent.ToVector() * alpha);
                        var upVector = stations[i].Up.ToVector() * (1d - alpha) + stations[next].Up.ToVector() * alpha; var up = Direction3D.Create(upVector - tangent.ToVector() * WireFormAuthoring.Dot(upVector, tangent.ToVector()));
                        net[u] = Quarter(center[u], up, Direction3D.Create(WireFormAuthoring.Cross(tangent.ToVector(), up.ToVector())), r, q);
                    }
                    nets[q] = net;
                }
                var longitudinal = new EdgeId[4];
                for (var q = 0; q < 4; q++) { longitudinal[q] = builder.AddEdge(vertices[i, q], vertices[next, q]); geometry.AddCurve(new(curves), CurveGeometry.FromBSpline(Bezier(nets[q].Select(row => row[0]).ToArray()))); bindings.AddEdgeBinding(new(longitudinal[q], new(curves++), new ParameterInterval(0, 1))); }
                for (var q = 0; q < 4; q++)
                {
                    var surfaceId = new SurfaceGeometryId(surfaces++); geometry.AddSurface(surfaceId, SurfaceGeometry.FromBSplineSurfaceWithKnots(new(3, 3, nets[q], "UNSPECIFIED", false, false, false, [4, 4], [4, 4], [0d, 1d], [0d, 1d], "UNSPECIFIED")));
                    var (face, coedges) = AddFace(builder, [(longitudinal[q], false), (rings[next, q], false), (longitudinal[(q + 1) % 4], true), (rings[i, q], true)]); bindings.AddFaceBinding(new(face, surfaceId)); faces.Add(face);
                    AddPcurve(bindings, coedges[0], face, surfaceId, new(0, 0), new(1, 0)); AddPcurve(bindings, coedges[1], face, surfaceId, new(1, 0), new(1, 1));
                    AddPcurve(bindings, coedges[2], face, surfaceId, new(0, 1), new(1, 1)); AddPcurve(bindings, coedges[3], face, surfaceId, new(0, 0), new(0, 1));
                }
            }
            var shell = builder.AddShell(faces); builder.AddBody([shell]); var body = new BrepBody(builder.Model, geometry, bindings, points);
            var bindingValidation = BrepBindingValidator.Validate(body, true); if (!bindingValidation.IsSuccess) return KernelResult<WireFormBuildResult>.Failure(bindingValidation.Diagnostics);
            var pcurves = BrepPcurveValidator.Validate(body, 1e-6, true); if (!pcurves.IsValid) return KernelResult<WireFormBuildResult>.Failure(pcurves.Diagnostics.Select(Diagnostic).ToArray());
            var preflight = BrepExportPreflight.Validate(body); if (!preflight.IsValid) return KernelResult<WireFormBuildResult>.Failure(preflight.Diagnostics.Where(x => x.Severity == BrepExportPreflightSeverity.Error).Select(x => Diagnostic($"wireform-knot-brep-invalid:{x.Code}:{x.Message}")).ToArray());
            var samples = Enumerable.Range(0, 2048).Select(i => knot.Evaluate((double)i / 2048)).ToArray(); var bounds = new[] { samples.Min(p => p.X) - r, samples.Min(p => p.Y) - r, samples.Min(p => p.Z) - r, samples.Max(p => p.X) + r, samples.Max(p => p.Y) + r, samples.Max(p => p.Z) + r };
            var volume = Math.PI * r * r * knot.LengthMm; var mass = volume * 1e-9 * feature.Material.Structural!.Density.SiValue;
            return KernelResult<WireFormBuildResult>.Success(new(feature, body, volume, mass, bounds,
                ["wireform-knot-periodic-semantic-centerline", "wireform-knot-frame-holonomy-corrected", $"wireform-knot-pcurves:{pcurves.PcurveCount}:max-error={pcurves.MaximumReconstructionDeviation:R}", "wireform-knot-non-rational-bspline-sweep", "wireform-knot-no-terminal-caps"]));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        { return KernelResult<WireFormBuildResult>.Failure([Diagnostic("wireform-knot-brep-construction-failed: " + exception.Message)]); }
    }

    private static List<Station> Stations(WireKnotPathAir knot)
    {
        var result = new List<Station>(); var count = knot.ApproximationSegmentCount; var up = knot.Input.Up.ToVector(); var previous = knot.Tangent(0d).ToVector(); var correction = knot.FrameClosure.AppliedCorrectionRadians;
        for (var i = 0; i < count; i++)
        {
            var t = (double)i / count; var tangent = knot.Tangent(t);
            if (i > 0) up = WireCoilGeometry.RotateFromTo(up, previous, tangent.ToVector());
            up = Direction3D.Create(up - tangent.ToVector() * WireFormAuthoring.Dot(up, tangent.ToVector())).ToVector();
            var corrected = WireCoilGeometry.Rotate(up, tangent.ToVector(), correction * t); result.Add(new(knot.Evaluate(t), tangent, Direction3D.Create(corrected))); previous = tangent.ToVector();
        }
        return result;
    }

    private static Point3D[] CenterBezier(Station a, Station b, WireKnotPathAir knot, int span)
    {
        var dt = 1d / knot.ApproximationSegmentCount; var t0 = (double)span / knot.ApproximationSegmentCount; var t1 = (double)(span + 1) / knot.ApproximationSegmentCount;
        var p0 = knot.Evaluate(t0); var p1 = knot.Evaluate(t1); return [p0, p0 + WireKnotGeometry.Derivative(knot, t0) * (dt / 3d), p1 - WireKnotGeometry.Derivative(knot, t1) * (dt / 3d), p1];
    }
    private static Point3D[] Quarter(Point3D center, Direction3D up, Direction3D right, double radius, int quarter) { var a = quarter * Math.PI / 2d; var b = a + Math.PI / 2d; const double k = .5522847498307936; Vector3D E(double x) => up.ToVector() * Math.Cos(x) + right.ToVector() * Math.Sin(x); Vector3D D(double x) => up.ToVector() * -Math.Sin(x) + right.ToVector() * Math.Cos(x); return [center + E(a) * radius, center + (E(a) + D(a) * k) * radius, center + (E(b) - D(b) * k) * radius, center + E(b) * radius]; }
    private static BSpline3Curve Bezier(IReadOnlyList<Point3D> controls) => new(3, controls, [4, 4], [0d, 1d], "UNSPECIFIED", false, false, "UNSPECIFIED");
    private static (FaceId Face, CoedgeId[] Coedges) AddFace(TopologyBuilder builder, IReadOnlyList<(EdgeId Edge, bool Reversed)> uses) { var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray(); for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed)); builder.AddLoop(new Loop(loop, ids)); return (builder.AddFace([loop]), ids); }
    private static void AddPcurve(BrepBindingModel bindings, CoedgeId coedge, FaceId face, SurfaceGeometryId surface, SurfaceParameterPoint start, SurfaceParameterPoint end) => bindings.AddPcurveBinding(new(coedge, face, surface, PcurveGeometry.Line(new(0, 1), start, end)));
    private static Core.Diagnostics.KernelDiagnostic Diagnostic(string message) => new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "FirmamentV2.WireForm.KnotPath");
}
