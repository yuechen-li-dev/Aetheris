using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

internal static class WireCoilAuthoring
{
    private const double ApproximationTolerance = 0.01d;

    internal static KernelResult<WireAxisCoilAir> CreateAxis(string name, int ordinal, string body, WireState input, double diameter)
    {
        if (!Length(body, "Radius", out var radius) || radius <= 0d) return FailAxis("wireform-coil-radius-invalid", name, "Radius must be finite and greater than zero.");
        if (!Number(body, "Turns", out var turns) || turns <= 0d) return FailAxis("wireform-coil-turns-invalid", name, "Turns must be finite and greater than zero.");
        var hasPitch = Length(body, "Pitch", out var pitch); var hasHeight = Length(body, "Height", out var height);
        if (!hasPitch && !hasHeight) return FailAxis("wireform-coil-parameters-inconsistent", name, "Specify Turns and either Pitch or Height.");
        if (hasPitch && pitch <= 0d || hasHeight && height <= 0d) return FailAxis("wireform-coil-parameters-inconsistent", name, "Pitch and Height must be greater than zero.");
        if (!hasPitch) pitch = height / turns; else if (!hasHeight) height = pitch * turns;
        else if (Math.Abs(height - pitch * turns) > 1e-8 * Math.Max(1d, height))
            return FailAxis("wireform-coil-parameters-inconsistent", name, $"Height must equal Turns × Pitch ({turns * pitch:G12} mm).");
        if (!Hand(body, out var hand)) return FailAxis("wireform-coil-handedness-invalid", name, "Handedness must be RightHanded or LeftHanded.");
        if (!Phase(body, out var phase)) return FailAxis("wireform-coil-start-phase-invalid", name, "StartPhase must be a finite angle.");
        var sign = hand == WireCoilHandedness.RightHanded ? 1d : -1d;
        var q = height / (2d * Math.PI * turns);
        var axis = Direction3D.Create(input.Tangent.ToVector() * q + input.Up.ToVector() * (sign * radius));
        var radial = input.Right;
        if (Math.Abs(phase) > 1e-15) { axis = Direction3D.Create(Rotate(axis.ToVector(), input.Tangent.ToVector(), phase)); radial = Direction3D.Create(Rotate(radial.ToVector(), input.Tangent.ToVector(), phase)); }
        var origin = input.Position - radial.ToVector() * radius;
        var length = Math.Sqrt(Math.Pow(2d * Math.PI * radius * turns, 2d) + height * height);
        var clearance = pitch - diameter;
        var prototype = new WireAxisCoilAir(name, ordinal, radius, turns, pitch, height, hand, phase, origin, axis, radial,
            input, input, length, clearance, ApproximationTolerance);
        var endPosition = prototype.Evaluate(1d); var endTangent = prototype.Tangent(1d);
        var endUp = WireCoilGeometry.TransportUp(input.Up, prototype.Tangent, Math.Max(32, (int)Math.Ceiling(turns * 32d)));
        var output = new WireState(endPosition, endTangent, endUp, input.AccumulatedLengthMm + length);
        return KernelResult<WireAxisCoilAir>.Success(prototype with { Output = output });
    }

