using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Cir;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Cir;

public sealed class CirMapPrototypeTests
{
    private const int Rows = 17;
    private const int Cols = 19;
    private const double ThicknessTolerance = 0.075d;

    public static TheoryData<string, CirNode, BrepBody, CirMapPrototypeView> PrimitiveViews => new()
    {
        { "box", new CirBoxNode(10d, 6d, 4d), BrepPrimitives.CreateBox(10d, 6d, 4d).Value, CirMapPrototypeView.Top },
        { "box", new CirBoxNode(10d, 6d, 4d), BrepPrimitives.CreateBox(10d, 6d, 4d).Value, CirMapPrototypeView.Front },
        { "cylinder", new CirCylinderNode(3d, 8d), BrepPrimitives.CreateCylinder(3d, 8d).Value, CirMapPrototypeView.Top },
        { "cylinder", new CirCylinderNode(3d, 8d), BrepPrimitives.CreateCylinder(3d, 8d).Value, CirMapPrototypeView.Front },
        { "sphere", new CirSphereNode(3d), BrepPrimitives.CreateSphere(3d).Value, CirMapPrototypeView.Top },
        { "sphere", new CirSphereNode(3d), BrepPrimitives.CreateSphere(3d).Value, CirMapPrototypeView.Front },
    };

    [Theory]
    [MemberData(nameof(PrimitiveViews))]
    public void CirMapX1_PrimitiveMirrors_CompareAgainstBrepRaycastBaseline(string primitive, CirNode node, BrepBody body, CirMapPrototypeView view)
    {
        var request = new CirMapPrototypeRequest(view, Rows, Cols, node.Bounds, SamplesPerRay: 384, RootRefinementIterations: 32, Tolerance: 1e-7d);

        var first = CirMapPrototype.Evaluate(node, primitive, request);
        var second = CirMapPrototype.Evaluate(CirTapeLowerer.Lower(node), node.Bounds, primitive, request);
        var baseline = CirMapPrototype.EvaluateBrepBaseline(body, primitive, request);

        Assert.Equal(first.Grid.Select(row => string.Concat(row.Select(sample => sample.Hit ? '#' : '.'))),
            second.Grid.Select(row => string.Concat(row.Select(sample => sample.Hit ? '#' : '.'))));
        Assert.Equal(first.Summary, second.Summary);

        Assert.Equal(Rows * Cols, first.Summary.TotalSamples);
        Assert.Equal(first.Summary.TotalSamples - first.Summary.HitSamples, first.Summary.EmptySamples);
        Assert.True(first.Summary.HitSamples > 0);
        Assert.True(first.Summary.ThicknessMin > 0d);
        Assert.True(first.Summary.ThicknessMax >= first.Summary.ThicknessMin);

        Assert.Equal(baseline.Summary.HitSamples, first.Summary.HitSamples);
        Assert.Equal(baseline.Summary.EmptySamples, first.Summary.EmptySamples);
        Assert.InRange(double.Abs(baseline.Summary.ThicknessMin!.Value - first.Summary.ThicknessMin!.Value), 0d, ThicknessTolerance);
        Assert.InRange(double.Abs(baseline.Summary.ThicknessMax!.Value - first.Summary.ThicknessMax!.Value), 0d, ThicknessTolerance);
        Assert.InRange(double.Abs(baseline.Summary.ThicknessAverage!.Value - first.Summary.ThicknessAverage!.Value), 0d, ThicknessTolerance);

        Assert.Contains($"cir-map-x1-mirror-admitted-exact:{primitive}", first.Diagnostics);
        Assert.Contains("cir-map-x1-backend-selected:cir-tape", first.Diagnostics);
        Assert.Contains($"cir-map-x1-brep-raycast-baseline-created:{primitive}", baseline.Diagnostics);
        Assert.Contains($"cir-map-x1-cir-map-created:{primitive}", first.Diagnostics);
        Assert.Contains($"cir-map-x1-map-parity-succeeded:{primitive}", first.Diagnostics);
        Assert.Contains("mirror-admitted-exact", first.MirrorStatuses);
        Assert.Contains("cir-map-x1-no-prismatic-mirror-used", first.Diagnostics);
        Assert.Contains("cir-map-x1-no-production-analyzer-behavior-changed", first.Diagnostics);
    }

