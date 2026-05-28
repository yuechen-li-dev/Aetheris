using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum ProfileChamferCorner
{
    PositiveXPositiveY,
}

public sealed record ProfileChamferExtrudeCase(
    string Name,
    double Width,
    double Depth,
    double Height,
    double ChamferDistance,
    ProfileChamferCorner Corner = ProfileChamferCorner.PositiveXPositiveY);

public sealed record ProfileChamferExtrudeTopologySummary(
    bool BodyProduced,
    int VertexCount,
    int EdgeCount,
    int FaceCount,
    int PlanarFaceCount,
    int CylindricalFaceCount,
    int SideFaceCount,
    int ChamferFaceCount,
    int LoopCount,
    int CoedgeCount,
    string Bounds);

public sealed record ProfileChamferExtrudeStepSummary(
    bool Exported,
    IReadOnlyList<string> PresentMarkers,
    IReadOnlyList<string> MissingRequiredMarkers,
    IReadOnlyList<string> AbsentMarkers,
    IReadOnlyList<string> UnexpectedPresentMarkers);

public sealed record ProfileChamferExtrudeRow(
    string CaseName,
    LabProfileStatus Status,
    bool Succeeded,
    ProfileChamferExtrudeTopologySummary Topology,
    ProfileChamferExtrudeStepSummary Step,
    IReadOnlyList<string> Diagnostics,
    string Recommendation);

public static class ProfileChamferExtrudeLab
{
    private const double Tol = 1e-9;

    private static readonly string[] RequiredStepMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE", "PLANE"];
    private static readonly string[] ForbiddenStepMarkers = ["CYLINDRICAL_SURFACE", "BREP_WITH_VOIDS"];
    private static readonly HashSet<string> AllowedRecommendations =
    [
        "profile-chamfer-extrude-ready-for-production-evaluation",
        "profile-chamfer-extrude-needs-profile-validation-hardening",
        "profile-chamfer-extrude-needs-emitter-parity-work",
        "profile-chamfer-extrude-invalid-rejected",
        "profile-chamfer-extrude-deferred",
    ];

    public static IReadOnlyList<ProfileChamferExtrudeRow> RunAll() =>
    [
        Run(new("canonical-centered-box", 10, 8, 6, 1)),
        Run(new("larger-valid-chamfer", 10, 8, 6, 2)),
        Run(new("non-square-rectangle", 12, 5, 7, 1)),
        Run(new("invalid-zero-chamfer-distance", 10, 8, 6, 0)),
        Run(new("invalid-too-large-chamfer-distance", 10, 8, 6, 4)),
        Run(new("invalid-width", 0, 8, 6, 1)),
        Run(new("invalid-depth", 10, -8, 6, 1)),
        Run(new("invalid-height", 10, 8, 0, 1)),
        Run(new("invalid-non-finite-width", double.PositiveInfinity, 8, 6, 1)),
    ];

