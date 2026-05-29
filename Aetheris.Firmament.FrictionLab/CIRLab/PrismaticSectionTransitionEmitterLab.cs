using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record PrismaticSection(double Z, IReadOnlyList<(double X, double Y)> OuterLoop, bool HasHoles = false, bool HasArcs = false, int OuterLoopCount = 1);

public sealed record PrismaticCorrespondenceMap(IReadOnlyList<int> VertexMap)
{
    public static PrismaticCorrespondenceMap Identity(int vertexCount) => new(Enumerable.Range(0, vertexCount).ToArray());
}

public sealed record PrismaticSectionTransitionCase(
    string Name,
    IReadOnlyList<PrismaticSection> Sections,
    PrismaticCorrespondenceMap? Correspondence);

public sealed record PrismaticTransitionTopologySummary(
    bool BodyProduced,
    int SectionCount,
    int VertexCount,
    int EdgeCount,
    int BottomProfileEdgeCount,
    int TopProfileEdgeCount,
    int TransitionEdgeCount,
    int CapFaceCount,
    int TransitionFaceCount,
    int StableIntervalFaceCount,
    int ChangedIntervalFaceCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

public sealed record PrismaticSectionTransitionStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record PrismaticSectionTransitionRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    PrismaticTransitionTopologySummary Topology,
    PrismaticSectionTransitionStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class PrismaticSectionTransitionEmitterLab
{
    public static readonly string[] AllowedRecommendations =
    [
        "prismatic-section-transition-ready-for-production-evaluation",
        "prismatic-section-transition-generic-ready-for-production-evaluation",
        "prismatic-section-transition-ready-for-controlled-route-evaluation",
        "prismatic-section-transition-needs-profile-validation-hardening",
        "prismatic-section-transition-needs-correspondence-hardening",
        "prismatic-section-transition-invalid-rejected",
        "prismatic-section-transition-deferred",
    ];

    public static IReadOnlyList<PrismaticSectionTransitionRow> RunAll() =>
    [
        Run(RectangleToInsetRectangle()),
        Run(ThreeSectionStableThenInsetRectangle()),
        Run(ScaledPentagon()),
        Run(ScaledHexagon()),
        Run(AsymmetricTranslatedPentagon()),
        Run(new("invalid-non-increasing-z", [RectangleSection(0, 10, 8), RectangleSection(0, 8, 6)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("invalid-zero-interval", [RectangleSection(0, 10, 8), RectangleSection(0, 8, 6)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("invalid-mismatched-vertex-count", [RectangleSection(0, 10, 8), RegularPolygonSection(1, 5, 5)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("invalid-missing-correspondence", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], null)),
        Run(new("invalid-self-intersecting-profile", [new PrismaticSection(0, [(0, 0), (2, 2), (0, 2), (2, 0)]), RectangleSection(1, 8, 6)], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-holes", [RectangleSection(0, 10, 8) with { HasHoles = true }, RectangleSection(1, 8, 6) with { HasHoles = true }], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-line-arc", [RectangleSection(0, 10, 8) with { HasArcs = true }, RectangleSection(1, 8, 6) with { HasArcs = true }], PrismaticCorrespondenceMap.Identity(4))),
        Run(new("deferred-multiple-loops", [RectangleSection(0, 10, 8) with { OuterLoopCount = 2 }, RectangleSection(1, 8, 6) with { OuterLoopCount = 2 }], PrismaticCorrespondenceMap.Identity(4))),
    ];

    public static PrismaticSectionTransitionCase RectangleToInsetRectangle() =>
        new("rectangle-to-inset-rectangle", [RectangleSection(0, 10, 8), RectangleSection(1, 8, 6)], PrismaticCorrespondenceMap.Identity(4));

    public static PrismaticSectionTransitionCase ThreeSectionStableThenInsetRectangle() =>
        new("three-section-stable-plus-transition", [RectangleSection(0, 10, 8), RectangleSection(5, 10, 8), RectangleSection(6, 8, 6)], PrismaticCorrespondenceMap.Identity(4));

    public static PrismaticSectionTransitionCase ScaledPentagon() =>
        new("scaled-pentagon", [RegularPolygonSection(0, 5, 5), RegularPolygonSection(2, 4, 5)], PrismaticCorrespondenceMap.Identity(5));

    public static PrismaticSectionTransitionCase ScaledHexagon() =>
        new("scaled-hexagon", [RegularPolygonSection(0, 6, 6), RegularPolygonSection(2, 4.5, 6)], PrismaticCorrespondenceMap.Identity(6));

    public static PrismaticSectionTransitionCase AsymmetricTranslatedPentagon() =>
        new("asymmetric-translated-pentagon",
            [
                new PrismaticSection(0, [(-4, -2), (1, -3), (5, 0), (2, 3.5), (-3, 2.5)]),
                new PrismaticSection(2, [(-3.25, -2.35), (1.75, -3.35), (5.75, -0.35), (2.75, 3.15), (-2.25, 2.15)]),
            ],
            PrismaticCorrespondenceMap.Identity(5));

    public static PrismaticSectionTransitionRow Run(PrismaticSectionTransitionCase c)
    {
        var request = new CorePrismatic.PrismaticSectionTransitionRequest(
            c.Sections.Select(s => new CorePrismatic.PrismaticSection(s.Z, s.OuterLoop, s.HasHoles, s.HasArcs, s.OuterLoopCount)).ToArray(),
            c.Correspondence is null ? null : new CorePrismatic.PrismaticCorrespondenceMap(c.Correspondence.VertexMap),
            new CorePrismatic.PrismaticSectionTransitionOptions(RunStepSmoke: true, TraceLabel: c.Name));
        var result = CorePrismatic.PrismaticSectionTransitionEmitter.Emit(request);
        var recommendation = result.Recommendation == "prismatic-section-transition-ready-for-controlled-route-evaluation"
            ? "prismatic-section-transition-generic-ready-for-production-evaluation"
            : result.Recommendation;

        return new(
            c.Name,
            ToLabStatus(result.Status),
            result.Status == CorePrismatic.PrismaticSectionTransitionStatus.Succeeded,
            ToLabTopology(result.Topology),
            ToLabStep(result.Step),
            StableDiagnostics(result.Diagnostics.Concat(LabDiagnostics(c.Name, result.Diagnostics))),
            recommendation);
    }

    private static IReadOnlyList<string> LabDiagnostics(string caseName, IReadOnlyList<string> v1Diagnostics)
    {
        var diagnostics = new List<string>
        {
            "edge-prismatic-x1-lab-started",
            "edge-prismatic-x3-generic-lab-started",
            $"edge-prismatic-x3-case-started:{caseName}",
        };

        if (v1Diagnostics.Contains("edge-prismatic-v1-correspondence-validated"))
        {
            diagnostics.Add("edge-prismatic-x1-correspondence-created");
            diagnostics.Add("edge-prismatic-x3-correspondence-created");
            diagnostics.Add("edge-prismatic-x3-section-stack-created");
        }

        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-section-validated", "edge-prismatic-x1-section-validated");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-transition-interval-created", "edge-prismatic-x1-transition-interval-created");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-cap-faces-created", "edge-prismatic-x1-cap-faces-created");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-transition-faces-created", "edge-prismatic-x1-transition-faces-created");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-body-created", "edge-prismatic-x1-body-created", "edge-prismatic-x3-body-created", "edge-prismatic-x3-prismatic-emitter-invoked");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-step-smoke-succeeded", "edge-prismatic-x1-step-smoke-succeeded", "edge-prismatic-x3-step-smoke-succeeded");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-topology-validated", "edge-prismatic-x3-topology-formula-validated");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-no-air-edge-sweep-used", "edge-prismatic-x1-no-air-edge-sweep-used", "edge-prismatic-x3-no-air-edge-sweep-used");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-no-brep-bounded-chamfer-used", "edge-prismatic-x1-no-brep-bounded-chamfer-used", "edge-prismatic-x3-no-brep-bounded-chamfer-used");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-no-topology-graft-used", "edge-prismatic-x1-no-topology-graft-used", "edge-prismatic-x3-no-topology-graft-used");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-no-3d-boolean-used", "edge-prismatic-x1-no-3d-boolean-used", "edge-prismatic-x3-no-3d-boolean-used");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-non-increasing-sections-rejected", "edge-prismatic-x1-non-increasing-sections-rejected", "edge-prismatic-x3-non-increasing-sections-rejected");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-mismatched-vertex-count-rejected", "edge-prismatic-x1-mismatched-vertex-count-rejected", "edge-prismatic-x3-mismatched-vertex-count-rejected");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-missing-correspondence-rejected", "edge-prismatic-x1-missing-correspondence-rejected", "edge-prismatic-x3-missing-correspondence-rejected");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-invalid-profile-rejected", "edge-prismatic-x1-invalid-profile-rejected", "edge-prismatic-x3-invalid-profile-rejected");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-holes-deferred", "edge-prismatic-x1-holes-deferred", "edge-prismatic-x3-holes-deferred");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-line-arc-deferred", "edge-prismatic-x1-line-arc-deferred", "edge-prismatic-x3-line-arc-deferred");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-multiple-loops-deferred", "edge-prismatic-x1-multiple-loops-deferred", "edge-prismatic-x3-multiple-loops-deferred");
        AddIf(v1Diagnostics, diagnostics, "edge-prismatic-v1-invalid-section-rejected", "edge-prismatic-x1-invalid-section-rejected", "edge-prismatic-x3-invalid-profile-rejected");
        return diagnostics;
    }

    private static void AddIf(IReadOnlyList<string> source, List<string> target, string sourceDiagnostic, params string[] targetDiagnostics)
    {
        if (source.Contains(sourceDiagnostic))
        {
            target.AddRange(targetDiagnostics);
        }
    }

    private static LabProfileStatus ToLabStatus(CorePrismatic.PrismaticSectionTransitionStatus status) => status switch
    {
        CorePrismatic.PrismaticSectionTransitionStatus.Succeeded => LabProfileStatus.Succeeded,
        CorePrismatic.PrismaticSectionTransitionStatus.Deferred => LabProfileStatus.Deferred,
        _ => LabProfileStatus.Failed,
    };

    private static PrismaticTransitionTopologySummary ToLabTopology(CorePrismatic.PrismaticTransitionTopologySummary t) => new(
        t.BodyProduced,
        t.SectionCount,
        t.VertexCount,
        t.EdgeCount,
        t.BottomProfileEdgeCount,
        t.TopProfileEdgeCount,
        t.TransitionEdgeCount,
        t.CapFaceCount,
        t.TransitionFaceCount,
        t.StableIntervalFaceCount,
        t.ChangedIntervalFaceCount,
        t.FaceCount,
        t.PlanarFaceCount,
        t.CylindricalFaceCount,
        t.LoopCount,
        t.CoedgeCount,
        t.Bounds);

    private static PrismaticSectionTransitionStepSummary ToLabStep(CorePrismatic.PrismaticSectionTransitionStepSummary s) => new(
        s.Exported,
        s.PresentMarkers,
        s.MissingRequiredMarkers,
        s.AbsentMarkers,
        s.UnexpectedPresentMarkers);

    private static PrismaticSection RectangleSection(double z, double width, double depth)
    {
        var x = width * 0.5d;
        var y = depth * 0.5d;
        return new(z, [(-x, -y), (x, -y), (x, y), (-x, y)]);
    }

    private static PrismaticSection RegularPolygonSection(double z, double radius, int vertices)
    {
        var points = Enumerable.Range(0, vertices)
            .Select(i =>
            {
                var a = ((Math.PI * 2d) * i / vertices) - (Math.PI * 0.5d);
                return (X: Math.Cos(a) * radius, Y: Math.Sin(a) * radius);
            })
            .ToArray();
        return new(z, points);
    }

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
}
