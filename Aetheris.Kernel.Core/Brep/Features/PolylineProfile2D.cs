using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>
/// Minimal M10 profile representation: one outer closed loop, line segments only.
/// Closure is implicit (last vertex connects to first).
/// </summary>
public sealed class PolylineProfile2D
{
    private PolylineProfile2D(IReadOnlyList<ProfilePoint2D> vertices)
    {
        Vertices = vertices;
    }

    public IReadOnlyList<ProfilePoint2D> Vertices { get; }

    public static KernelResult<PolylineProfile2D> Create(IReadOnlyList<ProfilePoint2D> vertices)
    {
        var diagnostics = ValidateVertices(vertices);
        return diagnostics.Count > 0
            ? KernelResult<PolylineProfile2D>.Failure(diagnostics)
            : KernelResult<PolylineProfile2D>.Success(new PolylineProfile2D(vertices.ToArray()));
    }

    public static PolylineProfile2D Rectangle(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "width must be finite and greater than zero.");
        }

        if (!double.IsFinite(height) || height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "height must be finite and greater than zero.");
        }

        return new PolylineProfile2D(
        [
            new ProfilePoint2D(-width * 0.5d, -height * 0.5d),
            new ProfilePoint2D(width * 0.5d, -height * 0.5d),
            new ProfilePoint2D(width * 0.5d, height * 0.5d),
            new ProfilePoint2D(-width * 0.5d, height * 0.5d),
        ]);
    }

    internal static List<KernelDiagnostic> ValidateVertices(IReadOnlyList<ProfilePoint2D>? vertices)
    {
        var diagnostics = new List<KernelDiagnostic>();
        if (vertices is null)
        {
            diagnostics.Add(CreateInvalidArgument("Profile vertices must be provided."));
            return diagnostics;
        }

        if (vertices.Count < 3)
        {
            diagnostics.Add(CreateInvalidArgument("Profile must contain at least three vertices."));
            return diagnostics;
        }

        var distinct = new HashSet<ProfilePoint2D>();
        for (var i = 0; i < vertices.Count; i++)
        {
            var current = vertices[i];
            if (!double.IsFinite(current.X) || !double.IsFinite(current.Y))
            {
                diagnostics.Add(CreateInvalidArgument("Profile coordinates must be finite."));
                return diagnostics;
            }

            distinct.Add(current);

            var next = vertices[(i + 1) % vertices.Count];
            var dx = next.X - current.X;
            var dy = next.Y - current.Y;
            if ((dx * dx) + (dy * dy) <= 0d)
            {
                diagnostics.Add(CreateInvalidArgument("Profile contains a zero-length segment (duplicate adjacent vertices)."));
                return diagnostics;
            }
        }

        if (distinct.Count < 3)
        {
            diagnostics.Add(CreateInvalidArgument("Profile must contain at least three distinct vertices."));
            return diagnostics;
        }

        var signedArea = 0d;
        for (var i = 0; i < vertices.Count; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];
            signedArea += (current.X * next.Y) - (next.X * current.Y);
        }

        if (double.Abs(signedArea) <= 1e-12d)
        {
            diagnostics.Add(CreateInvalidArgument("Profile polygon area is zero or numerically degenerate."));
        }

        return diagnostics;
    }

    private static KernelDiagnostic CreateInvalidArgument(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);
}
