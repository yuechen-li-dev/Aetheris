using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Cir.Mirrors;
using Aetheris.Kernel.Core.Math;
using CorePrismatic = Aetheris.Kernel.Core.Brep.Prismatic;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public sealed record ExperimentalPrismaticMapBounds(
    ExperimentalPrismaticMapPoint Min,
    ExperimentalPrismaticMapPoint Max,
    double SizeX,
    double SizeY,
    double SizeZ);

public sealed record ExperimentalPrismaticMapPoint(double X, double Y, double Z);

public sealed record ExperimentalPrismaticMapGuarantees(
    bool NoProductionAnalyzerBehaviorChanged,
    bool NoDefaultCliBehaviorChanged,
    bool NoStepInput,
    bool NoImportedStepMirrorInference,
    bool NoCirToBrepExtraction,
    bool NoTopologyIdentityClaim);

public sealed record ExperimentalPrismaticMapResult(
    bool Success,
    string Milestone,
    string CommandRoute,
    string CaseName,
    string GeneratedSourceKind,
    string BackendSelected,
    string MirrorStatus,
    string RequestedUse,
    string View,
    int Rows,
    int Cols,
    int OccupiedCount,
    int EmptyCount,
    double? ThicknessMin,
    double? ThicknessMax,
    double? ThicknessAverage,
    ExperimentalPrismaticMapBounds? Bounds,
    IReadOnlyList<string> KnownLosses,
    IReadOnlyList<string> Diagnostics,
    ExperimentalPrismaticMapGuarantees Guarantees,
    string? Error = null);

public static class ExperimentalPrismaticMapLab
{
    public const string Milestone = "EDGE-PRISMATIC-X9";
    public const string CommandRoute = "experimental prismatic-map";
    public const string RequestedUse = "map-occupancy";
    public const string View = "top";

    public static readonly IReadOnlyList<string> SupportedCases = ["rectangle-inset", "top-edge-chamfer"];

    private static readonly string[] KnownLossDescriptions =
    [
        "face identity lost",
        "loop identity lost",
        "split-face lineage lost",
        "feature role labels lost",
        "topology parity unavailable",
    ];

    private static readonly ExperimentalPrismaticMapGuarantees DefaultGuarantees = new(
        NoProductionAnalyzerBehaviorChanged: true,
        NoDefaultCliBehaviorChanged: true,
        NoStepInput: true,
        NoImportedStepMirrorInference: true,
        NoCirToBrepExtraction: true,
        NoTopologyIdentityClaim: true);

    public static ExperimentalPrismaticMapResult Run(string caseName, int rows, int cols)
    {
        var token = Normalize(caseName);
        var diagnostics = StartDiagnostics();
        diagnostics.Add($"edge-prismatic-x9-generated-source-created:{token}");
        diagnostics.Add($"edge-prismatic-x9-cir-mirror-admission-requested:{token}");

        var sections = CreateSections(token);
        if (sections is null)
        {
            diagnostics.Add($"edge-prismatic-x9-unknown-case:{token}");
            AddAuthorityDiagnostics(diagnostics);
            return Failure(token, rows, cols, "unknown-case", diagnostics);
        }

        var mirrorResult = CirPrismaticMirrorBuilder.BuildFromSections(token, sections);
        diagnostics.AddRange(mirrorResult.Diagnostics);

        if (!mirrorResult.Succeeded || mirrorResult.Mirror is null)
        {
            diagnostics.Add($"edge-prismatic-x9-mirror-unavailable:{token}");
            diagnostics.Add("edge-prismatic-x9-backend-selected:unsupported");
            AddAuthorityDiagnostics(diagnostics);
            return Failure(token, rows, cols, "mirror-unavailable", diagnostics, mirrorResult.Admission.StatusText);
        }

        diagnostics.Add($"edge-prismatic-x9-cir-mirror-admitted-exact:{token}");
        diagnostics.Add("edge-prismatic-x9-backend-selected:cir-convex-polyhedron");
        AddLossDiagnostics(diagnostics);

        var summary = mirrorResult.Mirror.CreateTopViewSummary(rows, cols);
        diagnostics.Add($"edge-prismatic-x9-map-summary-created:{token}");
        AddAuthorityDiagnostics(diagnostics);

        return new ExperimentalPrismaticMapResult(
            Success: true,
            Milestone,
            CommandRoute,
            CaseName: token,
            GeneratedSourceKind: "generated-air-prismatic-source",
            BackendSelected: "cir-convex-polyhedron",
            MirrorStatus: mirrorResult.Admission.StatusText,
            RequestedUse,
            View,
            Rows: summary.Rows,
            Cols: summary.Cols,
            OccupiedCount: summary.OccupiedCount,
            EmptyCount: summary.EmptyCount,
            ThicknessMin: summary.ThicknessMin,
            ThicknessMax: summary.ThicknessMax,
            ThicknessAverage: summary.ThicknessAverage,
            Bounds: ToBounds(summary.Bounds),
            KnownLosses: KnownLossDescriptions,
            Diagnostics: StableDiagnostics(diagnostics),
            Guarantees: DefaultGuarantees);
    }

