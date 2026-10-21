using System.Text.Json;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Features;

public sealed class BrepThroughHoleRevisionTests
{
    const string Ctc = "testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp";
    // This policy-specific import cannot share the corpus's default strict policy.
    static readonly Lazy<BrepBody> Source = new(() => Step242Importer.ImportBody(Step242Corpus.Text(Ctc),
        new ImportPolicy(PcurveQualificationToleranceMillimetres: .01)).Value);
    static ThroughHoleRevisionRequest Request => new("MountHole", [new(1), new(2)], new(98), new(-130, 45, 0), 25,
        new(new(-175, 30, -50), new(-115, 60, 0)), .01);

    [Fact]
    public void DestinationContainment_ExcludesEntireInteriorOfAnExistingOpening()
    {
        var cut = BrepBoolean.Subtract(BrepPrimitives.CreateBox(40, 40, 10).Value,
            StandardLibrary.StandardLibraryPrimitives.CreateSlotCut(16, 8, 24, 4).Value);
        Assert.True(cut.IsSuccess, string.Join(" | ", cut.Diagnostics.Select(d => d.Message)));
        var exported = Step242Exporter.ExportBody(cut.Value);
        Assert.True(exported.IsSuccess);
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess);
        var body = imported.Value;
        var top = Assert.Single(body.Topology.Faces, f => body.GetFaceSurface(f.Id).Plane is { } p
            && System.Math.Abs(p.Origin.Z - 5) < 1e-6 && System.Math.Abs(p.Normal.Z) > .99);
        Assert.True(AnalyticPlanarFaceDomain.TryContainsInPlanarFace(body, top.Id, new(0, 0, 5), ToleranceContext.Default, default, out var inOpening));
        Assert.False(inOpening);
        Assert.True(AnalyticPlanarFaceDomain.TryContainsInPlanarFace(body, top.Id, new(15, 0, 5), ToleranceContext.Default, default, out var inStock));
        Assert.True(inStock);
    }

    [Fact, Trait("Category", "SlowCorpus")]
    public void Discovery_GroupsSplitWallsAndReportsUnsupportedCandidates()
    {
        var discovery = BrepThroughHoleRevision.Discover(Source.Value, .01);
        Assert.True(discovery.Qualified);
        var hole = Assert.Single(discovery.Candidates, c => c.WallFaces.Contains(1));
        Assert.Equal(new[] { 1, 2 }, hole.WallFaces);
        Assert.True(hole.CanPlan); Assert.Equal(50, hole.Depth); Assert.Equal(98, hole.EntryFace);
        Assert.Contains(discovery.Candidates, c => !c.Recognized && c.Diagnostics.Count > 0);
        Assert.Equal(discovery.Candidates.Select(c => c.Id), BrepThroughHoleRevision.Discover(Source.Value.Clone(), .01).Candidates.Select(c => c.Id));
        var strict = BrepThroughHoleRevision.Discover(Source.Value, .001);
        Assert.False(strict.Qualified); Assert.DoesNotContain(strict.Candidates, c => c.CanPlan);
    }

    [Fact, Trait("Category", "SlowCorpus")]
    public void TwoLocalRegions_MoveAcrossCentralBossWithoutEditingIt()
    {
        var source = Source.Value;
        var candidate = Assert.Single(BrepThroughHoleRevision.Discover(source, .01).Candidates, c => c.WallFaces.Contains(1));
        var plan = BrepThroughHoleRevision.PlanMove(source, candidate.Id, "MovedMount", new(130,45,0), .01);
        Assert.True(plan.IsSuccess, string.Join(" | ", plan.Diagnostics.Select(d => d.Message)));
        var request = plan.Value.Request; var result = plan.Value.Revision;
        Assert.NotNull(request.DestinationWithin);
        var sweptEnvelope = new ThroughHoleEditBounds(request.Within.Min, request.DestinationWithin.Max);
        Assert.False(BrepThroughHoleRevision.Execute(source, request with { Within = sweptEnvelope, DestinationWithin = null }).IsSuccess);
        Assert.Equal(115, result.PreservedFaces); Assert.Equal(312, result.PreservedEdges);
        foreach (var face in source.Topology.Faces.Where(f => !candidate.WallFaces.Contains(f.Id.Value)))
            Assert.Equal(source.GetFaceSurface(face.Id), result.Body.GetFaceSurface(face.Id));
        Assert.False(BrepThroughHoleRevision.PlanMove(source, candidate.Id, "Occupied", new(160,45,0), .01).IsSuccess);
        var outside = BrepThroughHoleRevision.PlanMove(source, candidate.Id, "Outside", new(1000,45,0), .01);
        Assert.False(outside.IsSuccess);
        Assert.Contains(outside.Diagnostics, d => d.Message.Contains("outside the support face"));
        Assert.False(BrepThroughHoleRevision.PlanMove(source, "missing", "Missing", new(130,45,0), .01).IsSuccess);
    }

    [Fact, Trait("Category", "SlowCorpus")]
    public void Ctc01_ExplicitSourceAccuracy_QualifiesAllTrimsWithoutChangingGeometry()
    {
        var strict = Step242Corpus.Body(Ctc);
        var source = Source.Value;
        Assert.Equal(ImportQualificationStatus.Qualified, source.ImportQualification!.GeometryStatus);
        Assert.Equal(636, source.Bindings.PcurveBindings.Count());
        Assert.True(BrepPcurveValidator.Validate(source, .01, requireEveryCoedge: true, samples: 1025).IsValid);
        Assert.True(strict.ImportQualification!.MissingPcurves > 0);
        Assert.Equal(JsonSerializer.Serialize(strict.Geometry.Curves), JsonSerializer.Serialize(source.Geometry.Curves));
        Assert.Equal(JsonSerializer.Serialize(strict.Geometry.Surfaces), JsonSerializer.Serialize(source.Geometry.Surfaces));
        var rejected = BrepThroughHoleRevision.Execute(source, Request with { ImportToleranceMillimetres = .001 });
        Assert.False(rejected.IsSuccess);
    }

    [Fact, Trait("Category", "SlowCorpus")]
    public void Ctc01_ReplacesOneHole_PreservesResidualIdentityAndRoundTrips()
    {
        var source = Source.Value;
        var before = JsonSerializer.Serialize(source.Topology);
        var result = BrepThroughHoleRevision.Execute(source, Request);
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var edit = result.Value; var body = edit.Body;
        Assert.Equal(117, body.Topology.Faces.Count()); Assert.Equal(318, body.Topology.Edges.Count());
        Assert.Equal(206, body.Topology.Vertices.Count());
        Assert.Equal(115, edit.PreservedFaces); Assert.Equal(312, edit.PreservedEdges);
        Assert.All(edit.WallFaces, id => Assert.True(id.Value > 117));
        Assert.All(edit.RetiredWallFaces, id => Assert.DoesNotContain(body.Topology.Faces, f => f.Id == id));
        foreach (var f in source.Topology.Faces.Where(f => f.Id.Value > 2))
        {
            Assert.Equal(f.Id, edit.FaceCorrespondence[f.Id]);
            Assert.Equal(source.Bindings.GetFaceBinding(f.Id), body.Bindings.GetFaceBinding(f.Id));
            Assert.Equal(source.GetFaceSurface(f.Id), body.GetFaceSurface(f.Id));
            if (f.Id.Value is not (3 or 98)) Assert.Equal(f.LoopIds, body.Topology.GetFace(f.Id).LoopIds);
        }
        foreach (var edge in source.Topology.Edges.Where(e => body.Topology.Edges.Any(b => b.Id == e.Id)))
        {
            Assert.Equal(edge, body.Topology.GetEdge(edge.Id));
            Assert.Equal(source.Bindings.GetEdgeBinding(edge.Id), body.Bindings.GetEdgeBinding(edge.Id));
            Assert.Equal(source.GetEdgeCurve(edge.Id), body.GetEdgeCurve(edge.Id));
        }
        foreach (var vertex in source.Topology.Vertices.Where(v => body.Topology.Vertices.Any(b => b.Id == v.Id)))
        {
            Assert.True(source.TryGetVertexPoint(vertex.Id, out var oldPoint));
            Assert.True(body.TryGetVertexPoint(vertex.Id, out var newPoint));
            Assert.Equal(oldPoint, newPoint);
        }
        var survivingCoedges = body.Topology.Coedges.Select(c => c.Id).ToHashSet();
        Assert.All(source.Bindings.PcurveBindings.Where(b => survivingCoedges.Contains(b.CoedgeId)),
            binding => Assert.Contains(binding, body.Bindings.PcurveBindings));
        Assert.All(source.Bindings.VertexLoopParameterBindings, binding => Assert.Contains(binding, body.Bindings.VertexLoopParameterBindings));
        Assert.Equal(before, JsonSerializer.Serialize(source.Topology));
        Assert.True(BrepPcurveValidator.Validate(body, .01, requireEveryCoedge: true, samples: 1025).IsValid);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            ImportedRecoveryToleranceMillimetres = .01
        });
        Assert.True(step.IsSuccess);
        var roundtrip = Step242Importer.ImportBody(step.Value, new ImportPolicy(PcurveQualificationToleranceMillimetres: .01));
        Assert.True(roundtrip.IsSuccess);
        Assert.Equal(ImportQualificationStatus.Qualified, roundtrip.Value.ImportQualification!.GeometryStatus);
        var cylindricalWalls = roundtrip.Value.Bindings.FaceBindings.Where(b => roundtrip.Value.GetFaceSurface(b.FaceId).Cylinder is { } c
            && System.Math.Abs(c.Radius - 12.5) < 1e-6).Select(b => roundtrip.Value.GetFaceSurface(b.FaceId).Cylinder!.Value).ToArray();
        Assert.Equal(2, cylindricalWalls.Count(c => System.Math.Abs(c.Origin.X + 130) < 1e-6 && System.Math.Abs(c.Origin.Y - 45) < 1e-6));
        Assert.DoesNotContain(cylindricalWalls, c => System.Math.Abs(c.Origin.X + 160) < 1e-6 && System.Math.Abs(c.Origin.Y - 45) < 1e-6);
    }

    [Theory, Trait("Category", "SlowCorpus")]
    [InlineData("partial")]
    [InlineData("diameter")]
    [InlineData("plane")]
    [InlineData("collision")]
    [InlineData("off-plane")]
    public void UnprovedRevision_IsRejectedWithoutChangingInput(string kind)
    {
        var source = Source.Value; var before = JsonSerializer.Serialize(source.Topology);
        var request = kind switch
        {
            "partial" => Request with { WallFaces = [new FaceId(1)] },
            "diameter" => Request with { Diameter = 30 },
            "plane" => Request with { EntryFace = new(4) },
            "collision" => Request with { Center = new(160, 45, 0), Within = new(new(-175, 30, -50), new(180, 60, 0)) },
            _ => Request with { Center = new(-130, 45, 1) }
        };
        Assert.False(BrepThroughHoleRevision.Execute(source, request).IsSuccess);
        Assert.Equal(before, JsonSerializer.Serialize(source.Topology));
    }
}