    internal static KernelResult<WireSurfaceCoilAir> CreateSurface(string source, string name, int ordinal, string body, WireState input, double diameter)
    {
        var supportName = WireFormAuthoring.Property(body, "Surface") ?? WireFormAuthoring.Property(body, "Support");
        if (string.IsNullOrWhiteSpace(supportName)) return FailSurface("wireform-surfacecoil-support-unsupported", name, "Surface must name an authored Cylinder, Frustum, Cone, or Sphere.");
        var declaration = Regex.Match(source, $@"\b(?<kind>Cylinder|Frustum|Cone|Sphere)\s+{Regex.Escape(supportName.Trim())}\s*\{{", RegexOptions.CultureInvariant);
        if (!declaration.Success) return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Only authored analytic Cylinder, Frustum/Cone, and Sphere supports are admitted.");
        var open = source.IndexOf('{', declaration.Index); var close = MatchingBrace(source, open);
        if (close < 0) return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Support declaration is malformed.");
        var supportBody = source[(open + 1)..close]; var kind = declaration.Groups["kind"].Value; if (kind == "Cone") kind = "Frustum";
        if (!Number(body, "Turns", out var turns) || turns <= 0d) return FailSurface("wireform-coil-turns-invalid", name, "Turns must be finite and greater than zero.");
        if (!Hand(body, out var hand)) return FailSurface("wireform-coil-handedness-invalid", name, "Handedness must be RightHanded or LeftHanded.");
        if (!Phase(body, out var phase)) return FailSurface("wireform-coil-start-phase-invalid", name, "StartPhase must be a finite angle.");
        var sideText = WireFormAuthoring.Property(body, "Side") ?? "Outside";
        if (!Enum.TryParse<WireSurfaceSide>(sideText, false, out var side)) return FailSurface("wireform-surfacecoil-side-invalid", name, "Side must be Outside or Inside.");
        var hasClearance = Length(body, "Clearance", out var clearance); var hasOffset = Length(body, "CenterlineOffset", out var offset);
        if (hasClearance == hasOffset) return FailSurface("wireform-surfacecoil-offset-invalid", name, "Specify exactly one of Clearance or CenterlineOffset.");
        if (hasClearance && clearance < 0d || hasOffset && offset <= 0d) return FailSurface("wireform-surfacecoil-offset-invalid", name, "Clearance cannot be negative and CenterlineOffset must be positive.");
        if (hasClearance) offset = diameter / 2d + clearance; else clearance = offset - diameter / 2d;
        if (clearance < -1e-9) return FailSurface("wireform-surfacecoil-offset-invalid", name, "CenterlineOffset is smaller than the wire radius and would penetrate the support.");

        double r0, r1, supportHeight, pitch = 0d, startLat = 0d, endLat = 0d;
        if (kind == "Sphere")
        {
            if (!Length(supportBody, "Radius", out r0) || r0 <= 0d) return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Sphere Radius must be positive.");
            r1 = r0; supportHeight = 2d * r0;
            if (!Angle(body, "StartLatitude", out startLat) || !Angle(body, "EndLatitude", out endLat)) return FailSurface("wireform-surfacecoil-pole-singularity", name, "Sphere winding requires StartLatitude and EndLatitude.");
            if (Math.Abs(startLat) >= Math.PI / 2d - 1e-6 || Math.Abs(endLat) >= Math.PI / 2d - 1e-6)
                return FailSurface("wireform-surfacecoil-pole-singularity", name, "Latitude bounds must remain away from ±90 degrees.");
            var rho = side == WireSurfaceSide.Outside ? r0 + offset : r0 - offset;
            if (rho <= diameter / 2d) return FailSurface("wireform-surfacecoil-offset-invalid", name, "Inward sphere offset collapses the usable support.");
        }
        else
        {
            if (kind == "Cylinder")
            {
                if (!Length(supportBody, "Radius", out r0) || r0 <= 0d) return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Cylinder Radius must be positive.");
                r1 = r0;
            }
            else
            {
                if (!Length(supportBody, "BottomRadius", out r0) || !Length(supportBody, "TopRadius", out r1) || r0 < 0d || r1 < 0d || r0 + r1 <= 0d)
                    return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Frustum requires nonnegative BottomRadius and TopRadius.");
            }
            if (!Length(supportBody, "Height", out supportHeight) || supportHeight <= 0d) return FailSurface("wireform-surfacecoil-support-unsupported", supportName.Trim(), "Support Height must be positive.");
            if (!Length(body, "AxialPitch", out pitch) && !Length(body, "Pitch", out pitch) || pitch <= 0d) return FailSurface("wireform-coil-parameters-inconsistent", name, "Cylinder/frustum winding requires positive AxialPitch.");
            if (turns * pitch > supportHeight + 1e-9) return FailSurface("wireform-surfacecoil-span-invalid", name, "Turns × AxialPitch exceeds support Height.");
        }

        var sign = hand == WireCoilHandedness.RightHanded ? 1d : -1d;
        Direction3D axis, radial, longitude, latitude; Point3D origin;
        if (kind == "Sphere")
        {
            var rho = side == WireSurfaceSide.Outside ? r0 + offset : r0 - offset;
            var a = sign * 2d * Math.PI * turns * rho * Math.Cos(startLat); var b = (endLat - startLat) * rho; var norm = Math.Sqrt(a * a + b * b);
            var e0 = input.Up; var right = input.Right;
            longitude = Direction3D.Create(input.Tangent.ToVector() * (a / norm) + right.ToVector() * (b / norm));
            axis = Direction3D.Create(input.Tangent.ToVector() * (b / norm) - right.ToVector() * (a / norm));
            latitude = axis;
            radial = e0;
            var radialAtStart = e0.ToVector() * Math.Cos(startLat) + axis.ToVector() * Math.Sin(startLat);
            if (Math.Abs(phase) > 1e-15) { radial = Direction3D.Create(Rotate(radial.ToVector(), input.Tangent.ToVector(), phase)); longitude = Direction3D.Create(Rotate(longitude.ToVector(), input.Tangent.ToVector(), phase)); axis = Direction3D.Create(Rotate(axis.ToVector(), input.Tangent.ToVector(), phase)); latitude = axis; radialAtStart = radial.ToVector() * Math.Cos(startLat) + axis.ToVector() * Math.Sin(startLat); }
            origin = input.Position - radialAtStart * rho;
        }
        else
        {
            var slope = (r1 - r0) / supportHeight; var normalScale = Math.Sqrt(1d + slope * slope); var sideSign = side == WireSurfaceSide.Outside ? 1d : -1d;
            var effectiveR0 = r0 + sideSign * offset / normalScale; var effectiveR1 = r1 + sideSign * offset / normalScale;
            if (effectiveR0 <= diameter / 2d || effectiveR1 <= diameter / 2d) return FailSurface("wireform-surfacecoil-offset-invalid", name, "Offset frustum/cylinder radius collapses or inverts.");
            var c = (effectiveR1 - effectiveR0) * pitch * turns / supportHeight; var a = sign * 2d * Math.PI * turns * effectiveR0; var b = pitch * turns;
            var nominalRadial = input.Right; var nominalAxis = input.Up; var nominalCirc = Direction3D.Create(WireFormAuthoring.Cross(nominalAxis.ToVector(), nominalRadial.ToVector()));
            var derivative = nominalRadial.ToVector() * c + nominalCirc.ToVector() * a + nominalAxis.ToVector() * b;
            radial = Direction3D.Create(WireCoilGeometry.RotateFromTo(nominalRadial.ToVector(), derivative, input.Tangent.ToVector()));
            axis = Direction3D.Create(WireCoilGeometry.RotateFromTo(nominalAxis.ToVector(), derivative, input.Tangent.ToVector()));
            longitude = Direction3D.Create(WireFormAuthoring.Cross(axis.ToVector(), radial.ToVector())); latitude = axis;
            if (Math.Abs(phase) > 1e-15) { radial = Direction3D.Create(Rotate(radial.ToVector(), input.Tangent.ToVector(), phase)); axis = Direction3D.Create(Rotate(axis.ToVector(), input.Tangent.ToVector(), phase)); longitude = Direction3D.Create(WireFormAuthoring.Cross(axis.ToVector(), radial.ToVector())); latitude = axis; }
            var axialShift = -sideSign * slope * offset / normalScale;
            origin = input.Position - radial.ToVector() * effectiveR0 - axis.ToVector() * axialShift;
            r0 = effectiveR0; r1 = effectiveR1;
        }

        var prototype = new WireSurfaceCoilAir(name, ordinal, supportName.Trim(), kind, side, clearance, offset, turns, pitch,
            startLat, endLat, hand, phase, r0, r1, supportHeight, origin, axis, radial, longitude, latitude,
            input, input, 0d, 0d, clearance, ApproximationTolerance);
        var length = WireCoilGeometry.IntegrateLength(prototype); var self = WireCoilGeometry.MeasureSelfClearance(prototype, diameter);
        var endUp = WireCoilGeometry.TransportUp(input.Up, prototype.Tangent, Math.Max(64, (int)Math.Ceiling(turns * 48d)));
        var output = new WireState(prototype.Evaluate(1d), prototype.Tangent(1d), endUp, input.AccumulatedLengthMm + length);
        return KernelResult<WireSurfaceCoilAir>.Success(prototype with { LengthMm = length, MinimumSelfClearanceMm = self, Output = output });
    }

