using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

/// <summary>
/// A stereographic chart removes the native longitude seam and latitude poles
/// from a bounded spherical face. Its inverse evaluates the exact sphere;
/// triangulation and chord/normal refinement remain shared with other supports.
/// Source loops, including holes, are never replaced by their UV bounding box.
/// </summary>
internal static class SphericalTrimTessellator
{
    public static KernelResult<DisplayFaceMeshPatch> Tessellate(FaceId faceId, SphereSurface sphere,
        IReadOnlyList<IReadOnlyList<Point3D>> loops, int outerIndex, bool aligned,
        DisplayTessellationOptions options, DisplayTessellationExecutionBudget? budget)
    {
        KernelDiagnostic Warning(string message, string source) => new(KernelDiagnosticCode.ValidationFailed,
            KernelDiagnosticSeverity.Warning, message, source);
        KernelResult<DisplayFaceMeshPatch> Fail(string reason) => KernelResult<DisplayFaceMeshPatch>.Failure([
            new(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error,
                $"Face {faceId.Value} spherical trim: {reason}", "Viewer.Tessellation.SphereTrim.ChartUnsupported")]);

        var directions = new List<IReadOnlyList<Vector3D>>();
        foreach (var loop in loops)
        {
            if (loop.Count < 3) return Fail("fewer than three boundary samples");
            var points = new List<Vector3D>();
            foreach (var point in loop)
            {
                var offset = point - sphere.Center;
                var residual = double.Abs(offset.Length - sphere.Radius);
                if (!double.IsFinite(residual) || residual > options.ChordTolerance)
                    return Fail($"boundary support residual={residual:R} mm exceeds display chord budget={options.ChordTolerance:R} mm");
                var direction = offset * (1d / offset.Length);
                if (points.Count == 0 || (direction - points[^1]).Length > 1e-10) points.Add(direction);
            }
            if (points.Count > 1 && (points[0] - points[^1]).Length <= 1e-10) points.RemoveAt(points.Count - 1);
            if (points.Count < 3) return Fail("collapsed boundary has no two-dimensional region");
            directions.Add(points);
        }
        var outer = directions[outerIndex];
        // The directed vector area, rather than a sample centroid, picks the
        // material-side interior. A nonuniformly sampled great circle has an
        // in-plane centroid and would put the chart singularity on its boundary.
        var center = new Vector3D(0, 0, 0);
        for (var i = 0; i < outer.Count; i++) center += outer[i].Cross(outer[(i + 1) % outer.Count]);
        if (!aligned) center = -center;
        if (center.Length <= 1e-10) return Fail("outer boundary does not determine a bounded chart");
        center *= 1d / center.Length;
        var reference = double.Abs(center.Dot(sphere.Axis.ToVector())) < .9 ? sphere.Axis.ToVector() : sphere.XAxis.ToVector();
        var x = reference - center * reference.Dot(center);
        x *= 1d / x.Length;
        var y = center.Cross(x);
        var chartLoops = new List<IReadOnlyList<(double U, double V)>>();
        foreach (var loop in directions)
        {
            var projected = new List<(double U, double V)>();
            foreach (var point in loop)
            {
                var denominator = 1d + point.Dot(center);
                if (denominator <= 1e-8) return Fail("boundary crosses the excluded stereographic chart pole");
                projected.Add((point.Dot(x) / denominator, point.Dot(y) / denominator));
            }
            chartLoops.Add(projected);
        }
        Vector3D Direction(double u, double v)
        {
            var squared = u * u + v * v;
            return (center * (1d - squared) + x * (2d * u) + y * (2d * v)) * (1d / (1d + squared));
        }
        return TrimmedSurfaceTessellator.Tessellate(faceId, chartLoops,
            (u, v) => sphere.Center + Direction(u, v) * sphere.Radius, Direction, options,
            double.NegativeInfinity, double.PositiveInfinity, double.NegativeInfinity, double.PositiveInfinity,
            Warning, budget, SurfaceGeometryKind.Sphere, outerIndex);
    }
}
