using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Forge;

public sealed record ForgeRoundedRectangleProfile(double Width, double Depth, double CornerRadius, int CornerSegments = 8)
{
    public static KernelResult<ForgeRoundedRectangleProfile> Create(double width, double depth, double cornerRadius, int cornerSegments = 8)
    {
        var diagnostics = Validate(width, depth, cornerRadius, cornerSegments);
        if (diagnostics.Count > 0)
        {
            return KernelResult<ForgeRoundedRectangleProfile>.Failure(diagnostics);
        }

        return KernelResult<ForgeRoundedRectangleProfile>.Success(new ForgeRoundedRectangleProfile(width, depth, cornerRadius, cornerSegments));
    }

    public KernelResult<PolylineProfile2D> ToPolylineProfile()
    {
        var diagnostics = Validate(Width, Depth, CornerRadius, CornerSegments);
        if (diagnostics.Count > 0)
        {
            return KernelResult<PolylineProfile2D>.Failure(diagnostics);
        }

        if (CornerRadius <= 1e-12d)
        {
            return KernelResult<PolylineProfile2D>.Success(PolylineProfile2D.Rectangle(Width, Depth));
        }

        var halfWidth = Width * 0.5d;
        var halfDepth = Depth * 0.5d;
        var radius = CornerRadius;
        var segmentCount = CornerSegments;
        var vertices = new List<ProfilePoint2D>(4 * segmentCount);
        const double quarterTurn = double.Pi * 0.5d;

        AppendCorner(halfWidth - radius, halfDepth - radius, 0d, quarterTurn);
        AppendCorner(-(halfWidth - radius), halfDepth - radius, quarterTurn, quarterTurn);
        AppendCorner(-(halfWidth - radius), -(halfDepth - radius), 2d * quarterTurn, quarterTurn);
        AppendCorner(halfWidth - radius, -(halfDepth - radius), 3d * quarterTurn, quarterTurn);

        return PolylineProfile2D.Create(vertices);

        void AppendCorner(double centerX, double centerY, double startAngle, double sweepAngle)
        {
            for (var i = 0; i < segmentCount; i++)
            {
                var t = i / (double)segmentCount;
                var angle = startAngle + (sweepAngle * t);
                vertices.Add(new ProfilePoint2D(
                    centerX + (radius * double.Cos(angle)),
                    centerY + (radius * double.Sin(angle))));
            }
        }
    }

    public static IReadOnlyList<KernelDiagnostic> Validate(double width, double depth, double cornerRadius, int cornerSegments = 8)
    {
        var diagnostics = new List<KernelDiagnostic>();
        ValidatePositiveFinite(width, nameof(width), diagnostics);
        ValidatePositiveFinite(depth, nameof(depth), diagnostics);

        if (!double.IsFinite(cornerRadius) || cornerRadius < 0d)
        {
            diagnostics.Add(Invalid($"{nameof(cornerRadius)} must be finite and greater than or equal to zero."));
        }

        if (double.IsFinite(width) && double.IsFinite(depth) && double.IsFinite(cornerRadius))
        {
            var maxRadius = double.Min(width, depth) * 0.5d;
            if (cornerRadius > maxRadius)
            {
                diagnostics.Add(Invalid($"{nameof(cornerRadius)} must be less than or equal to min(width, depth) / 2."));
            }
        }

        if (cornerSegments < 2)
        {
            diagnostics.Add(Invalid($"{nameof(cornerSegments)} must be greater than or equal to 2."));
        }

        return diagnostics;
    }

    private static void ValidatePositiveFinite(double value, string paramName, ICollection<KernelDiagnostic> diagnostics)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            diagnostics.Add(Invalid($"{paramName} must be finite and greater than zero."));
        }
    }

    private static KernelDiagnostic Invalid(string message) =>
        new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);
}
