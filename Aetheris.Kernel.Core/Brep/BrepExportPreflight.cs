using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep;

public enum BrepExportPreflightSeverity { Warning, Error }

/// <summary>Why an export-facing preflight finding exists.  Coverage gaps are deliberately non-fatal.</summary>
public enum BrepExportPreflightFindingClassification
{
    InvalidGeometry,
    InvalidTopology,
    UnsupportedCheck,
    ValidLegacyRepresentation,
    SuspiciousNeedsReview,
}

/// <summary>Structured, export-facing evidence for a B-rep contradiction.</summary>
public sealed record BrepExportPreflightDiagnostic(
    string Code,
    BrepExportPreflightSeverity Severity,
    string Stage,
    string Message,
    int? BodyId = null,
    int? FaceId = null,
    int? LoopId = null,
    int? CoedgeIndex = null,
    int? EdgeId = null,
    string? SurfaceFamily = null,
    double? MeasuredDeviation = null,
    double? AllowedTolerance = null,
    BrepExportPreflightFindingClassification Classification = BrepExportPreflightFindingClassification.SuspiciousNeedsReview)
{
    public string Context => $"BrepExportPreflight{(BodyId is null ? string.Empty : $".Body:{BodyId}")}{(FaceId is null ? string.Empty : $".Face:{FaceId}")}{(LoopId is null ? string.Empty : $".Loop:{LoopId}")}{(EdgeId is null ? string.Empty : $".Edge:{EdgeId}")}";
}

public sealed record BrepExportPreflightResult(
    bool IsValid,
    IReadOnlyList<BrepExportPreflightDiagnostic> Diagnostics,
    int CheckedBodies,
    int CheckedFaces,
    int CheckedLoops,
    int CheckedEdges)
{
    public int ErrorCount => Diagnostics.Count(d => d.Severity == BrepExportPreflightSeverity.Error);
    public int WarningCount => Diagnostics.Count(d => d.Severity == BrepExportPreflightSeverity.Warning);
}

/// <summary>
/// Final common safety gate for analytic B-reps immediately before STEP serialization.
/// It deliberately validates only topology and analytic geometry that the current model
/// can prove; unsupported surface containment is reported as a non-blocking warning.
/// </summary>
public static class BrepExportPreflight
{
    private static readonly ToleranceContext Tolerances = ToleranceContext.Default;

    public static BrepExportPreflightResult Validate(BrepBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var diagnostics = new List<BrepExportPreflightDiagnostic>();
        var model = body.Topology;
        int? bodyId = model.Bodies.Count() == 1 ? model.Bodies.Single().Id.Value : null;
        var checkedEdges = new HashSet<EdgeId>();
        var checkedFaces = 0;
        var checkedLoops = 0;

        foreach (var face in model.Faces.OrderBy(face => face.Id.Value))
        {
            checkedFaces++;
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null)
            {
                AddError("brep-preflight-missing-support-surface", "face-support", "Face is missing its support surface.", faceId: face.Id.Value);
                continue;
            }

            foreach (var loopId in face.LoopIds)
            {
                if (!model.TryGetLoop(loopId, out var loop) || loop is null)
                {
                    AddError("brep-preflight-missing-loop", "loop", "Face references a missing loop.", faceId: face.Id.Value, loopId: loopId.Value);
                    continue;
                }

                checkedLoops++;
                ValidateLoop(face.Id, loop, surface);
            }
        }

        foreach (var edge in model.Edges.OrderBy(edge => edge.Id.Value))
        {
            if (!checkedEdges.Add(edge.Id)) continue;
            ValidateEdge(edge);
        }

        // A shared edge is independently checked against every support surface which uses it.
        // This catches e.g. a circle lying on its cylinder but not its conical neighbour.
        foreach (var face in model.Faces)
        {
            if (!body.TryGetFaceSurfaceGeometry(face.Id, out var surface) || surface is null) continue;
            foreach (var loopId in face.LoopIds)
            {
                if (!model.TryGetLoop(loopId, out var loop) || loop is null) continue;
                foreach (var coedgeId in loop.CoedgeIds)
                {
                    if (!model.TryGetCoedge(coedgeId, out var coedge) || coedge is null) continue;
                    if (!model.TryGetEdge(coedge.EdgeId, out var edge) || edge is null) continue;
                    ValidateTrimOnSurface(face.Id, loop.Id, coedge.EdgeId, edge, surface);
                }
            }
        }

        return new BrepExportPreflightResult(!diagnostics.Any(d => d.Severity == BrepExportPreflightSeverity.Error), diagnostics, model.Bodies.Count(), checkedFaces, checkedLoops, checkedEdges.Count);