    private static bool Length(string body, string name, out double value) => WireFormAuthoring.TryLength(WireFormAuthoring.Property(body, name), out value);
    private static bool Angle(string body, string name, out double value) => WireFormAuthoring.TryAngle(WireFormAuthoring.Property(body, name), out value);
    private static bool Number(string body, string name, out double value) => double.TryParse(WireFormAuthoring.Property(body, name), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    private static bool Hand(string body, out WireCoilHandedness hand) => Enum.TryParse(WireFormAuthoring.Property(body, "Handedness") ?? "RightHanded", false, out hand);
    private static bool Phase(string body, out double phase) { var text = WireFormAuthoring.Property(body, "StartPhase"); if (text is null) { phase = 0d; return true; } return WireFormAuthoring.TryAngle(text, out phase); }
    private static int MatchingBrace(string source, int open) { var depth = 0; for (var i = open; i >= 0 && i < source.Length; i++) { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; } return -1; }
    private static Vector3D Rotate(Vector3D v, Vector3D a, double angle) => v * Math.Cos(angle) + WireFormAuthoring.Cross(a, v) * Math.Sin(angle) + a * (WireFormAuthoring.Dot(a, v) * (1d - Math.Cos(angle)));
    private static KernelResult<WireAxisCoilAir> FailAxis(string code, string name, string message) => KernelResult<WireAxisCoilAir>.Failure([Diagnostic($"{code}:{name}: {message}")]);
    private static KernelResult<WireSurfaceCoilAir> FailSurface(string code, string name, string message) => KernelResult<WireSurfaceCoilAir>.Failure([Diagnostic($"{code}:{name}: {message}")]);
    private static Core.Diagnostics.KernelDiagnostic Diagnostic(string message) => new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "FirmamentV2.WireForm.Coil");
}

