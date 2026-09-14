using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

[CollectionDefinition("StorageGateMeshing", DisableParallelization = true)]
public sealed class StorageGateMeshingCollection;

// Keep wall-clock-bounded production meshing outside competing xUnit collections.
// The exporter retains its ordinary five-second budget.
[Collection("StorageGateMeshing")]
public sealed class DifferenceEngineStorageGateTests
{
    [Fact]
    public void MixedCatalogBuildsTheRequestedShaftAndReusesItsDefinition()
    {
        var result = Compile("gear-with-ordinary-part");
        Assert.Equal(2, result.Geometry!.DefinitionBodies.Count);
        var shaft = result.Geometry.DefinitionBodies.Single(x => x.Key.StartsWith("Shaft<", StringComparison.Ordinal)).Value;
        Assert.Equal(3, shaft.Topology.Faces.Count());
        var cylinder = Assert.Single(shaft.Geometry.Surfaces.Select(p => p.Value), x => x.Kind == SurfaceGeometryKind.Cylinder).Cylinder!.Value;
        Assert.Equal(4, cylinder.Radius, 9);
        Assert.DoesNotContain(shaft.Geometry.Surfaces.Select(p => p.Value), x => x.Kind == SurfaceGeometryKind.LinearExtrusion);
        var z = shaft.Topology.Vertices.Select(v => { Assert.True(shaft.TryGetVertexPoint(v.Id, out var p)); return p.Z; }).ToArray();
        Assert.Equal(0, z.Min(), 9); Assert.Equal(40, z.Max(), 9);
        var output = AssemblyIrAp242Exporter.Export(result);
        Assert.True(output.IsSuccess);
        var imported = Step242AssemblyImporter.Import(output.Value);
        Assert.True(imported.IsSuccess);
        Assert.Equal(6, imported.Value.Occurrences.Count);
        Assert.Equal(4, imported.Value.Definitions.Count);
        Assert.Equal(output.Value, AssemblyIrAp242Exporter.Export(Compile("gear-with-ordinary-part")).Value);
    }

