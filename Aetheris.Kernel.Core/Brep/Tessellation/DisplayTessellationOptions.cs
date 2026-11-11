using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public sealed record DisplayTessellationOptions(
    double AngularToleranceRadians,
    double ChordTolerance,
    int MinimumSegments,
    int MaximumSegments)
{
    /// <summary>A mesh for people to look at: cylinders and cones as quad bands on their edges' own samples, and faces
    /// made to meet along shared edges (<see cref="DisplayEdgeConformer"/>). Off for analyses that read each face's
    /// mesh as samples of its surface and are calibrated against the per-face meshing.</summary>
    public bool Presentation { get; init; }

    public static DisplayTessellationOptions Default { get; } = new(
        AngularToleranceRadians: double.Pi / 12d,
        ChordTolerance: 0.05d,
        MinimumSegments: 12,
        MaximumSegments: 256);

    /// <summary>Automatic static viewport quality for a fitted model, independent of STEP and mesh-export budgets.</summary>
    public static DisplayTessellationOptions ForViewport(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? (Math.Point3D?)p : null)
            .Where(p => p.HasValue).Select(p => p!.Value)
            .Concat(body.Geometry.Surfaces.SelectMany(s => s.Value.BSplineSurfaceWithKnots?.ControlPoints
                .SelectMany(row => row) ?? []))
            .Concat(body.Geometry.Curves.SelectMany(c => c.Value.BSpline3?.ControlPoints ?? []))
            .ToArray();
        if (points.Length < 2) return Default with { Presentation = true };
        var diagonal = new Math.Vector3D(points.Max(p => p.X) - points.Min(p => p.X),
            points.Max(p => p.Y) - points.Min(p => p.Y), points.Max(p => p.Z) - points.Min(p => p.Z)).Length;
        if (!(diagonal > 0d) || !double.IsFinite(diagonal)) return Default with { Presentation = true };
        // Detail follows size: the chord tolerance is a fraction of the part, so a large bore gets many columns and a
        // small hole few, while the angular limit keeps any arc from looking polygonal under smooth shading.
        return Default with { Presentation = true, AngularToleranceRadians = double.Pi / 16d, ChordTolerance = double.Clamp(diagonal / 600d, .005d, .25d), MinimumSegments = 8, MaximumSegments = 1024 };
    }

    public static KernelResult<DisplayTessellationOptions> Create(
        double angularToleranceRadians,
        double chordTolerance,
        int minimumSegments = 8,
        int maximumSegments = 256)
    {
        var diagnostics = Validate(angularToleranceRadians, chordTolerance, minimumSegments, maximumSegments);
        if (diagnostics.Count > 0)
        {
            return KernelResult<DisplayTessellationOptions>.Failure(diagnostics);
        }

        return KernelResult<DisplayTessellationOptions>.Success(new DisplayTessellationOptions(
            angularToleranceRadians,
            chordTolerance,
            minimumSegments,
            maximumSegments));
    }

    public IReadOnlyList<KernelDiagnostic> Validate()
        => Validate(AngularToleranceRadians, ChordTolerance, MinimumSegments, MaximumSegments);

    private static List<KernelDiagnostic> Validate(
        double angularToleranceRadians,
        double chordTolerance,
        int minimumSegments,
        int maximumSegments)
    {
        var diagnostics = new List<KernelDiagnostic>();

        if (!double.IsFinite(angularToleranceRadians) || angularToleranceRadians <= 0d)
        {
            diagnostics.Add(CreateInvalidOptionDiagnostic("AngularToleranceRadians must be finite and greater than zero."));
        }

        if (!double.IsFinite(chordTolerance) || chordTolerance <= 0d)
        {
            diagnostics.Add(CreateInvalidOptionDiagnostic("ChordTolerance must be finite and greater than zero."));
        }

        if (minimumSegments <= 0)
        {
            diagnostics.Add(CreateInvalidOptionDiagnostic("MinimumSegments must be greater than zero."));
        }

        if (maximumSegments <= 0)
        {
            diagnostics.Add(CreateInvalidOptionDiagnostic("MaximumSegments must be greater than zero."));
        }

        if (minimumSegments > 0 && maximumSegments > 0 && minimumSegments > maximumSegments)
        {
            diagnostics.Add(CreateInvalidOptionDiagnostic("MinimumSegments must be less than or equal to MaximumSegments."));
        }

        return diagnostics;
    }

    private static KernelDiagnostic CreateInvalidOptionDiagnostic(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message);
}
