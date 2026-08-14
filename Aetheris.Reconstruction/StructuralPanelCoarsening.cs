using Aetheris.Geometry;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Surfacing;

namespace Aetheris.Reconstruction;

public sealed record StructuralCoarseningPolicy(
    double MaximumFeatureAngleDegrees = 20,
    double MaximumTangentialFraction = .25,
    double MaximumResidualToBoundaryScale = .2,
    int MaximumExactCandidates = 1000);

public sealed record StructuralPanelRegion(
    string StableId, IReadOnlyList<string> SourceChartIds, IReadOnlyList<int> SourceTriangles,
    PanelIr Panel, SurfaceResidualField? ResidualField, double BaseRms, double CorrectedRms,
    double MaximumTangentialResidual, double NormalDiscontinuityDegrees, string RepresentationDecision);

public sealed record StructuralPanelCoarseningResult(
    IReadOnlyList<StructuralPanelRegion> Panels, int InputPanelCount, int MergedPairCount,
    int RejectedTopology, int RejectedFeature, int RejectedParameterization, int RejectedResidual,
    int ExactCandidateCount, int DeferredCandidateCount, IReadOnlyList<string> RepresentativeDecisions)
{
    public double MergeRatio => InputPanelCount / (double)Math.Max(1, Panels.Count);
}

/// <summary>
/// First bounded structural coarsener: globally selects disjoint adjacent two-Panel unions,
/// then admits only disk unions whose six-edge outline has four supported dominant corners.
/// It is intentionally narrower than arbitrary multi-chart parameterization.
/// </summary>
public static class StructuralPanelCoarsener
{
    private sealed record Candidate(QuadAtlasChart A, QuadAtlasChart B, PanelIr Panel, int[] Corners,
        double BaseRms, double CorrectedRms, double TangentialMaximum, double FeatureAngle, SurfaceResidualField? Residual, double Utility, string Decision);