        void ValidateLoop(FaceId faceId, Loop loop, SurfaceGeometry surface)
        {
            if (loop.CoedgeIds.Count == 0)
            {
                AddError("brep-preflight-loop-not-closed", "loop", "Loop has no coedges.", faceId.Value, loop.Id.Value);
                return;
            }

            var seen = new HashSet<CoedgeId>();
            for (var i = 0; i < loop.CoedgeIds.Count; i++)
            {
                var coedgeId = loop.CoedgeIds[i];
                if (!seen.Add(coedgeId))
                    AddError("brep-preflight-duplicate-coedge", "loop", "Loop reuses a coedge.", faceId.Value, loop.Id.Value, i);
                if (!model.TryGetCoedge(coedgeId, out var coedge) || coedge is null)
                {
                    AddError("brep-preflight-missing-coedge", "loop", "Loop references a missing coedge.", faceId.Value, loop.Id.Value, i);
                    continue;
                }
                if (coedge.LoopId != loop.Id)
                    AddError("brep-preflight-coedge-loop-mismatch", "loop", "Coedge belongs to another loop.", faceId.Value, loop.Id.Value, i, coedge.EdgeId.Value);
                if (!model.TryGetEdge(coedge.EdgeId, out var edge) || edge is null)
                {
                    AddError("brep-preflight-missing-edge", "loop", "Coedge references a missing edge.", faceId.Value, loop.Id.Value, i, coedge.EdgeId.Value);
                    continue;
                }

                var nextId = loop.CoedgeIds[(i + 1) % loop.CoedgeIds.Count];
                if (!model.TryGetCoedge(nextId, out var next) || next is null || !model.TryGetEdge(next.EdgeId, out var nextEdge) || nextEdge is null) continue;
                var end = coedge.IsReversed ? edge.StartVertexId : edge.EndVertexId;
                var expectedStart = next.IsReversed ? nextEdge.EndVertexId : nextEdge.StartVertexId;
                if (end != expectedStart)
                    AddError(i == loop.CoedgeIds.Count - 1 ? "brep-preflight-loop-not-closed" : "brep-preflight-coedge-disconnected", "loop", $"Coedge chain is disconnected: expected vertex {end.Value}, actual vertex {expectedStart.Value}.", faceId.Value, loop.Id.Value, i, coedge.EdgeId.Value);

                if (coedge.NextCoedgeId != nextId)
                    AddError("brep-preflight-coedge-link-mismatch", "loop", "Coedge NextCoedgeId does not match declared loop order.", faceId.Value, loop.Id.Value, i, coedge.EdgeId.Value);
            }
        }