    public static ExperimentalPrismaticMapResult LossyRequestRejected(string caseName, string request, int rows, int cols)
    {
        var token = Normalize(caseName);
        var requestToken = Normalize(request);
        var diagnostics = StartDiagnostics();
        diagnostics.Add($"edge-prismatic-x9-generated-source-created:{token}");
        diagnostics.Add($"edge-prismatic-x9-lossy-request-rejected:{requestToken}");
        diagnostics.Add("edge-prismatic-x9-backend-selected:unsupported");
        AddLossDiagnostics(diagnostics);
        AddAuthorityDiagnostics(diagnostics);
        return Failure(token, rows, cols, "lossy-request-rejected", diagnostics, "mirror-rejected-lossy-for-request");
    }

    private static IReadOnlyList<CorePrismatic.PrismaticSection>? CreateSections(string caseName) => caseName switch
    {
        "rectangle-inset" =>
        [
            new CorePrismatic.PrismaticSection(0d, [(-5d, -3d), (5d, -3d), (5d, 3d), (-5d, 3d)]),
            new CorePrismatic.PrismaticSection(4d, [(-4d, -2d), (4d, -2d), (4d, 2d), (-4d, 2d)]),
        ],
        "top-edge-chamfer" => CorePrismatic.PrismaticTopEdgeChamferPrototype.CreateSectionStack(new CorePrismatic.PrismaticTopEdgeChamferRequest(10d, 6d, 4d, 1d)),
        _ => null,
    };

    private static ExperimentalPrismaticMapResult Failure(
        string caseName,
        int rows,
        int cols,
        string error,
        IReadOnlyList<string> diagnostics,
        string mirrorStatus = "unsupported") =>
        new(
            Success: false,
            Milestone,
            CommandRoute,
            CaseName: caseName,
            GeneratedSourceKind: "generated-air-prismatic-source",
            BackendSelected: "unsupported",
            MirrorStatus: mirrorStatus,
            RequestedUse,
            View,
            Rows: rows,
            Cols: cols,
            OccupiedCount: 0,
            EmptyCount: rows > 0 && cols > 0 ? rows * cols : 0,
            ThicknessMin: null,
            ThicknessMax: null,
            ThicknessAverage: null,
            Bounds: null,
            KnownLosses: KnownLossDescriptions,
            Diagnostics: StableDiagnostics(diagnostics),
            Guarantees: DefaultGuarantees,
            Error: error);

    private static List<string> StartDiagnostics() =>
    [
        "edge-prismatic-x9-cli-route-started",
    ];

    private static void AddLossDiagnostics(List<string> diagnostics)
    {
        diagnostics.Add("edge-prismatic-x9-loss-face-identity");
        diagnostics.Add("edge-prismatic-x9-loss-loop-identity");
        diagnostics.Add("edge-prismatic-x9-loss-split-face-lineage");
        diagnostics.Add("edge-prismatic-x9-loss-feature-role-labels");
        diagnostics.Add("edge-prismatic-x9-loss-topology-parity");
    }

    private static void AddAuthorityDiagnostics(List<string> diagnostics)
    {
        diagnostics.Add("edge-prismatic-x9-no-step-input");
        diagnostics.Add("edge-prismatic-x9-no-imported-step-mirror-inference");
        diagnostics.Add("edge-prismatic-x9-no-production-analyzer-behavior-changed");
        diagnostics.Add("edge-prismatic-x9-no-default-cli-behavior-changed");
        diagnostics.Add("edge-prismatic-x9-no-cir-to-brep-extraction");
        diagnostics.Add("edge-prismatic-x9-no-topology-identity-claim");
    }

    private static ExperimentalPrismaticMapBounds ToBounds(CirBounds bounds) =>
        new(ToPoint(bounds.Min), ToPoint(bounds.Max), bounds.SizeX, bounds.SizeY, bounds.SizeZ);

    private static ExperimentalPrismaticMapPoint ToPoint(Point3D point) => new(point.X, point.Y, point.Z);

    private static string Normalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Distinct(StringComparer.Ordinal).ToArray();
}
