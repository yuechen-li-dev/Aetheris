using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum CirPrismaticMirrorStrategy
{
    HalfSpaceConvexPolyhedron,
    SectionStackImplicit,
}

public enum CirPrismaticMirrorRequestKind
{
    PointContainmentAndMapOccupancy,
    FaceIdentity,
    TopologyParity,
}

public sealed record CirPrismaticPointExpectation(string Name, Point3D Point, bool ExpectedInside, string Reason);

public sealed record CirPrismaticPointClassification(string Name, Point3D Point, bool ExpectedInside, bool ActualInside, double SignedDistance, bool Matched, string Reason);

public sealed record CirPrismaticMapSummary(
    int Rows,
    int Cols,
    int TotalSamples,
    int HitSamples,
    int EmptySamples,
    double? ThicknessMin,
    double? ThicknessMax,
    double? ThicknessAverage,
    string Bounds)
{
    public override string ToString() => FormattableString.Invariant(
        $"rows={Rows};cols={Cols};total={TotalSamples};hit={HitSamples};empty={EmptySamples};min={Fmt(ThicknessMin)};max={Fmt(ThicknessMax)};avg={Fmt(ThicknessAverage)};bounds={Bounds}");

    private static string Fmt(double? value) => value is { } v ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "null";
}

public sealed record CirPrismaticMirrorResult(
    string CaseName,
    CirPrismaticMirrorStrategy Strategy,
    string MirrorStatus,
    string Capabilities,
    string KnownLosses,
    bool Succeeded,
    IReadOnlyList<CirPrismaticPointClassification> PointClassifications,
    CirPrismaticMapSummary? MapSummary,
    IReadOnlyList<string> Diagnostics,
    string Recommendation)
{
    public string StableProjection() => string.Join("|", new[]
    {
        CaseName,
        Strategy.ToString(),
        MirrorStatus,
        Capabilities,
        KnownLosses,
        Succeeded.ToString(),
        MapSummary?.ToString() ?? "no-map",
        string.Join(",", PointClassifications.Select(p => FormattableString.Invariant($"{p.Name}:{p.ActualInside}:{p.SignedDistance:0.######}:{p.Matched}"))),
        string.Join(",", Diagnostics),
        Recommendation,
    });
}

public static class CirPrismaticMirrorLab
{
    private const double DefaultTolerance = 1e-7d;
    private const int MapRows = 16;
    private const int MapCols = 16;
    private const int ThicknessSamples = 96;

    public static IReadOnlyList<CirPrismaticMirrorResult> RunRequiredCases() =>
    [
        Evaluate("rectangle-inset", PrismaticSectionTransitionEmitterLab.RectangleToInsetRectangle(), CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron),
        Evaluate("rectangle-inset", PrismaticSectionTransitionEmitterLab.RectangleToInsetRectangle(), CirPrismaticMirrorStrategy.SectionStackImplicit),
        Evaluate("top-edge-chamfer", TopEdgeChamferCase(), CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron),
        Evaluate("top-edge-chamfer", TopEdgeChamferCase(), CirPrismaticMirrorStrategy.SectionStackImplicit),
    ];