    [Fact]
    public void FullCircleCompositionProducesTenRealCylindricalIndexHoles()
    {
        var result = Compile("index-plate-with-gear");
        var plate = result.Geometry!.DefinitionBodies.Single(x => x.Key.StartsWith("DecimalIndexPlate<", StringComparison.Ordinal)).Value;
        var holes = plate.Geometry.Surfaces.Select(p => p.Value).Where(s => s.Cylinder is { Radius: > 2.19 and < 2.21 })
            .Select(s => s.Cylinder!.Value).ToArray();
        var centers = holes.Select(c => (X: double.Round(c.Origin.X, 7), Y: double.Round(c.Origin.Y, 7))).Distinct().ToArray();
        Assert.Equal(10, centers.Length);
        foreach (var center in centers) Assert.Equal(26, double.Sqrt(center.X * center.X + center.Y * center.Y), 6);
        var mesh = AssemblyDisplayMeshExporter.Export(result);
        var definition = mesh.Definitions.Single(d => d.Identity.StartsWith("DecimalIndexPlate<", StringComparison.Ordinal));
        Assert.Equal(DisplayMeshPipeline.SurfaceMeshIr.ToString(), definition.MeshPipeline);
        Assert.True(SurfaceMeshIrTessellator.TryBuild(plate,
            SurfaceMeshPolicy.FromDisplayOptions(new DisplayTessellationOptions(double.Pi / 16, .3, 6, 64)), out var document));
        Assert.True(SurfaceMeshIrValidator.TryValidate(document, out _));
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var sharedMesh, out var topology));
        Assert.True(topology.IsWatertight);
        Assert.True(topology.IsConnected);
        Assert.True(topology.IsOutwardOriented);
        Assert.Equal(0, topology.CrackCount);
        Assert.Equal(0, topology.NonManifoldEdgeCount);
        Assert.Equal(0, topology.DuplicateTriangleCount);
        Assert.Equal(0, topology.ZeroAreaTriangleCount);
        Assert.Equal(sharedMesh.TriangleIndices.Count, definition.Indices.Length);
        // The assembly adapter must not apply face sense twice to the already
        // oriented IR patches. Both caps and inward-facing bores stay consistent.
        for (var i = 0; i < definition.Indices.Length; i += 3)
        {
            var a = definition.Indices[i]; var b = definition.Indices[i + 1]; var c = definition.Indices[i + 2];
            var normal = new Vector3D(definition.Normals[a * 3], definition.Normals[a * 3 + 1], definition.Normals[a * 3 + 2]);
            Assert.True((At(definition, b) - At(definition, a)).Cross(At(definition, c) - At(definition, a)).Dot(normal) > 0);
        }
        var expected = double.Pi * (32 * 32 - 6.15 * 6.15 - 10 * 2.2 * 2.2) * 6;
        Assert.InRange(Volume(definition) / expected, .98, 1.02);
        Assert.Equal(AssemblyDisplayMeshExporter.Serialize(mesh), AssemblyDisplayMeshExporter.Serialize(AssemblyDisplayMeshExporter.Export(Compile("index-plate-with-gear"))));
    }

    [Fact]
    public void GearMeshIncludesEveryFlankWithOutwardNormalsAndClosedVolume()
    {
        var result = Compile("gear-with-ordinary-part");
        var gear = result.Geometry!.DefinitionBodies["Drive"];
        Assert.Contains(gear.Geometry.Surfaces.Select(p => p.Value), s => s.Kind == SurfaceGeometryKind.LinearExtrusion);
        var tessellation = BrepDisplayTessellator.Tessellate(gear);
        Assert.True(tessellation.IsSuccess, string.Join("\n", tessellation.Diagnostics.Select(d => d.Message)));
        Assert.Equal(gear.Topology.Faces.Count(), tessellation.Value.FacePatches.Count);
        foreach (var patch in tessellation.Value.FacePatches)
        {
            Assert.NotEmpty(patch.TriangleIndices);
            Assert.All(patch.Normals, n => Assert.InRange(n.LengthSquared, .9999, 1.0001));
            Assert.True(gear.TryGetFaceSurfaceGeometry(patch.FaceId, out var surface));
            if (surface!.Kind == SurfaceGeometryKind.LinearExtrusion)
                Assert.All(patch.Normals, n => Assert.InRange(double.Abs(n.Z), 0, 1e-7));
        }
        var mesh = AssemblyDisplayMeshExporter.Export(result).Definitions.Single(d => d.Identity == "Drive");
        var volume = Volume(mesh);
        Assert.InRange(volume, double.Pi * (17.5 * 17.5 - 4.2 * 4.2) * 8, double.Pi * (22 * 22 - 4.2 * 4.2) * 8);
        // For a prism, the oriented surface integral must agree with cap area * height.
        // This detects a "successful" mesh with inward/missing/degenerate side normals.
        double capArea = 0;
        for (var i = 0; i < mesh.Indices.Length; i += 3)
        {
            var a = At(mesh, mesh.Indices[i]); var b = At(mesh, mesh.Indices[i + 1]); var c = At(mesh, mesh.Indices[i + 2]);
            if (double.Abs(a.Z - 8) < 1e-9 && double.Abs(b.Z - 8) < 1e-9 && double.Abs(c.Z - 8) < 1e-9)
                capArea += (b - a).Cross(c - a).Z / 2;
        }
        Assert.InRange(volume / (capArea * 8), .995, 1.005);
    }

    private static AssemblyM1CompilationResult Compile(string name)
    {
        var result = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath($"fixtures/Canonical/AssemblyInterfaces/{name}.firmament"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        return result;
    }
    private static Vector3D At(AssemblyDisplayMeshDefinition m, int i) => new(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]);
    private static double Volume(AssemblyDisplayMeshDefinition m)
    {
        double volume = 0;
        for (var i = 0; i < m.Indices.Length; i += 3)
            volume += At(m, m.Indices[i]).Dot(At(m, m.Indices[i + 1]).Cross(At(m, m.Indices[i + 2]))) / 6;
        return volume;
    }
}
