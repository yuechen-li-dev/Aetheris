using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Firmament.FrictionLab.CIRLab;

public enum AirPrimitiveBaselineKind { Cylinder, ConeFrustum, Sphere, Torus }
public enum AirPrimitiveCandidateKind { AirExtrude, AirRevolve }

public sealed record AirPrimitiveMatrixCase(string Name, AirPrimitiveBaselineKind Primitive, double A, double B, double C = 0d);
public sealed record AirPrimitiveTopologySummary(bool BodyProduced, int VertexCount, int EdgeCount, int FaceCount, int PlaneFaceCount, int CylindricalFaceCount, int ConicalFaceCount, int SphericalFaceCount, int ToroidalFaceCount, int LoopCount, int CoedgeCount);
public sealed record AirPrimitiveStepSmokeSummary(bool Exported, IReadOnlyList<string> PresentMarkers, IReadOnlyList<string> MissingMarkers, bool ContainsBrepWithVoids, IReadOnlyList<string> Diagnostics);
public sealed record AirPrimitiveMatrixRow(AirPrimitiveMatrixCase Case, string RowKind, AirPrimitiveCandidateKind? CandidateKind, AirPrimitiveTopologySummary Topology, AirPrimitiveStepSmokeSummary StepSmoke, IReadOnlyList<string> Diagnostics, bool IsValidInput, bool TopologyParityWithBaseline, bool StepParityWithBaseline, string Recommendation);

public static class AirPrimitiveMatrixLab
{
    public static readonly string[] AllowedRecommendations = ["ready-for-production-migration", "needs-emitter-parity-work", "needs-air-revolve-lab-support", "keep-direct-constructor-for-now"];

    public static IReadOnlyList<AirPrimitiveMatrixRow> RunMatrix()
    {
        var rows = new List<AirPrimitiveMatrixRow>();
        rows.AddRange(RunCase(new("cyl-5x10", AirPrimitiveBaselineKind.Cylinder, 5, 10)));
        rows.AddRange(RunCase(new("cyl-3x12", AirPrimitiveBaselineKind.Cylinder, 3, 12)));
        rows.AddRange(RunCase(new("cyl-invalid", AirPrimitiveBaselineKind.Cylinder, -1, 10)));

        rows.AddRange(RunCase(new("frustum-5-2-10", AirPrimitiveBaselineKind.ConeFrustum, 5, 2, 10)));
        rows.AddRange(RunCase(new("cone-apex-5-0-10", AirPrimitiveBaselineKind.ConeFrustum, 5, 0, 10)));
        rows.AddRange(RunCase(new("cone-invalid", AirPrimitiveBaselineKind.ConeFrustum, 5, -1, 10)));

        rows.AddRange(RunCase(new("sphere-5", AirPrimitiveBaselineKind.Sphere, 5, 0)));
        rows.AddRange(RunCase(new("sphere-2.5", AirPrimitiveBaselineKind.Sphere, 2.5, 0)));
        rows.AddRange(RunCase(new("sphere-invalid", AirPrimitiveBaselineKind.Sphere, -1, 0)));

        rows.AddRange(RunCase(new("torus-8-2", AirPrimitiveBaselineKind.Torus, 8, 2)));
        rows.AddRange(RunCase(new("torus-5-1", AirPrimitiveBaselineKind.Torus, 5, 1)));
        rows.AddRange(RunCase(new("torus-invalid-major", AirPrimitiveBaselineKind.Torus, 0, 1)));
        rows.AddRange(RunCase(new("torus-invalid-minor", AirPrimitiveBaselineKind.Torus, 5, 0)));
        rows.AddRange(RunCase(new("torus-invalid-intersect", AirPrimitiveBaselineKind.Torus, 3, 3)));
        return rows;
    }

    public static IReadOnlyList<AirPrimitiveMatrixRow> RunCase(AirPrimitiveMatrixCase @case)
    {
        var baselineDiag = new List<string> { "air-x5-primitive-matrix-lab-started" };
        var baselineResult = CreateBaseline(@case);
        if (!baselineResult.IsSuccess || baselineResult.Value is null)
        {
            baselineDiag.AddRange(baselineResult.Diagnostics.Select(d => d.Message));
            baselineDiag.Add("air-x5-baseline-created:false");
            var baselineRow = BuildRow(@case, "baseline", null, null, baselineDiag, false, "keep-direct-constructor-for-now", null);
            var candidateRows = BuildCandidates(@case, null, false);
            return [baselineRow, ..candidateRows];
        }

        baselineDiag.Add("air-x5-baseline-created");
        var baselineBody = baselineResult.Value;
        var baselineRowSuccess = BuildRow(@case, "baseline", null, baselineBody, baselineDiag, true, "keep-direct-constructor-for-now", null);
        var candidates = BuildCandidates(@case, baselineBody, true);
        return [baselineRowSuccess, ..candidates];
    }

