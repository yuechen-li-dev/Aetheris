using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.StandardLibrary.Materials;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Deterministic local state consumed and produced by every WireForm operation.</summary>
public sealed record WireState(Point3D Position, Direction3D Tangent, Direction3D Up, double AccumulatedLengthMm)
{
    public Direction3D Right => Direction3D.Create(WireFormAuthoring.Cross(Tangent.ToVector(), Up.ToVector()));
}

public abstract record WireFormOperationAir(string Name, int Ordinal, WireState Input, WireState Output, double LengthMm)
{
    public string StableId(string wireFormName) => $"wireform:{wireFormName}:operation:{Ordinal}:{Name}";
}

public sealed record WireStraightAir(string Name, int Ordinal, double AuthoredLengthMm, WireState Input, WireState Output)
    : WireFormOperationAir(Name, Ordinal, Input, Output, AuthoredLengthMm);

public sealed record WireBendAir(string Name, int Ordinal, double RadiusMm, double AngleRadians, string Plane,
    Direction3D PlaneNormal, Point3D Center, Direction3D StartRadial, WireState Input, WireState Output)
    : WireFormOperationAir(Name, Ordinal, Input, Output, RadiusMm * Math.Abs(AngleRadians));

/// <summary>WireForm semantic AIR. Bend radii are centerline radii.</summary>
public sealed record WireFormFeatureAir(string Name, double DiameterMm, string MaterialReference, ResolvedMaterial Material,
    WireState StartState, IReadOnlyList<WireFormOperationAir> Operations, string FrameTransportPolicy)
{
    public double WireRadiusMm => DiameterMm / 2d;
    public double TotalStraightLengthMm => Operations.OfType<WireStraightAir>().Sum(x => x.LengthMm);
    public double TotalBendLengthMm => Operations.OfType<WireBendAir>().Sum(x => x.LengthMm);
    public double TotalWireLengthMm => Operations.Sum(x => x.LengthMm);
    public WireState EndState => Operations.Count == 0 ? StartState : Operations[^1].Output;
}

public sealed record WireFormBuildResult(WireFormFeatureAir Feature, BrepBody Body, double VolumeMm3,
    double MassKilograms, IReadOnlyList<double> Bounds, IReadOnlyList<string> ValidationEvidence);

