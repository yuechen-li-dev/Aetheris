using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum ProfileStackExtrudeExecutionStatus
{
    Succeeded,
    InvalidProfileStack,
    UnsupportedProfileShape,
    CompositionBuildFailed,
    Failed
}

public sealed record ProfileStackLayer(
    double ZMin,
    double ZMax,
    double? InnerCircleRadius,
    string Role,
    IReadOnlyList<string> Diagnostics,
    double? TopInnerCircleRadius = null);

public sealed record ProfileStackExtrudeSpec(
    double Width,
    double Depth,
    double ZMin,
    double ZMax,
    IReadOnlyList<ProfileStackLayer> Layers,
    IReadOnlyList<string> Diagnostics,
    double CenterX = 0d,
    double CenterY = 0d);

public sealed record ProfileStackExtrudeExecutionResult(
    ProfileStackExtrudeExecutionStatus Status,
    BrepBody? Body,
    IReadOnlyList<string> Diagnostics);

public static class ProfileStackExtrudeExecutor
{
    public static ProfileStackExtrudeExecutionResult Execute(ProfileStackExtrudeSpec spec)
    {
        var diagnostics = new List<string> { "profile-stack executor started." };
        diagnostics.AddRange(spec.Diagnostics);
        if (!TryValidate(spec, diagnostics, out var orderedLayers))
        {
            return new(ProfileStackExtrudeExecutionStatus.InvalidProfileStack, null, diagnostics);
        }

        diagnostics.Add($"profile-stack layer-count={orderedLayers.Count}.");
        for (var i = 0; i < orderedLayers.Count; i++)
        {
            var l = orderedLayers[i];
            diagnostics.Add($"profile-stack layer[{i}] role={l.Role} zMin={l.ZMin:0.###} zMax={l.ZMax:0.###} radius={(l.InnerCircleRadius?.ToString("0.###") ?? "none") } topRadius={(l.TopInnerCircleRadius?.ToString("0.###") ?? "same")}.");
        }

        if (orderedLayers.All(l => !l.InnerCircleRadius.HasValue))
        { diagnostics.Add("profile-stack unsupported-shape: no cut intervals were provided."); return new(ProfileStackExtrudeExecutionStatus.UnsupportedProfileShape, null, diagnostics); }
        if (orderedLayers.Any(l => l.InnerCircleRadius.HasValue && l.InnerCircleRadius.Value <= 0d))
        { diagnostics.Add("profile-stack unsupported-shape: cut interval has non-positive inner radius."); return new(ProfileStackExtrudeExecutionStatus.UnsupportedProfileShape, null, diagnostics); }

        var holes = new List<SupportedBooleanHole>();
        var zAxis = Direction3D.Create(new Vector3D(0, 0, 1));
        var xAxis = Direction3D.Create(new Vector3D(1, 0, 0));
        foreach (var l in orderedLayers)
        {
            if (!l.InnerCircleRadius.HasValue) continue;
            var span = l.ZMin <= spec.ZMin + 1e-9 && l.ZMax >= spec.ZMax - 1e-9
                ? SupportedBooleanHoleSpanKind.Through
                : (Math.Abs(l.ZMax - spec.ZMax) < 1e-9
                    ? SupportedBooleanHoleSpanKind.BlindFromTop
                    : SupportedBooleanHoleSpanKind.Contained);
            if (l.TopInnerCircleRadius.HasValue && Math.Abs(l.TopInnerCircleRadius.Value - l.InnerCircleRadius.Value) > 1e-9)
            {
                var bottomRadius = l.InnerCircleRadius.Value;
                var topRadius = l.TopInnerCircleRadius.Value;
                var semiAngle = Math.Atan(Math.Abs(topRadius - bottomRadius) / (l.ZMax - l.ZMin));
                var radiusScale = Math.Tan(semiAngle);
                var axis = topRadius >= bottomRadius ? zAxis : Direction3D.Create(new Vector3D(0, 0, -1));
                var axisOriginZ = topRadius >= bottomRadius ? l.ZMin - bottomRadius / radiusScale : l.ZMax + topRadius / radiusScale;
                var minParam = topRadius >= bottomRadius ? bottomRadius / radiusScale : topRadius / radiusScale;
                var maxParam = topRadius >= bottomRadius ? topRadius / radiusScale : bottomRadius / radiusScale;
                var cone = new RecognizedCone(new Point3D(spec.CenterX, spec.CenterY, axisOriginZ), axis, minParam, maxParam, semiAngle, Math.Min(bottomRadius, topRadius), Math.Max(bottomRadius, topRadius));
                holes.Add(new SupportedBooleanHole(l.Role, new AnalyticSurface(AnalyticSurfaceKind.Cone, Cone: cone), spec.CenterX, spec.CenterY,
                    new Point3D(spec.CenterX, spec.CenterY, l.ZMin), new Point3D(spec.CenterX, spec.CenterY, l.ZMax), zAxis, xAxis,
                    bottomRadius, topRadius, span, l.ZMin, l.ZMax));
                continue;
            }
            var cyl = new RecognizedCylinder(new Point3D(spec.CenterX, spec.CenterY, 0), zAxis, l.InnerCircleRadius!.Value, l.ZMin, l.ZMax);
            holes.Add(new SupportedBooleanHole(l.Role, new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cyl), spec.CenterX, spec.CenterY,
                new Point3D(spec.CenterX, spec.CenterY, l.ZMin), new Point3D(spec.CenterX, spec.CenterY, l.ZMax), zAxis, xAxis,
                l.InnerCircleRadius.Value, l.InnerCircleRadius.Value, span, l.ZMin, l.ZMax));
        }

        diagnostics.Add("profile-stack composition build invoked.");
        diagnostics.Add("profile-stack executor route: no 3D subtract route used.");
        var extents = new AxisAlignedBoxExtents(-spec.Width / 2d, spec.Width / 2d, -spec.Depth / 2d, spec.Depth / 2d, spec.ZMin, spec.ZMax);
        var composition = new SafeBooleanComposition(extents, holes, SafeBooleanRootDescriptor.FromBox(extents));
        var built = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(composition, ToleranceContext.Default);
        if (!built.IsSuccess || built.Value is null)
        {
            diagnostics.Add("profile-stack composition build failed.");
            diagnostics.AddRange(built.Diagnostics.Select(d => d.Message));
            return new(ProfileStackExtrudeExecutionStatus.CompositionBuildFailed, null, diagnostics);
        }

        diagnostics.Add("profile-stack composition build succeeded.");
        return new(ProfileStackExtrudeExecutionStatus.Succeeded, built.Value, diagnostics);
    }