    private static IEnumerable<AirPrimitiveMatrixRow> BuildCandidates(AirPrimitiveMatrixCase @case, BrepBody? baselineBody, bool valid)
    {
        var list = new List<AirPrimitiveMatrixRow>();
        if (@case.Primitive == AirPrimitiveBaselineKind.Cylinder)
        {
            list.Add(BuildUnavailable(@case, AirPrimitiveCandidateKind.AirExtrude, "air-x5-air-candidate-unavailable:no-circular-profile-extrude-api"));
            list.Add(BuildCandidateFromResult(@case, AirPrimitiveCandidateKind.AirRevolve, CreateCylinderRevolveCandidate(@case), baselineBody, valid));
        }
        else if (@case.Primitive == AirPrimitiveBaselineKind.ConeFrustum)
        {
            list.Add(BuildCandidateFromResult(@case, AirPrimitiveCandidateKind.AirRevolve, CreateConeRevolveCandidate(@case), baselineBody, valid));
        }
        else
        {
            list.Add(BuildUnavailable(@case, AirPrimitiveCandidateKind.AirRevolve, "air-x5-air-candidate-unavailable:current-brep-revolve-supports-only-two-point-line-segment-profile"));
        }

        return list;
    }

    private static AirPrimitiveMatrixRow BuildUnavailable(AirPrimitiveMatrixCase @case, AirPrimitiveCandidateKind kind, string reason)
        => new(@case, "candidate", kind, EmptyTopology(), EmptyStep([reason]), [reason], false, false, false, "needs-air-revolve-lab-support");

    private static AirPrimitiveMatrixRow BuildCandidateFromResult(AirPrimitiveMatrixCase @case, AirPrimitiveCandidateKind kind, KernelResult<BrepBody> result, BrepBody? baselineBody, bool valid)
    {
        var d = new List<string>();
        if (!valid)
        {
            d.Add("air-x5-air-candidate-unavailable:invalid-baseline-input");
            return new(@case, "candidate", kind, EmptyTopology(), EmptyStep(d), d, false, false, false, "keep-direct-constructor-for-now");
        }

        if (!result.IsSuccess || result.Value is null)
        {
            d.Add("air-x5-air-candidate-unavailable:construction-failed");
            d.AddRange(result.Diagnostics.Select(x => x.Message));
            return new(@case, "candidate", kind, EmptyTopology(), EmptyStep(d), d, true, false, false, "needs-air-revolve-lab-support");
        }

        d.Add("air-x5-air-candidate-created");
        var row = BuildRow(@case, "candidate", kind, result.Value, d, true, "needs-emitter-parity-work", baselineBody);
        return row;
    }

    private static AirPrimitiveMatrixRow BuildRow(AirPrimitiveMatrixCase @case, string rowKind, AirPrimitiveCandidateKind? candidateKind, BrepBody? body, IReadOnlyList<string> diagnostics, bool validInput, string fallbackRecommendation, BrepBody? baselineBody)
    {
        var topology = body is null ? EmptyTopology() : SummarizeTopology(body);
        var step = body is null ? EmptyStep(["no body"]) : SummarizeStep(@case.Primitive, body);
        var topologyParity = body is not null && baselineBody is not null && topology == SummarizeTopology(baselineBody);
        var stepParity = body is not null && baselineBody is not null && StepParity(SummarizeStep(@case.Primitive, baselineBody), step);
        var recommendation = fallbackRecommendation;
        if (candidateKind is not null && validInput && body is not null)
        {
            if (@case.Primitive == AirPrimitiveBaselineKind.Cylinder && topologyParity && stepParity) recommendation = "ready-for-production-migration";
            else if ((@case.Primitive == AirPrimitiveBaselineKind.Sphere || @case.Primitive == AirPrimitiveBaselineKind.Torus) && topologyParity && stepParity) recommendation = "keep-direct-constructor-for-now";
            else recommendation = topologyParity && stepParity ? "needs-emitter-parity-work" : fallbackRecommendation;
        }

        var full = diagnostics.ToList();
        full.Add(topologyParity ? "air-x5-topology-parity-succeeded" : $"air-x5-topology-parity-mismatch:{topology}");
        full.Add(stepParity ? "air-x5-step-smoke-succeeded" : "air-x5-step-smoke-failed");
        full.Add($"air-x5-recommendation:{recommendation}");
        return new(@case, rowKind, candidateKind, topology, step, full, validInput, topologyParity, stepParity, recommendation);
    }

