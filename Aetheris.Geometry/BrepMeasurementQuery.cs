using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Geometry;

public sealed record MeasurementPick(string? OccurrenceId, int? FaceId, double[] RayOrigin, double[] RayDirection);
public sealed record MeasurementRequest(string Mode, MeasurementPick First, MeasurementPick? Second = null, bool AllowApproximate = false);
public sealed class MeasurementTarget(string occurrenceId, BrepBody body, Transform3D transform)
{
    public string OccurrenceId { get; } = occurrenceId;
    public BrepBody Body { get; } = body;
    public Transform3D Transform { get; } = transform;
    private readonly Lazy<DisplayTessellationResult> _display = new(() => PrepareDisplay(body));
    internal DisplayTessellationResult Display => _display.Value;

    private static DisplayTessellationResult PrepareDisplay(BrepBody body)
    {
        var options = DisplayTessellationOptions.ForViewport(body);
        if (!OperatingSystem.IsBrowser()) return BrepDisplayTessellator.TessellateBoundedPartial(body, options);
        // Same policy as WebMeshBuilder: browser WASM cannot block on a monitor. Its Worker owns
        // responsiveness. Do not enter the native Task.Run/WaitAsync budget wrapper here.
        var result = BrepDisplayTessellator.Tessellate(body, options);
        if (!result.IsSuccess) throw new MeasurementException("measurement-display-unavailable",
            "This BRep could not produce a synchronous measurement pick proxy: " + string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return result.Value;
    }
}
public sealed record MeasurementReference(string OccurrenceId, int FaceId, string SurfaceKind, double[] Point,
    string Quality, double? DiameterMm = null);
public sealed record MeasurementResult(string Mode, MeasurementReference First, MeasurementReference? Second,
    double? DistanceMm, double[]? DeltaMm, double[]? WitnessA, double[]? WitnessB, double? AngleDegrees,
    string Quality, string Explanation);
public sealed class MeasurementException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Read-only BRep measurement authority. Support-plane and infinite-axis distances are deliberately
/// distinct from finite material clearance. Surface points use the existing analytic trim policy;
/// a display approximation is available only by explicit request and remains labeled approximate.
/// Callers own revision checks and supply geometry from the displayed compilation, never a recompile.
/// </summary>
public static class BrepMeasurementQuery
{
    private sealed record Reference(MeasurementReference Display, SurfaceGeometry Surface, Transform3D Transform, Point3D Point);

    public static MeasurementResult Measure(IReadOnlyList<MeasurementTarget> targets, MeasurementRequest request)
    {
        if (request?.First is null) throw Error("measurement-request-invalid", "A first measurement pick is required.");
        if (request.Mode is not ("points" or "planes" or "axes"))
            throw Error("measurement-mode-unsupported", "Choose Surface points, Support planes, or Axes.");
        var a = Resolve(targets, request.First, request.Mode == "points" && request.AllowApproximate);
        var b = request.Second is null ? null : Resolve(targets, request.Second, request.Mode == "points" && request.AllowApproximate);
        var quality = a.Display.Quality == "Approximate display" || b?.Display.Quality == "Approximate display"
            ? "Approximate display" : "Analytic BRep";
        Point3D p = a.Point, q = b?.Point ?? p;
        double? angle = null;
        string explanation;
        switch (request.Mode)
        {
            case "planes":
                var n = PlaneNormal(a);
                explanation = "Spacing of infinite support planes; not minimum clearance between trimmed faces.";
                if (b is not null)
                {
                    var m = PlaneNormal(b);
                    angle = Angle(n, m);
                    if (n.Cross(m).Length > 1e-10)
                        return new(request.Mode, a.Display, b.Display, null, null, null, null, angle, quality,
                            "The support planes are not parallel. Their acute angle is shown; no parallel spacing exists.");
                    q = p + n * ((b.Point - p).Dot(n));
                }
                break;
            case "axes":
                var (o, u) = Axis(a);
                explanation = "Shortest spacing of infinite analytic axes; not wall thickness or material clearance.";
                p = o;
                if (b is not null)
                {
                    var (v0, v) = Axis(b);
                    angle = Angle(u, v);
                    var w = o - v0;
                    var dot = u.Dot(v);
                    // Cross-product magnitude avoids cancellation in 1-dot² near parallel axes.
                    var denominator = u.Cross(v).LengthSquared;
                    if (denominator <= 1e-20) { p = o; q = v0 + v * w.Dot(v); }
                    else
                    {
                        var s = (dot * v.Dot(w) - u.Dot(w)) / denominator;
                        var t = (v.Dot(w) - dot * u.Dot(w)) / denominator;
                        p = o + u * s; q = v0 + v * t;
                    }
                }
                break;
            default:
                explanation = quality == "Approximate display"
                    ? "Distance between picked points. Includes a display-mesh point; unsuitable for tolerance verification."
                    : "Distance between analytically resolved surface picks; not minimum clearance between faces.";
                break;
        }
        if (b is null) return new(request.Mode, a.Display, null, null, null, Coordinates(p), null, null, quality, explanation);
        var delta = q - p;
        if (!double.IsFinite(delta.Length)) throw Error("measurement-numerical-failure", "The measured coordinates exceeded the finite query range.");
        return new(request.Mode, a.Display, b.Display, delta.Length, [delta.X, delta.Y, delta.Z], Coordinates(p), Coordinates(q), angle, quality, explanation);
    }