    public static ProfileChamferExtrudeRow Run(ProfileChamferExtrudeCase c)
    {
        var diagnostics = new List<string> { "edge-profile-x1-profile-chamfer-lab-started" };

        if (!FinitePositive(c.Width) || !FinitePositive(c.Depth) || !FinitePositive(c.Height))
        {
            diagnostics.Add("edge-profile-x1-invalid-dimensions-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-chamfer-extrude-invalid-rejected");
        }

        if (!double.IsFinite(c.ChamferDistance) || c.ChamferDistance <= Tol)
        {
            diagnostics.Add("edge-profile-x1-invalid-chamfer-distance-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-chamfer-extrude-invalid-rejected");
        }

        var safeLimit = Math.Min(c.Width, c.Depth) * 0.5d;
        if (c.ChamferDistance >= safeLimit - Tol)
        {
            diagnostics.Add("edge-profile-x1-chamfer-distance-too-large-rejected");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-chamfer-extrude-invalid-rejected");
        }

        var profilePoints = BuildChamferedRectangle(c.Width, c.Depth, c.ChamferDistance, c.Corner);
        diagnostics.Add("edge-profile-x1-chamfered-profile-created");

        var labProfile = ToLabProfile(profilePoints);
        var validation = ResolvedProfile2DLab.Evaluate(c.Name, labProfile);
        diagnostics.AddRange(validation.Diagnostics);
        if (validation.Diagnostics.Contains("profile-loop-self-intersection", StringComparer.Ordinal))
        {
            diagnostics.Add("edge-profile-x1-profile-self-intersection-rejected");
        }

        if (validation.Status != LabProfileStatus.Succeeded)
        {
            var rec = validation.Diagnostics.Contains("profile-loop-self-intersection", StringComparer.Ordinal)
                ? "profile-chamfer-extrude-needs-profile-validation-hardening"
                : "profile-chamfer-extrude-invalid-rejected";
            return Stop(c.Name, validation.Status, diagnostics, rec);
        }

        diagnostics.Add("edge-profile-x1-profile-validated");
        diagnostics.Add("edge-profile-x1-profile-extrude-attempted");

        var profileResult = PolylineProfile2D.Create(profilePoints.Select(p => new ProfilePoint2D(p.X, p.Y)).ToArray());
        if (!profileResult.IsSuccess || profileResult.Value is null)
        {
            diagnostics.Add("edge-profile-x1-profile-extrude-failed:polyline-profile-validation");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-chamfer-extrude-needs-profile-validation-hardening");
        }

        var frame = new ExtrudeFrame3D(
            new Point3D(0, 0, 0),
            Direction3D.Create(new Vector3D(0, 0, 1)),
            Direction3D.Create(new Vector3D(1, 0, 0)));
        var bodyResult = BrepExtrude.Create(profileResult.Value, frame, c.Height);
        if (!bodyResult.IsSuccess || bodyResult.Value is null)
        {
            diagnostics.Add("edge-profile-x1-profile-extrude-failed:brep-extrude-create");
            return Stop(c.Name, LabProfileStatus.Failed, diagnostics, "profile-chamfer-extrude-needs-emitter-parity-work");
        }

        var body = bodyResult.Value;
        diagnostics.Add("edge-profile-x1-profile-extrude-succeeded");
        diagnostics.Add("edge-profile-x1-no-air-edge-sweep-used");
        diagnostics.Add("edge-profile-x1-no-brep-bounded-chamfer-used");
        diagnostics.Add("edge-profile-x1-no-topology-graft-used");
        diagnostics.Add("edge-profile-x1-no-3d-boolean-used");

        var topology = SummarizeTopology(body, c);
        if (topology.ChamferFaceCount == 1)
        {
            diagnostics.Add("edge-profile-x1-chamfer-face-identified");
        }

        var step = SummarizeStep(body);
        var stepSucceeded = step.Exported && step.MissingRequiredMarkers.Count == 0 && step.UnexpectedPresentMarkers.Count == 0;
        diagnostics.Add(stepSucceeded ? "edge-profile-x1-step-smoke-succeeded" : "edge-profile-x1-step-smoke-failed:markers");

        var topologySucceeded = topology.BodyProduced
            && topology.VertexCount == 10
            && topology.EdgeCount == 15
            && topology.FaceCount == 7
            && topology.PlanarFaceCount == 7
            && topology.CylindricalFaceCount == 0
            && topology.SideFaceCount == 5
            && topology.ChamferFaceCount == 1;

        var recommendation = topologySucceeded && stepSucceeded
            ? "profile-chamfer-extrude-ready-for-production-evaluation"
            : "profile-chamfer-extrude-needs-emitter-parity-work";

        return new(
            c.Name,
            LabProfileStatus.Succeeded,
            topologySucceeded && stepSucceeded,
            topology,
            step,
            StableDiagnostics(diagnostics),
            recommendation);
    }

    private static IReadOnlyList<(double X, double Y)> BuildChamferedRectangle(double width, double depth, double chamferDistance, ProfileChamferCorner corner)
    {
        if (corner != ProfileChamferCorner.PositiveXPositiveY)
        {
            throw new ArgumentOutOfRangeException(nameof(corner), corner, "EDGE-PROFILE-X1 only supports the +X,+Y rectangle corner.");
        }

        var halfWidth = width * 0.5d;
        var halfDepth = depth * 0.5d;
        return
        [
            (-halfWidth, -halfDepth),
            (halfWidth, -halfDepth),
            (halfWidth, halfDepth - chamferDistance),
            (halfWidth - chamferDistance, halfDepth),
            (-halfWidth, halfDepth),
        ];
    }

    private static LabResolvedProfile2D ToLabProfile(IReadOnlyList<(double X, double Y)> points)
    {
        var curves = new List<LabAirCurve2D>(points.Count);
        for (var i = 0; i < points.Count; i++)
        {
            curves.Add(new LabAirLineSegment2D(points[i], points[(i + 1) % points.Count]));
        }

        return new LabResolvedProfile2D([new LabAirLoop2D(curves, "outer")]);
    }