/// <summary>Parser and ordered state-machine lowering for the bounded X0 WireForm vocabulary.</summary>
public static class WireFormAuthoring
{
    public const string FrameTransportPolicy = "The authored local Up/Right bend-plane axis is rotated with the tangent through each bend (rotation-minimal rigid transport about the bend normal); Straight preserves the frame.";
    private static readonly Regex Declaration = new(@"\bWireForm\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);
    private static readonly Regex Operation = new(@"\b(?<kind>Straight|Bend)\s+(?<name>[A-Za-z_]\w*)\s*\{", RegexOptions.CultureInvariant);

    public static bool IsWireFormSource(string source) => Declaration.IsMatch(source);

    public static KernelResult<WireFormFeatureAir> Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var declaration = Declaration.Match(source);
        if (!declaration.Success) return Fail("wireform-declaration-missing", "No WireForm declaration was found.");
        var open = source.IndexOf('{', declaration.Index);
        var close = MatchingBrace(source, open);
        var name = declaration.Groups["name"].Value;
        if (close < 0) return Fail("wireform-declaration-malformed", $"WireForm '{name}' has no closing brace.");
        var body = source[(open + 1)..close];

        if (!TryLength(Property(body, "Diameter"), out var diameter) || diameter <= 0d)
            return Fail("wireform-diameter-invalid", $"WireForm '{name}' Diameter must be a finite length greater than zero.");
        var materialReference = (Property(body, "Material") ?? "Standard.Materials.StainlessSteel.304_Annealed").Trim('"');
        var material = new MaterialResolver().Resolve(materialReference);
        if (!material.IsSuccess || material.Material is null)
            return Fail("wireform-material-unresolved", material.Message ?? $"Material '{materialReference}' could not be resolved.");

        if (!TryVector(Property(body, "Origin"), out var origin))
            return Fail("wireform-start-frame-invalid", $"WireForm '{name}' requires StartFrame Origin: [x,y,z].");
        if (!TryVector(Property(body, "Tangent"), out var tangentVector) || !TryDirection(tangentVector, out var tangent))
            return Fail("wireform-start-frame-invalid", $"WireForm '{name}' requires a nonzero StartFrame Tangent.");
        if (!TryVector(Property(body, "Up"), out var upVector))
            return Fail("wireform-start-frame-invalid", $"WireForm '{name}' requires StartFrame Up.");
        var upRejected = upVector - tangent.ToVector() * Dot(upVector, tangent.ToVector());
        if (!TryDirection(upRejected, out var up))
            return Fail("wireform-start-frame-invalid", $"WireForm '{name}' Up must not be parallel to Tangent.");

        var state = new WireState(new(origin.X, origin.Y, origin.Z), tangent, up, 0d);
        var operations = new List<WireFormOperationAir>();
        foreach (Match match in Operation.Matches(body))
        {
            var operationOpen = body.IndexOf('{', match.Index);
            var operationClose = MatchingBrace(body, operationOpen);
            if (operationClose < 0) return Fail("wireform-operation-malformed", $"Operation '{match.Groups["name"].Value}' has no closing brace.");
            var operationBody = body[(operationOpen + 1)..operationClose];
            var operationName = match.Groups["name"].Value;
            var ordinal = operations.Count + 1;
            if (match.Groups["kind"].Value == "Straight")
            {
                if (!TryLength(Property(operationBody, "Length"), out var length) || length <= 0d)
                    return Fail("wireform-straight-length-invalid", $"{operationName}: Length must be finite and greater than zero.");
                var output = state with { Position = state.Position + state.Tangent.ToVector() * length, AccumulatedLengthMm = state.AccumulatedLengthMm + length };
                operations.Add(new WireStraightAir(operationName, ordinal, length, state, output));
                state = output;
                continue;
            }

            if (!TryLength(Property(operationBody, "Radius"), out var radius) || radius <= 0d)
                return Fail("wireform-bend-radius-invalid", $"{operationName}: centerline Radius must be finite and greater than zero.");
            if (radius <= diameter / 2d + ToleranceContext.Default.Linear)
                return Fail("wireform-bend-radius-invalid", $"{operationName}: centerline Radius {radius:G6} mm must exceed wire radius {diameter / 2d:G6} mm (geometric-only policy).");
            if (!TryAngle(Property(operationBody, "Angle"), out var angle) || Math.Abs(angle) <= ToleranceContext.Default.Angular || Math.Abs(angle) > Math.PI + ToleranceContext.Default.Angular)
                return Fail("wireform-bend-angle-invalid", $"{operationName}: Angle must be nonzero and no greater than 180 degrees in magnitude.");
            var plane = Property(operationBody, "Plane") ?? "Up";
            if (plane is not ("Up" or "Right"))
                return Fail("wireform-bend-plane-invalid", $"{operationName}: Plane must be Up or Right in the current local frame.");
            var authoredNormal = plane == "Up" ? state.Up : state.Right;
            var axis = angle >= 0d ? authoredNormal : Direction3D.Create(authoredNormal.ToVector() * -1d);
            var sweep = Math.Abs(angle);
            var startRadial = Direction3D.Create(Cross(state.Tangent.ToVector(), axis.ToVector()));
            var center = state.Position - startRadial.ToVector() * radius;
            var endRadial = Rotate(startRadial.ToVector(), axis.ToVector(), sweep);
            var endTangent = Direction3D.Create(Rotate(state.Tangent.ToVector(), axis.ToVector(), sweep));
            var endUp = Direction3D.Create(Rotate(state.Up.ToVector(), axis.ToVector(), sweep));
            var bendLength = radius * sweep;
            var bendOutput = new WireState(center + endRadial * radius, endTangent, endUp, state.AccumulatedLengthMm + bendLength);
            operations.Add(new WireBendAir(operationName, ordinal, radius, angle, plane, axis, center, startRadial, state, bendOutput));
            state = bendOutput;
        }
        if (operations.Count == 0) return Fail("wireform-operations-empty", $"WireForm '{name}' requires at least one Straight or Bend operation.");
        return KernelResult<WireFormFeatureAir>.Success(new(name, diameter, materialReference, material.Material,
            new WireState(new(origin.X, origin.Y, origin.Z), tangent, up, 0d), operations, FrameTransportPolicy));
    }