    public static CirPrismaticMirrorResult Evaluate(
        string caseName,
        PrismaticSectionTransitionCase sourceCase,
        CirPrismaticMirrorStrategy strategy,
        CirPrismaticMirrorRequestKind requestKind = CirPrismaticMirrorRequestKind.PointContainmentAndMapOccupancy,
        double tolerance = DefaultTolerance)
    {
        var diagnostics = new List<string>
        {
            "cir-prismatic-x1-lab-started",
            $"cir-prismatic-x1-case-started:{caseName}",
            strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
                ? "cir-prismatic-x1-strategy-halfspace-attempted"
                : "cir-prismatic-x1-strategy-section-stack-attempted",
            "cir-prismatic-x1-loss-face-identity",
            "cir-prismatic-x1-loss-loop-identity",
            "cir-prismatic-x1-loss-split-face-lineage",
            "cir-prismatic-x1-loss-topology-parity",
            "cir-prismatic-x1-no-production-analyzer-behavior-changed",
            "cir-prismatic-x1-no-cir-to-brep-extraction",
        };

        if (requestKind is CirPrismaticMirrorRequestKind.FaceIdentity or CirPrismaticMirrorRequestKind.TopologyParity)
        {
            diagnostics.Add("cir-prismatic-x1-mirror-rejected-lossy-for-request");
            diagnostics.Add(requestKind == CirPrismaticMirrorRequestKind.FaceIdentity
                ? "cir-prismatic-x1-request-face-identity-rejected"
                : "cir-prismatic-x1-request-topology-parity-rejected");
            return new(caseName, strategy, "mirror-rejected-lossy-for-request", "none", LossesText(), false, [], null, StableDiagnostics(diagnostics), "cir-prismatic-mirror-invalid-rejected");
        }

        var validationBlocker = ValidateCase(sourceCase, tolerance);
        if (validationBlocker is not null)
        {
            diagnostics.Add(strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
                ? $"cir-prismatic-x1-strategy-halfspace-blocked:{validationBlocker}"
                : $"cir-prismatic-x1-strategy-section-stack-blocked:{validationBlocker}");
            return new(caseName, strategy, "mirror-unavailable", "none", LossesText(), false, [], null, StableDiagnostics(diagnostics), "cir-prismatic-mirror-deferred");
        }

        IPrismaticMirrorEvaluator evaluator = strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
            ? HalfSpacePrismaticMirror.Create(sourceCase.Sections, tolerance)
            : SectionStackPrismaticMirror.Create(sourceCase.Sections, tolerance);

        diagnostics.Add(strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
            ? "cir-prismatic-x1-strategy-halfspace-succeeded"
            : "cir-prismatic-x1-strategy-section-stack-succeeded");
        diagnostics.Add($"cir-prismatic-x1-mirror-admitted-exact:{caseName}");
        diagnostics.Add($"cir-prismatic-x1-point-classification-succeeded:{caseName}");
        diagnostics.Add($"cir-prismatic-x1-map-summary-created:{caseName}");

        var classifications = TestPoints(caseName).Select(p => Classify(evaluator, p, tolerance)).ToArray();
        if (classifications.Any(p => !p.Matched))
        {
            diagnostics.Add($"cir-prismatic-x1-point-classification-mismatch:{caseName}");
        }

        var summary = CreateMapSummary(evaluator, tolerance);
        return new(
            caseName,
            strategy,
            "mirror-admitted-exact",
            "point-containment,map-occupancy,section-sampling,approximate-volume",
            LossesText(),
            classifications.All(p => p.Matched),
            classifications,
            summary,
            StableDiagnostics(diagnostics),
            strategy == CirPrismaticMirrorStrategy.HalfSpaceConvexPolyhedron
                ? "cir-prismatic-mirror-use-convex-polyhedron-first"
                : "cir-prismatic-mirror-needs-section-stack-evaluator");
    }

    private static PrismaticSectionTransitionCase TopEdgeChamferCase() =>
        new(
            "top-edge-chamfer",
            PrismaticTopEdgeChamferLab.CreateSectionStack(new PrismaticTopEdgeChamferCase("canonical-top-pos-x-edge", 10, 8, 6, 1)),
            PrismaticCorrespondenceMap.Identity(4));

    private static string? ValidateCase(PrismaticSectionTransitionCase sourceCase, double tolerance)
    {
        if (sourceCase.Correspondence is null)
        {
            return "missing-correspondence";
        }

        if (sourceCase.Sections.Count < 2)
        {
            return "too-few-sections";
        }

        var vertexCount = sourceCase.Sections[0].OuterLoop.Count;
        if (vertexCount < 3 || sourceCase.Correspondence.VertexMap.Count != vertexCount)
        {
            return "unsupported-correspondence";
        }

        for (var i = 0; i < sourceCase.Sections.Count; i++)
        {
            var section = sourceCase.Sections[i];
            if (section.HasArcs || section.HasHoles || section.OuterLoopCount != 1)
            {
                return "non-line-only-single-loop-section";
            }

            if (section.OuterLoop.Count != vertexCount)
            {
                return "mismatched-vertex-count";
            }

            if (SignedArea(section.OuterLoop) <= tolerance)
            {
                return "non-convex-or-non-ccw-profile";
            }

            if (!IsConvex(section.OuterLoop, tolerance))
            {
                return "non-convex-profile";
            }

            if (i > 0 && section.Z <= sourceCase.Sections[i - 1].Z + tolerance)
            {
                return "non-increasing-z";
            }
        }

        return null;
    }

    private static CirPrismaticPointClassification Classify(IPrismaticMirrorEvaluator evaluator, CirPrismaticPointExpectation expectation, double tolerance)
    {
        var distance = evaluator.SignedDistance(expectation.Point);
        var inside = distance <= tolerance;
        return new(expectation.Name, expectation.Point, expectation.ExpectedInside, inside, distance, inside == expectation.ExpectedInside, expectation.Reason);
    }

