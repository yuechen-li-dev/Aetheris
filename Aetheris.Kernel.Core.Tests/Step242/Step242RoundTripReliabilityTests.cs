using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242RoundTripReliabilityTests
{
    [Fact]
    public void RoundTrip_MultiCycle_BoxSubset_TextIsStableAcrossCycles()
    {
        var cycle0 = Step242FixtureCorpus.CanonicalBoxGolden;
        var cycle1 = RoundTripExport(cycle0);
        var cycle2 = RoundTripExport(cycle1);
        var cycle3 = RoundTripExport(cycle2);

        Assert.Equal(cycle0, cycle1);
        Assert.Equal(cycle1, cycle2);
        Assert.Equal(cycle2, cycle3);
    }

    [Fact]
    public void RoundTrip_BoxSubset_PreservesStructuralInvariants_AndSmokeOperations()
    {
        var original = BrepPrimitives.CreateBox(4d, 6d, 8d);
        Assert.True(original.IsSuccess);

        var imported = Import(Step242Exporter.ExportBody(original.Value).Value);

        AssertTopologyInvariantMatch(original.Value, imported);
        AssertGeometryKindsAreSubset(imported);
        Assert.Equal(6, imported.Topology.Faces.Count());
        Assert.Equal(12, imported.Topology.Edges.Count());

        var tessellation = BrepDisplayTessellator.Tessellate(imported);
        Assert.True(tessellation.IsSuccess);
        Assert.NotEmpty(tessellation.Value.FacePatches);
        Assert.NotEmpty(tessellation.Value.EdgePolylines);

        var ray = new Ray3D(new Point3D(0d, 0d, 6d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var pick = BrepPicker.Pick(imported, tessellation.Value, ray, PickQueryOptions.Default with { NearestOnly = true });
        Assert.True(pick.IsSuccess);
        Assert.Single(pick.Value);
    }

    [Fact]
    public void ImportBody_BrokenReference_ReturnsDeterministicDiagnosticWithoutThrowing()
    {
        var brokenReference = CorruptManifoldShellReference(Step242FixtureCorpus.CanonicalBoxGolden);

        var import = Step242Importer.ImportBody(brokenReference);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Entity:9999", diagnostic.Source);
        Assert.Contains("Missing referenced entity", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExportBody_SameImportedBody_ProducesDeterministicText()
    {
        var imported = Import(Step242FixtureCorpus.CanonicalBoxGolden);

        var first = Step242Exporter.ExportBody(imported);
        var second = Step242Exporter.ExportBody(imported);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
    }

    private static BrepBody Import(string stepText)
    {
        var import = Step242Importer.ImportBody(stepText);
        Assert.True(import.IsSuccess);
        return import.Value;
    }

    private static string RoundTripExport(string inputStep)
    {
        var body = Import(inputStep);
        var export = Step242Exporter.ExportBody(body);
        Assert.True(export.IsSuccess);
        return export.Value;
    }

    private static void AssertTopologyInvariantMatch(BrepBody expected, BrepBody actual)
    {
        Assert.Equal(expected.Topology.Bodies.Count(), actual.Topology.Bodies.Count());
        Assert.Equal(expected.Topology.Shells.Count(), actual.Topology.Shells.Count());
        Assert.Equal(expected.Topology.Faces.Count(), actual.Topology.Faces.Count());
        Assert.Equal(expected.Topology.Loops.Count(), actual.Topology.Loops.Count());
        Assert.Equal(expected.Topology.Coedges.Count(), actual.Topology.Coedges.Count());
        Assert.Equal(expected.Topology.Edges.Count(), actual.Topology.Edges.Count());
        Assert.Equal(expected.Topology.Vertices.Count(), actual.Topology.Vertices.Count());
    }

    private static void AssertGeometryKindsAreSubset(BrepBody body)
    {
        Assert.All(body.Geometry.Curves, curve => Assert.Equal(CurveGeometryKind.Line3, curve.Value.Kind));
        Assert.All(body.Geometry.Surfaces, surface => Assert.Equal(SurfaceGeometryKind.Plane, surface.Value.Kind));
    }

    private static string CorruptManifoldShellReference(string stepText)
    {
        return Regex.Replace(
            stepText,
            "(MANIFOLD_SOLID_BREP\\('.*?',)#\\d+(\\);)",
            "$1#9999$2",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
    }
}
