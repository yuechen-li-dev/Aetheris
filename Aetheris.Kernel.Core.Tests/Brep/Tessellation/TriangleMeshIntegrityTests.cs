using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Tests.Step242;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class TriangleMeshIntegrityTests
{
    private static TriangleMesh Tetrahedron() => new(
        [new(0, 0, 0), new(2, 0, 0), new(0, 2, 0), new(0, 0, 2)], [],
        [0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3], new HashSet<(int, int)>(), "test");

    [Fact]
    public void PositiveVolumeAndTwoUsesPerEdgeDoNotProveOrientation()
    {
        var mesh = Tetrahedron(); var indices = mesh.TriangleIndices.ToArray();
        (indices[1], indices[2]) = (indices[2], indices[1]);
        Assert.False(TriangleMeshValidator.TryValidateClosed(mesh with { TriangleIndices = indices }, out var report, out var failure));
        Assert.Equal(0, report.CrackCount); Assert.Equal(0, report.NonManifoldEdgeCount);
        Assert.True(report.SignedVolume > 0);
        Assert.Equal(3, report.OrientationConflictEdgeCount);
        Assert.False(report.IsOutwardOriented); Assert.False(report.HasValidSignedVolume);
        Assert.Contains("orientationConflicts=3", failure);
    }

    [Fact]
    public void ClosedVolumeRemainsStableFarFromWorldOrigin()
    {
        var mesh = Tetrahedron();
        foreach (var translation in new[] { new Vector3D(0, 0, 0), new Vector3D(1e12, -1e12, 1e12) })
        {
            var moved = mesh with { Positions = mesh.Positions.Select(p => p + translation).ToArray() };
            Assert.True(TriangleMeshValidator.TryValidateClosed(moved, out var report, out var failure), failure);
            Assert.Equal(8d / 6, report.SignedVolume, 12);
            Assert.True(report.IsConsistentlyOriented); Assert.True(report.HasValidSignedVolume);
        }
    }

    [Fact]
    public void EntirelyInwardClosedMeshIsNotReportedOutward()
    {
        var mesh = Tetrahedron();
        var indices = mesh.TriangleIndices.Chunk(3).SelectMany(t => new[] { t[0], t[2], t[1] }).ToArray();
        Assert.False(TriangleMeshValidator.TryValidateClosed(mesh with { TriangleIndices = indices }, out var report, out _));
        Assert.True(report.IsConsistentlyOriented); Assert.True(report.HasValidSignedVolume);
        Assert.False(report.IsOutwardOriented); Assert.True(report.SignedVolume < 0);
    }

    [Fact]
    public void StlPrecisionFailureCannotReplaceAnExistingFile()
    {
        var folder = Step242Corpus.Path("artifacts/local/stl-integrity/tests"); Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, Guid.NewGuid() + ".stl");
        File.WriteAllText(file, "preserve previous artifact");
        try
        {
            var mesh = Tetrahedron();
            mesh = mesh with { Positions = mesh.Positions.Select(p => p + new Vector3D(1e9, 1e9, 1e9)).ToArray() };
            var error = Assert.Throws<InvalidOperationException>(() => BinaryStlExporter.Export(file, mesh));
            Assert.Contains("float32", error.Message);
            Assert.Equal("preserve previous artifact", File.ReadAllText(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void Ctc01HasOppositeDirectedUsesAndOriginIndependentVolume()
    {
        var body = Step242Corpus.Body("testdata/step242/nist/CTC/nist_ctc_01_asme1_ap242-e1.stp");
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document, out var failure), failure);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out var report));
        Assert.Equal(0, report.OrientationConflictEdgeCount);
        var edges = mesh.TriangleIndices.Chunk(3).SelectMany(t => new[] { (t[0], t[1]), (t[1], t[2]), (t[2], t[0]) })
            .GroupBy(e => e).ToDictionary(g => g.Key, g => g.Count());
        Assert.All(edges, pair => Assert.Equal(pair.Value, edges.GetValueOrDefault((pair.Key.Item2, pair.Key.Item1))));
        foreach (var origin in new[] { new Point3D(0, 0, 0), new Point3D(1000, 2000, 3000), new Point3D(-1000, -2000, -3000) })
        {
            var volume = mesh.TriangleIndices.Chunk(3).Sum(t => (mesh.Positions[t[0]] - origin)
                .Dot((mesh.Positions[t[1]] - origin).Cross(mesh.Positions[t[2]] - origin)) / 6);
            Assert.InRange(double.Abs(volume - report.SignedVolume), 0, .001);
        }
        // Frozen independent STL witness: the old mixed winding produced 15.44M.
        Assert.InRange(report.SignedVolume, 14_640_000, 14_655_000);
    }
}