    private static IReadOnlyList<CirPrismaticPointExpectation> TestPoints(string caseName) => caseName switch
    {
        "rectangle-inset" =>
        [
            new("center-mid-height", new Point3D(0, 0, 0.5), true, "inside center at mid-height"),
            new("outside-far-pos-x", new Point3D(7, 0, 0.5), false, "outside far +X"),
            new("outside-far-pos-y", new Point3D(0, 6, 0.5), false, "outside far +Y"),
            new("lower-full-rectangle-only", new Point3D(4.75, 0, 0.05), true, "inside lower wide rectangle below inset transition"),
            new("upper-inset-excluded", new Point3D(4.75, 0, 0.95), false, "outside upper inset at same X"),
            new("near-side-plane-inside", new Point3D(4 - (DefaultTolerance * 0.25), 0, 1), true, "inside within tolerance near upper +X side"),
            new("near-side-plane-outside", new Point3D(4 + (DefaultTolerance * 2), 0, 1), false, "outside beyond tolerance near upper +X side"),
        ],
        "top-edge-chamfer" =>
        [
            new("lower-body-below-transition", new Point3D(4.75, 0, 4.5), true, "inside lower body below transition"),
            new("above-inset-excluded-pos-x", new Point3D(4.75, 0, 6), false, "outside upper inset excluded area near +X top transition"),
            new("below-chamfer-plane", new Point3D(4.25, 0, 5.25), true, "inside below chamfer plane near transition"),
            new("beyond-chamfer-plane", new Point3D(4.75, 0, 5.75), false, "outside beyond chamfer plane"),
            new("center-inside", new Point3D(0, 0, 3), true, "center inside"),
        ],
        _ => [new("center", new Point3D(0, 0, 0), true, "default center")],
    };

    private static CirPrismaticMapSummary CreateMapSummary(IPrismaticMirrorEvaluator evaluator, double tolerance)
    {
        var bounds = evaluator.Bounds;
        var thicknesses = new List<double>();
        var total = MapRows * MapCols;
        for (var row = 0; row < MapRows; row++)
        {
            var y = bounds.Min.Y + ((row + 0.5d) / MapRows * bounds.SizeY);
            for (var col = 0; col < MapCols; col++)
            {
                var x = bounds.Min.X + ((col + 0.5d) / MapCols * bounds.SizeX);
                var thickness = EstimateZThickness(evaluator, x, y, tolerance);
                if (thickness > tolerance)
                {
                    thicknesses.Add(thickness);
                }
            }
        }

        return new(
            MapRows,
            MapCols,
            total,
            thicknesses.Count,
            total - thicknesses.Count,
            thicknesses.Count == 0 ? null : thicknesses.Min(),
            thicknesses.Count == 0 ? null : thicknesses.Max(),
            thicknesses.Count == 0 ? null : thicknesses.Average(),
            FormatBounds(bounds));
    }

    private static double EstimateZThickness(IPrismaticMirrorEvaluator evaluator, double x, double y, double tolerance)
    {
        var bounds = evaluator.Bounds;
        var step = bounds.SizeZ / ThicknessSamples;
        var insideCount = 0;
        for (var i = 0; i < ThicknessSamples; i++)
        {
            var z = bounds.Min.Z + ((i + 0.5d) / ThicknessSamples * bounds.SizeZ);
            if (evaluator.SignedDistance(new Point3D(x, y, z)) <= tolerance)
            {
                insideCount++;
            }
        }

        return insideCount * step;
    }

    private static string FormatBounds(CirBounds bounds) => FormattableString.Invariant(
        $"[{bounds.Min.X:0.###},{bounds.Min.Y:0.###},{bounds.Min.Z:0.###}]..[{bounds.Max.X:0.###},{bounds.Max.Y:0.###},{bounds.Max.Z:0.###}]");

    private static string LossesText() => "face-identity-lost,loop-identity-lost,split-face-lineage-lost,feature-role-labels-lost,topology-parity-unavailable";

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static double SignedArea(IReadOnlyList<(double X, double Y)> loop)
    {
        var area = 0d;
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        return area * 0.5d;
    }

