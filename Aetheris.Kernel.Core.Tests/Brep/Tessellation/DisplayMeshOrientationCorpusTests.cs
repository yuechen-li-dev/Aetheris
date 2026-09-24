using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

/// <summary>
/// A display mesh of a closed solid has to be outward-oriented as a whole, not per face. The cheapest total check is
/// the divergence theorem: summing the signed tetrahedron volume of every triangle gives the part's volume when the
/// winding is consistently outward, and gives nonsense - frequently a negative number - when it is not.
/// <para>
/// This is worth pinning because it was false for years without anything noticing. Face sense was projected nowhere,
/// so each face carried whatever its own parameterization produced, and the corpus came out negative on seven files
/// of seventeen. Planes looked right only because import folded the sense into the plane support itself, which made
/// them the one kind every consumer had to special-case.
/// </para>
/// </summary>
public sealed class DisplayMeshOrientationCorpusTests
{
    public static TheoryData<string> ClosedBrepFiles => new()
    {
        "testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp",
        "testdata/step242/nist/STC/nist_stc_06_asme1_ap242-e3.stp",
    };

    [Theory]
    [MemberData(nameof(ClosedBrepFiles))]
    [Trait("Category", "SlowCorpus")]
    public void DisplayMesh_OfAClosedBody_IsOutwardOriented(string relativePath)
    {
        var import = Step242Importer.ImportBody(ReadFixture(relativePath));
        Assert.True(import.IsSuccess);

        var display = DisplayPreparationFallbackBuilder.Build(import.Value, null, null, TimeSpan.FromSeconds(30));
        Assert.True(display.IsSuccess);
        AssertOutwardOrientation(import.Value, import.Diagnostics, display.Value);
    }

    internal static void AssertOutwardOrientation(BrepBody body, IReadOnlyList<KernelDiagnostic> diagnostics, DisplayTessellationResult display)
    {
        var unresolvedShells = body.FaceOrientationReport?.Shells
            .Where(shell => shell.Qualification == FaceOrientationQualification.DerivedLocallyConsistentGlobalUnknown)
            .ToArray() ?? [];
        if (unresolvedShells.Length > 0)
        {
            Assert.All(unresolvedShells, shell => Assert.False(shell.IsClosedManifold));
            Assert.Contains(diagnostics, diagnostic =>
                diagnostic.Source == "Importer.StepOrientation.GlobalOrientationUnknown");
            return;
        }

        var (windingVolume, normalVolume) = MeasureVolumes(display);

        Assert.True(windingVolume > 0d, $"triangle winding encloses a negative volume ({windingVolume:G6}), so the mesh is not outward-oriented");
        // Winding and stored normals are two independent statements of the same orientation; they have to agree.
        Assert.True(
            double.Abs(windingVolume - normalVolume) <= 0.02d * windingVolume,
            $"winding volume {windingVolume:G6} and stored-normal volume {normalVolume:G6} disagree");
    }

    private static (double WindingVolume, double NormalVolume) MeasureVolumes(DisplayTessellationResult display)
    {
        var windingVolume = 0d;
        var normalVolume = 0d;
        foreach (var patch in display.FacePatches)
        {
            for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[index]];
                var b = patch.Positions[patch.TriangleIndices[index + 1]];
                var c = patch.Positions[patch.TriangleIndices[index + 2]];
                var va = a - Point3D.Origin;
                var vb = b - Point3D.Origin;
                var vc = c - Point3D.Origin;
                windingVolume += va.Cross(vb).Dot(vc) / 6d;

                var area = (b - a).Cross(c - a).Length / 2d;
                var summed = patch.Normals[patch.TriangleIndices[index]]
                    + patch.Normals[patch.TriangleIndices[index + 1]]
                    + patch.Normals[patch.TriangleIndices[index + 2]];
                if (!summed.TryNormalize(out var normal))
                {
                    continue;
                }

                var centroid = new Vector3D((a.X + b.X + c.X) / 3d, (a.Y + b.Y + c.Y) / 3d, (a.Z + b.Z + c.Z) / 3d);
                normalVolume += centroid.Dot(normal) * area / 3d;
            }
        }

        return (windingVolume, normalVolume);
    }

    private static string ReadFixture(string relativePath)
        => File.ReadAllText(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
