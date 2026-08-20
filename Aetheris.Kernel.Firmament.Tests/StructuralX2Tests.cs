using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Firmament.Structural;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class StructuralX2Tests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void WeldedWorkbench_LowersSemanticMembersBeforeGeometry_AndRoundTripsAsAssembly()
    {
        var path = Path.Combine(Root, "fixtures", "Canonical", "Structural", "welded-workbench.firmament");
        var result = StructuralAuthoring.Compile(File.ReadAllText(path), path);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        var report = Assert.IsType<StructuralReport>(result.Report);
        Assert.Equal(10, report.Members.Count);
        Assert.Equal(10, report.Assembly.MemberDefinitions.Count);
        Assert.Equal(10, report.Assembly.MemberOccurrences.Count);
        Assert.Equal(12, report.Assembly.JointInterfaces.Count);
        Assert.Equal(12, report.Joints.Count);
        Assert.Equal(12, report.Interfaces.Count);
        Assert.Equal(4, report.Interfaces.Count(x => x.Relation == "SharedCutSurface"));
        Assert.All(report.Interfaces, x =>
        {
            Assert.True(x.RetainedHalfSpacesOpposed);
            Assert.Equal(0d, x.VolumetricOverlapMm3);
        });
        Assert.All(report.Interfaces.Where(x => x.Relation == "SharedCutSurface"), x =>
        {
            Assert.True(x.MatingSurfacesCoincident);
            Assert.Equal("SeparatingAngleBisector", x.SelectedStrategy);
            Assert.Contains(x.RejectedCandidates, candidate => candidate.StartsWith("ReflexAngleBisector:", StringComparison.Ordinal));
        });
        Assert.Equal(12, report.Welds.Count);
        Assert.Equal(4, report.CutList.Count);
        Assert.Contains(report.CutList, x => x.Quantity == 4 && x.FinishedLengthMm == 780 && x.StartTreatment == "Square" && x.EndTreatment == "TrimmedTo");
        Assert.Contains(report.CutList, x => x.Quantity == 2 && x.FinishedLengthMm == 1000 && x.StartTreatment == "Miter 45deg");
        Assert.Contains(report.CutList, x => x.Quantity == 2 && x.FinishedLengthMm == 560 && x.StartTreatment == "TrimmedTo" && x.EndTreatment == "TrimmedTo");
        Assert.True(report.MembersEnclosed);
        Assert.Equal(10, report.BodyCount);
        Assert.Equal(100, report.PlaneCount);
        Assert.Equal([-20d,-20d,0d,1020d,620d,820d], report.Bounds);

        var imported = Step242AssemblyImporter.Import(result.StepText!);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
        Assert.Equal(10, imported.Value.Occurrences.Count);
        Assert.Equal(10, imported.Value.Definitions.Count(x => x.Geometry is not null));
    }

    [Fact]
    public void WeldedWorkbench_IsDeterministic()
    {
        var path = Path.Combine(Root, "fixtures", "Canonical", "Structural", "welded-workbench.firmament");
        var source = File.ReadAllText(path);
        var first = StructuralAuthoring.Compile(source, path);
        var second = StructuralAuthoring.Compile(source, path);
        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(first.StepText, second.StepText);
        Assert.Equal(first.Report!.StepSha256, second.Report!.StepSha256);
        Assert.Equal(StructuralAuthoring.CutListJson(first.Report), StructuralAuthoring.CutListJson(second.Report));
    }

    [Fact]
    public void MiterPathingPolicy_RejectsReflexOverlapCandidate()
    {
        var result = StructuralJointPathingPolicy.SelectMiter(new(new(-1,0,0), new(0,1,0)));
        Assert.True(result.IsSuccess);
        Assert.Equal("SeparatingAngleBisector", result.SelectedStrategy);
        Assert.True(result.RetainedHalfSpacesOpposed);
        Assert.Equal(new Vector3D(-1,-1,0), result.PlaneNormal);
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StartsWith("ReflexAngleBisector:", StringComparison.Ordinal));
    }

    [Fact]
    public void PictureFrameRail_ReimportedTopProfile_IsIsoscelesTrapezoidNotParallelogram()
    {
        var path = Path.Combine(Root, "fixtures", "Canonical", "Structural", "welded-workbench.firmament");
        var compiled = StructuralAuthoring.Compile(File.ReadAllText(path), path);
        Assert.True(compiled.IsSuccess, string.Join(Environment.NewLine, compiled.Diagnostics));
        var frontRail = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<StructuralMemberIr>>(compiled.RealizedMembers),
            member => member.StableId == "member:FrontRail");
        Assert.True(Math.Abs(frontRail.StartPlane.CenterDepth) < 1e-7,
            $"Unexpected FrontRail start plane: {frontRail.StartPlane}");
        Assert.Equal(1d, frontRail.StartPlane.XCoefficient, 7);
        Assert.Equal(-1d, frontRail.EndPlane.XCoefficient, 7);
        var directBody = frontRail.Body;
        AssertTopProfile(directBody, "direct member BRep");
        var imported = Step242AssemblyImporter.Import(compiled.StepText!);
        Assert.True(imported.IsSuccess);
        var body = Assert.IsType<BrepBody>(imported.Value.Definitions.Single(x => x.Name == "member:FrontRail").Geometry);
        AssertTopProfile(body, "reimported AP242 member definition");

        static void AssertTopProfile(BrepBody body, string stage)
        {
        var top = body.Topology.Faces.Single(face =>
        {
            var surface = body.GetFaceSurface(face.Id);
            return surface.Kind == SurfaceGeometryKind.Plane && surface.Plane!.Value.Normal.Z > .999
                && Math.Abs(surface.Plane.Value.Origin.Z - 820d) < 1e-7;
        });
        var points = top.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Select(id => body.Topology.GetCoedge(id)).Select(coedge => (Coedge: coedge, Edge: body.Topology.GetEdge(coedge.EdgeId)))
            .Select(use => { Assert.True(body.TryGetVertexPoint(use.Coedge.IsReversed ? use.Edge.EndVertexId : use.Edge.StartVertexId, out var point)); return point; })
            .Distinct().ToArray();
        Assert.True(points.Length == 4, $"{stage}: expected four trapezoid vertices, got {points.Length}: {string.Join("; ", points.Select(p => $"({p.X:G17},{p.Y:G17},{p.Z:G17})"))}");
        var lowerY = points.Where(point => Math.Abs(point.Y + 20d) < 1e-7).OrderBy(point => point.X).ToArray();
        var upperY = points.Where(point => Math.Abs(point.Y - 20d) < 1e-7).OrderBy(point => point.X).ToArray();
        Assert.Equal([-20d, 1020d], lowerY.Select(point => point.X).ToArray());
        Assert.True(upperY.Select(point => point.X).SequenceEqual([20d, 980d]),
            $"{stage}: unexpected upper edge; face={string.Join("; ", points.Select(p => $"({p.X:G17},{p.Y:G17},{p.Z:G17})"))}; all-top={string.Join("; ", body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : default).Where(p => Math.Abs(p.Z-820d)<1e-7).Select(p => $"({p.X:G17},{p.Y:G17},{p.Z:G17})"))}");
        Assert.Equal(1040d, lowerY[1].X - lowerY[0].X);
        Assert.Equal(960d, upperY[1].X - upperY[0].X);
        }
    }

    [Fact]
    public void MiterCapSupportPlaneMismatch_ReportsActionableStepAndPictureFrameDiagnostic()
    {
        var path = Path.Combine(Root, "fixtures", "Canonical", "Structural", "welded-workbench.firmament");
        var compiled = StructuralAuthoring.Compile(File.ReadAllText(path), path);
        Assert.True(compiled.IsSuccess, string.Join(Environment.NewLine, compiled.Diagnostics));
        var member = Assert.Single(compiled.RealizedMembers!, item => item.StableId == "member:FrontRail");
        var body = member.Body;
        var startFace = body.Topology.Faces.OrderBy(face => face.Id.Value).First();
        var startBinding = body.Bindings.GetFaceBinding(startFace.Id);
        var geometry = new BrepGeometryStore();
        foreach (var entry in body.Geometry.Curves)
            geometry.AddCurve(entry.Key, entry.Value);
        foreach (var entry in body.Geometry.Surfaces)
        {
            var replacement = entry.Key == startBinding.SurfaceGeometryId
                ? SurfaceGeometry.FromPlane(new PlaneSurface(
                    entry.Value.Plane!.Value.Origin,
                    Direction3D.Create(new Vector3D(-1, -1, 0)),
                    Direction3D.Create(new Vector3D(0, 0, 1))))
                : entry.Value;
            geometry.AddSurface(entry.Key, replacement);
        }
        var points = body.Topology.Vertices.ToDictionary(
            vertex => vertex.Id,
            vertex => { Assert.True(body.TryGetVertexPoint(vertex.Id, out var point)); return point; });
        var malformed = new BrepBody(body.Topology, geometry, body.Bindings, points);

        var diagnostic = Assert.Single(StructuralMemberBrepEmitter.ValidateCapSupportPlanes("FrontRail", malformed));
        Assert.StartsWith("structural-member-cap-support-plane-mismatch:FrontRail:Start:", diagnostic, StringComparison.Ordinal);
        Assert.Contains("STEP resolves vertices from incident support planes", diagnostic, StringComparison.Ordinal);
        Assert.Contains("picture-frame miters into a parallelogram", diagnostic, StringComparison.Ordinal);
        Assert.Contains("normal = -Z + X*Start.XCoefficient + Y*Start.YCoefficient", diagnostic, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zero-length-member.firmament", "structural-zero-length-member-path")]
    [InlineData("invalid-section-dimensions.firmament", "structural-section-invalid-dimensions")]
    [InlineData("wall-thickness-too-large.firmament", "structural-section-wall-too-large")]
    [InlineData("missing-section-orientation.firmament", "structural-member-orientation-required")]
    [InlineData("disconnected-joint.firmament", "structural-disconnected-joint")]
    [InlineData("miter-geometry-unsupported.firmament", "structural-miter-round-section-unsupported")]
    [InlineData("joint-unknown-member.firmament", "structural-joint-unknown-member")]
    public void InvalidStructuralFixtures_IsolateExpectedDiagnostic(string file, string expected)
    {
        var path = Path.Combine(Root, "fixtures", "Invalid", "Structural", file);
        var result = StructuralAuthoring.Compile(File.ReadAllText(path), path);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Contains(expected, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("square-tube-member.firmament")]
    [InlineData("rectangular-tube-member.firmament")]
    [InlineData("oriented-angle-member.firmament")]
    [InlineData("butt-joint.firmament")]
    [InlineData("miter-joint.firmament")]
    [InlineData("simple-structural-frame.firmament")]
    public void CanonicalStructuralFixtures_CompileAndReimport(string file)
    {
        var path = Path.Combine(Root, "fixtures", "Canonical", "Structural", file);
        var result = StructuralAuthoring.Compile(File.ReadAllText(path), path);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.True(Step242AssemblyImporter.Import(result.StepText!).IsSuccess);
    }

    private static string FindRoot() { var d = new DirectoryInfo(AppContext.BaseDirectory); while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent; return d?.FullName ?? throw new DirectoryNotFoundException(); }
}