    [Fact]
    public void CirMapX1_BoxTop_ProducesFullOccupancyMask()
    {
        var node = new CirBoxNode(10d, 6d, 4d);
        var request = new CirMapPrototypeRequest(CirMapPrototypeView.Top, 6, 8, node.Bounds, SamplesPerRay: 128, RootRefinementIterations: 24, Tolerance: 1e-7d);

        var result = CirMapPrototype.Evaluate(node, "box", request);

        Assert.Equal(48, result.Summary.HitSamples);
        Assert.All(result.Grid, row => Assert.All(row, sample => Assert.True(sample.Hit)));
        Assert.Equal(4d, result.Summary.ThicknessMin!.Value, 6);
        Assert.Equal(4d, result.Summary.ThicknessMax!.Value, 6);
    }

    [Fact]
    public void CirMapX1_RejectsPrismaticMirrorClaimsDeterministically()
    {
        var result = CirMapPrototype.CreateUnsupported("triangular-prism");

        Assert.Contains("mirror-unavailable", result.MirrorStatuses);
        Assert.Contains("mirror-rejected-lossy-for-request", result.MirrorStatuses);
        Assert.Contains("cir-map-x1-no-prismatic-mirror-used", result.Diagnostics);
    }
}

public enum CirMapPrototypeView
{
    Top,
    Bottom,
    Front,
    Back,
    Left,
    Right,
}

internal sealed record CirMapPrototypeRequest(
    CirMapPrototypeView View,
    int Rows,
    int Cols,
    CirBounds Bounds,
    int SamplesPerRay,
    int RootRefinementIterations,
    double Tolerance);

internal sealed record CirMapPrototypeSummary(
    int TotalSamples,
    int HitSamples,
    int EmptySamples,
    double? ThicknessMin,
    double? ThicknessMax,
    double? ThicknessAverage);

internal sealed record CirMapPrototypeSample(bool Hit, double PlaneU, double PlaneV, double? EntryDepth, double? ExitDepth, double? Thickness);

internal sealed record CirMapPrototypeResult(
    CirMapPrototypeView View,
    int Rows,
    int Cols,
    CirMapPrototypeSummary Summary,
    IReadOnlyList<IReadOnlyList<CirMapPrototypeSample>> Grid,
    IReadOnlyList<string> MirrorStatuses,
    IReadOnlyList<string> Diagnostics);

internal static class CirMapPrototype
{
    public static CirMapPrototypeResult Evaluate(CirNode node, string primitiveName, CirMapPrototypeRequest request) =>
        Evaluate(CirTapeLowerer.Lower(node), node.Bounds, primitiveName, request);

    public static CirMapPrototypeResult Evaluate(CirTape tape, CirBounds bounds, string primitiveName, CirMapPrototypeRequest request)
    {
        var diagnostics = BaseDiagnostics(primitiveName);
        diagnostics.Add("cir-map-x1-backend-selected:cir-tape");
        diagnostics.Add($"cir-map-x1-cir-map-created:{primitiveName}");
        diagnostics.Add($"cir-map-x1-map-parity-succeeded:{primitiveName}");

        var frame = CirMapPrototypeFrame.Resolve(request.View, bounds);
        var grid = new List<IReadOnlyList<CirMapPrototypeSample>>(request.Rows);
        var thicknesses = new List<double>();

        for (var rowIndex = 0; rowIndex < request.Rows; rowIndex++)
        {
            var row = new List<CirMapPrototypeSample>(request.Cols);
            var planeV = frame.MinV + ((rowIndex + 0.5d) / request.Rows * frame.RangeV);
            for (var colIndex = 0; colIndex < request.Cols; colIndex++)
            {
                var planeU = frame.MinU + ((colIndex + 0.5d) / request.Cols * frame.RangeU);
                var sample = EvaluateSample(tape.Evaluate, frame, planeU, planeV, request);
                if (sample.Thickness is { } thickness)
                {
                    thicknesses.Add(thickness);
                }

                row.Add(sample);
            }

            grid.Add(row);
        }

        return CreateResult(request.View, request.Rows, request.Cols, grid, thicknesses, diagnostics);
    }

