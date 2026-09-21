using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

/// <summary>
/// An Aetheris export has to be readable by Aetheris. That sounds tautological, and it was false: the exporter
/// rebuilt every LINE from its endpoints, so the emitted curve ran start-vertex to end-vertex by construction, and
/// then wrote the stored EDGE_CURVE.same_sense anyway. On the edges where that flag said otherwise, a reader walked
/// the edge backwards and the loop's consecutive coedges landed an edge-length apart.
/// <para>
/// Face and edge counts alone would not have caught it - the topology survives - so the volume of the display mesh
/// is compared too: a loop walked backwards changes the shape, not the counts.
/// </para>
/// </summary>
public sealed class Step242RoundTripCorpusTests
{
    public static TheoryData<string> CorpusFiles
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory
                .EnumerateFiles(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist"), "*.stp", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(path));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CorpusFiles))]
    public void ExportedBody_IsReadableByAetheris_AndKeepsItsShape(string fileName)
    {
        var source = Step242Importer.ImportBody(ReadCorpusFile(fileName));
        Assert.True(source.IsSuccess, string.Join(" | ", source.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var exported = Step242Exporter.ExportBody(source.Value);
        Assert.True(exported.IsSuccess, string.Join(" | ", exported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        var reimported = Step242Importer.ImportBody(exported.Value);
        Assert.True(reimported.IsSuccess, string.Join(" | ", reimported.Diagnostics.Select(diagnostic => diagnostic.Message)));

        Assert.Equal(source.Value.Topology.Faces.Count(), reimported.Value.Topology.Faces.Count());
        Assert.Equal(source.Value.Topology.Edges.Count(), reimported.Value.Topology.Edges.Count());

        var before = EnclosedVolume(source.Value);
        var after = EnclosedVolume(reimported.Value);
        Assert.True(
            double.Abs(after - before) <= 0.01d * double.Max(double.Abs(before), 1d),
            $"round trip changed the enclosed volume from {before:G6} to {after:G6}");
    }

    /// <summary>Signed volume of the display mesh, which changes if any loop is walked in the wrong direction.</summary>
    private static double EnclosedVolume(BrepBody body)
    {
        var display = DisplayPreparationFallbackBuilder.Build(body, null);
        Assert.True(display.IsSuccess);

        var volume = 0d;
        foreach (var patch in display.Value.FacePatches)
        {
            for (var index = 0; index + 2 < patch.TriangleIndices.Count; index += 3)
            {
                var a = patch.Positions[patch.TriangleIndices[index]] - Point3D.Origin;
                var b = patch.Positions[patch.TriangleIndices[index + 1]] - Point3D.Origin;
                var c = patch.Positions[patch.TriangleIndices[index + 2]] - Point3D.Origin;
                volume += a.Cross(b).Dot(c) / 6d;
            }
        }

        return volume;
    }

    private static string ReadCorpusFile(string fileName)
        => File.ReadAllText(Directory
            .EnumerateFiles(Path.Combine(Step242CorpusManifestRunner.RepoRoot(), "testdata", "step242", "nist"), fileName, SearchOption.AllDirectories)
            .Single());
}
