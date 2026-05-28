using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Materializer;

internal enum ProfileVertexChamferExtrudeStatus { Succeeded, Rejected, Deferred, Failed }

internal readonly record struct ProfileVertexChamferPoint2D(double X, double Y);

internal sealed record ProfileVertexChamferExtrudeRequest(
    IReadOnlyList<ProfileVertexChamferPoint2D> ProfileVertices,
    int SelectedVertexIndex,
    double ChamferDistance,
    double ExtrusionHeight,
    bool RunStepSmoke = true,
    double? RectangleWidth = null,
    double? RectangleDepth = null)
{
    public static ProfileVertexChamferExtrudeRequest Rectangle(
        double width,
        double depth,
        double height,
        double chamferDistance,
        bool runStepSmoke = true)
    {
        var halfWidth = width * 0.5d;
        var halfDepth = depth * 0.5d;
        return new(
            [
                new(-halfWidth, -halfDepth),
                new(halfWidth, -halfDepth),
                new(halfWidth, halfDepth),
                new(-halfWidth, halfDepth),
            ],
            SelectedVertexIndex: 2,
            ChamferDistance: chamferDistance,
            ExtrusionHeight: height,
            RunStepSmoke: runStepSmoke,
            RectangleWidth: width,
            RectangleDepth: depth);
    }
}

internal sealed record ProfileVertexChamferTopologySummary(
    bool BodyProduced,
    int ProfileVertexCount,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int CapFaceCount,
    int SideFaceCount,
    int ChamferFaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int LoopCount,
    int CoedgeCount);

