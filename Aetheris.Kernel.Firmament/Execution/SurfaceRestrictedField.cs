using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Execution;

internal enum SurfaceRestrictedFieldStatus
{
    Ready,
    Deferred,
    Unsupported,
    Invalid
}

internal enum SubtractOperandSide
{
    Left,
    Right
}

internal enum RestrictedFieldSignClassification
{
    InsideOpposite,
    OutsideOpposite,
    Boundary
}

internal readonly record struct RestrictedDomain2D(double UMin, double UMax, double VMin, double VMax)
{
    internal bool Contains(double u, double v) => u >= UMin && u <= UMax && v >= VMin && v <= VMax;
}

internal readonly record struct PlanarRectangleParameterization(
    Point3D Corner00,
    Vector3D UVector,
    Vector3D VVector,
    Vector3D Normal,
    RestrictedDomain2D Domain)
{
    internal Point3D Evaluate(double u, double v) => Corner00 + (UVector * u) + (VVector * v);
}

internal sealed record RestrictedFieldSample(
    double U,
    double V,
    Point3D Point,
    double Value,
    RestrictedFieldSignClassification SignClassification,
    IReadOnlyList<string> Diagnostics);

internal sealed record SurfaceRestrictedField(
    SourceSurfaceDescriptor SourceSurface,
    PlanarRectangleParameterization? Parameterization,
    CirTape? OppositeTape,
    SurfaceRestrictedFieldStatus Status,
    IReadOnlyList<string> Diagnostics)
{
    internal RestrictedFieldSample Evaluate(double u, double v, ToleranceContext? tolerance = null)
    {
        var diag = new List<string>();
        if (Status != SurfaceRestrictedFieldStatus.Ready || Parameterization is null || OppositeTape is null)
        {
            diag.Add("restricted-field-not-ready");
            return new RestrictedFieldSample(u, v, Point3D.Origin, double.NaN, RestrictedFieldSignClassification.Boundary, diag);
        }

        var domain = Parameterization.Value.Domain;
        if (!domain.Contains(u, v))
        {
            diag.Add("sample-outside-domain");
        }

        var point = Parameterization.Value.Evaluate(u, v);
        var value = OppositeTape.Evaluate(point);
        var tol = tolerance ?? ToleranceContext.Default;
        var sign = double.Abs(value) <= tol.Linear
            ? RestrictedFieldSignClassification.Boundary
            : value < 0d ? RestrictedFieldSignClassification.InsideOpposite : RestrictedFieldSignClassification.OutsideOpposite;
        diag.Add("evaluation-available");
        diag.Add("contour-extraction-not-implemented");
        return new RestrictedFieldSample(u, v, point, value, sign, diag);
    }
}

internal static class SurfaceRestrictedFieldFactory
{
    internal static SurfaceRestrictedField ForSubtractSource(CirSubtractNode root, SourceSurfaceDescriptor source, SubtractOperandSide side)
    {
        var opposite = side == SubtractOperandSide.Left ? root.Right : root.Left;
        var routing = side == SubtractOperandSide.Left ? "opposite-operand-selected:right" : "opposite-operand-selected:left";
        return ForSourceAndOpposite(source, opposite, routing);
    }

    internal static SurfaceRestrictedField ForSourceAndOpposite(SourceSurfaceDescriptor source, CirNode opposite, string routingDiagnostic)
    {
        var diagnostics = new List<string>();
        if (!TryBuildPlanarRectangle(source, out var parameterization, out var parameterizationDiagnostic))
        {
            diagnostics.Add(parameterizationDiagnostic);
            diagnostics.Add("export-materialization-unchanged");
            return new SurfaceRestrictedField(source, null, null, SurfaceRestrictedFieldStatus.Unsupported, diagnostics);
        }

        diagnostics.Add("planar-rectangle-parameterization-constructed");

        diagnostics.Add(routingDiagnostic);

        try
        {
            var tape = CirTapeLowerer.Lower(opposite);
            diagnostics.Add("opposite-tape-lowered");
            diagnostics.Add("evaluation-available");
            diagnostics.Add("contour-extraction-not-implemented");
            diagnostics.Add("export-materialization-unchanged");
            return new SurfaceRestrictedField(source, parameterization, tape, SurfaceRestrictedFieldStatus.Ready, diagnostics);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"opposite-tape-lowering-failed:{ex.GetType().Name}");
            diagnostics.Add("export-materialization-unchanged");
            return new SurfaceRestrictedField(source, parameterization, null, SurfaceRestrictedFieldStatus.Deferred, diagnostics);
        }
    }

    private static bool TryBuildPlanarRectangle(SourceSurfaceDescriptor source, out PlanarRectangleParameterization parameterization, out string diagnostic)
    {
        parameterization = default;
        if (source.Family != SurfacePatchFamily.Planar)
        {
            diagnostic = "source-surface-unsupported:non-planar";
            return false;
        }

        if (source.BoundedPlanarGeometry is not { } bounded)
        {
            diagnostic = "bounded-rectangle-missing";
            return false;
        }

        if (bounded.Kind != BoundedPlanarPatchGeometryKind.Rectangle)
        {
            diagnostic = $"source-surface-unsupported:bounded-kind-{bounded.Kind}";
            return false;
        }

        var u = bounded.Corner10 - bounded.Corner00;
        var v = bounded.Corner01 - bounded.Corner00;
        if (u.Length <= 1e-12d || v.Length <= 1e-12d)
        {
            diagnostic = "invalid-planar-rectangle-degenerate-vectors";
            return false;
        }

        var normal = u.Cross(v);
        if (normal.Length <= 1e-12d)
        {
            diagnostic = "invalid-planar-rectangle-degenerate-normal";
            return false;
        }

        parameterization = new PlanarRectangleParameterization(bounded.Corner00, u, v, normal / normal.Length, new RestrictedDomain2D(0d, 1d, 0d, 1d));
        diagnostic = "ok";
        return true;
    }
}
