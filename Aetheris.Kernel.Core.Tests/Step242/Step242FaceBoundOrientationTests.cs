using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>FACE_BOUND orientation .F. means the face uses its EDGE_LOOP reversed.</summary>
public sealed class Step242FaceBoundOrientationTests
{
    // 405 of CTC-04's bounds are .F.; its inside pocket corners are concave sphere octants.
    private const string Ctc04 = "testdata/step242/nist/CTC/nist_ctc_04_asme1_ap242-e1.stp";

    [Fact]
    public void ReversedBounds_KeepConcaveSphereCornersAsOctants_ThroughExportAndReimport()
    {
        var imported = Step242Corpus.Body(Ctc04);
        AssertSphereCornersAreOctants(imported);

        var exported = Step242Exporter.ExportBody(imported);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics.Select(d => d.Message)));
        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join("\n", reimported.Diagnostics.Select(d => d.Message)));
        AssertSphereCornersAreOctants(reimported.Value);
    }

    private static void AssertSphereCornersAreOctants(BrepBody body)
    {
        var mesh = BrepDisplayTessellator.Tessellate(body);
        Assert.True(mesh.IsSuccess);
        var spheres = 0;
        foreach (var patch in mesh.Value.FacePatches)
        {
            if (!body.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface) || surface!.Kind != SurfaceGeometryKind.Sphere) continue;
            spheres++;
            var area = Enumerable.Range(0, patch.TriangleIndices.Count / 3).Sum(t =>
            {
                var a = patch.Positions[patch.TriangleIndices[3 * t]];
                var b = patch.Positions[patch.TriangleIndices[3 * t + 1]];
                var c = patch.Positions[patch.TriangleIndices[3 * t + 2]];
                return (b - a).Cross(c - a).Length / 2;
            });
            var r = surface.Sphere!.Value.Radius;
            // A corner patch is at most an octant; a reversed loop draws the other seven eighths.
            Assert.True(area <= 4 * System.Math.PI * r * r / 8 * 1.05, $"sphere face {patch.FaceId.Value}: area {area:F2} exceeds an octant of r={r}");
        }
        Assert.Equal(22, spheres);
    }
}
