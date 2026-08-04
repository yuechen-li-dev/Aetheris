using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.EdgeFinishing;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.EdgeFinishing;

public sealed class BrepBoundedChamferCornerStepEvidenceTests
{
    [Fact]
    public void LegacySyntheticThreeEdgeConvexVertex_FailsEnforcedPreflight()
    {
        var result = BrepBoundedChamfer.ChamferAxisAlignedBoxSingleCorner(
            new(-10, 10, -10, 10, -10, 10),
            BrepBoundedChamferCorner.XMaxYMaxZMax,
            1);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal((10, 15, 7), (result.Value.Topology.Vertices.Count(), result.Value.Topology.Edges.Count(), result.Value.Topology.Faces.Count()));
        var preflight = BrepExportPreflight.Validate(result.Value);
        Assert.False(preflight.IsValid);
        Assert.Contains(preflight.Diagnostics, diagnostic => diagnostic.Code == "brep-preflight-coedge-disconnected");
        var exported = Step242Exporter.ExportBody(result.Value, new Step242ExportOptions
        {
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        Assert.False(exported.IsSuccess);
    }

    [Fact]
    public void LegacyTwoEdgeConvexJunction_PassesAetherisRoundTrip_ButIsNotModernAdmission()
    {
        var source = BrepPrimitives.CreateBox(20, 20, 20);
        Assert.True(source.IsSuccess);
        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralIncidentEdgePair(
            source.Value,
            new(BrepBoundedChamferCorner.XMaxYMaxZMax, BrepBoundedChamferCornerIncidentEdge.XNegative, BrepBoundedChamferCornerIncidentEdge.YNegative),
            1);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal((10, 15, 7), (result.Value.Topology.Vertices.Count(), result.Value.Topology.Edges.Count(), result.Value.Topology.Faces.Count()));
        AssertRoundTrip(result.Value, 10, 15, 7, "legacy-two-edge-convex-junction.step");
    }

    [Fact]
    public void LegacyThreeEdgeConvexVertex_PassesAetherisRoundTrip_ButIsNotModernAdmission()
    {
        var source = BrepPrimitives.CreateTriangularPrism(8, 6, 10);
        Assert.True(source.IsSuccess);
        var result = BrepBoundedChamfer.ChamferTrustedPolyhedralSingleCorner(source.Value, BrepBoundedChamferCorner.XMaxYMaxZMax, 1);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.Message)));
        Assert.Equal((8, 12, 6), (result.Value.Topology.Vertices.Count(), result.Value.Topology.Edges.Count(), result.Value.Topology.Faces.Count()));
        AssertRoundTrip(result.Value, 8, 12, 6, "legacy-three-edge-convex-vertex.step");
    }

    private static void AssertRoundTrip(BrepBody body, int vertices, int edges, int faces, string artifactName)
    {
        var preflight = BrepExportPreflight.Validate(body);
        Assert.True(preflight.IsValid, string.Join(Environment.NewLine, preflight.Diagnostics.Select(d => d.Message)));
        var exported = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = "AIR-CHAMFER-CORNER-POLICY-A0",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        Assert.True(exported.IsSuccess, string.Join(Environment.NewLine, exported.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(SHA256.HashData(Encoding.UTF8.GetBytes(exported.Value)));
        var artifactDirectory = Environment.GetEnvironmentVariable("AETHERIS_CORNER_ARTIFACT_DIR");
        if (!string.IsNullOrWhiteSpace(artifactDirectory))
        {
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(Path.Combine(artifactDirectory, artifactName), exported.Value, Encoding.UTF8);
        }
        var imported = Step242Importer.ImportBody(exported.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(d => d.Message)));
        Assert.Equal((vertices, edges, faces), (imported.Value.Topology.Vertices.Count(), imported.Value.Topology.Edges.Count(), imported.Value.Topology.Faces.Count()));
    }
}