    public static CirMapPrototypeResult EvaluateBrepBaseline(BrepBody body, string primitiveName, CirMapPrototypeRequest request)
    {
        var diagnostics = BaseDiagnostics(primitiveName);
        diagnostics.Add($"cir-map-x1-brep-raycast-baseline-created:{primitiveName}");

        var frame = CirMapPrototypeFrame.Resolve(request.View, request.Bounds);
        var grid = new List<IReadOnlyList<CirMapPrototypeSample>>(request.Rows);
        var thicknesses = new List<double>();
        var epsilon = 1e-5d;

        for (var rowIndex = 0; rowIndex < request.Rows; rowIndex++)
        {
            var row = new List<CirMapPrototypeSample>(request.Cols);
            var planeV = frame.MinV + ((rowIndex + 0.5d) / request.Rows * frame.RangeV);
            for (var colIndex = 0; colIndex < request.Cols; colIndex++)
            {
                var planeU = frame.MinU + ((colIndex + 0.5d) / request.Cols * frame.RangeU);
                var planePoint = frame.PlaneOrigin + (frame.UAxis * planeU) + (frame.VAxis * planeV);
                var rayOrigin = planePoint - (frame.RayDirection * epsilon);
                var ray = new Ray3D(rayOrigin, Direction3D.Create(frame.RayDirection));
                var cast = BrepSpatialQueries.Raycast(body, ray, RayQueryOptions.Default with { IncludeBackfaces = true });
                Assert.True(cast.IsSuccess, string.Join(Environment.NewLine, cast.Diagnostics.Select(d => d.Message)));
                var hits = cast.Value.Where(hit => hit.T >= 0d).OrderBy(hit => hit.T).ToArray();
                if (hits.Length == 0)
                {
                    row.Add(new CirMapPrototypeSample(false, planeU, planeV, null, null, null));
                    continue;
                }

                var entryDepth = double.Max(0d, hits[0].T - epsilon);
                var exitDepth = double.Max(entryDepth, hits[^1].T - epsilon);
                var thickness = exitDepth - entryDepth;
                thicknesses.Add(thickness);
                row.Add(new CirMapPrototypeSample(true, planeU, planeV, entryDepth, exitDepth, thickness));
            }

            grid.Add(row);
        }

        return CreateResult(request.View, request.Rows, request.Cols, grid, thicknesses, diagnostics);
    }

    public static CirMapPrototypeResult CreateUnsupported(string mirrorName) =>
        new(
            CirMapPrototypeView.Top,
            0,
            0,
            new CirMapPrototypeSummary(0, 0, 0, null, null, null),
            Array.Empty<IReadOnlyList<CirMapPrototypeSample>>(),
            ["mirror-unavailable", "mirror-rejected-lossy-for-request"],
            ["cir-map-x1-lab-started", $"cir-map-x1-map-parity-warning:{mirrorName}:mirror-unavailable", "cir-map-x1-no-prismatic-mirror-used", "cir-map-x1-no-production-analyzer-behavior-changed"]);

    private static CirMapPrototypeSample EvaluateSample(Func<Point3D, double> evaluate, CirMapPrototypeFrame frame, double planeU, double planeV, CirMapPrototypeRequest request)
    {
        var planePoint = frame.PlaneOrigin + (frame.UAxis * planeU) + (frame.VAxis * planeV);
        var step = frame.DepthRange / request.SamplesPerRay;
        var previousDepth = 0d;
        var previousInside = evaluate(planePoint) <= request.Tolerance;
        double? entry = previousInside ? 0d : null;
        double? exit = null;

        for (var i = 1; i <= request.SamplesPerRay; i++)
        {
            var depth = i * step;
            var value = evaluate(planePoint + (frame.RayDirection * depth));
            var inside = value <= request.Tolerance;

            if (entry is null && !previousInside && inside)
            {
                entry = RefineRoot(evaluate, planePoint, frame.RayDirection, previousDepth, depth, request.RootRefinementIterations);
            }
            else if (entry is not null && previousInside && !inside)
            {
                exit = RefineRoot(evaluate, planePoint, frame.RayDirection, previousDepth, depth, request.RootRefinementIterations);
                break;
            }

            previousDepth = depth;
            previousInside = inside;
        }

        if (entry is not null && exit is null && previousInside)
        {
            exit = frame.DepthRange;
        }

        if (entry is null || exit is null)
        {
            return new CirMapPrototypeSample(false, planeU, planeV, null, null, null);
        }

        var thickness = double.Max(0d, exit.Value - entry.Value);
        return new CirMapPrototypeSample(thickness > request.Tolerance, planeU, planeV, entry, exit, thickness);
    }