    private static string? Property(string body, string name)
    {
        var match = Regex.Match(body, $@"\b{Regex.Escape(name)}\s*:\s*(?<value>\[[^\]]+\]|[^;\r\n}}]+)", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }
    private static bool TryLength(string? text, out double value)
    {
        value = default;
        return text is not null && new BoundedMeasureExpression(text, "mm").TryEvaluate(out value);
    }
    private static bool TryAngle(string? text, out double value)
    {
        value = default;
        if (!TryUnit(text, "deg", out var degrees)) return false;
        value = degrees * Math.PI / 180d;
        return double.IsFinite(value);
    }
    private static bool TryUnit(string? text, string unit, out double value)
    {
        value = default;
        if (text is null) return false;
        var match = Regex.Match(text, $@"^(?<value>[-+.\deE]+){unit}$", RegexOptions.CultureInvariant);
        return match.Success && double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
    private static bool TryVector(string? text, out Vector3D value)
    {
        value = default;
        if (text is null) return false;
        var parts = text.Trim().TrimStart('[').TrimEnd(']').Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;
        var values = new double[3];
        for (var i = 0; i < 3; i++)
        {
            var token = parts[i].EndsWith("mm", StringComparison.Ordinal) ? parts[i][..^2] : parts[i];
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i])) return false;
        }
        value = new(values[0], values[1], values[2]); return true;
    }
    private static bool TryDirection(Vector3D vector, out Direction3D direction)
    {
        try { direction = Direction3D.Create(vector); return true; }
        catch (ArgumentException) { direction = default; return false; }
    }
    private static int MatchingBrace(string source, int open) { var depth = 0; for (var i = open; i >= 0 && i < source.Length; i++) { if (source[i] == '{') depth++; else if (source[i] == '}' && --depth == 0) return i; } return -1; }
    private static Vector3D Rotate(Vector3D vector, Vector3D axis, double angle) => vector * Math.Cos(angle) + Cross(axis, vector) * Math.Sin(angle) + axis * (Dot(axis, vector) * (1d - Math.Cos(angle)));
    internal static Vector3D Cross(Vector3D a, Vector3D b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    internal static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    private sealed class BoundedMeasureExpression(string source, string unit)
    {
        private int index;
        public bool TryEvaluate(out double value) { value = default; index = 0; if (!Expression(out var result) || result.Dimension != 1) return false; Space(); if (index != source.Length || !double.IsFinite(result.Value)) return false; value = result.Value; return true; }
        private bool Expression(out Quantity result) { if (!Term(out result)) return false; while (true) { Space(); if (index >= source.Length || source[index] is not ('+' or '-')) return true; var operation = source[index++]; if (!Term(out var right) || result.Dimension != right.Dimension) return false; result = new(operation == '+' ? result.Value + right.Value : result.Value - right.Value, result.Dimension); } }
        private bool Term(out Quantity result) { if (!Factor(out result)) return false; while (true) { Space(); if (index >= source.Length || source[index] is not ('*' or '/')) return true; var operation = source[index++]; if (!Factor(out var right)) return false; var dimension = operation == '*' ? result.Dimension + right.Dimension : result.Dimension - right.Dimension; if (dimension is < 0 or > 1 || operation == '/' && Math.Abs(right.Value) <= double.Epsilon) return false; result = new(operation == '*' ? result.Value * right.Value : result.Value / right.Value, dimension); } }
        private bool Factor(out Quantity result) { Space(); result = default; if (index >= source.Length) return false; if (source[index] == '(') { index++; if (!Expression(out result)) return false; Space(); return index < source.Length && source[index++] == ')'; } var sign = 1d; if (source[index] is '+' or '-') sign = source[index++] == '-' ? -1d : 1d; Space(); var start = index; while (index < source.Length && (char.IsDigit(source[index]) || source[index] is '.' or 'e' or 'E' || source[index] is '+' or '-' && index > start && source[index - 1] is 'e' or 'E')) index++; if (start == index || !double.TryParse(source[start..index], NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return false; var dimension = 0; if (source.AsSpan(index).StartsWith(unit, StringComparison.Ordinal)) { index += unit.Length; dimension = 1; } result = new(sign * number, dimension); return true; }
        private void Space() { while (index < source.Length && char.IsWhiteSpace(source[index])) index++; }
        private readonly record struct Quantity(double Value, int Dimension);
    }
    private static KernelResult<WireFormFeatureAir> Fail(string code, string message) => KernelResult<WireFormFeatureAir>.Failure([new(
        Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, $"{code}: {message}", "FirmamentV2.WireForm")]);
}

/// <summary>Exact circular sweep realization for 3D line/arc WireForm centerlines.</summary>
public static class WireFormBRepMaterializer
{
    private static readonly ToleranceContext Tolerance = ToleranceContext.Default;

    public static KernelResult<WireFormBuildResult> Build(WireFormFeatureAir feature)
    {
        var diagnostics = Validate(feature);
        if (diagnostics.Count > 0) return KernelResult<WireFormBuildResult>.Failure(diagnostics.Select(Error).ToArray());
        try
        {
            var count = feature.Operations.Count;
            var builder = new TopologyBuilder(); var geometry = new BrepGeometryStore(); var bindings = new BrepBindingModel();
            var ringVertices = new VertexId[count + 1]; var ringEdges = new EdgeId[count + 1]; var vertexPoints = new Dictionary<VertexId, Point3D>();
            var states = feature.Operations.Select(x => x.Input).Append(feature.EndState).ToArray();
            var nextCurve = 1; var nextSurface = 1;
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i]; ringVertices[i] = builder.AddVertex();
                vertexPoints[ringVertices[i]] = state.Position + state.Up.ToVector() * feature.WireRadiusMm;
                ringEdges[i] = builder.AddEdge(ringVertices[i], ringVertices[i]);
                var curve = new CurveGeometryId(nextCurve++);
                geometry.AddCurve(curve, CurveGeometry.FromCircle(new Circle3Curve(state.Position, state.Tangent, feature.WireRadiusMm, state.Up)));
                bindings.AddEdgeBinding(new(ringEdges[i], curve, new ParameterInterval(0d, 2d * Math.PI)));
            }
            var faces = new List<FaceId>();
            for (var i = 0; i < count; i++)
            {
                var operation = feature.Operations[i]; var seam = builder.AddEdge(ringVertices[i], ringVertices[i + 1]);
                var seamCurve = new CurveGeometryId(nextCurve++); var surface = new SurfaceGeometryId(nextSurface++);
                if (operation is WireStraightAir straight)
                {
                    geometry.AddCurve(seamCurve, CurveGeometry.FromLine(new Line3Curve(vertexPoints[ringVertices[i]], straight.Input.Tangent)));
                    bindings.AddEdgeBinding(new(seam, seamCurve, new ParameterInterval(0d, straight.LengthMm)));
                    geometry.AddSurface(surface, SurfaceGeometry.FromCylinder(new CylinderSurface(straight.Input.Position, straight.Input.Tangent, feature.WireRadiusMm, straight.Input.Up)));
                }
                else if (operation is WireBendAir bend)
                {
                    var axis = bend.PlaneNormal.ToVector(); var up = bend.Input.Up.ToVector(); var axial = WireFormAuthoring.Dot(up, axis);
                    var seamCenter = bend.Center + axis * (axial * feature.WireRadiusMm);
                    var seamRadial = bend.StartRadial.ToVector() * bend.RadiusMm + (up - axis * axial) * feature.WireRadiusMm;
                    var seamRadius = seamRadial.Length; var seamReference = Direction3D.Create(seamRadial);
                    geometry.AddCurve(seamCurve, CurveGeometry.FromCircle(new Circle3Curve(seamCenter, bend.PlaneNormal, seamRadius, seamReference)));
                    bindings.AddEdgeBinding(new(seam, seamCurve, new ParameterInterval(0d, Math.Abs(bend.AngleRadians))));
                    geometry.AddSurface(surface, SurfaceGeometry.FromTorus(new TorusSurface(bend.Center, bend.PlaneNormal, bend.RadiusMm, feature.WireRadiusMm, bend.StartRadial)));
                }
                else throw new NotSupportedException("WireForm X0 admits Straight and Bend only.");
                var side = AddFace(builder, [(seam, false), (ringEdges[i + 1], false), (seam, true), (ringEdges[i], true)]);
                bindings.AddFaceBinding(new(side, surface)); faces.Add(side);
            }
            var startSurface = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(startSurface, SurfaceGeometry.FromPlane(new PlaneSurface(feature.StartState.Position, Direction3D.Create(feature.StartState.Tangent.ToVector() * -1d), feature.StartState.Up)));
            var startFace = AddFace(builder, [(ringEdges[0], false)]); bindings.AddFaceBinding(new(startFace, startSurface)); faces.Add(startFace);
            var endSurface = new SurfaceGeometryId(nextSurface++);
            geometry.AddSurface(endSurface, SurfaceGeometry.FromPlane(new PlaneSurface(feature.EndState.Position, feature.EndState.Tangent, feature.EndState.Up)));
            var endFace = AddFace(builder, [(ringEdges[^1], true)]); bindings.AddFaceBinding(new(endFace, endSurface)); faces.Add(endFace);
            var shell = builder.AddShell(faces); builder.AddBody([shell]); var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints);
            var bindingValidation = BrepBindingValidator.Validate(body, true);
            if (!bindingValidation.IsSuccess) return KernelResult<WireFormBuildResult>.Failure(bindingValidation.Diagnostics);
            var preflight = BrepExportPreflight.Validate(body);
            if (!preflight.IsValid) return KernelResult<WireFormBuildResult>.Failure(preflight.Diagnostics.Where(x => x.Severity == BrepExportPreflightSeverity.Error).Select(x => Error($"wireform-brep-invalid: {x.Code}: {x.Message}")).ToArray());
            var samples = feature.Operations.SelectMany(x => Sample(x, 64)).ToArray(); var r = feature.WireRadiusMm;
            var bounds = new[] { samples.Min(x => x.X) - r, samples.Min(x => x.Y) - r, samples.Min(x => x.Z) - r,
                samples.Max(x => x.X) + r, samples.Max(x => x.Y) + r, samples.Max(x => x.Z) + r };
            var volume = Math.PI * r * r * feature.TotalWireLengthMm;
            var mass = volume * 1e-9 * feature.Material.Structural!.Density.SiValue;
            return KernelResult<WireFormBuildResult>.Success(new(feature, body, volume, mass, bounds,
                ["wireform-state-replay-deterministic", "wireform-centerline-tangent", "wireform-self-intersection-clear", "wireform-analytic-circular-sweep", "wireform-geometric-minimum-radius"]));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        { return KernelResult<WireFormBuildResult>.Failure([Error("wireform-brep-construction-failed: " + exception.Message)]); }
    }

    public static IReadOnlyList<string> Validate(WireFormFeatureAir feature)
    {
        var diagnostics = new List<string>();
        if (feature.Operations.Count == 0) diagnostics.Add("wireform-operations-empty: at least one operation is required.");
        foreach (var operation in feature.Operations)
        {
            if (operation.LengthMm <= Tolerance.Linear) diagnostics.Add($"wireform-operation-degenerate:{operation.Name}");
            if (operation is WireBendAir bend && bend.RadiusMm <= feature.WireRadiusMm + Tolerance.Linear)
                diagnostics.Add($"wireform-bend-radius-invalid:{bend.Name}: centerline radius must exceed wire radius.");
        }
        var clearance = feature.DiameterMm;
        for (var i = 0; i + 1 < feature.Operations.Count; i++)
        {
            var distance = AdjacentNonlocalDistance(feature.Operations[i], feature.Operations[i + 1], clearance);
            if (distance + Tolerance.Linear < clearance)
                diagnostics.Add($"wireform-self-intersection:{feature.Operations[i].Name}:{feature.Operations[i + 1].Name}: adjacent operations collide away from their intended tangent join; conservative clearance is {distance:G6} mm, required {clearance:G6} mm.");
        }
        for (var i = 0; i < feature.Operations.Count; i++) for (var j = i + 2; j < feature.Operations.Count; j++)
        {
            var distance = ConservativeDistance(feature.Operations[i], feature.Operations[j]);
            if (distance + Tolerance.Linear < clearance)
                diagnostics.Add($"wireform-self-intersection:{feature.Operations[i].Name}:{feature.Operations[j].Name}: centerlines are {distance:G6} mm apart; required clearance is {clearance:G6} mm. Intentional contact is unsupported in X0.");
        }
        return diagnostics;
    }

    private static double AdjacentNonlocalDistance(WireFormOperationAir a, WireFormOperationAir b, double clearance)
    {
        const int count = 192; var aa = Sample(a, count); var bb = Sample(b, count); var best = double.PositiveInfinity;
        // A two-diameter arclength neighborhood around the common endpoint is the
        // intended continuous tube join. Compare every chord pair outside it so
        // adjacent operations cannot retrace/collide elsewhere.
        for (var i = 0; i < aa.Count - 1; i++) for (var j = 0; j < bb.Count - 1; j++)
        {
            var fromJoinA = a.LengthMm * (count - i - 1d) / count;
            var fromJoinB = b.LengthMm * j / count;
            if (fromJoinA < clearance || fromJoinB < clearance) continue;
            best = Math.Min(best, SegmentDistance(aa[i], aa[i + 1], bb[j], bb[j + 1]));
        }
        return double.IsPositiveInfinity(best) ? double.PositiveInfinity : Math.Max(0d, best - Sagitta(a, count) - Sagitta(b, count));
    }

    private static double ConservativeDistance(WireFormOperationAir a, WireFormOperationAir b)
    {
        var aa = Sample(a, 96); var bb = Sample(b, 96); var best = double.PositiveInfinity;
        for (var i = 0; i < aa.Count - 1; i++) for (var j = 0; j < bb.Count - 1; j++) best = Math.Min(best, SegmentDistance(aa[i], aa[i + 1], bb[j], bb[j + 1]));
        // Chords alone can overestimate arc/arc or arc/line clearance. Subtract each
        // arc's maximum chord sagitta so this is a conservative lower bound and the
        // no-contact policy fails closed (at the cost of bounded false positives).
        return Math.Max(0d, best - Sagitta(a, 96) - Sagitta(b, 96));
    }
    private static double Sagitta(WireFormOperationAir operation, int count) => operation is WireBendAir bend
        ? bend.RadiusMm * (1d - Math.Cos(Math.Abs(bend.AngleRadians) / (2d * count))) : 0d;
    private static IReadOnlyList<Point3D> Sample(WireFormOperationAir operation, int count) => operation switch
    {
        WireStraightAir line => [line.Input.Position, line.Output.Position],
        WireBendAir bend => Enumerable.Range(0, count + 1).Select(i => bend.Center + Rotate(bend.StartRadial.ToVector(), bend.PlaneNormal.ToVector(), Math.Abs(bend.AngleRadians) * i / count) * bend.RadiusMm).ToArray(),
        _ => []
    };
    private static double SegmentDistance(Point3D p1, Point3D q1, Point3D p2, Point3D q2)
    {
        var d1 = q1 - p1; var d2 = q2 - p2; var r = p1 - p2; var a = Dot(d1, d1); var e = Dot(d2, d2); var f = Dot(d2, r); double s, t;
        if (a <= 1e-24 && e <= 1e-24) return (p1 - p2).Length;
        if (a <= 1e-24) { s = 0d; t = Math.Clamp(f / e, 0d, 1d); }
        else { var c = Dot(d1, r); if (e <= 1e-24) { t = 0d; s = Math.Clamp(-c / a, 0d, 1d); } else { var b = Dot(d1, d2); var denominator = a * e - b * b; s = denominator == 0d ? 0d : Math.Clamp((b * f - c * e) / denominator, 0d, 1d); t = (b * s + f) / e; if (t < 0d) { t = 0d; s = Math.Clamp(-c / a, 0d, 1d); } else if (t > 1d) { t = 1d; s = Math.Clamp((b - c) / a, 0d, 1d); } } }
        return (p1 + d1 * s - (p2 + d2 * t)).Length;
    }
    private static Vector3D Rotate(Vector3D vector, Vector3D axis, double angle) => vector * Math.Cos(angle) + WireFormAuthoring.Cross(axis, vector) * Math.Sin(angle) + axis * (Dot(axis, vector) * (1d - Math.Cos(angle)));
    private static double Dot(Vector3D a, Vector3D b) => WireFormAuthoring.Dot(a, b);
    private static FaceId AddFace(TopologyBuilder builder, IReadOnlyList<(EdgeId Edge, bool Reversed)> uses) { var loop = builder.AllocateLoopId(); var ids = uses.Select(_ => builder.AllocateCoedgeId()).ToArray(); for (var i = 0; i < uses.Count; i++) builder.AddCoedge(new(ids[i], uses[i].Edge, loop, ids[(i + 1) % ids.Length], ids[(i + ids.Length - 1) % ids.Length], uses[i].Reversed)); builder.AddLoop(new Loop(loop, ids)); return builder.AddFace([loop]); }
    private static Core.Diagnostics.KernelDiagnostic Error(string message) => new(Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "FirmamentV2.WireForm");
}

