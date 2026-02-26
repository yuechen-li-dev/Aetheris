using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Results;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242RoundTripReliabilityTests
{
    [Fact]
    public void RoundTripReliability_SingleCycleCanonicalBox_ValidatesTessellatesAndPicks()
    {
        var export = Step242RoundTripFixtureCorpus.CreateCanonicalBoxStepText();
        Assert.True(export.IsSuccess);

        var import = Step242Importer.ImportBody(export.Value);

        Assert.True(import.IsSuccess);
        AssertBodySubsetInvariants(import.Value);
        AssertTessellationAndPickingSmoke(import.Value);
    }

    [Fact]
    public void RoundTripReliability_MultiCycleCanonicalBox_CanonicalTextStabilizesAfterOneImportExportCycle()
    {
        var cycle0Export = Step242RoundTripFixtureCorpus.CreateCanonicalBoxStepText();
        Assert.True(cycle0Export.IsSuccess);

        var cycle1Import = Step242Importer.ImportBody(cycle0Export.Value);
        Assert.True(cycle1Import.IsSuccess);
        AssertBodySubsetInvariants(cycle1Import.Value);

        var cycle1Export = Step242Exporter.ExportBody(cycle1Import.Value);
        Assert.True(cycle1Export.IsSuccess);

        var cycle2Import = Step242Importer.ImportBody(cycle1Export.Value);
        Assert.True(cycle2Import.IsSuccess);
        AssertBodySubsetInvariants(cycle2Import.Value);

        var cycle2Export = Step242Exporter.ExportBody(cycle2Import.Value);
        Assert.True(cycle2Export.IsSuccess);

        var cycle3Import = Step242Importer.ImportBody(cycle2Export.Value);
        Assert.True(cycle3Import.IsSuccess);
        AssertBodySubsetInvariants(cycle3Import.Value);

        var cycle3Export = Step242Exporter.ExportBody(cycle3Import.Value);
        Assert.True(cycle3Export.IsSuccess);

        // Canonicalization claim for M24: output is deterministic and byte-stable once the body
        // has been normalized by one import/export cycle.
        Assert.Equal(cycle1Export.Value, cycle2Export.Value);
        Assert.Equal(cycle2Export.Value, cycle3Export.Value);

        AssertTessellationAndPickingSmoke(cycle3Import.Value);
    }

    [Fact]
    public void RoundTripReliability_DeterministicImportExportFromSameStepInput_ReturnsSameCanonicalText()
    {
        var fixture = Step242RoundTripFixtureCorpus.CreateCanonicalBoxStepText();
        Assert.True(fixture.IsSuccess);

        var first = ImportThenExport(fixture.Value);
        var second = ImportThenExport(fixture.Value);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void ImportBody_BrokenReferenceInParseableText_ReturnsDeterministicDiagnostic()
    {
        var brokenReferenceText = """
ISO-10303-21;
HEADER;
ENDSEC;
DATA;
#1=CARTESIAN_POINT($,(0,0,0));
#2=VERTEX_POINT($,#1);
#3=CARTESIAN_POINT($,(1,0,0));
#4=VERTEX_POINT($,#3);
#5=CARTESIAN_POINT($,(0,0,0));
#6=DIRECTION($,(1,0,0));
#7=VECTOR($,#6,1);
#8=LINE($,#5,#7);
#9=EDGE_CURVE($,#2,#4,#8,.T.);
#10=ORIENTED_EDGE($,$,$,#9,.T.);
#11=EDGE_LOOP($,(#10));
#12=FACE_OUTER_BOUND($,#11,.T.);
#13=CARTESIAN_POINT($,(0,0,0));
#14=DIRECTION($,(0,0,1));
#15=DIRECTION($,(1,0,0));
#16=AXIS2_PLACEMENT_3D($,#13,#14,#15);
#17=PLANE($,#16);
#18=ADVANCED_FACE((#12),#17,.T.);
#19=CLOSED_SHELL($,(#18));
#20=MANIFOLD_SOLID_BREP('x',#999);
ENDSEC;
END-ISO-10303-21;
""";

        var import = Step242Importer.ImportBody(brokenReferenceText);

        Assert.False(import.IsSuccess);
        var diagnostic = Assert.Single(import.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal(KernelDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("Entity:999", diagnostic.Source);
        Assert.Contains("#999", diagnostic.Message, StringComparison.Ordinal);
    }

    private static void AssertBodySubsetInvariants(BrepBody body)
    {
        var bodies = body.Topology.Bodies.ToArray();
        var shells = body.Topology.Shells.ToArray();
        var faces = body.Topology.Faces.ToArray();
        var edges = body.Topology.Edges.ToArray();
        var vertices = body.Topology.Vertices.ToArray();
        var faceBindings = body.Bindings.FaceBindings.ToArray();
        var edgeBindings = body.Bindings.EdgeBindings.ToArray();

        Assert.Single(bodies);
        Assert.Single(shells);
        Assert.Equal(6, faces.Length);
        Assert.Equal(6, faceBindings.Length);
        Assert.Equal(12, edges.Length);
        Assert.Equal(12, edgeBindings.Length);
        Assert.Equal(8, vertices.Length);

        foreach (var faceBinding in faceBindings)
        {
            var surface = body.Geometry.GetSurface(faceBinding.SurfaceGeometryId);
            Assert.Equal(SurfaceGeometryKind.Plane, surface.Kind);
        }

        foreach (var edgeBinding in edgeBindings)
        {
            var curve = body.Geometry.GetCurve(edgeBinding.CurveGeometryId);
            Assert.Equal(CurveGeometryKind.Line3, curve.Kind);
        }
    }

    private static void AssertTessellationAndPickingSmoke(BrepBody body)
    {
        var tessellation = BrepDisplayTessellator.Tessellate(body);
        Assert.True(tessellation.IsSuccess);
        Assert.Equal(6, tessellation.Value.FacePatches.Count);

        var ray = new Ray3D(new Point3D(0d, 0d, 3d), Direction3D.Create(new Vector3D(0d, 0d, -1d)));
        var pick = BrepPicker.Pick(body, tessellation.Value, ray, PickQueryOptions.Default with { NearestOnly = true });

        Assert.True(pick.IsSuccess);
        Assert.Single(pick.Value, hit => hit.EntityKind == SelectionEntityKind.Face);
    }

    private static KernelResult<string> ImportThenExport(string stepText)
    {
        var import = Step242Importer.ImportBody(stepText);
        if (!import.IsSuccess)
        {
            return KernelResult<string>.Failure(import.Diagnostics);
        }

        return Step242Exporter.ExportBody(import.Value);
    }
}

internal static class Step242RoundTripFixtureCorpus
{
    // M24 generated golden fixture for the supported subset. We intentionally build this from the
    // canonical CreateBox primitive so the scenario remains small and easy to regenerate in tests.
    public static KernelResult<string> CreateCanonicalBoxStepText()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d);
        if (!box.IsSuccess)
        {
            return KernelResult<string>.Failure(box.Diagnostics);
        }

        return Step242Exporter.ExportBody(box.Value, new Step242ExportOptions
        {
            ProductName = "M24-CANONICAL-BOX",
            ApplicationName = "Aetheris M24 Fixture"
        });
    }
}