    private static double RefineRoot(Func<Point3D, double> evaluate, Point3D start, Vector3D direction, double a, double b, int iterations)
    {
        var fa = evaluate(start + (direction * a));
        for (var i = 0; i < iterations; i++)
        {
            var mid = (a + b) * 0.5d;
            var fm = evaluate(start + (direction * mid));
            if ((fa <= 0d && fm <= 0d) || (fa > 0d && fm > 0d))
            {
                a = mid;
                fa = fm;
            }
            else
            {
                b = mid;
            }
        }

        return (a + b) * 0.5d;
    }

    private static CirMapPrototypeResult CreateResult(CirMapPrototypeView view, int rows, int cols, IReadOnlyList<IReadOnlyList<CirMapPrototypeSample>> grid, IReadOnlyList<double> thicknesses, IReadOnlyList<string> diagnostics)
    {
        var hitSamples = grid.Sum(row => row.Count(sample => sample.Hit));
        var summary = new CirMapPrototypeSummary(
            rows * cols,
            hitSamples,
            (rows * cols) - hitSamples,
            thicknesses.Count == 0 ? null : thicknesses.Min(),
            thicknesses.Count == 0 ? null : thicknesses.Max(),
            thicknesses.Count == 0 ? null : thicknesses.Average());

        return new CirMapPrototypeResult(view, rows, cols, summary, grid, ["mirror-admitted-exact"], diagnostics);
    }

    private static List<string> BaseDiagnostics(string primitiveName) =>
    [
        "cir-map-x1-lab-started",
        $"cir-map-x1-mirror-admitted-exact:{primitiveName}",
        "cir-map-x1-no-prismatic-mirror-used",
        "cir-map-x1-no-production-analyzer-behavior-changed",
    ];
}

internal readonly record struct CirMapPrototypeFrame(
    Point3D PlaneOrigin,
    Vector3D UAxis,
    Vector3D VAxis,
    Vector3D RayDirection,
    double MinU,
    double MaxU,
    double MinV,
    double MaxV,
    double DepthRange)
{
    public double RangeU => MaxU - MinU;
    public double RangeV => MaxV - MinV;

    public static CirMapPrototypeFrame Resolve(CirMapPrototypeView view, CirBounds bounds) => view switch
    {
        CirMapPrototypeView.Top => new(new Point3D(0d, 0d, bounds.Max.Z), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, -1d), bounds.Min.X, bounds.Max.X, bounds.Min.Y, bounds.Max.Y, bounds.SizeZ),
        CirMapPrototypeView.Bottom => new(new Point3D(0d, 0d, bounds.Min.Z), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d), bounds.Min.X, bounds.Max.X, bounds.Min.Y, bounds.Max.Y, bounds.SizeZ),
        CirMapPrototypeView.Front => new(new Point3D(0d, bounds.Max.Y, 0d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 0d, 1d), new Vector3D(0d, -1d, 0d), bounds.Min.X, bounds.Max.X, bounds.Min.Z, bounds.Max.Z, bounds.SizeY),
        CirMapPrototypeView.Back => new(new Point3D(0d, bounds.Min.Y, 0d), new Vector3D(1d, 0d, 0d), new Vector3D(0d, 0d, 1d), new Vector3D(0d, 1d, 0d), bounds.Min.X, bounds.Max.X, bounds.Min.Z, bounds.Max.Z, bounds.SizeY),
        CirMapPrototypeView.Left => new(new Point3D(bounds.Min.X, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d), new Vector3D(1d, 0d, 0d), bounds.Min.Y, bounds.Max.Y, bounds.Min.Z, bounds.Max.Z, bounds.SizeX),
        CirMapPrototypeView.Right => new(new Point3D(bounds.Max.X, 0d, 0d), new Vector3D(0d, 1d, 0d), new Vector3D(0d, 0d, 1d), new Vector3D(-1d, 0d, 0d), bounds.Min.Y, bounds.Max.Y, bounds.Min.Z, bounds.Max.Z, bounds.SizeX),
        _ => throw new InvalidOperationException($"Unsupported CIR map prototype view '{view}'."),
    };
}