    public static StructuralPanelCoarseningResult Coarsen(TriangleSurfaceMesh mesh, QuadAtlas atlas, StructuralCoarseningPolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(mesh); ArgumentNullException.ThrowIfNull(atlas); policy ??= new();
        var chartById = atlas.Charts.ToDictionary(x => x.StableId, StringComparer.Ordinal); var candidates = new List<Candidate>();
        var rejectedTopology = 0; var rejectedFeature = 0; var rejectedParameterization = 0; var rejectedResidual = 0; var decisions = new List<string>();
        var internalSeams = atlas.Seams.Where(x => x.ChartUses.Count == 2 && !x.IsSourceBoundary).OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray();
        var cheapFeature = internalSeams.ToDictionary(seam => seam.StableId,
            seam => NormalAngle(mesh, chartById[seam.ChartUses[0]].SourceTriangles, chartById[seam.ChartUses[1]].SourceTriangles), StringComparer.Ordinal);
        // Bounded first pass: exact residual projection is intentionally expensive. Evaluate only
        // mutual best smooth-neighbor proposals, then use global matching over admitted proposals.
        var preferred = internalSeams.SelectMany(seam => seam.ChartUses.Select(chart => (chart, seam)))
            .GroupBy(x => x.chart, StringComparer.Ordinal).ToDictionary(g => g.Key,
                g => g.OrderBy(x => cheapFeature[x.seam.StableId]).ThenBy(x => x.seam.StableId, StringComparer.Ordinal).First().seam.StableId, StringComparer.Ordinal);
        var proposedSeams = internalSeams.Where(seam => seam.ChartUses.Any(chart => preferred[chart] == seam.StableId))
            .OrderBy(seam => cheapFeature[seam.StableId]).ThenBy(seam => seam.StableId, StringComparer.Ordinal).ToArray();
        var boundedSeams = proposedSeams.Take(policy.MaximumExactCandidates);
        foreach (var seam in boundedSeams)
        {
            var a = chartById[seam.ChartUses[0]]; var b = chartById[seam.ChartUses[1]];
            var feature = cheapFeature[seam.StableId]; if (feature > policy.MaximumFeatureAngleDegrees) { rejectedFeature++; continue; }
            var faces = a.SourceTriangles.Concat(b.SourceTriangles).Distinct().ToArray(); if (!TryBoundaryCycle(mesh, faces, out var boundary) || boundary.Length < 4) { rejectedTopology++; continue; }
            var corners = DominantCorners(mesh, boundary); if (corners.Length != 4 || !NonFolded(corners.Select(i => mesh.Vertices[i]).ToArray())) { rejectedParameterization++; continue; }
            var panel = Materialize($"structural-{a.StableId}-{b.StableId}", corners.Select(i => mesh.Vertices[i]).ToArray()); if (!panel.IsSuccess) { rejectedParameterization++; continue; }
            var patch = panel.Panel!.AuthoredPatch;
            var boundaryScale = Enumerable.Range(0, 4).Average(i => (mesh.Vertices[corners[(i + 1) % 4]] - mesh.Vertices[corners[i]]).Length);
            var queryPolicy = new DistanceQueryPolicy { SubdivisionBudget = 128, IterationBudget = 20, LinearTolerance = Math.Max(1e-9, boundaryScale * .01), RelativeTolerance = 1e-8 };
            ResidualDecompositionSample[] samples;
            try
            {
                samples = faces.SelectMany(face => Vertices(mesh.Triangles[face])).Distinct().Select(vertex =>
                    SurfaceResidualExtractor.Decompose(mesh.Vertices[vertex], FaceNormalAtVertex(mesh, faces, vertex), patch,
                        queryPolicy)).Concat(faces.Select(face =>
                    SurfaceResidualExtractor.Decompose(FaceCentroid(mesh, face), FaceNormal(mesh, face), patch,
                        queryPolicy))).ToArray();
            }
            catch (InvalidOperationException) { rejectedResidual++; continue; }
            var baseRms = Math.Sqrt(samples.Average(x => x.Residual.LengthSquared)); var tangent = samples.Max(x => x.TangentialMagnitude);
            if (tangent > policy.MaximumTangentialFraction * boundaryScale || baseRms > policy.MaximumResidualToBoundaryScale * boundaryScale) { rejectedResidual++; continue; }
            var offset = FitOffsetGrid(samples, patch.Domain); var residual = new SurfaceResidualField($"residual-{panel.Panel.StableId}", patch.StableId, patch.Domain, offset, null,
                ResidualSeamPolicy.ZeroAtBoundary, new(PredicateEvidenceKind.Sampled, mesh.SourceIdentity, "closest-point normal decomposition; 3 x 3 piecewise-bilinear scalar fit with zero structural seam", baseRms, samples.Max(x => x.Residual.Length), 0, 0, samples.Length));
            var correctedRms = Math.Sqrt(samples.Average(x => (residual.Evaluate(patch, x.U, x.V).Point - x.SourcePoint).LengthSquared));
            var representations = new[]
            {
                new JudgmentCandidate<string>("base-only", _ => baseRms <= .02 * boundaryScale, _ => 1 - baseRms / Math.Max(boundaryScale, 1e-15), _ => "bounded support already meets structural tolerance", 0),
                new JudgmentCandidate<string>("offset-grid", _ => correctedRms <= baseRms && tangent <= policy.MaximumTangentialFraction * boundaryScale, _ => .9 - correctedRms / Math.Max(boundaryScale, 1e-15), _ => "sampled scalar normal offset with explicit complexity penalty", 1)
            };
            var judgment = new JudgmentEngine<string>().Evaluate("base-only", representations);
            if (judgment.Selection is null) { rejectedResidual++; continue; }
            var choice = judgment.Selection.Value.Candidate.Name;
            var admittedResidual = choice == "offset-grid" ? residual : null; var utility = 1 - (choice == "offset-grid" ? correctedRms : baseRms) / Math.Max(boundaryScale, 1e-15) - feature / 180;
            candidates.Add(new(a, b, panel.Panel, corners, baseRms, correctedRms, tangent, feature, admittedResidual, utility, choice));
            if (decisions.Count < 20) decisions.Add($"{a.StableId}+{b.StableId}: {choice}; baseRms={baseRms:R}; correctedRms={correctedRms:R}; tangentMax={tangent:R}");
        }
        var index = atlas.Charts.Select((chart, i) => (chart.StableId, i)).ToDictionary(x => x.StableId, x => x.i, StringComparer.Ordinal);
        var matching = GlobalQuadLayoutMatcher.Match(atlas.Charts.Count, candidates.Select((c, i) => new GlobalMatchingEdge(index[c.A.StableId], index[c.B.StableId], c.Utility, $"merge-{i:D8}")).ToArray());
        var candidateByPair = candidates.GroupBy(c => Key(index[c.A.StableId], index[c.B.StableId])).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Utility).First());
        var merged = new HashSet<string>(StringComparer.Ordinal); var result = new List<StructuralPanelRegion>(); var mergedCount = 0;
        for (var i = 0; i < matching.Mate.Count; i++) if (matching.Mate[i] > i && candidateByPair.TryGetValue(Key(i, matching.Mate[i]), out var candidate))
        {
            merged.Add(candidate.A.StableId); merged.Add(candidate.B.StableId); mergedCount++;
            result.Add(new(candidate.Panel.StableId, [candidate.A.StableId, candidate.B.StableId], candidate.A.SourceTriangles.Concat(candidate.B.SourceTriangles).ToArray(), candidate.Panel,
                candidate.Residual, candidate.BaseRms, candidate.CorrectedRms, candidate.TangentialMaximum, candidate.FeatureAngle, candidate.Decision));
        }
        foreach (var chart in atlas.Charts.Where(x => !merged.Contains(x.StableId))) result.Add(new(chart.StableId, [chart.StableId], chart.SourceTriangles, chart.StrictPanel, null, 0, 0, 0, 0, "retained structural Panel"));
        return new(result.OrderBy(x => x.StableId, StringComparer.Ordinal).ToArray(), atlas.Charts.Count, mergedCount, rejectedTopology, rejectedFeature, rejectedParameterization, rejectedResidual,
            Math.Min(policy.MaximumExactCandidates, proposedSeams.Length), Math.Max(0, proposedSeams.Length - policy.MaximumExactCandidates), decisions);
    }

    private static BilinearScalarField FitOffsetGrid(IReadOnlyList<ResidualDecompositionSample> samples, ParametricDomain domain)
    {
        var sum = new double[3, 3]; var count = new int[3, 3];
        foreach (var sample in samples)
        {
            var i = (int)Math.Round((sample.U - domain.U.Minimum) / (domain.U.Maximum - domain.U.Minimum) * 2); var j = (int)Math.Round((sample.V - domain.V.Minimum) / (domain.V.Maximum - domain.V.Minimum) * 2);
            if (i == 0 || i == 2 || j == 0 || j == 2) continue; sum[i, j] += sample.NormalComponent; count[i, j]++;
        }
        var values = new double[3, 3]; if (count[1, 1] > 0) values[1, 1] = sum[1, 1] / count[1, 1]; return new(values, domain);
    }
    private static PanelResult Materialize(string id, Point3D[] p)
    {
        var s = new RuledBoundary.Line(id + ":s", p[0], p[1]); var e = new RuledBoundary.Line(id + ":e", p[1], p[2]); var n = new RuledBoundary.Line(id + ":n", p[3], p[2]); var w = new RuledBoundary.Line(id + ":w", p[0], p[3]);
        return PanelFactory.FromBoundaryPatch(new(id, s, n, w, e, [new(s.StableId,"coarsening","boundary chain"),new(n.StableId,"coarsening","boundary chain"),new(w.StableId,"coarsening","boundary chain"),new(e.StableId,"coarsening","boundary chain")]));
    }
    private static bool TryBoundaryCycle(TriangleSurfaceMesh mesh, IReadOnlyList<int> faces, out int[] cycle)
    {
        var directed = faces.SelectMany(face => mesh.Triangles[face].DirectedEdges()).ToArray(); var outer = directed.Where(e => directed.Count(x => Key(x.A, x.B) == Key(e.A, e.B)) == 1).ToArray();
        if (outer.Length < 4) { cycle = []; return false; } var next = outer.GroupBy(x => x.A).ToDictionary(g => g.Key, g => g.Select(x => x.B).Distinct().ToArray()); if (next.Any(x => x.Value.Length != 1)) { cycle = []; return false; }
        var start = outer.Min(x => x.A); var list = new List<int>(); var current = start;
        do { if (list.Contains(current) || !next.TryGetValue(current, out var values)) { cycle = []; return false; } list.Add(current); current = values[0]; } while (current != start && list.Count <= outer.Length);
        cycle = current == start && list.Count == outer.Length ? list.ToArray() : []; return cycle.Length > 0;
    }
    private static int[] DominantCorners(TriangleSurfaceMesh mesh, int[] boundary)
    {
        if (boundary.Length == 4) return boundary;
        var scored = boundary.Select((vertex, i) => { var a = mesh.Vertices[boundary[(i + boundary.Length - 1) % boundary.Length]] - mesh.Vertices[vertex]; var b = mesh.Vertices[boundary[(i + 1) % boundary.Length]] - mesh.Vertices[vertex]; var turn = a.TryNormalize(out a) && b.TryNormalize(out b) ? Math.PI - Math.Acos(Math.Clamp(a.Dot(b), -1, 1)) : 0; return (i, turn); }).OrderByDescending(x => x.turn).ThenBy(x => boundary[x.i]).Take(4).Select(x => x.i).Order().ToArray();
        return scored.Select(i => boundary[i]).ToArray();
    }
    private static bool NonFolded(Point3D[] p) { var n = (p[1]-p[0]).Cross(p[2]-p[0])+(p[2]-p[0]).Cross(p[3]-p[0]); return n.Length>1e-15&&Enumerable.Range(0,4).All(i=>(p[(i+1)%4]-p[i]).Cross(p[(i+2)%4]-p[(i+1)%4]).Dot(n)>=-1e-12); }
    private static double NormalAngle(TriangleSurfaceMesh mesh, IReadOnlyList<int> a, IReadOnlyList<int> b) { var na=a.Select(x=>FaceNormal(mesh,x)).Aggregate(Vector3D.Zero,(x,y)=>x+y);var nb=b.Select(x=>FaceNormal(mesh,x)).Aggregate(Vector3D.Zero,(x,y)=>x+y);return na.TryNormalize(out na)&&nb.TryNormalize(out nb)?Math.Acos(Math.Clamp(na.Dot(nb),-1,1))*180/Math.PI:180; }
    private static Vector3D FaceNormal(TriangleSurfaceMesh mesh,int face){var t=mesh.Triangles[face];var n=(mesh.Vertices[t.B]-mesh.Vertices[t.A]).Cross(mesh.Vertices[t.C]-mesh.Vertices[t.A]);return n.TryNormalize(out n)?n:Vector3D.Zero;}
    private static Point3D FaceCentroid(TriangleSurfaceMesh mesh,int face){var t=mesh.Triangles[face];var a=mesh.Vertices[t.A];var b=mesh.Vertices[t.B];var c=mesh.Vertices[t.C];return new((a.X+b.X+c.X)/3,(a.Y+b.Y+c.Y)/3,(a.Z+b.Z+c.Z)/3);}
    private static Vector3D FaceNormalAtVertex(TriangleSurfaceMesh mesh,IReadOnlyList<int> faces,int vertex){var n=faces.Where(f=>Vertices(mesh.Triangles[f]).Contains(vertex)).Select(f=>FaceNormal(mesh,f)).Aggregate(Vector3D.Zero,(a,b)=>a+b);return n.TryNormalize(out n)?n:new(0,0,1);}
    private static int[] Vertices(Triangle t)=>[t.A,t.B,t.C]; private static (int,int) Key(int a,int b)=>a<b?(a,b):(b,a);
}