    private static bool IsConvex(IReadOnlyList<(double X, double Y)> loop, double tolerance)
    {
        for (var i = 0; i < loop.Count; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Count];
            var c = loop[(i + 2) % loop.Count];
            var cross = ((b.X - a.X) * (c.Y - b.Y)) - ((b.Y - a.Y) * (c.X - b.X));
            if (cross < -tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private interface IPrismaticMirrorEvaluator
    {
        CirBounds Bounds { get; }

        double SignedDistance(Point3D point);
    }

    private sealed record Plane(double A, double B, double C, double D)
    {
        public double Evaluate(Point3D point) => (A * point.X) + (B * point.Y) + (C * point.Z) + D;
    }

    private sealed class HalfSpacePrismaticMirror : IPrismaticMirrorEvaluator
    {
        private readonly IReadOnlyList<Plane> _planes;

        private HalfSpacePrismaticMirror(IReadOnlyList<Plane> planes, CirBounds bounds)
        {
            _planes = planes;
            Bounds = bounds;
        }

        public CirBounds Bounds { get; }

        public static HalfSpacePrismaticMirror Create(IReadOnlyList<PrismaticSection> sections, double tolerance)
        {
            var planes = new List<Plane>();
            var minZ = sections.Min(s => s.Z);
            var maxZ = sections.Max(s => s.Z);
            planes.Add(new Plane(0, 0, -1, minZ));
            planes.Add(new Plane(0, 0, 1, -maxZ));

            for (var sectionIndex = 0; sectionIndex < sections.Count - 1; sectionIndex++)
            {
                var lower = sections[sectionIndex];
                var upper = sections[sectionIndex + 1];
                for (var i = 0; i < lower.OuterLoop.Count; i++)
                {
                    var a0 = ToPoint(lower.OuterLoop[i], lower.Z);
                    var a1 = ToPoint(lower.OuterLoop[(i + 1) % lower.OuterLoop.Count], lower.Z);
                    var b0 = ToPoint(upper.OuterLoop[i], upper.Z);
                    var edge = a1 - a0;
                    var lift = b0 - a0;
                    var normal = Cross(edge, lift);
                    var length = double.Sqrt((normal.X * normal.X) + (normal.Y * normal.Y) + (normal.Z * normal.Z));
                    if (length <= tolerance)
                    {
                        continue;
                    }

                    var nx = normal.X / length;
                    var ny = normal.Y / length;
                    var nz = normal.Z / length;
                    planes.Add(new Plane(nx, ny, nz, -((nx * a0.X) + (ny * a0.Y) + (nz * a0.Z))));
                }
            }

            return new(planes, BoundsFor(sections));
        }

        public double SignedDistance(Point3D point) => _planes.Max(p => p.Evaluate(point));
    }

    private sealed class SectionStackPrismaticMirror : IPrismaticMirrorEvaluator
    {
        private readonly IReadOnlyList<PrismaticSection> _sections;

        private SectionStackPrismaticMirror(IReadOnlyList<PrismaticSection> sections)
        {
            _sections = sections;
            Bounds = BoundsFor(sections);
        }

        public CirBounds Bounds { get; }

        public static SectionStackPrismaticMirror Create(IReadOnlyList<PrismaticSection> sections, double _) => new(sections);

        public double SignedDistance(Point3D point)
        {
            if (point.Z < Bounds.Min.Z)
            {
                return Bounds.Min.Z - point.Z;
            }

            if (point.Z > Bounds.Max.Z)
            {
                return point.Z - Bounds.Max.Z;
            }

            var interval = 0;
            for (var i = 0; i < _sections.Count - 1; i++)
            {
                if (point.Z >= _sections[i].Z && point.Z <= _sections[i + 1].Z)
                {
                    interval = i;
                    break;
                }
            }

            var lower = _sections[interval];
            var upper = _sections[interval + 1];
            var t = (point.Z - lower.Z) / (upper.Z - lower.Z);
            var maxSide = double.NegativeInfinity;
            for (var i = 0; i < lower.OuterLoop.Count; i++)
            {
                var a = Interpolate(lower.OuterLoop[i], upper.OuterLoop[i], t);
                var b = Interpolate(lower.OuterLoop[(i + 1) % lower.OuterLoop.Count], upper.OuterLoop[(i + 1) % lower.OuterLoop.Count], t);
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var length = double.Sqrt((dx * dx) + (dy * dy));
                var value = (((point.X - a.X) * dy) - ((point.Y - a.Y) * dx)) / length;
                maxSide = double.Max(maxSide, value);
            }

            var zDistance = double.Max(Bounds.Min.Z - point.Z, point.Z - Bounds.Max.Z);
            return double.Max(maxSide, zDistance);
        }
    }

    private static Point3D ToPoint((double X, double Y) point, double z) => new(point.X, point.Y, z);

    private static (double X, double Y) Interpolate((double X, double Y) lower, (double X, double Y) upper, double t) =>
        (lower.X + ((upper.X - lower.X) * t), lower.Y + ((upper.Y - lower.Y) * t));

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));

    private static CirBounds BoundsFor(IReadOnlyList<PrismaticSection> sections)
    {
        var xs = sections.SelectMany(s => s.OuterLoop.Select(p => p.X)).ToArray();
        var ys = sections.SelectMany(s => s.OuterLoop.Select(p => p.Y)).ToArray();
        return new(new Point3D(xs.Min(), ys.Min(), sections.Min(s => s.Z)), new Point3D(xs.Max(), ys.Max(), sections.Max(s => s.Z)));
    }
}