    private static Reference Resolve(IReadOnlyList<MeasurementTarget> targets, MeasurementPick pick, bool approximate)
    {
        if (pick.RayOrigin is not { Length: 3 } || pick.RayDirection is not { Length: 3 }
            || pick.RayOrigin.Concat(pick.RayDirection).Any(v => !double.IsFinite(v)))
            throw Error("measurement-ray-invalid", "A pick requires a finite origin and a nonzero finite direction.");
        var scale = pick.RayDirection.Max(double.Abs);
        if (scale == 0 || !Direction3D.TryCreate(new(pick.RayDirection[0] / scale, pick.RayDirection[1] / scale, pick.RayDirection[2] / scale), out var direction))
            throw Error("measurement-ray-invalid", "A pick requires a finite origin and a nonzero finite direction.");
        var ray = new Ray3D(new(pick.RayOrigin[0], pick.RayOrigin[1], pick.RayOrigin[2]), direction);
        var candidates = new List<(Reference Reference, double Distance)>();
        foreach (var target in targets.Where(t => pick.OccurrenceId is null || t.OccurrenceId == pick.OccurrenceId))
        {
            if (!target.Transform.IsRigid()) throw Error("measurement-transform-unsupported", "Measurement requires a rigid occurrence placement.");
            var inverse = target.Transform.Inverse();
            var local = new Ray3D(inverse.Apply(ray.Origin), inverse.Apply(ray.Direction));
            if (pick.FaceId is null && target.Body.Topology.Faces.Any(face => !target.Display.FacePatches.Any(patch => patch.FaceId == face.Id)))
                throw Error("measurement-face-reference-required", "This body has undisplayed faces. Select an explicit visible face so the query cannot see through missing geometry.");
            // The display picker locates the frontmost trim, including surfaces not admitted by the
            // analytic picker. Never see through an unsupported foreground face to a supported back face.
            // Match the viewport's double-sided picking, including inward-facing hole walls.
            var hits = BrepPicker.Pick(target.Body, target.Display, local, PickQueryOptions.Default with { IncludeBackfaces = true });
            if (!hits.IsSuccess) continue;
            var faceHit = hits.Value.Where(h => h.FaceId is not null && (pick.FaceId is null || h.FaceId.Value.Value == pick.FaceId))
                .OrderBy(h => h.T).FirstOrDefault();
            if (faceHit?.FaceId is not { } id) continue;
            if (!target.Body.TryGetFaceSurfaceGeometry(id, out var surface) || surface is null) continue;
            var exact = AnalyticDisplayQuery.TryIntersectFace(target.Body, id, local, out var hit);
            var point = target.Transform.Apply(exact ? hit.Position : faceHit.Point);
            var diameter = surface.Cylinder is { } cylinder ? 2 * cylinder.Radius : (double?)null;
            var reference = new MeasurementReference(target.OccurrenceId, id.Value, surface.Kind.ToString(), Coordinates(point),
                exact ? "Analytic BRep" : "Approximate display", diameter);
            candidates.Add((new(reference, surface, target.Transform, point), (point - ray.Origin).Dot(ray.Direction.ToVector())));
        }
        var chosen = candidates.OrderBy(c => c.Distance).ThenBy(c => c.Reference.Display.OccurrenceId, StringComparer.Ordinal).FirstOrDefault().Reference;
        if (chosen is null) throw Error("measurement-pick-missed", "No current BRep face was hit. Pick a visible face; mesh-only geometry cannot be measured analytically.");
        if (chosen.Display.Quality != "Analytic BRep" && !approximate)
            throw Error("measurement-surface-unsupported", "This trimmed surface has no admitted analytic pick. Choose another face or explicitly enable approximate display points.");
        return chosen;
    }

    private static Vector3D PlaneNormal(Reference reference) => reference.Surface.Plane is { } plane
        ? reference.Transform.Apply(plane.Normal).ToVector()
        : throw Error("measurement-plane-required", $"Select planar faces for support-plane spacing; this face is {reference.Display.SurfaceKind}.");
    private static (Point3D, Vector3D) Axis(Reference reference)
    {
        var surface = reference.Surface;
        if (surface.Cylinder is { } cylinder) return (reference.Transform.Apply(cylinder.Origin), reference.Transform.Apply(cylinder.Axis).ToVector());
        if (surface.Cone is { } cone) return (reference.Transform.Apply(cone.Apex), reference.Transform.Apply(cone.Axis).ToVector());
        if (surface.Torus is { } torus) return (reference.Transform.Apply(torus.Center), reference.Transform.Apply(torus.Axis).ToVector());
        throw Error("measurement-axis-required", $"Select cylindrical, conical or toroidal faces for axis spacing; this face is {reference.Display.SurfaceKind}.");
    }
    private static double Angle(Vector3D a, Vector3D b) => double.Acos(double.Clamp(double.Abs(a.Dot(b)), 0, 1)) * 180 / double.Pi;
    private static double[] Coordinates(Point3D p) => [p.X, p.Y, p.Z];
    private static MeasurementException Error(string code, string message) => new(code, message);
}