    private static bool TryValidate(ProfileStackExtrudeSpec spec, List<string> diagnostics, out IReadOnlyList<ProfileStackLayer> orderedLayers)
    {
        orderedLayers = spec.Layers.OrderBy(l => l.ZMin).ToArray();
        if (orderedLayers.Count == 0 || spec.ZMax - spec.ZMin <= 1e-9)
        {
            diagnostics.Add("profile-stack validation failed: empty layers or invalid global z-span.");
            return false;
        }

        for (var i = 0; i < orderedLayers.Count; i++)
        {
            var l = orderedLayers[i];
            diagnostics.AddRange(l.Diagnostics);
            if (l.ZMax - l.ZMin <= 1e-9)
            {
                diagnostics.Add($"profile-stack validation failed: layer[{i}] has non-positive z-span.");
                return false;
            }

            if (l.ZMin < spec.ZMin - 1e-9 || l.ZMax > spec.ZMax + 1e-9)
            {
                diagnostics.Add($"profile-stack validation failed: layer[{i}] outside global z-span.");
                return false;
            }

            if (i > 0)
            {
                var prev = orderedLayers[i - 1];
                if (l.ZMin < prev.ZMax - 1e-9)
                {
                    diagnostics.Add($"profile-stack validation accepted overlapping semantic entry-prep layer between [{i - 1}] and [{i}].");
                }
                else if (Math.Abs(prev.ZMax - l.ZMin) > 1e-9)
                {
                    diagnostics.Add($"profile-stack validation failed: non-contiguous layer ordering between [{i - 1}] and [{i}].");
                    return false;
                }
            }
        }

        if (orderedLayers[0].ZMin > spec.ZMin + 1e-9 || orderedLayers.Max(l => l.ZMax) < spec.ZMax - 1e-9)
        {
            diagnostics.Add("profile-stack validation failed: layers do not fully cover global z-span.");
            return false;
        }

        diagnostics.Add("profile-stack validation passed.");
        return true;
    }
}
