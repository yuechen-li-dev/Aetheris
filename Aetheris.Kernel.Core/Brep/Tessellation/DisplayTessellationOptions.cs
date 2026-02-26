using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Brep.Tessellation;

public sealed record DisplayTessellationOptions(
    double AngularToleranceRadians,
    double ChordTolerance,
    int MinimumSegments,
    int MaximumSegments)
{
    public static DisplayTessellationOptions Default { get; } = new(
        AngularToleranceRadians: double.Pi / 12d,
        ChordTolerance: 0.05d,
        MinimumSegments: 12,
        MaximumSegments: 256);

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
