using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

/// <summary>A measured cylinder group. Recognized proves the bounded through-hole
/// family, not original manufacturing history or clearance at a new destination.</summary>
public sealed record ThroughHoleCandidate(string Id, bool Recognized, bool CanPlan,
    IReadOnlyList<int> WallFaces, IReadOnlyList<int> StepFaceEntities,
    Point3D AxisOrigin, Vector3D Axis, double Diameter, Point3D? Center,
    double? Depth, int? EntryFace, int? ExitFace, IReadOnlyList<string> Diagnostics)
{
    public int? AxisIndex { get; init; }
    public string Kind => Recognized ? "RoundThroughHole" : "CylindricalGroup";
    public string Status => !Recognized ? "UnsupportedFamily" : CanPlan ? "Recognized" : "BlockedByImportQualification";
}

public sealed record ThroughHoleDiscovery(bool Qualified, double ImportToleranceMillimetres,
    IReadOnlyList<ThroughHoleCandidate> Candidates, IReadOnlyList<string> Diagnostics);

public sealed record ThroughHoleMovePlan(ThroughHoleRevisionRequest Request,
    ThroughHoleRevisionResult Revision);
public sealed record ThroughHoleRemovalPlan(ThroughHoleRemovalRequest Request, ThroughHoleRemovalResult Removal);

public static partial class BrepThroughHoleRevision
{
    public static ThroughHoleDiscovery Discover(BrepBody source, double importToleranceMillimetres = .001)
    {
        ArgumentNullException.ThrowIfNull(source);
        var diagnostics = new List<string>();
        try { RequireQualified(source, importToleranceMillimetres); }
        catch (Exception error) when (IsRecognitionFailure(error)) { diagnostics.Add(error.Message); }
        var qualified = diagnostics.Count == 0;
        var candidates = new List<ThroughHoleCandidate>();
        var visited = new HashSet<FaceId>();
        var incidence = Incidence(source);
        foreach (var face in source.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            if (visited.Contains(face.Id) || !source.TryGetFaceSurfaceGeometry(face.Id, out var surface)
                || surface?.Cylinder is not { } cylinder) continue;
            var walls = ConnectedWalls(source, face.Id, cylinder, incidence);
            visited.UnionWith(walls);
            var ids = walls.Select(id => id.Value).Order().ToArray();
            var entities = walls.Select(id => source.Bindings.GetFaceBinding(id).SourceStepEntityId)
                .OfType<int>().Order().ToArray();
            // Identity is scoped to the pinned source and independent of enumeration,
            // destination, user alias and geometric coordinates.
            var id = entities.Length == ids.Length
                ? "through-hole:step-" + string.Join('+', entities)
                : "through-hole:face-" + string.Join('+', ids);
            Region? region = null;
            var reasons = new List<string>(diagnostics);
            try { region = Recognize(source, face.Id); }
            catch (Exception error) when (IsRecognitionFailure(error)) { reasons.Add(error.Message); }
            var entry = region?.Openings.MaxBy(o => Coordinate(Surface(source, o.Face.Id).Plane!.Value.Origin, region!.Axis)).Face;
            var exit = region?.Openings.MinBy(o => Coordinate(Surface(source, o.Face.Id).Plane!.Value.Origin, region!.Axis)).Face;
            candidates.Add(new(id, region is not null, qualified && region is not null, ids, entities,
                cylinder.Origin, cylinder.Axis.ToVector(), 2 * cylinder.Radius,
                region is null ? null : WithCoordinate(cylinder.Origin, region.Axis, region.High),
                region is null ? null : region.High - region.Low, entry?.Id.Value, exit?.Id.Value, reasons)
            { AxisIndex = region?.Axis });
        }
        return new(qualified, importToleranceMillimetres, candidates, diagnostics);
    }