    private static bool StepParity(AirPrimitiveStepSmokeSummary a, AirPrimitiveStepSmokeSummary b) => a.Exported && b.Exported && a.MissingMarkers.Count == 0 && b.MissingMarkers.Count == 0 && !a.ContainsBrepWithVoids && !b.ContainsBrepWithVoids;

    private static KernelResult<BrepBody> CreateBaseline(AirPrimitiveMatrixCase c) => c.Primitive switch
    {
        AirPrimitiveBaselineKind.Cylinder => BrepPrimitives.CreateCylinder(c.A, c.B),
        AirPrimitiveBaselineKind.ConeFrustum => CreateConeFrustumBaseline(c.A, c.B, c.C),
        AirPrimitiveBaselineKind.Sphere => BrepPrimitives.CreateSphere(c.A),
        AirPrimitiveBaselineKind.Torus => BrepPrimitives.CreateTorus(c.A, c.B),
        _ => throw new ArgumentOutOfRangeException()
    };

    private static KernelResult<BrepBody> CreateConeFrustumBaseline(double bottom, double top, double height) => CreateConeRevolve(bottom, top, height);
    private static KernelResult<BrepBody> CreateConeRevolveCandidate(AirPrimitiveMatrixCase c) => CreateConeRevolve(c.A, c.B, c.C);
    private static KernelResult<BrepBody> CreateConeRevolve(double bottom, double top, double height)
    {
        var frame = new ExtrudeFrame3D(new Point3D(0,0,0), Direction3D.Create(new Vector3D(0,0,1)), Direction3D.Create(new Vector3D(1,0,0)));
        return BrepRevolve.Create([new(bottom, -height*0.5), new(top, height*0.5)], frame, new RevolveAxis3D(new Point3D(0,0,0), new Vector3D(0,0,1)));
    }
    private static KernelResult<BrepBody> CreateCylinderRevolveCandidate(AirPrimitiveMatrixCase c) => CreateConeRevolve(c.A, c.A, c.B);
    private static readonly string[] CommonMarkers = ["ISO-10303-21", "MANIFOLD_SOLID_BREP", "ADVANCED_FACE"];
    private static string[] MarkersFor(AirPrimitiveBaselineKind p) => p switch { AirPrimitiveBaselineKind.Cylinder => [..CommonMarkers, "CYLINDRICAL_SURFACE", "PLANE"], AirPrimitiveBaselineKind.ConeFrustum => [..CommonMarkers, "CONICAL_SURFACE"], AirPrimitiveBaselineKind.Sphere => [..CommonMarkers, "SPHERICAL_SURFACE"], AirPrimitiveBaselineKind.Torus => [..CommonMarkers, "TOROIDAL_SURFACE"], _ => CommonMarkers };
    private static AirPrimitiveTopologySummary EmptyTopology() => new(false,0,0,0,0,0,0,0,0,0,0);
    private static AirPrimitiveStepSmokeSummary EmptyStep(IReadOnlyList<string> d) => new(false, [], CommonMarkers, false, d);
    private static AirPrimitiveTopologySummary SummarizeTopology(BrepBody body) => new(true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Plane), body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Cylinder), body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Cone), body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Sphere), body.Topology.Faces.Count(f=>body.GetFaceSurface(f.Id).Kind==SurfaceGeometryKind.Torus), body.Topology.Loops.Count(), body.Topology.Coedges.Count());
    private static AirPrimitiveStepSmokeSummary SummarizeStep(AirPrimitiveBaselineKind p, BrepBody body)
    {
        var result = Step242Exporter.ExportBody(body);
        if (!result.IsSuccess || result.Value is null) return new(false, [], MarkersFor(p), false, result.Diagnostics.Select(x=>x.Message).ToArray());
        var markers = MarkersFor(p);
        var present = markers.Where(m=>result.Value.Contains(m,StringComparison.Ordinal)).ToArray();
        return new(true, present, markers.Except(present).ToArray(), result.Value.Contains("BREP_WITH_VOIDS", StringComparison.Ordinal), []);
    }
}
