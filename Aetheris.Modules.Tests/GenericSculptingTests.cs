using Aetheris.Surfacing;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.Modules.Tests;

public sealed class GenericSculptingTests
{
    [Fact]
    public void ExactFaceGraftRetainsNeighborsAndReplaysItsActualPatch()
    {
        var result = Compile("exact-face"); var before = result.States["exact"]; var after = result.OutputState!;
        Assert.Null(after.Construction); Assert.Same(before.Body.Topology, after.Body.Topology);
        Assert.Null(after.Body.SafeBooleanComposition);
        Assert.Equal(before.Body.Geometry.Curves, after.Body.Geometry.Curves);
        foreach (var face in before.Body.Topology.Faces.Where(f => f.Id.Value != 2))
        {
            Assert.True(before.Body.TryGetFaceSurfaceGeometry(face.Id, out var oldSupport));
            Assert.True(after.Body.TryGetFaceSurfaceGeometry(face.Id, out var support)); Assert.Equal(oldSupport, support);
        }
        Assert.True(after.Body.TryGetFaceSurfaceGeometry(new(2), out var crown));
        Assert.Equal(8.5625, crown!.BSplineSurfaceWithKnots!.Evaluate(.5, .5).Z, 7);
        Assert.True(after.Body.Bindings.TryGetFaceBinding(new(2), out var crownBinding));
        Assert.True(crownBinding.Orientation.IsAlignedWithSurface);
        Assert.False(after.SemanticInventory.ContainsKey("Face2"));
        Assert.Contains(after.GeometryAssociations!, a => a.SemanticTarget == "crown" && a.FaceIds.SequenceEqual(new[] { 2 }));
        Assert.True(BrepPcurveValidator.Validate(after.Body, 1e-6, true).IsValid);
        var saved = ConstructionStateSerializer.Serialize(after.ConstructionAuthority!); Assert.True(saved.IsSuccess);
        var loaded = ConstructionStateSerializer.Deserialize(saved.Json!); Assert.True(loaded.IsSuccess);
        var replay = ConstructionStateReplayer.Replay(loaded.ConstructionState!);
        Assert.True(replay.IsSuccess, string.Join(";", replay.Diagnostics.Select(d => d.Message)));
        Assert.Equal(after.StateId, replay.OutputState!.StateId);
        var export = SculptStepExporter.Export(after, result.ModelName); Assert.True(export.IsSuccess);
        Assert.Equal(export.Step, SculptStepExporter.Export(replay.OutputState, result.ModelName).Step);
        Assert.True(Step242Importer.ImportBody(export.Step!).IsSuccess);
        var stale = (ReplaceRegionOperation)after.ConstructionAuthority!.Operations.Single().Payload;
        var refused = ReplaceRegionSculptor.Apply(after, "stale", stale);
        Assert.False(refused.IsSuccess); Assert.Null(refused.OutputState);
    }

    [Theory]
    [InlineData("preserve: [Face1]", "preserve: [Face2]", "sculpt-preserve-conflict")]
    [InlineData("existing: Edge2", "existing: Edge5", "surf-face-boundary-mismatch")]
    [InlineData("continuity: G0", "continuity: G1", "surf-face-continuity-unsupported")]
    [InlineData("ClosedManifold, OrientationConsistency", "ClosedManifold, NoSelfIntersection", "surf-face-global-intersection-unqualified")]
    [InlineData("10mm, 8mm, 9mm", "10mm, 8mm, 8mm", "sculpt-envelope-exceeded")]
    public void ExactGraftRejectsUnprovenIntentAtomically(string old, string replacement, string code)
    {
        var result = SculptingAuthoring.Compile(Source("exact-face").Replace(old, replacement));
        Assert.False(result.IsSuccess); Assert.Null(result.OutputState); Assert.Single(result.States);
        Assert.Contains(result.Diagnostics, d => d.Code == code);
    }

    [Fact]
    public void RawPmiCannotDisappearThroughAnExactEdit()
    {
        var result = Compile("exact-face"); var operation = (ReplaceRegionOperation)result.OutputState!.ConstructionAuthority!.Operations.Single().Payload;
        var pmiPath = Path.Combine(Root(), "fixtures/Regression/BodyStateDebt/explicit-pmi.firmament");
        var pmi = SculptingAuthoring.CompileFile(pmiPath); Assert.True(pmi.IsSuccess);
        var original = pmi.OutputState!; var step = SculptStepExporter.Export(original, pmi.ModelName).Step;
        var refused = ReplaceRegionSculptor.Apply(original, "edited", operation);
        Assert.False(refused.IsSuccess); Assert.Contains(refused.Diagnostics, d => d.Code == "surf-face-pmi-projection-unsupported");
        Assert.Equal(step, SculptStepExporter.Export(original, pmi.ModelName).Step);
    }