public static class WireCoilGeometry
{
    public static Point3D EvaluateAxis(WireAxisCoilAir coil, double t)
    {
        var theta = (coil.Handedness == WireCoilHandedness.RightHanded ? 1d : -1d) * 2d * Math.PI * coil.Turns * Math.Clamp(t, 0d, 1d);
        return coil.AxisOrigin + Rotate(coil.StartRadial.ToVector(), coil.Axis.ToVector(), theta) * coil.RadiusMm + coil.Axis.ToVector() * (coil.HeightMm * t);
    }
    public static Direction3D TangentAxis(WireAxisCoilAir coil, double t)
    {
        var sign = coil.Handedness == WireCoilHandedness.RightHanded ? 1d : -1d; var radial = Rotate(coil.StartRadial.ToVector(), coil.Axis.ToVector(), sign * 2d * Math.PI * coil.Turns * t);
        return Direction3D.Create(WireFormAuthoring.Cross(coil.Axis.ToVector(), radial) * (sign * 2d * Math.PI * coil.Turns * coil.RadiusMm) + coil.Axis.ToVector() * coil.HeightMm);
    }
    public static Point3D EvaluateSurface(WireSurfaceCoilAir coil, double t)
    {
        t = Math.Clamp(t, 0d, 1d); var sign = coil.Handedness == WireCoilHandedness.RightHanded ? 1d : -1d; var theta = sign * 2d * Math.PI * coil.Turns * t;
        if (coil.SupportKind == "Sphere")
        {
            var rho = coil.Side == WireSurfaceSide.Outside ? coil.SupportRadius0Mm + coil.CenterlineOffsetMm : coil.SupportRadius0Mm - coil.CenterlineOffsetMm;
            var lat = coil.StartLatitudeRadians + (coil.EndLatitudeRadians - coil.StartLatitudeRadians) * t;
            var equator = Rotate(coil.StartRadial.ToVector(), coil.SupportAxis.ToVector(), theta);
            return coil.SupportOrigin + (equator * Math.Cos(lat) + coil.SupportAxis.ToVector() * Math.Sin(lat)) * rho;
        }
        var r = coil.SupportRadius0Mm + (coil.SupportRadius1Mm - coil.SupportRadius0Mm) * (coil.AxialPitchMm * coil.Turns * t / coil.SupportHeightMm);
        return coil.SupportOrigin + Rotate(coil.StartRadial.ToVector(), coil.SupportAxis.ToVector(), theta) * r + coil.SupportAxis.ToVector() * (coil.AxialPitchMm * coil.Turns * t);
    }
    public static Direction3D TangentSurface(WireSurfaceCoilAir coil, double t)
    {
        const double h = 1e-6; var a = EvaluateSurface(coil, Math.Max(0d, t - h)); var b = EvaluateSurface(coil, Math.Min(1d, t + h)); return Direction3D.Create(b - a);
    }
    public static double IntegrateLength(WireCoilAir coil)
    {
        const int n = 4096; var total = 0d; var p = coil.Evaluate(0d); for (var i = 1; i <= n; i++) { var q = coil.Evaluate((double)i / n); total += (q - p).Length; p = q; } return total;
    }
    public static double MeasureSelfClearance(WireCoilAir coil, double diameter)
    {
        var count = Math.Max(256, (int)Math.Ceiling(coil.Turns * 128d)); var points = Enumerable.Range(0, count + 1).Select(i => coil.Evaluate((double)i / count)).ToArray(); var exclusion = Math.Max(2, (int)Math.Floor(count / coil.Turns * .6d)); var best = double.PositiveInfinity;
        for (var i = 0; i < points.Length; i++) for (var j = i + exclusion; j < points.Length; j++) best = Math.Min(best, (points[i] - points[j]).Length);
        return best - diameter;
    }
    public static (double MaxMm, double RmsMm) MeasureCenterlineApproximation(WireCoilAir coil, int segments) => WireEvaluablePathGeometry.MeasureCenterlineApproximation(coil, segments);
    public static Direction3D TransportUp(Direction3D initial, Func<double, Direction3D> tangent, int count)
    {
        var up = initial.ToVector(); var previous = tangent(0d).ToVector(); for (var i = 1; i <= count; i++) { var next = tangent((double)i / count).ToVector(); up = RotateFromTo(up, previous, next); up -= next * WireFormAuthoring.Dot(up, next); up = Direction3D.Create(up).ToVector(); previous = next; } return Direction3D.Create(up);
    }
    internal static Vector3D RotateFromTo(Vector3D value, Vector3D from, Vector3D to)
    {
        var f = Direction3D.Create(from).ToVector(); var t = Direction3D.Create(to).ToVector(); var dot = Math.Clamp(WireFormAuthoring.Dot(f, t), -1d, 1d); if (dot > 1d - 1e-14) return value;
        var cross = WireFormAuthoring.Cross(f, t); if (cross.Length < 1e-12) { var seed = Math.Abs(f.X) < .8 ? new Vector3D(1, 0, 0) : new Vector3D(0, 1, 0); cross = WireFormAuthoring.Cross(f, seed); }
        return Rotate(value, Direction3D.Create(cross).ToVector(), Math.Acos(dot));
    }
    internal static Vector3D Rotate(Vector3D v, Vector3D a, double angle) => v * Math.Cos(angle) + WireFormAuthoring.Cross(a, v) * Math.Sin(angle) + a * (WireFormAuthoring.Dot(a, v) * (1d - Math.Cos(angle)));
}