        void ValidateEdge(Edge edge)
        {
            if (!model.TryGetVertex(edge.StartVertexId, out _) || !model.TryGetVertex(edge.EndVertexId, out _))
            {
                AddError("brep-preflight-missing-vertex", "edge", "Edge references a missing endpoint vertex.", edgeId: edge.Id.Value);
                return;
            }
            if (!body.Bindings.TryGetEdgeBinding(edge.Id, out var binding) || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) || curve is null || binding.TrimInterval is null)
            {
                AddError("brep-preflight-missing-curve", "edge", "Edge is missing a bound curve or trim interval.", edgeId: edge.Id.Value);
                return;
            }
            if (edge.StartVertexId == edge.EndVertexId && curve.Kind is not CurveGeometryKind.Circle3 and not CurveGeometryKind.Ellipse3)
                AddError("brep-preflight-edge-degenerate", "edge", "Non-periodic edge uses the same topology vertex at both endpoints.", edgeId: edge.Id.Value);
            var a = Evaluate(curve, binding.TrimInterval.Value.Start);
            var b = Evaluate(curve, binding.TrimInterval.Value.End);
            if (a is null || b is null || !Finite(a.Value) || !Finite(b.Value))
                AddError("brep-preflight-edge-degenerate", "edge", "Edge curve endpoint is non-finite or unsupported.", edgeId: edge.Id.Value);
            if (a is not null && b is not null
                && body.TryGetVertexPoint(edge.StartVertexId, out var startPoint)
                && body.TryGetVertexPoint(edge.EndVertexId, out var endPoint))
            {
                var expectedStart = binding.OrientedEdgeSense ? startPoint : endPoint;
                var expectedEnd = binding.OrientedEdgeSense ? endPoint : startPoint;
                var startDeviation = Distance(a.Value, expectedStart);
                var endDeviation = Distance(b.Value, expectedEnd);
                if (startDeviation > Tolerances.Linear || endDeviation > Tolerances.Linear)
                    AddError("brep-preflight-edge-curve-endpoint-mismatch", "edge", "Edge curve trim endpoints do not match the topology edge endpoints.", edgeId: edge.Id.Value, deviation: double.Max(startDeviation, endDeviation));
            }
            if (a is not null && b is not null && curve.Kind is CurveGeometryKind.Line3 && Distance(a.Value, b.Value) <= Tolerances.Linear)
                AddError("brep-preflight-edge-degenerate", "edge", "Line edge has zero geometric length.", edgeId: edge.Id.Value, deviation: Distance(a.Value, b.Value));
        }

        void ValidateTrimOnSurface(FaceId faceId, LoopId loopId, EdgeId edgeId, Edge edge, SurfaceGeometry surface)
        {
            if (!body.Bindings.TryGetEdgeBinding(edgeId, out var binding) || !body.Geometry.TryGetCurve(binding.CurveGeometryId, out var curve) || curve is null || binding.TrimInterval is null) return;
            var parameters = new[] { binding.TrimInterval.Value.Start, binding.TrimInterval.Value.End, (binding.TrimInterval.Value.Start + binding.TrimInterval.Value.End) / 2d };
            foreach (var parameter in parameters.Distinct())
            {
                var point = Evaluate(curve, parameter);
                if (point is null) continue;
                var deviation = SurfaceDeviation(surface, point.Value, out var supported);
                if (!supported)
                {
                    AddWarning("brep-preflight-check-unsupported", "trim-surface", $"Containment check is not implemented for {surface.Kind}.", faceId.Value, loopId.Value, edgeId: edgeId.Value, surface: surface.Kind.ToString());
                    return;
                }
                if (deviation > Tolerances.Linear)
                    AddError("brep-preflight-trim-off-surface", "trim-surface", $"Trim point does not lie on {surface.Kind}; deviation {deviation:G6} exceeds tolerance {Tolerances.Linear:G6}.", faceId.Value, loopId.Value, edgeId: edgeId.Value, surface: surface.Kind.ToString(), deviation: deviation);
            }
        }

        void AddError(string code, string stage, string message, int? faceId = null, int? loopId = null, int? coedgeIndex = null, int? edgeId = null, string? surface = null, double? deviation = null) =>
            diagnostics.Add(new(code, BrepExportPreflightSeverity.Error, stage, message, bodyId, faceId, loopId, coedgeIndex, edgeId, surface, deviation, deviation is null ? null : Tolerances.Linear, ClassificationFor(code, stage)));
        void AddWarning(string code, string stage, string message, int? faceId = null, int? loopId = null, int? coedgeIndex = null, int? edgeId = null, string? surface = null, double? deviation = null) =>
            diagnostics.Add(new(code, BrepExportPreflightSeverity.Warning, stage, message, bodyId, faceId, loopId, coedgeIndex, edgeId, surface, deviation, deviation is null ? null : Tolerances.Linear, ClassificationFor(code, stage)));
    }

    private static BrepExportPreflightFindingClassification ClassificationFor(string code, string stage) => code switch
    {
        "brep-preflight-check-unsupported" => BrepExportPreflightFindingClassification.UnsupportedCheck,
        "brep-preflight-trim-off-surface" or "brep-preflight-missing-support-surface" => BrepExportPreflightFindingClassification.InvalidGeometry,
        _ when stage is "loop" or "edge" => BrepExportPreflightFindingClassification.InvalidTopology,
        _ => BrepExportPreflightFindingClassification.SuspiciousNeedsReview,
    };

    private static Point3D? Evaluate(CurveGeometry curve, double t) => curve.Kind switch
    {
        CurveGeometryKind.Line3 when curve.Line3 is Line3Curve line => line.Evaluate(t),
        CurveGeometryKind.Circle3 when curve.Circle3 is Circle3Curve circle => circle.Evaluate(t),
        CurveGeometryKind.Ellipse3 when curve.Ellipse3 is Ellipse3Curve ellipse => ellipse.Evaluate(t),
        CurveGeometryKind.BSpline3 when curve.BSpline3 is BSpline3Curve spline => spline.Evaluate(t),
        _ => null,
    };

    private static double SurfaceDeviation(SurfaceGeometry surface, Point3D point, out bool supported)
    {
        supported = true;
        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane when surface.Plane is PlaneSurface plane => double.Abs((point - plane.Origin).Dot(plane.Normal.ToVector())),
            SurfaceGeometryKind.Cylinder when surface.Cylinder is CylinderSurface cylinder => double.Abs(RadialDistance(point, cylinder.Origin, cylinder.Axis) - cylinder.Radius),
            SurfaceGeometryKind.Cone when surface.Cone is ConeSurface cone => double.Abs(RadialDistance(point, cone.Apex, cone.Axis) - double.Abs((point - cone.Apex).Dot(cone.Axis.ToVector())) * double.Tan(cone.SemiAngleRadians)),
            SurfaceGeometryKind.Sphere when surface.Sphere is SphereSurface sphere => double.Abs(Distance(point, sphere.Center) - sphere.Radius),
            SurfaceGeometryKind.Torus when surface.Torus is TorusSurface torus => double.Abs(double.Sqrt(double.Pow(RadialDistance(point, torus.Center, torus.Axis) - torus.MajorRadius, 2d) + double.Pow((point - torus.Center).Dot(torus.Axis.ToVector()), 2d)) - torus.MinorRadius),
            _ => Unsupported(out supported),
        };
    }

    private static double Unsupported(out bool supported) { supported = false; return 0d; }
    private static double RadialDistance(Point3D point, Point3D origin, Direction3D axis) { var v = point - origin; var axial = v.Dot(axis.ToVector()); return (v - axis.ToVector() * axial).Length; }
    private static double Distance(Point3D a, Point3D b) => (a - b).Length;
    private static bool Finite(Point3D point) => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);
}