public static class WireFormReportFactory
{
    public static FirmamentWireFormReport Create(WireFormBuildResult built, string stepSha256, bool reimportedManifold)
    {
        var feature = built.Feature;
        var surfaces = built.Body.Topology.Faces.Select(face => built.Body.GetFaceSurface(face.Id).Kind).ToArray();
        var operations = feature.Operations.Select(operation => new FirmamentWireOperationReport(
            operation.Ordinal, operation.Name, operation is WireStraightAir ? "Straight" : "Bend", operation.LengthMm,
            operation is WireBendAir bend ? bend.RadiusMm : null,
            operation is WireBendAir bendAngle ? bendAngle.AngleRadians * 180d / Math.PI : null,
            operation is WireBendAir bendPlane ? bendPlane.Plane : null, operation.StableId(feature.Name),
            State(operation.Input), State(operation.Output), operation is WireStraightAir ? "LineSegment" : "CircularArc",
            operation is WireStraightAir ? "Cylinder" : "Torus")).ToArray();
        return new(feature.Name, feature.DiameterMm, feature.Material.Identity.FirmamentPath, feature.Operations.Count,
            feature.Operations.Count(x => x is WireStraightAir), feature.Operations.Count(x => x is WireBendAir),
            feature.TotalStraightLengthMm, feature.TotalBendLengthMm, feature.TotalWireLengthMm, built.VolumeMm3, built.MassKilograms,
            Terminal("TerminalStart", feature.StartState, feature.DiameterMm), Terminal("TerminalEnd", feature.EndState, feature.DiameterMm),
            feature.Operations.OfType<WireBendAir>().Select(x => x.RadiusMm).DefaultIfEmpty(double.NaN).Min(),
            "CenterlineRadius", "GeometricOnly: centerline bend radius must exceed Diameter/2",
            "Nonadjacent operations use deterministic 3D chord witnesses with arc-sagitta error bounds; contact/overlap fails closed.",
            feature.FrameTransportPolicy, operations, built.Bounds,
            surfaces.Count(x => x == SurfaceGeometryKind.Cylinder), surfaces.Count(x => x == SurfaceGeometryKind.Torus),
            surfaces.Count(x => x == SurfaceGeometryKind.Plane), surfaces.Count(x => x is not (SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus or SurfaceGeometryKind.Plane)),
            surfaces.Count(x => x == SurfaceGeometryKind.BSplineSurfaceWithKnots), 0, true, stepSha256, true, reimportedManifold);
    }

    private static FirmamentWireStateReport State(WireState state) => new(V(state.Position), V(state.Tangent), V(state.Up), V(state.Right), state.AccumulatedLengthMm);
    private static FirmamentWireTerminalReport Terminal(string name, WireState state, double diameter) => new(name, V(state.Position), V(state.Tangent), V(state.Up), diameter);
    private static double[] V(Point3D p) => [p.X, p.Y, p.Z];
    private static double[] V(Direction3D d) => [d.X, d.Y, d.Z];
}