    /// <summary>Derives two local stock regions and executes the same production
    /// operation to prove admission. No swept movement corridor is required.</summary>
    public static KernelResult<ThroughHoleMovePlan> PlanMove(BrepBody source, string candidateId,
        string featureId, Point3D destination, double importToleranceMillimetres = .001,
        double clearanceMillimetres = .1)
    {
        if (!double.IsFinite(clearanceMillimetres) || clearanceMillimetres <= System.Math.Max(Tol, importToleranceMillimetres))
            return PlanFailure("Clearance must be finite and greater than the numerical import uncertainty.");
        var discovery = Discover(source, importToleranceMillimetres);
        var candidate = discovery.Candidates.SingleOrDefault(c => c.Id == candidateId);
        if (candidate is null) return PlanFailure("Candidate does not exist in this pinned source. Inspect the source again; no nearest-feature fallback is performed.");
        if (!candidate.CanPlan) return PlanFailure(string.Join(" | ", candidate.Diagnostics));
        var region = Recognize(source, new(candidate.WallFaces[0]));
        ThroughHoleEditBounds BoundsAt(Point3D center)
        {
            var r = candidate.Diameter / 2 + clearanceMillimetres;
            return new(WithCoordinate(center - new Vector3D(r, r, r), region.Axis, region.Low), WithCoordinate(center + new Vector3D(r, r, r), region.Axis, region.High));
        }
        var request = new ThroughHoleRevisionRequest(featureId, candidate.WallFaces.Select(id => new FaceId(id)).ToArray(),
            new(candidate.EntryFace!.Value), destination, candidate.Diameter, BoundsAt(candidate.Center!.Value), importToleranceMillimetres)
        { DestinationWithin = BoundsAt(destination) };
        var executed = Execute(source, request);
        return executed.IsSuccess ? KernelResult<ThroughHoleMovePlan>.Success(new(request, executed.Value), executed.Diagnostics)
            : KernelResult<ThroughHoleMovePlan>.Failure(executed.Diagnostics);
    }

    static bool IsRecognitionFailure(Exception error) => error is ArgumentException or InvalidOperationException or KeyNotFoundException or NotSupportedException;
    public static KernelResult<ThroughHoleRemovalPlan> PlanRemove(BrepBody source, string candidateId, string featureId,
        double importToleranceMillimetres = .001, double clearanceMillimetres = .1)
    {
        if (!double.IsFinite(clearanceMillimetres) || clearanceMillimetres <= System.Math.Max(Tol, importToleranceMillimetres))
            return KernelResult<ThroughHoleRemovalPlan>.Failure([new(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,
                "firmament-imported-hole-plan-rejected: Clearance must exceed numerical import uncertainty.","Brep.ThroughHoleRevision")]);
        var candidate = Discover(source, importToleranceMillimetres).Candidates.SingleOrDefault(c => c.Id == candidateId);
        if (candidate?.CanPlan != true) return KernelResult<ThroughHoleRemovalPlan>.Failure([new(KernelDiagnosticCode.ValidationFailed,KernelDiagnosticSeverity.Error,
            "firmament-imported-hole-plan-rejected: " + (candidate is null?"Candidate does not exist in this pinned source.":string.Join(" | ",candidate.Diagnostics)),"Brep.ThroughHoleRevision")]);
        var region = Recognize(source, new(candidate.WallFaces[0])); var center = candidate.Center!.Value;
        var r = candidate.Diameter / 2 + clearanceMillimetres;
        var bounds = new ThroughHoleEditBounds(WithCoordinate(center - new Vector3D(r, r, r), region.Axis, region.Low), WithCoordinate(center + new Vector3D(r, r, r), region.Axis, region.High));
        var request = new ThroughHoleRemovalRequest(featureId, candidate.WallFaces.Select(f => new FaceId(f)).ToArray(), bounds, importToleranceMillimetres);
        var result = Remove(source, request);
        return result.IsSuccess ? KernelResult<ThroughHoleRemovalPlan>.Success(new(request, result.Value), result.Diagnostics)
            : KernelResult<ThroughHoleRemovalPlan>.Failure(result.Diagnostics);
    }
    static KernelResult<ThroughHoleMovePlan> PlanFailure(string reason) => KernelResult<ThroughHoleMovePlan>.Failure([
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
            "firmament-imported-hole-plan-rejected: " + reason, "Brep.ThroughHoleRevision")]);
}