public static class WireEvaluablePathGeometry
{
    public static (double MaxMm, double RmsMm) MeasureCenterlineApproximation(WireEvaluablePathAir path, int segments)
    {
        var squared = 0d; var maximum = 0d; var count = 0;
        for (var segment = 0; segment < segments; segment++)
        {
            var t0 = (double)segment / segments; var t1 = (double)(segment + 1) / segments; var p0 = path.Evaluate(t0); var p1 = path.Evaluate(t1); var chord = (p1 - p0).Length;
            var d0 = path is WireKnotPathAir knot0 ? WireKnotGeometry.Derivative(knot0, t0) / segments : path.Tangent(t0).ToVector() * chord;
            var d1 = path is WireKnotPathAir knot1 ? WireKnotGeometry.Derivative(knot1, t1) / segments : path.Tangent(t1).ToVector() * chord;
            var controls = new[] { p0, p0 + d0 / 3d, p1 - d1 / 3d, p1 };
            foreach (var local in new[] { .25d, .5d, .75d }) { var inverse = 1d - local; var approximate = new Point3D(controls[0].X * inverse * inverse * inverse + 3d * controls[1].X * inverse * inverse * local + 3d * controls[2].X * inverse * local * local + controls[3].X * local * local * local, controls[0].Y * inverse * inverse * inverse + 3d * controls[1].Y * inverse * inverse * local + 3d * controls[2].Y * inverse * local * local + controls[3].Y * local * local * local, controls[0].Z * inverse * inverse * inverse + 3d * controls[1].Z * inverse * inverse * local + 3d * controls[2].Z * inverse * local * local + controls[3].Z * local * local * local); var exact = path.Evaluate(t0 + (t1 - t0) * local); var error = (approximate - exact).Length; maximum = Math.Max(maximum, error); squared += error * error; count++; }
        }
        return (maximum, Math.Sqrt(squared / count));
    }
}