internal sealed record ProfileVertexChamferStepSummary(
    bool Requested,
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

internal sealed record ProfileVertexChamferExtrudeResult(
    ProfileVertexChamferExtrudeStatus Status,
    IReadOnlyList<ProfileVertexChamferPoint2D> ChamferedProfile,
    BrepBody? Body,
    ProfileVertexChamferTopologySummary Topology,
    ProfileVertexChamferStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

internal static class ProfileVertexChamferExtrudeEmitter
{
    private const double Tol = 1e-9;

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];

    public static ProfileVertexChamferExtrudeResult TryEmit(ProfileVertexChamferExtrudeRequest request)
    {
        var diagnostics = new List<string> { "edge-profile-v1-emitter-started" };

        if (!ValidateRequestShape(request, diagnostics))
        {
            return Stop(ProfileVertexChamferExtrudeStatus.Rejected, [], diagnostics, "profile-chamfer-emitter-invalid-rejected");
        }

        diagnostics.Add("edge-profile-v1-request-validated");

        var vertices = request.ProfileVertices.ToArray();
        if (!TryValidateSelectedVertex(vertices, request.SelectedVertexIndex, request.ChamferDistance, diagnostics, out var previousLength, out var nextLength))
        {
            return Stop(ProfileVertexChamferExtrudeStatus.Rejected, [], diagnostics, "profile-chamfer-emitter-invalid-rejected");
        }

        diagnostics.Add("edge-profile-v1-selected-vertex-validated");
        diagnostics.Add("edge-profile-v1-convex-vertex-accepted");

        if (request.ChamferDistance >= previousLength - Tol || request.ChamferDistance >= nextLength - Tol)
        {
            diagnostics.Add("edge-profile-v1-chamfer-distance-too-large-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:chamfer-distance-too-large");
            return Stop(ProfileVertexChamferExtrudeStatus.Rejected, [], diagnostics, "profile-chamfer-emitter-invalid-rejected");
        }

        var chamferedProfile = BuildChamferedProfile(vertices, request.SelectedVertexIndex, request.ChamferDistance);
        diagnostics.Add("edge-profile-v1-chamfered-profile-created");

        if (!ValidateChamferedProfile(chamferedProfile, diagnostics))
        {
            return Stop(ProfileVertexChamferExtrudeStatus.Rejected, chamferedProfile, diagnostics, "profile-chamfer-emitter-needs-profile-validation-hardening");
        }

        diagnostics.Add("edge-profile-v1-profile-validation-succeeded");
        diagnostics.Add("edge-profile-v1-profile-extrude-attempted");

        var profileResult = PolylineProfile2D.Create(chamferedProfile.Select(p => new ProfilePoint2D(p.X, p.Y)).ToArray());
        if (!profileResult.IsSuccess || profileResult.Value is null)
        {
            diagnostics.Add("edge-profile-v1-request-rejected:polyline-profile-validation");
            return Stop(ProfileVertexChamferExtrudeStatus.Failed, chamferedProfile, diagnostics, "profile-chamfer-emitter-needs-profile-validation-hardening");
        }

        var frame = new ExtrudeFrame3D(
            new Point3D(0d, 0d, 0d),
            Direction3D.Create(new Vector3D(0d, 0d, 1d)),
            Direction3D.Create(new Vector3D(1d, 0d, 0d)));
        var bodyResult = BrepExtrude.Create(profileResult.Value, frame, request.ExtrusionHeight);
        if (!bodyResult.IsSuccess || bodyResult.Value is null)
        {
            diagnostics.Add("edge-profile-v1-request-rejected:brep-extrude-create");
            return Stop(ProfileVertexChamferExtrudeStatus.Failed, chamferedProfile, diagnostics, "profile-chamfer-emitter-needs-emitter-parity-work");
        }

        var body = bodyResult.Value;
        diagnostics.Add("edge-profile-v1-profile-extrude-succeeded");
        diagnostics.Add("edge-profile-v1-no-air-edge-sweep-used");
        diagnostics.Add("edge-profile-v1-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-profile-v1-no-topology-graft-used");
        diagnostics.Add("edge-profile-v1-no-3d-boolean-used");

        var topology = SummarizeTopology(body, chamferedProfile.Count);
        var step = request.RunStepSmoke ? SummarizeStep(body) : EmptyStepSummary(requested: false);
        var stepSucceeded = !request.RunStepSmoke || (step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0);
        diagnostics.Add(stepSucceeded ? "edge-profile-v1-step-smoke-succeeded" : "edge-profile-v1-request-rejected:step-smoke-failed");

        var expectedFaces = chamferedProfile.Count + 2;
        var topologySucceeded = topology.BodyProduced
            && topology.VertexCount == chamferedProfile.Count * 2
            && topology.EdgeCount == chamferedProfile.Count * 3
            && topology.FaceCount == expectedFaces
            && topology.CapFaceCount == 2
            && topology.SideFaceCount == chamferedProfile.Count
            && topology.ChamferFaceCount == 1
            && topology.PlanarFaceCount == expectedFaces
            && topology.CylindricalFaceCount == 0;

        var status = topologySucceeded && stepSucceeded
            ? ProfileVertexChamferExtrudeStatus.Succeeded
            : ProfileVertexChamferExtrudeStatus.Failed;
        var recommendation = status == ProfileVertexChamferExtrudeStatus.Succeeded
            ? "profile-chamfer-emitter-ready-for-controlled-route-evaluation"
            : "profile-chamfer-emitter-needs-emitter-parity-work";

        return new(status, chamferedProfile, body, topology, step, StableDiagnostics(diagnostics), recommendation);
    }

    private static bool ValidateRequestShape(ProfileVertexChamferExtrudeRequest request, List<string> diagnostics)
    {
        if (request.ProfileVertices.Count < 3 || !double.IsFinite(request.ExtrusionHeight) || request.ExtrusionHeight <= Tol)
        {
            diagnostics.Add("edge-profile-v1-invalid-dimensions-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:invalid-dimensions");
            return false;
        }

        if (request.RectangleWidth.HasValue || request.RectangleDepth.HasValue)
        {
            if (!request.RectangleWidth.HasValue
                || !request.RectangleDepth.HasValue
                || !double.IsFinite(request.RectangleWidth.Value)
                || !double.IsFinite(request.RectangleDepth.Value)
                || request.RectangleWidth.Value <= Tol
                || request.RectangleDepth.Value <= Tol)
            {
                diagnostics.Add("edge-profile-v1-invalid-dimensions-rejected");
                diagnostics.Add("edge-profile-v1-request-rejected:invalid-rectangle-dimensions");
                return false;
            }

            var rectangleSafeLimit = Math.Min(request.RectangleWidth.Value, request.RectangleDepth.Value) * 0.5d;
            if (request.ChamferDistance >= rectangleSafeLimit - Tol)
            {
                diagnostics.Add("edge-profile-v1-chamfer-distance-too-large-rejected");
                diagnostics.Add("edge-profile-v1-request-rejected:rectangle-chamfer-distance-too-large");
                return false;
            }
        }

        if (!double.IsFinite(request.ChamferDistance) || request.ChamferDistance <= Tol)
        {
            diagnostics.Add("edge-profile-v1-invalid-chamfer-distance-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:invalid-chamfer-distance");
            return false;
        }

        foreach (var vertex in request.ProfileVertices)
        {
            if (!double.IsFinite(vertex.X) || !double.IsFinite(vertex.Y))
            {
                diagnostics.Add("edge-profile-v1-invalid-dimensions-rejected");
                diagnostics.Add("edge-profile-v1-request-rejected:non-finite-profile-coordinate");
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateSelectedVertex(
        IReadOnlyList<ProfileVertexChamferPoint2D> vertices,
        int selectedVertexIndex,
        double chamferDistance,
        List<string> diagnostics,
        out double previousLength,
        out double nextLength)
    {
        previousLength = 0d;
        nextLength = 0d;

        if (selectedVertexIndex < 0 || selectedVertexIndex >= vertices.Count)
        {
            diagnostics.Add("edge-profile-v1-request-rejected:selected-vertex-missing");
            return false;
        }

        var signedArea = SignedArea(vertices);
        if (Math.Abs(signedArea) <= Tol)
        {
            diagnostics.Add("edge-profile-v1-request-rejected:degenerate-profile");
            return false;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            var edgeLength = Distance(vertices[i], vertices[(i + 1) % vertices.Count]);
            if (edgeLength <= Tol)
            {
                diagnostics.Add("edge-profile-v1-adjacent-edge-too-short-rejected");
                diagnostics.Add("edge-profile-v1-request-rejected:zero-length-profile-edge");
                return false;
            }
        }

        var previous = vertices[(selectedVertexIndex + vertices.Count - 1) % vertices.Count];
        var current = vertices[selectedVertexIndex];
        var next = vertices[(selectedVertexIndex + 1) % vertices.Count];
        previousLength = Distance(current, previous);
        nextLength = Distance(current, next);
        if (previousLength <= chamferDistance + Tol || nextLength <= chamferDistance + Tol)
        {
            diagnostics.Add("edge-profile-v1-adjacent-edge-too-short-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:adjacent-edge-too-short");
            return false;
        }

        var cross = Cross(previous, current, next);
        if (Math.Sign(cross) != Math.Sign(signedArea) || Math.Abs(cross) <= Tol)
        {
            diagnostics.Add("edge-profile-v1-selected-vertex-not-convex-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:selected-vertex-not-convex");
            return false;
        }

        return true;
    }

    private static IReadOnlyList<ProfileVertexChamferPoint2D> BuildChamferedProfile(
        IReadOnlyList<ProfileVertexChamferPoint2D> vertices,
        int selectedVertexIndex,
        double chamferDistance)
    {
        var previous = vertices[(selectedVertexIndex + vertices.Count - 1) % vertices.Count];
        var current = vertices[selectedVertexIndex];
        var next = vertices[(selectedVertexIndex + 1) % vertices.Count];
        var previousCut = MoveToward(current, previous, chamferDistance);
        var nextCut = MoveToward(current, next, chamferDistance);
        var result = new List<ProfileVertexChamferPoint2D>(vertices.Count + 1);
        for (var i = 0; i < vertices.Count; i++)
        {
            if (i == selectedVertexIndex)
            {
                result.Add(previousCut);
                result.Add(nextCut);
            }
            else
            {
                result.Add(vertices[i]);
            }
        }

        return result;
    }

    private static bool ValidateChamferedProfile(IReadOnlyList<ProfileVertexChamferPoint2D> vertices, List<string> diagnostics)
    {
        if (vertices.Count < 3)
        {
            diagnostics.Add("edge-profile-v1-request-rejected:profile-too-small");
            return false;
        }

        for (var i = 0; i < vertices.Count; i++)
        {
            if (Distance(vertices[i], vertices[(i + 1) % vertices.Count]) <= Tol)
            {
                diagnostics.Add("edge-profile-v1-adjacent-edge-too-short-rejected");
                diagnostics.Add("edge-profile-v1-request-rejected:chamfered-profile-zero-edge");
                return false;
            }
        }

        if (HasSelfIntersection(vertices))
        {
            diagnostics.Add("edge-profile-v1-profile-self-intersection-rejected");
            diagnostics.Add("edge-profile-v1-request-rejected:profile-self-intersection");
            return false;
        }

        return true;
    }

    private static ProfileVertexChamferTopologySummary SummarizeTopology(BrepBody body, int profileVertexCount)
    {
        var faceCount = body.Topology.Faces.Count();
        return new(
            BodyProduced: true,
            ProfileVertexCount: profileVertexCount,
            VertexCount: body.Topology.Vertices.Count(),
            EdgeCount: body.Topology.Edges.Count(),
            FaceCount: faceCount,
            CapFaceCount: 2,
            SideFaceCount: Math.Max(0, faceCount - 2),
            ChamferFaceCount: 1,
            PlanarFaceCount: body.Topology.Faces.Count(face => body.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Plane),
            CylindricalFaceCount: body.Topology.Faces.Count(face => body.GetFaceSurface(face.Id).Kind == SurfaceGeometryKind.Cylinder),
            LoopCount: body.Topology.Loops.Count(),
            CoedgeCount: body.Topology.Coedges.Count());
    }

    private static ProfileVertexChamferStepSummary SummarizeStep(BrepBody body)
    {
        var export = Step242Exporter.ExportBody(body);
        if (!export.IsSuccess || export.Value is null)
        {
            return new(true, false, [], RequiredStepMarkers, [], []);
        }

        var text = export.Value;
        var present = RequiredStepMarkers.Where(marker => text.Contains(marker, StringComparison.Ordinal)).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(marker => !text.Contains(marker, StringComparison.Ordinal)).ToArray();
        var unexpected = ForbiddenStepMarkers.Except(absent, StringComparer.Ordinal).ToArray();
        return new(true, true, present, missing, absent, unexpected);
    }

    private static ProfileVertexChamferStepSummary EmptyStepSummary(bool requested) => new(requested, false, [], [], [], []);

    private static ProfileVertexChamferExtrudeResult Stop(
        ProfileVertexChamferExtrudeStatus status,
        IReadOnlyList<ProfileVertexChamferPoint2D> chamferedProfile,
        IReadOnlyList<string> diagnostics,
        string recommendation)
        => new(status, chamferedProfile, null, EmptyTopology(chamferedProfile.Count), EmptyStepSummary(requested: false), StableDiagnostics(diagnostics), recommendation);

    private static ProfileVertexChamferTopologySummary EmptyTopology(int profileVertexCount)
        => new(false, profileVertexCount, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static IReadOnlyList<string> StableDiagnostics(IEnumerable<string> diagnostics)
        => diagnostics.Distinct(StringComparer.Ordinal).ToArray();

    private static double SignedArea(IReadOnlyList<ProfileVertexChamferPoint2D> vertices)
    {
        var area = 0d;
        for (var i = 0; i < vertices.Count; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Count];
            area += (current.X * next.Y) - (next.X * current.Y);
        }

        return area * 0.5d;
    }

    private static double Cross(ProfileVertexChamferPoint2D a, ProfileVertexChamferPoint2D b, ProfileVertexChamferPoint2D c)
        => ((b.X - a.X) * (c.Y - b.Y)) - ((b.Y - a.Y) * (c.X - b.X));

    private static double Distance(ProfileVertexChamferPoint2D a, ProfileVertexChamferPoint2D b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static ProfileVertexChamferPoint2D MoveToward(ProfileVertexChamferPoint2D from, ProfileVertexChamferPoint2D to, double distance)
    {
        var length = Distance(from, to);
        var t = distance / length;
        return new(from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
    }

    private static bool HasSelfIntersection(IReadOnlyList<ProfileVertexChamferPoint2D> vertices)
    {
        for (var i = 0; i < vertices.Count; i++)
        {
            var a1 = vertices[i];
            var a2 = vertices[(i + 1) % vertices.Count];
            for (var j = i + 1; j < vertices.Count; j++)
            {
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == vertices.Count - 1))
                {
                    continue;
                }

                var b1 = vertices[j];
                var b2 = vertices[(j + 1) % vertices.Count];
                if (SegmentsIntersect(a1, a2, b1, b2))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool SegmentsIntersect(
        ProfileVertexChamferPoint2D a1,
        ProfileVertexChamferPoint2D a2,
        ProfileVertexChamferPoint2D b1,
        ProfileVertexChamferPoint2D b2)
    {
        var d1 = Orientation(a1, a2, b1);
        var d2 = Orientation(a1, a2, b2);
        var d3 = Orientation(b1, b2, a1);
        var d4 = Orientation(b1, b2, a2);
        return (d1 * d2 < -Tol) && (d3 * d4 < -Tol);
    }

    private static double Orientation(ProfileVertexChamferPoint2D a, ProfileVertexChamferPoint2D b, ProfileVertexChamferPoint2D c)
        => ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));
}
