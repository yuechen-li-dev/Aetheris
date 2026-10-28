using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Analysis;

public sealed record FirmamentVolumeMeasurement(double? ValueMm3, double? LowerBoundMm3, double? UpperBoundMm3,
    string Method, bool Authoritative, int? RegionsVisited = null, bool BudgetExhausted = false, string? Diagnostic = null)
{
    public int? AffineCutRegions { get; init; }
    public int? IntervalFallbackRegions { get; init; }
    public double? ErrorBoundMm3 => LowerBoundMm3 is { } lo && UpperBoundMm3 is { } hi ? (hi - lo) * .5 : null;
}
public sealed record FirmamentBoundsMeasurement(double[] MinimumMm, double[] MaximumMm, string Method);
public sealed record FirmamentTopologyMeasurement(int Faces, int Edges, int Vertices, int Shells);

/// <summary>Compiler inspection and source assertions consume this same measurement authority.
/// Mesh-derived mass properties remain a separately requested diagnostic comparison.</summary>
public sealed class FirmamentMeasurements
{
    private readonly FirmamentStepExportResult product;
    private readonly RectangularPlateDefinition? rectangularPlate;
    public BrepBody Body { get; }
    public string? TargetBodyId { get; }
    public FirmamentTopologyMeasurement Topology { get; }
    public FirmamentBoundsMeasurement? Bounds { get; }

    public FirmamentMeasurements(FirmamentStepExportResult product)
    {
        this.product = product;
        var imported = Step242Importer.ImportBody(product.StepText);
        if (!imported.IsSuccess) throw new InvalidOperationException("firmament-measurement-import-failed: " + string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
        Body = imported.Value;
        Topology = new(Body.Topology.Faces.Count(), Body.Topology.Edges.Count(), Body.Topology.Vertices.Count(), Body.Topology.Shells.Count());
        var document = product.RuntimeAdmissionDocument;
        var candidates = (document?.Composes is { Count: > 0 } composes ? composes.Select(c => c.Name)
            : document?.SymbolTable is { } symbols ? symbols.Symbols.Where(s => s.Kind == FirmamentV2CanonicalSymbolKind.Body).Select(s => s.Name)
            : document?.Solids.Where(s => s.RecordType != "Profile").Select(s => s.Name) ?? []).Distinct(StringComparer.Ordinal).ToArray();
        // Some legacy executors identify their generating Profile, not its owning
        // material Struct. Resolve through admitted material symbols before using
        // an executor's internal identity as an author-visible assertion target.
        TargetBodyId = new[] { product.RuntimeBodySourceName, product.RuntimeCorrespondence?.BodyStableId, product.ExportedFeatureId }
            .FirstOrDefault(name => name is not null && candidates.Contains(name, StringComparer.Ordinal))
            ?? (candidates.Length == 1 ? candidates[0] : null);
        if (BrepRectangularPlateQuery.TryDefine(Body, out var plate, out _))
        {
            rectangularPlate = plate;
            Bounds = new([plate!.Origin.X, plate.Origin.Y, plate.Origin.Z],
                [plate.Origin.X + plate.Width, plate.Origin.Y + plate.Height, plate.Origin.Z + plate.Thickness], "ExactRectangularStockEnvelope");
        }
        else if (Body.Geometry.Surfaces.All(s => s.Value.Kind == SurfaceGeometryKind.Plane)
            && Body.Geometry.Curves.All(c => c.Value.Kind == CurveGeometryKind.Line3))
        {
            var points = Body.Topology.Vertices.Select(v => Body.TryGetVertexPoint(v.Id, out var p) ? p : throw new InvalidOperationException("firmament-bounds-unavailable: Missing vertex coordinates.")).ToArray();
            if (points.Length > 0) Bounds = new([points.Min(p => p.X), points.Min(p => p.Y), points.Min(p => p.Z)],
                [points.Max(p => p.X), points.Max(p => p.Y), points.Max(p => p.Z)], "ExactPlanarLinearBoundaryExtrema");
        }
    }

    private sealed record VolumeContext(BrepMassPropertiesResult? Exact, RectangularPlateDefinition? Plate, SdfNode? Root, bool ForceSdf);
    private static readonly JudgmentEngine<VolumeContext> VolumePolicy = new();
    private static readonly JudgmentCandidate<VolumeContext>[] VolumeStrategies =
    [
        new("ExactBrep", c => !c.ForceSdf && c.Exact is not null, _ => 2,
            RejectionReason: _ => "No admitted exact volume recognizer, or SDF was explicitly requested.", TieBreakerPriority: 0),
        new("ExactRectangularPlate", c => !c.ForceSdf && c.Plate is not null, _ => 2,
            RejectionReason: _ => "The complete rectangular-stock/through-hole query did not admit this body, or SDF was explicitly requested.", TieBreakerPriority: 1),
        new("BoundedSdf", c => c.Root is not null, _ => 1,
            RejectionReason: _ => "The compiler retained no analytic CIR field qualified to measure the exported primitive. Display-only or legacy Boolean mirrors are not volume authority.", TieBreakerPriority: 2)
    ];

    public FirmamentVolumeMeasurement Volume(double? absoluteErrorMm3 = null, bool forceSdf = false, int maximumRegions = 1_000_001)
    {
        BrepMassPropertiesResult? exact = null;
        if (!forceSdf && BrepMassProperties.TryEvaluateExactVolume(Body, out var recognized)) exact = recognized;
        var root = product.Cir is { RuntimeRootIsExportedPrimitive: true } cir ? cir.RuntimeRoot : null;
        var decision = VolumePolicy.Evaluate(new(exact, rectangularPlate, root, forceSdf), VolumeStrategies);
        if (!decision.IsSuccess) return new(null, null, null, "Unavailable", false, Diagnostic:
            "firmament-v2-assert-volume-measurement-unavailable: " + string.Join(" ", decision.Rejections.Select(r => r.Reason)));
        if (decision.Selection!.Value.Candidate.Name == "ExactBrep")
            return new(exact!.AbsoluteVolume, exact.AbsoluteVolume, exact.AbsoluteVolume, exact.EvaluationMethod, true);
        if (decision.Selection.Value.Candidate.Name == "ExactRectangularPlate")
        {
            var plate = rectangularPlate!;
            var value = (plate.Width * plate.Height - plate.Holes.Sum(h => Math.PI * h.Diameter * h.Diameter / 4)) * plate.Thickness;
            return new(value, value, value, "ExactRectangularStockMinusThroughHoles", true);
        }
        var bounds = root!.Bounds;
        var error = absoluteErrorMm3 ?? bounds.SizeX * bounds.SizeY * bounds.SizeZ * .005;
        var result = SdfAdaptiveVolumeEstimator.EstimateBounded(SdfTapeLowerer.Lower(root), bounds, error, maximumRegions);
        return new(result.EstimatedVolume, result.LowerBound, result.UpperBound, "SdfAdaptiveOccupiedVolumeInterval", true, result.RegionsVisited, result.BudgetExhausted)
        { AffineCutRegions = result.AffineCutRegions, IntervalFallbackRegions = result.IntervalFallbackRegions };
    }
}