internal static class WireCoilBRepMaterializer
{
    private sealed record Station(Point3D Point, Direction3D Tangent, Direction3D Up, double StepLength);
    internal static KernelResult<WireFormBuildResult> Build(WireFormFeatureAir feature)
    {
        try
        {
            var stations = Stations(feature); var r = feature.WireRadiusMm; var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel(); var points = new Dictionary<VertexId, Point3D>(); var curves = 1; var surfaces = 1;
            var vertices = new VertexId[stations.Count, 4]; var rings = new EdgeId[stations.Count, 4]; var ringControls = new Point3D[stations.Count, 4][];
            for (var i = 0; i < stations.Count; i++)
            {
                var right = Direction3D.Create(WireFormAuthoring.Cross(stations[i].Tangent.ToVector(), stations[i].Up.ToVector()));
                for (var q = 0; q < 4; q++)
                {
                    var controls = Quarter(stations[i].Point, stations[i].Up, right, r, q); ringControls[i, q] = controls;
                    vertices[i, q] = builder.AddVertex(); points[vertices[i, q]] = controls[0];
                }
                for (var q = 0; q < 4; q++)
                {
                    rings[i, q] = builder.AddEdge(vertices[i, q], vertices[i, (q + 1) % 4]); var spline = Bezier(ringControls[i, q]); geometry.AddCurve(new(curves), CurveGeometry.FromBSpline(spline)); bindings.AddEdgeBinding(new(rings[i, q], new(curves++), new ParameterInterval(0, 1)));
                }
            }
            var faces = new List<FaceId>();
            for (var i = 0; i + 1 < stations.Count; i++)
            {
                var c0 = CenterBezier(stations[i], stations[i + 1]); var net = new Point3D[4][];
                var nets = new Point3D[4][][];
                for (var q = 0; q < 4; q++) { net = new Point3D[4][]; for (var u = 0; u < 4; u++) { var alpha = u / 3d; var up = Direction3D.Create(stations[i].Up.ToVector() * (1d - alpha) + stations[i + 1].Up.ToVector() * alpha); var tangent = Direction3D.Create(stations[i].Tangent.ToVector() * (1d - alpha) + stations[i + 1].Tangent.ToVector() * alpha); up = Direction3D.Create(up.ToVector() - tangent.ToVector() * WireFormAuthoring.Dot(up.ToVector(), tangent.ToVector())); var right = Direction3D.Create(WireFormAuthoring.Cross(tangent.ToVector(), up.ToVector())); net[u] = Quarter(c0[u], up, right, r, q); } nets[q] = net; }
                var longitudinal = new EdgeId[4];
                for (var q = 0; q < 4; q++) { longitudinal[q] = builder.AddEdge(vertices[i, q], vertices[i + 1, q]); var spline = Bezier(nets[q].Select(row => row[0]).ToArray()); geometry.AddCurve(new(curves), CurveGeometry.FromBSpline(spline)); bindings.AddEdgeBinding(new(longitudinal[q], new(curves++), new ParameterInterval(0, 1))); }
                for (var q = 0; q < 4; q++) { var surface = new BSplineSurfaceWithKnots(3, 3, nets[q], "UNSPECIFIED", false, false, false, [4, 4], [4, 4], [0d, 1d], [0d, 1d], "UNSPECIFIED"); geometry.AddSurface(new(surfaces), SurfaceGeometry.FromBSplineSurfaceWithKnots(surface)); var face = AddFace(builder, [(longitudinal[q], false), (rings[i + 1, q], false), (longitudinal[(q + 1) % 4], true), (rings[i, q], true)]); bindings.AddFaceBinding(new(face, new(surfaces++))); faces.Add(face); }
            }
            var startPlane = new PlaneSurface(stations[0].Point, Direction3D.Create(stations[0].Tangent.ToVector() * -1d), stations[0].Up); var endPlane = new PlaneSurface(stations[^1].Point, stations[^1].Tangent, stations[^1].Up);
            var start = AddFace(builder, Enumerable.Range(0, 4).Select(q => (rings[0, 3 - q], true)).ToArray()); geometry.AddSurface(new(surfaces), SurfaceGeometry.FromPlane(startPlane)); bindings.AddFaceBinding(new(start, new(surfaces++))); faces.Add(start);
            var end = AddFace(builder, Enumerable.Range(0, 4).Select(q => (rings[stations.Count - 1, q], false)).ToArray()); geometry.AddSurface(new(surfaces), SurfaceGeometry.FromPlane(endPlane)); bindings.AddFaceBinding(new(end, new(surfaces++))); faces.Add(end);
            var shell = builder.AddShell(faces); builder.AddBody([shell]); var body = new BrepBody(builder.Model, geometry, bindings, points); var validation = BrepBindingValidator.Validate(body, true); if (!validation.IsSuccess) return KernelResult<WireFormBuildResult>.Failure(validation.Diagnostics);
            var samples = stations.Select(x => x.Point).ToArray(); var bounds = new[] { samples.Min(p => p.X) - r, samples.Min(p => p.Y) - r, samples.Min(p => p.Z) - r, samples.Max(p => p.X) + r, samples.Max(p => p.Y) + r, samples.Max(p => p.Z) + r };
            var volume = Math.PI * r * r * feature.TotalWireLengthMm; var mass = volume * 1e-9 * feature.Material.Structural!.Density.SiValue;
            return KernelResult<WireFormBuildResult>.Success(new(feature, body, volume, mass, bounds, ["wireform-coil-evaluable-centerline-authority", "wireform-coil-parallel-transport", "wireform-coil-non-rational-bspline-sweep", "wireform-coil-deterministic-approximation"]));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return KernelResult<WireFormBuildResult>.Failure([new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, "wireform-coil-brep-construction-failed: " + ex.Message, "FirmamentV2.WireForm.Coil")]); }
    }
    private static List<Station> Stations(WireFormFeatureAir feature)
    {
        var raw = new List<(Point3D P, Direction3D T, double Ds)>();
        foreach (var op in feature.Operations)
        {
            var n = op switch { WireStraightAir => 1, WireBendAir b => Math.Max(1, (int)Math.Ceiling(Math.Abs(b.AngleRadians) / (Math.PI / 16d))), WireCoilAir c => Math.Max(16, (int)Math.Ceiling(c.Turns * 32d)), _ => 1 };
            for (var i = raw.Count == 0 ? 0 : 1; i <= n; i++) { var t = (double)i / n; var p = op switch { WireStraightAir s => s.Input.Position + (s.Output.Position - s.Input.Position) * t, WireBendAir b => b.Center + WireCoilGeometry.Rotate(b.StartRadial.ToVector(), b.PlaneNormal.ToVector(), Math.Abs(b.AngleRadians) * t) * b.RadiusMm, WireCoilAir c => c.Evaluate(t), _ => op.Input.Position }; var tangent = op switch { WireStraightAir s => s.Input.Tangent, WireBendAir b => Direction3D.Create(WireCoilGeometry.Rotate(b.Input.Tangent.ToVector(), b.PlaneNormal.ToVector(), Math.Abs(b.AngleRadians) * t)), WireCoilAir c => c.Tangent(t), _ => op.Input.Tangent }; raw.Add((p, tangent, op.LengthMm / n)); }
        }
        var result = new List<Station>(); var up = feature.StartState.Up; Direction3D? previous = null; foreach (var item in raw) { if (previous is { } prior) up = Direction3D.Create(WireCoilGeometry.RotateFromTo(up.ToVector(), prior.ToVector(), item.T.ToVector())); up = Direction3D.Create(up.ToVector() - item.T.ToVector() * WireFormAuthoring.Dot(up.ToVector(), item.T.ToVector())); result.Add(new(item.P, item.T, up, item.Ds)); previous = item.T; } return result;
    }
    private static Point3D[] CenterBezier(Station a, Station b) { var ds = (b.Point - a.Point).Length; return [a.Point, a.Point + a.Tangent.ToVector() * (ds / 3d), b.Point - b.Tangent.ToVector() * (ds / 3d), b.Point]; }
    private static Point3D[] Quarter(Point3D center, Direction3D up, Direction3D right, double radius, int quarter) { var a = quarter * Math.PI / 2d; var b = a + Math.PI / 2d; const double k = .5522847498307936; Vector3D E(double x) => up.ToVector() * Math.Cos(x) + right.ToVector() * Math.Sin(x); Vector3D D(double x) => up.ToVector() * -Math.Sin(x) + right.ToVector() * Math.Cos(x); return [center + E(a) * radius, center + (E(a) + D(a) * k) * radius, center + (E(b) - D(b) * k) * radius, center + E(b) * radius]; }
    private static BSpline3Curve Bezier(IReadOnlyList<Point3D> controls) => new(3, controls, [4, 4], [0d, 1d], "UNSPECIFIED", false, false, "UNSPECIFIED");
    private static FaceId AddFace(TopologyBuilder builder, IReadOnlyList<(EdgeId Edge, bool Reversed)> uses) { var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray(); for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed)); builder.AddLoop(new Loop(loop, ids)); return builder.AddFace([loop]); }
}
