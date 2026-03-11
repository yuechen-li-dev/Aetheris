using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Import;

public enum ImportLaneKind
{
    Auto = 0,
    ExactBRep = 1,
    Tessellated = 2,
    Approximation = 3,
    Compatibility = 4
}

public enum ImportRepresentationTruthKind
{
    Unknown = 0,
    ExactBRep = 1,
    TessellatedOrFaceted = 2,
    Approximation = 3,
    CompatibilityAdjusted = 4
}

public sealed record ImportRequest(string SourceText, ImportPolicy? Policy = null);

public sealed record ImportPolicy(ImportLaneKind PreferredLane = ImportLaneKind.Auto);

public sealed record ImportResult(
    KernelResult<BrepBody> BodyResult,
    string Connector,
    ImportLaneKind Lane,
    ImportRepresentationTruthKind RepresentationTruth,
    string SourceFamily)
{
    public static ImportResult Failure(IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        return new ImportResult(
            KernelResult<BrepBody>.Failure(diagnostics),
            Connector: "none",
            Lane: ImportLaneKind.Auto,
            RepresentationTruth: ImportRepresentationTruthKind.Unknown,
            SourceFamily: "unknown");
    }
}
