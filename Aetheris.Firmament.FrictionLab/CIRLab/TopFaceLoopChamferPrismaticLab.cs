using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum FaceLoopChamferSelectionKind
{
    FaceBoundaryLoop,
    OpenChain,
    ArbitraryGraph,
}

public enum FaceLoopChamferOwningFaceKind
{
    TopCap,
    BottomCap,
    SideFace,
    NonPlanarFace,
}

public enum FaceLoopChamferLoopKind
{
    Outer,
    Inner,
}

public enum FaceLoopChamferRuleKind
{
    UniformSymmetric,
    NonUniform,
}

public sealed record FaceLoopChamferSelection(
    FaceLoopChamferSelectionKind SelectionKind = FaceLoopChamferSelectionKind.FaceBoundaryLoop,
    FaceLoopChamferOwningFaceKind OwningFace = FaceLoopChamferOwningFaceKind.TopCap,
    FaceLoopChamferLoopKind LoopKind = FaceLoopChamferLoopKind.Outer,
    bool IsClosed = true,
    int EdgeCount = 4,
    bool OrderedCoedges = true);

public sealed record TopFaceLoopChamferCase(
    string Name,
    double Width,
    double Depth,
    double Height,
    double ChamferDistance,
    FaceLoopChamferSelection? Selection = null,
    FaceLoopChamferRuleKind Rule = FaceLoopChamferRuleKind.UniformSymmetric);

public sealed record FaceLoopChamferTopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int CapFaceCount,
    int LowerPrismSideFaceCount,
    int TransitionFaceCount,
    int ChamferTransitionFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