    private static ProfileChamferExtrudeTopologySummary SummarizeTopology(BrepBody body, ProfileChamferExtrudeCase c)
    {
        var faceCount = body.Topology.Faces.Count();
        var planarFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Plane);
        var cylindricalFaceCount = body.Topology.Faces.Count(f => body.GetFaceSurface(f.Id).Kind == SurfaceGeometryKind.Cylinder);
        var sideFaceCount = Math.Max(0, faceCount - 2);
        var chamferFaceCount = body.Topology.Faces.Count(f => IsChamferSideFace(body, f.Id, c));
        var bounds = FormattableString.Invariant($"[{(-c.Width * 0.5d):0.###},{(-c.Depth * 0.5d):0.###},0]..[{(c.Width * 0.5d):0.###},{(c.Depth * 0.5d):0.###},{c.Height:0.###}]");

        return new(
            true,
            body.Topology.Vertices.Count(),
            body.Topology.Edges.Count(),
            faceCount,
            planarFaceCount,
            cylindricalFaceCount,
            sideFaceCount,
            chamferFaceCount,
            body.Topology.Loops.Count(),
            body.Topology.Coedges.Count(),
            bounds);
    }

    private static bool IsChamferSideFace(BrepBody body, Aetheris.Kernel.Core.Topology.FaceId faceId, ProfileChamferExtrudeCase c)
    {
        var edges = body.GetEdges(faceId);
        var hasBottomBevel = false;
        var hasTopBevel = false;
        foreach (var edge in edges)
        {
            var curve = body.GetEdgeCurve(edge);
            if (curve.Kind != Aetheris.Kernel.Core.Geometry.CurveGeometryKind.Line3 || curve.Line3 is null)
            {
                continue;
            }

            var line = curve.Line3.Value;
            var origin = line.Origin;
            var direction = line.Direction.ToVector();
            var expectedLength = c.ChamferDistance * Math.Sqrt(2d);
            var isBevelDirection = Math.Abs(Math.Abs(direction.X) - (1d / Math.Sqrt(2d))) <= 1e-6
                && Math.Abs(Math.Abs(direction.Y) - (1d / Math.Sqrt(2d))) <= 1e-6
                && Math.Abs(direction.Z) <= 1e-6;
            if (!isBevelDirection)
            {
                continue;
            }

            var binding = body.Bindings.GetEdgeBinding(edge);
            var lengthMatches = binding.TrimInterval is { } trim && Math.Abs((trim.End - trim.Start) - expectedLength) <= 1e-6;
            if (!lengthMatches)
            {
                continue;
            }

            if (Math.Abs(origin.Z) <= 1e-6)
            {
                hasBottomBevel = true;
            }
            else if (Math.Abs(origin.Z - c.Height) <= 1e-6)
            {
                hasTopBevel = true;
            }
        }

        return hasBottomBevel && hasTopBevel;
    }

    private static ProfileChamferExtrudeStepSummary SummarizeStep(BrepBody body)
    {
        var export = Step242Exporter.ExportBody(body);
        if (!export.IsSuccess || export.Value is null)
        {
            return new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []);
        }

        var text = export.Value;
        var present = RequiredStepMarkers.Where(m => text.Contains(m, StringComparison.Ordinal)).OrderBy(m => m, StringComparer.Ordinal).ToArray();
        var missing = RequiredStepMarkers.Except(present, StringComparer.Ordinal).OrderBy(m => m, StringComparer.Ordinal).ToArray();
        var absent = ForbiddenStepMarkers.Where(m => !text.Contains(m, StringComparison.Ordinal)).OrderBy(m => m, StringComparer.Ordinal).ToArray();
        var unexpected = ForbiddenStepMarkers.Where(m => text.Contains(m, StringComparison.Ordinal)).OrderBy(m => m, StringComparer.Ordinal).ToArray();
        return new(true, present, missing, absent, unexpected);
    }

    private static ProfileChamferExtrudeRow Stop(string name, LabProfileStatus status, List<string> diagnostics, string recommendation)
    {
        var rec = AllowedRecommendations.Contains(recommendation) ? recommendation : "profile-chamfer-extrude-deferred";
        return new(name, status, false, EmptyTopology(), new(false, [], RequiredStepMarkers, ForbiddenStepMarkers, []), StableDiagnostics(diagnostics), rec);
    }

    private static ProfileChamferExtrudeTopologySummary EmptyTopology() => new(false, 0, 0, 0, 0, 0, 0, 0, 0, 0, "empty");

    private static string[] StableDiagnostics(IEnumerable<string> diagnostics) => diagnostics.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

    private static bool FinitePositive(double value) => double.IsFinite(value) && value > Tol;
}