    [Fact]
    public void CreasedBoxRefinementRetainsTheExactStockBoundary()
    {
        var result = Compile("box-cage"); var cage = result.MeshStates["cage"]; var source = result.States["exact"];
        var before = SubdivisionSculptor.Evaluate(cage.Cage);
        var refined = SubdivisionSculptor.Apply(cage, "refined", new SubdivideCage("refine", 2));
        var mesh = SubdivisionSculptor.Evaluate(refined.Cage);
        Assert.True(TriangleMeshValidator.TryValidateClosed(mesh, out var topology, out _));
        Assert.Equal(2560, topology.SignedVolume, 6); Assert.Equal(2560, Volume(before), 6);
        Assert.Equal(96, refined.Cage.Faces.Count); Assert.Equal(98, refined.Cage.Vertices.Count);
        Assert.All(refined.Cage.Faces, f => Assert.Single(f.SourceFaceIds));
        Assert.Equal(source.StateId.Value, refined.SourceBodyStateId);
        Assert.Equal(source.RealizedStepText, SculptStepExporter.Export(source, result.ModelName).Step);
        Assert.True(Volume(SubdivisionSculptor.Evaluate(result.MeshStates["refined"].Cage)) < 2560);
        Assert.NotEqual(SubdivisionSculptor.Fingerprint(result.MeshStates["refined"].Cage), SubdivisionSculptor.Fingerprint(result.OutputMesh!.Cage));
    }

    [Theory]
    [InlineData("box-cage")]
    [InlineData("sphere-cage")]
    public void CageProgramSerializesAndReplaysDeterministically(string fixture)
    {
        var result = Compile(fixture); var state = result.OutputMesh!;
        var json = MeshConstructionReplayer.Serialize(state.Construction);
        var replay = MeshConstructionReplayer.Replay(json, state.Name);
        Assert.Equal(state.StateId, replay.StateId);
        Assert.Equal(SubdivisionSculptor.Fingerprint(state.Cage), SubdivisionSculptor.Fingerprint(replay.Cage));
        Assert.Equal(MeshSculptExport.Glb(state), MeshSculptExport.Glb(replay));
        Assert.Equal("NotCertified", state.SelfIntersectionStatus); Assert.False(state.StepExportAvailable);
        Assert.True(TriangleMeshValidator.TryValidateClosed(SubdivisionSculptor.Evaluate(replay.Cage), out _, out _));
    }

    [Fact]
    public void SphereConversionHasABoundedDeclaredErrorAndScalingIsExplicit()
    {
        var result = Compile("sphere-cage"); var root = result.MeshStates["cage"];
        Assert.InRange(root.ConversionDeviationMm, 0, 1);
        Assert.All(root.Cage.Vertices, v => Assert.Equal(10, (v.Position - Point3D.Origin).Length, 8));
        var refined = result.MeshStates["refined"]; var stretched = result.OutputMesh!;
        for (var i = 0; i < refined.Cage.Vertices.Count; i++)
        {
            var a = refined.Cage.Vertices[i].Position; var b = stretched.Cage.Vertices[i].Position;
            Assert.Equal(a.X * 1.5, b.X, 8); Assert.Equal(a.Y, b.Y, 8); Assert.Equal(a.Z * .75, b.Z, 8);
        }
        var tighter = SculptingAuthoring.Compile(Source("sphere-cage").Replace("maximumDeviation: 1mm", "maximumDeviation: 0.001mm"));
        Assert.False(tighter.IsSuccess); Assert.Contains(tighter.Diagnostics, d => d.Code == "mesh-conversion-deviation-exceeded");
    }

    [Fact]
    public void CreaseSuccessionAndRefinementBudgetsCannotSilentlyRebindOrOverallocate()
    {
        var result = Compile("box-cage"); var cage = result.MeshStates["cage"];
        var oldEdge = Assert.Single(cage.Edges.Take(1)).Id;
        var next = SubdivisionSculptor.Apply(cage, "next", new SubdivideCage("next", 1));
        var stale = Assert.Throws<InvalidOperationException>(() => SubdivisionSculptor.Apply(next, "stale", new CreaseCageEdges("stale", [oldEdge], true)));
        Assert.StartsWith("mesh-edge-unresolved:", stale.Message);
        var maximum = SubdivisionSculptor.Apply(cage, "maximum", new SubdivideCage("maximum", 4));
        var refused = Assert.Throws<InvalidOperationException>(() => SubdivisionSculptor.Apply(maximum, "over", new SubdivideCage("over", 4)));
        Assert.StartsWith("mesh-subdivision-budget-exceeded:", refused.Message);
        Assert.Equal(1536, maximum.Cage.Faces.Count);
    }

    private static double Volume(TriangleMesh mesh) { Assert.True(TriangleMeshValidator.TryValidateClosed(mesh, out var report, out _)); return report.SignedVolume; }
    private static SculptingCompileResult Compile(string fixture) { var r = SculptingAuthoring.CompileFile(Path.Combine(Root(), "fixtures/Regression/GenericSculpting/" + fixture + ".firmament")); Assert.True(r.IsSuccess, string.Join(";", r.Diagnostics.Select(d => d.Message))); return r; }
    private static string Source(string fixture) => File.ReadAllText(Path.Combine(Root(), "fixtures/Regression/GenericSculpting/" + fixture + ".firmament"));
    private static string Root() { var p = new DirectoryInfo(AppContext.BaseDirectory); while (p is not null && !File.Exists(Path.Combine(p.FullName, "Aetheris.slnx"))) p = p.Parent; return p!.FullName; }
}
