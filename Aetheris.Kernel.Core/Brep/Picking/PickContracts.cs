using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Picking;

public enum SelectionEntityKind
{
    Unknown = 0,
    Face = 1,
    Edge = 2,
    Body = 3,
}

public sealed record PickHit(
    double T,
    Point3D Point,
    Direction3D? Normal,
    SelectionEntityKind EntityKind,
    FaceId? FaceId,
    EdgeId? EdgeId,
    BodyId? BodyId,
    int? SourcePatchIndex = null,
    int? SourcePrimitiveIndex = null);

public sealed record PickQueryOptions(
    bool NearestOnly,
    bool IncludeBackfaces,
    double EdgeTolerance,
    double SortTieTolerance,
    double? MaxDistance)
{
    public static PickQueryOptions Default { get; } = new(
        NearestOnly: false,
        IncludeBackfaces: false,
        EdgeTolerance: 0.05d,
        SortTieTolerance: 1e-6d,
        MaxDistance: null);

    public IReadOnlyList<KernelDiagnostic> Validate()
    {
        var diagnostics = new List<KernelDiagnostic>();

        if (!double.IsFinite(EdgeTolerance) || EdgeTolerance <= 0d)
        {
            diagnostics.Add(CreateInvalidOption("EdgeTolerance must be finite and greater than zero."));
        }

        if (!double.IsFinite(SortTieTolerance) || SortTieTolerance < 0d)
        {
            diagnostics.Add(CreateInvalidOption("SortTieTolerance must be finite and greater than or equal to zero."));
        }

        if (MaxDistance.HasValue && (!double.IsFinite(MaxDistance.Value) || MaxDistance.Value < 0d))
        {
            diagnostics.Add(CreateInvalidOption("MaxDistance must be null or a finite value greater than or equal to zero."));
        }

        return diagnostics;
    }

    private static KernelDiagnostic CreateInvalidOption(string message)
        => new(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, Source: nameof(PickQueryOptions));
}