public sealed record FaceLoopChamferStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record FaceLoopChamferRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    bool PrismaticEmitterInvoked,
    FaceLoopChamferTopologySummary Topology,
    FaceLoopChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class TopFaceLoopChamferPrismaticLab
{
    public static readonly string[] AllowedRecommendations =
    [
        "face-loop-chamfer-ready-for-corpus",
        "face-loop-chamfer-needs-loop-selection-hardening",
        "face-loop-chamfer-needs-corner-policy-hardening",
        "face-loop-chamfer-invalid-rejected",
        "face-loop-chamfer-deferred",
    ];

    public static IReadOnlyList<FaceLoopChamferRow> RunAll() =>
    [
        Run(new("canonical-top-face-outer-loop", 10, 8, 6, 1)),
        Run(new("larger-valid-top-face-outer-loop", 10, 8, 6, 2)),
        Run(new("non-square-valid-top-face-outer-loop", 12, 5, 7, 1)),
        Run(new("invalid-zero-width", 0, 8, 6, 1)),
        Run(new("invalid-negative-depth", 10, -8, 6, 1)),
        Run(new("invalid-zero-height", 10, 8, 0, 1)),
        Run(new("invalid-non-finite-width", double.NaN, 8, 6, 1)),
        Run(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
        Run(new("invalid-negative-chamfer-distance", 10, 8, 6, -1)),
        Run(new("invalid-non-finite-chamfer-distance", 10, 8, 6, double.PositiveInfinity)),
        Run(new("invalid-too-large-chamfer-distance", 10, 8, 6, 4)),
        Run(new("invalid-non-closed-loop", 10, 8, 6, 1, new(IsClosed: false))),
        Run(new("deferred-inner-loop", 10, 8, 6, 1, new(LoopKind: FaceLoopChamferLoopKind.Inner))),
        Run(new("deferred-open-chain", 10, 8, 6, 1, new(SelectionKind: FaceLoopChamferSelectionKind.OpenChain))),
        Run(new("rejected-arbitrary-graph", 10, 8, 6, 1, new(SelectionKind: FaceLoopChamferSelectionKind.ArbitraryGraph))),
        Run(new("rejected-non-uniform-rule", 10, 8, 6, 1, Rule: FaceLoopChamferRuleKind.NonUniform)),
        Run(new("deferred-non-planar-owning-face", 10, 8, 6, 1, new(OwningFace: FaceLoopChamferOwningFaceKind.NonPlanarFace))),
    ];

    public static FaceLoopChamferRow Run(TopFaceLoopChamferCase c)
    {
        var request = new CorePrismatic.PrismaticTopFaceLoopChamferRequest(
            c.Width,
            c.Depth,
            c.Height,
            c.ChamferDistance,
            c.Selection is null ? null : new CorePrismatic.FaceLoopChamferSelection(
                ToCore(c.Selection.SelectionKind),
                ToCore(c.Selection.OwningFace),
                ToCore(c.Selection.LoopKind),
                c.Selection.IsClosed,
                c.Selection.EdgeCount,
                c.Selection.OrderedCoedges),
            ToCore(c.Rule),
            ExportStep: true);
        var result = CorePrismatic.PrismaticTopFaceLoopChamferPrototype.Emit(request);
        return new(
            c.Name,
            ToLabStatus(result.Status),
            result.Succeeded,
            result.Diagnostics.Contains("edge-loop-x1-prismatic-emitter-invoked"),
            ToLabTopology(result.Topology),
            ToLabStep(result.Step),
            result.Diagnostics,
            result.Recommendation);
    }

    public static IReadOnlyList<PrismaticSection> CreateSectionStack(TopFaceLoopChamferCase c) =>
        CorePrismatic.PrismaticTopFaceLoopChamferPrototype.CreateSectionStack(new CorePrismatic.PrismaticTopFaceLoopChamferRequest(c.Width, c.Depth, c.Height, c.ChamferDistance))
            .Select(s => new PrismaticSection(s.Z, s.OuterLoop, s.HasHoles, s.HasArcs, s.OuterLoopCount))
            .ToArray();

    private static LabProfileStatus ToLabStatus(CorePrismatic.FaceLoopChamferStatus status) => status switch
    {
        CorePrismatic.FaceLoopChamferStatus.Succeeded => LabProfileStatus.Succeeded,
        CorePrismatic.FaceLoopChamferStatus.Deferred => LabProfileStatus.Deferred,
        _ => LabProfileStatus.Failed,
    };

    private static FaceLoopChamferTopologySummary ToLabTopology(CorePrismatic.FaceLoopChamferTopologySummary t) => new(
        t.BodyProduced,
        t.SectionCount,
        t.VertexCount,
        t.EdgeCount,
        t.FaceCount,
        t.PlanarFaceCount,
        t.CylindricalFaceCount,
        t.CapFaceCount,
        t.LowerPrismSideFaceCount,
        t.TransitionFaceCount,
        t.ChamferTransitionFaceCount,
        t.LoopCount,
        t.CoedgeCount,
        t.Bounds);

    private static FaceLoopChamferStepSummary ToLabStep(CorePrismatic.FaceLoopChamferStepSummary s) => new(
        s.Exported,
        s.PresentMarkers,
        s.MissingRequiredMarkers,
        s.AbsentMarkers,
        s.UnexpectedPresentMarkers);

    private static CorePrismatic.FaceLoopChamferSelectionKind ToCore(FaceLoopChamferSelectionKind kind) => kind switch
    {
        FaceLoopChamferSelectionKind.OpenChain => CorePrismatic.FaceLoopChamferSelectionKind.OpenChain,
        FaceLoopChamferSelectionKind.ArbitraryGraph => CorePrismatic.FaceLoopChamferSelectionKind.ArbitraryGraph,
        _ => CorePrismatic.FaceLoopChamferSelectionKind.FaceBoundaryLoop,
    };

    private static CorePrismatic.FaceLoopChamferOwningFaceKind ToCore(FaceLoopChamferOwningFaceKind kind) => kind switch
    {
        FaceLoopChamferOwningFaceKind.BottomCap => CorePrismatic.FaceLoopChamferOwningFaceKind.BottomCap,
        FaceLoopChamferOwningFaceKind.SideFace => CorePrismatic.FaceLoopChamferOwningFaceKind.SideFace,
        FaceLoopChamferOwningFaceKind.NonPlanarFace => CorePrismatic.FaceLoopChamferOwningFaceKind.NonPlanarFace,
        _ => CorePrismatic.FaceLoopChamferOwningFaceKind.TopCap,
    };

    private static CorePrismatic.FaceLoopChamferLoopKind ToCore(FaceLoopChamferLoopKind kind) => kind == FaceLoopChamferLoopKind.Inner
        ? CorePrismatic.FaceLoopChamferLoopKind.Inner
        : CorePrismatic.FaceLoopChamferLoopKind.Outer;

    private static CorePrismatic.FaceLoopChamferRuleKind ToCore(FaceLoopChamferRuleKind kind) => kind == FaceLoopChamferRuleKind.NonUniform
        ? CorePrismatic.FaceLoopChamferRuleKind.NonUniform
        : CorePrismatic.FaceLoopChamferRuleKind.UniformSymmetric;
}
