using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Visualization;

public enum WireframeView { Isometric, Front, Top, Right }

public sealed record BrepWireframeOptions(
    WireframeView View = WireframeView.Isometric,
    int Density = 8,
    int CurveSamples = 48,
    int Width = 1200,
    int Height = 700,
    string Background = "#10151d",
    string IsoLineColor = "#79b8ff",
    string BoundaryColor = "#dbeafe");

public sealed record BrepWireframeEvidence(
    WireframeView View,
    int Density,
    int FaceCount,
    int FacesWithTrimmedIsolines,
    int EdgeCount,
    int IsoPolylineCount,
    int BoundaryPolylineCount,
    IReadOnlyDictionary<string, int> SurfaceFamilies,
    IReadOnlyList<string> UnsupportedSurfaceFamilies,
    IReadOnlyList<string> UnsupportedCurveFamilies,
    string Sha256);

public sealed record BrepWireframeResult(string Svg, BrepWireframeEvidence Evidence);

/// <summary>
/// Deterministic diagnostic SVG renderer for exact BRep models. Topology edges are sampled from
/// their authoritative 3D curves. Face isolines are sampled from exact supports and clipped in UV
/// with the face's pcurve loops using the even/odd trim rule. The SVG is evidence/inspection output,
/// not a tessellated product representation or a hidden-line engineering drawing.
/// </summary>
public static class BrepWireframeSvgRenderer
{
    public static BrepWireframeResult Render(BrepBody body, BrepWireframeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        options ??= new();
        if (options.Density is < 2 or > 32) throw new ArgumentOutOfRangeException(nameof(options), "Wireframe density must be between 2 and 32.");
        if (options.CurveSamples is < 8 or > 256) throw new ArgumentOutOfRangeException(nameof(options), "CurveSamples must be between 8 and 256.");
        if (options.Width < 320 || options.Height < 240) throw new ArgumentOutOfRangeException(nameof(options), "Wireframe dimensions are too small.");

        var iso = new List<IReadOnlyList<Point3D>>();
        var boundaries = new List<IReadOnlyList<Point3D>>();
        var unsupported = new SortedSet<string>(StringComparer.Ordinal);
        var unsupportedCurves = new SortedSet<string>(StringComparer.Ordinal);
        var families = body.Topology.Faces.Select(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface) ? surface?.Kind.ToString() ?? "Unbound" : "Unbound")
            .GroupBy(name => name, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var trimmedFaces = 0;

        foreach (var edge in body.Topology.Edges.OrderBy(edge => edge.Id.Value))
        {
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding)
                || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) || curve is null) continue;
            if (!CanEvaluate(curve)) { unsupportedCurves.Add(curve.UnsupportedKind ?? curve.Kind.ToString()); continue; }
            var interval = binding.TrimInterval ?? CurveDomain(curve);
            if (interval is null) continue;
            var points = SampleCurve(curve, interval.Value, options.CurveSamples).ToArray();
            if (points.Length > 1) boundaries.Add(points);
        }

        foreach (var face in body.Topology.Faces.OrderBy(face => face.Id.Value))
        {
            if (!body.Bindings.TryGetFaceBinding(face.Id, out var faceBinding)
                || !body.Geometry.TryGetSurface(faceBinding.SurfaceGeometryId, out var surface) || surface is null) continue;
            var loops = TrimLoops(body, face).ToArray();
            if (loops.Length == 0 || loops.All(loop => loop.Count < 3)) continue;
            var uv = loops.SelectMany(loop => loop).ToArray();
            var u0 = uv.Min(point => point.U); var u1 = uv.Max(point => point.U);
            var v0 = uv.Min(point => point.V); var v1 = uv.Max(point => point.V);
            if (!double.IsFinite(u0) || !double.IsFinite(u1) || !double.IsFinite(v0) || !double.IsFinite(v1)
                || u1 - u0 <= 1e-12d || v1 - v0 <= 1e-12d) continue;
            if (!CanEvaluate(surface)) { unsupported.Add(surface.Kind.ToString()); continue; }
            var before = iso.Count;
            for (var line = 1; line < options.Density; line++)
            {
                var u = u0 + (u1 - u0) * line / options.Density;
                AddTrimmedIso(iso, loops, options.CurveSamples, t => new(u, v0 + (v1 - v0) * t), point => Evaluate(surface, point.U, point.V));
                var v = v0 + (v1 - v0) * line / options.Density;
                AddTrimmedIso(iso, loops, options.CurveSamples, t => new(u0 + (u1 - u0) * t, v), point => Evaluate(surface, point.U, point.V));
            }
            if (iso.Count > before) trimmedFaces++;
        }

        var all = boundaries.Concat(iso).SelectMany(points => points).ToArray();
        if (all.Length == 0) throw new InvalidOperationException("The BRep contains no renderable bound curves or trimmed surface supports.");
        var svg = ComposeSvg(all, iso, boundaries, options);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(svg))).ToLowerInvariant();
        return new(svg, new(options.View, options.Density, body.Topology.Faces.Count(), trimmedFaces,
            body.Topology.Edges.Count(), iso.Count, boundaries.Count, families, unsupported.ToArray(), unsupportedCurves.ToArray(), hash));
    }

    private static IEnumerable<IReadOnlyList<SurfaceParameterPoint>> TrimLoops(BrepBody body, Face face)
    {
        foreach (var loopId in face.LoopIds)
        {
            var loop = body.Topology.GetLoop(loopId); var points = new List<SurfaceParameterPoint>();
            foreach (var coedgeId in loop.CoedgeIds)
            {
                var coedge = body.Topology.GetCoedge(coedgeId);
                if (!body.Bindings.TryGetPcurveBinding(coedgeId, out var binding)) continue;
                var reverse = coedge.IsReversed == binding.SameSense;
                for (var sample = 0; sample <= 16; sample++)
                {
                    var fraction = sample / 16d;
                    var parameter = reverse
                        ? binding.Pcurve.Domain.End - (binding.Pcurve.Domain.End - binding.Pcurve.Domain.Start) * fraction
                        : binding.Pcurve.Domain.Start + (binding.Pcurve.Domain.End - binding.Pcurve.Domain.Start) * fraction;
                    var point = binding.Pcurve.Evaluate(parameter);
                    if (points.Count == 0 || DistanceSquared(points[^1], point) > 1e-20d) points.Add(point);
                }
            }
            if (points.Count >= 3) yield return points;
        }
    }

    private static void AddTrimmedIso(List<IReadOnlyList<Point3D>> output, IReadOnlyList<IReadOnlyList<SurfaceParameterPoint>> loops,
        int samples, Func<double, SurfaceParameterPoint> parameter, Func<SurfaceParameterPoint, Point3D> evaluate)
    {
        List<Point3D>? run = null;
        for (var index = 0; index < samples; index++)
        {
            var a = parameter(index / (double)samples); var b = parameter((index + 1d) / samples);
            var midpoint = new SurfaceParameterPoint((a.U + b.U) / 2d, (a.V + b.V) / 2d);
            if (InsideTrim(midpoint, loops))
            {
                run ??= [];
                if (run.Count == 0) run.Add(evaluate(a));
                run.Add(evaluate(b));
            }
            else if (run is { Count: > 1 }) { output.Add(run); run = null; }
            else run = null;
        }
        if (run is { Count: > 1 }) output.Add(run);
    }

    private static bool InsideTrim(SurfaceParameterPoint point, IReadOnlyList<IReadOnlyList<SurfaceParameterPoint>> loops)
    {
        var crossings = 0;
        foreach (var polygon in loops)
        {
            var inside = false;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                if ((a.V > point.V) == (b.V > point.V)) continue;
                var intersection = (b.U - a.U) * (point.V - a.V) / (b.V - a.V) + a.U;
                if (point.U < intersection) inside = !inside;
            }
            if (inside) crossings++;
        }
        return crossings % 2 == 1;
    }

    private static string ComposeSvg(IReadOnlyList<Point3D> all, IReadOnlyList<IReadOnlyList<Point3D>> iso,
        IReadOnlyList<IReadOnlyList<Point3D>> boundaries, BrepWireframeOptions options)
    {
        var projected = all.Select(point => Project(point, options.View)).ToArray();
        var minX = projected.Min(p => p.X); var maxX = projected.Max(p => p.X);
        var minY = projected.Min(p => p.Y); var maxY = projected.Max(p => p.Y);
        const double margin = 42d;
        var scale = System.Math.Min((options.Width - 2d * margin) / System.Math.Max(maxX - minX, 1e-9d),
            (options.Height - 2d * margin) / System.Math.Max(maxY - minY, 1e-9d));
        string P(Point3D point)
        {
            var p = Project(point, options.View);
            return FormattableString.Invariant($"{margin + (p.X - minX) * scale:F2},{margin + (p.Y - minY) * scale:F2}");
        }
        var svg = new StringBuilder();
        svg.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(options.Width).Append("\" height=\"").Append(options.Height)
            .Append("\" viewBox=\"0 0 ").Append(options.Width).Append(' ').Append(options.Height).Append("\"><rect width=\"100%\" height=\"100%\" fill=\"")
            .Append(options.Background).Append("\"/><g fill=\"none\" stroke-linecap=\"round\" stroke-linejoin=\"round\">");
        foreach (var line in iso) svg.Append("<polyline points=\"").Append(string.Join(' ', line.Select(P))).Append("\" stroke=\"")
            .Append(options.IsoLineColor).Append("\" stroke-width=\"1\" opacity=\".43\"/>");
        foreach (var line in boundaries) svg.Append("<polyline points=\"").Append(string.Join(' ', line.Select(P))).Append("\" stroke=\"")
            .Append(options.BoundaryColor).Append("\" stroke-width=\"1.35\" opacity=\".88\"/>");
        svg.Append("</g><text x=\"24\" y=\"").Append(options.Height - 20).Append("\" fill=\"").Append(options.BoundaryColor)
            .Append("\" opacity=\".78\" font-family=\"ui-monospace,monospace\" font-size=\"14\">Aetheris exact BRep wireframe · ")
            .Append(options.View).Append(" · density ").Append(options.Density).Append("</text></svg>");
        return svg.ToString();
    }

    private static (double X, double Y) Project(Point3D p, WireframeView view) => view switch
    {
        WireframeView.Front => (p.X, -p.Z),
        WireframeView.Top => (p.X, -p.Y),
        WireframeView.Right => (p.Y, -p.Z),
        _ => (p.Z + .45d * p.X, -p.Y + .28d * p.X - .18d * p.Z)
    };

    private static bool CanEvaluate(SurfaceGeometry surface) => surface.Kind switch
    {
        SurfaceGeometryKind.LinearExtrusion => CanEvaluate(surface.LinearExtrusion!.Value.Directrix),
        SurfaceGeometryKind.SurfaceOfRevolution => CanEvaluate(surface.SurfaceOfRevolution!.Value.Directrix),
        _ => surface.Kind is SurfaceGeometryKind.Plane or SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Cone
            or SurfaceGeometryKind.Sphere or SurfaceGeometryKind.Torus or SurfaceGeometryKind.BSplineSurfaceWithKnots
    };

    private static bool CanEvaluate(CurveGeometry curve) => curve.Kind is CurveGeometryKind.Line3 or CurveGeometryKind.Circle3
        or CurveGeometryKind.BSpline3 or CurveGeometryKind.Ellipse3 or CurveGeometryKind.Hyperbola3;

    private static Point3D Evaluate(SurfaceGeometry surface, double u, double v) => surface.Kind switch
    {
        SurfaceGeometryKind.Plane => surface.Plane!.Value.Evaluate(u, v),
        SurfaceGeometryKind.Cylinder => surface.Cylinder!.Value.Evaluate(u, v),
        SurfaceGeometryKind.Cone => surface.Cone!.Value.Evaluate(u, v),
        SurfaceGeometryKind.Sphere => surface.Sphere!.Value.Evaluate(u, v),
        SurfaceGeometryKind.Torus => surface.Torus!.Value.Evaluate(u, v),
        SurfaceGeometryKind.BSplineSurfaceWithKnots => surface.BSplineSurfaceWithKnots!.Evaluate(u, v),
        SurfaceGeometryKind.LinearExtrusion => EvaluateLinearExtrusion(surface.LinearExtrusion!.Value, u, v),
        SurfaceGeometryKind.SurfaceOfRevolution => EvaluateRevolution(surface.SurfaceOfRevolution!.Value, u, v),
        _ => throw new InvalidOperationException($"Unsupported surface family {surface.Kind}.")
    };

    private static Point3D EvaluateLinearExtrusion(LinearExtrusionSurface surface, double u, double v) =>
        EvaluateCurve(surface.Directrix, u) + surface.ExtrusionVector * v;

    private static Point3D EvaluateRevolution(SurfaceOfRevolutionSurface surface, double u, double v)
    {
        var point = EvaluateCurve(surface.Directrix, u); var axis = surface.AxisDirection.ToVector();
        var offset = point - surface.AxisOrigin; var axial = axis * offset.Dot(axis); var radial = offset - axial;
        return surface.AxisOrigin + axial + radial * System.Math.Cos(v) + axis.Cross(radial) * System.Math.Sin(v);
    }

    private static IEnumerable<Point3D> SampleCurve(CurveGeometry curve, ParameterInterval interval, int samples)
    {
        for (var i = 0; i <= samples; i++) yield return EvaluateCurve(curve, interval.Start + (interval.End - interval.Start) * i / samples);
    }

    private static Point3D EvaluateCurve(CurveGeometry curve, double parameter) => curve.Kind switch
    {
        CurveGeometryKind.Line3 => curve.Line3!.Value.Evaluate(parameter),
        CurveGeometryKind.Circle3 => curve.Circle3!.Value.Evaluate(parameter),
        CurveGeometryKind.BSpline3 => curve.BSpline3!.Value.Evaluate(parameter),
        CurveGeometryKind.Ellipse3 => curve.Ellipse3!.Value.Evaluate(parameter),
        CurveGeometryKind.Hyperbola3 => curve.Hyperbola3!.Value.Evaluate(parameter),
        _ => throw new InvalidOperationException($"Unsupported curve family {curve.Kind}.")
    };

    private static ParameterInterval? CurveDomain(CurveGeometry curve) => curve.Kind switch
    {
        CurveGeometryKind.BSpline3 => new(curve.BSpline3!.Value.DomainStart, curve.BSpline3.Value.DomainEnd),
        _ => null
    };

    private static double DistanceSquared(SurfaceParameterPoint a, SurfaceParameterPoint b)
    {
        var u = a.U - b.U; var v = a.V - b.V; return u * u + v * v;
    }
}
