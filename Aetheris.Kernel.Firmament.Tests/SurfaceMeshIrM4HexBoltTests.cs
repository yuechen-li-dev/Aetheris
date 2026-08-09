using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SurfaceMeshIrM4HexBoltTests
{
    [Fact]
    public void HexBolt_UsesOnlySharedBoundedConicAndRootFilletPatches()
    {
        var bolt = HexBoltBuilder.Create(McMasterHexBoltSpecs.Reference91180A151, "surface-mesh-ir-m4").Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(bolt.Body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        Assert.True(SurfaceMeshIrValidator.TryValidate(document, out var failure), failure);

        var cones = document.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Cone).ToArray();
        var roots = document.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Torus).ToArray();
        Assert.Equal(8, cones.Length); // six head sectors and two tip sectors
        Assert.Equal(2, roots.Length);
        Assert.All(cones.Concat(roots), patch => Assert.All(patch.Cells, cell => Assert.IsType<QuadCell>(cell)));

        var hyperbolas = document.SharedBoundaries.Where(boundary => boundary.CurveKind == CurveGeometryKind.Hyperbola3).ToArray();
        Assert.Equal(6, hyperbolas.Length);
        Assert.All(hyperbolas, boundary =>
        {
            Assert.Equal(2, boundary.Uses.Count);
            var consumers = document.Patches.Where(patch => patch.Cells.SelectMany(cell => cell.VertexIds).Intersect(boundary.Samples.Select(sample => sample.Id)).Count() == boundary.Samples.Count).ToArray();
            Assert.Equal(2, consumers.Length);
        });

        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out var topology));
        Assert.True(topology.IsWatertight);
        Assert.True(topology.IsOutwardOriented);
        Assert.Equal(0, topology.NonManifoldEdgeCount);
        Assert.Equal(0, topology.CrackCount);
        Assert.NotEmpty(mesh.HardEdges);
    }

    [Fact]
    public void HexBolt_RootFilletRespectsDirectedContactBoundaries_Deterministically()
    {
        var body = HexBoltBuilder.Create(McMasterHexBoltSpecs.Reference91180A151, "root-edge-sense").Value.Body;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var first));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var second));
        Assert.Equal(first.Metrics.DeterministicHash, second.Metrics.DeterministicHash);
        Assert.All(first.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Torus), patch =>
        {
            Assert.False(patch.HasPeriodicUSeam);
            Assert.False(patch.HasPeriodicVSeam);
            Assert.All(patch.Cells, cell => Assert.IsType<QuadCell>(cell));
        });
    }

    [Fact]
    public void FirmamentHexBoltStepRoundTrip_UsesTheSameSurfaceMeshIrRoute()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../testdata/firmament/examples/mcmaster_91180a151_threadless_hex_bolt.firmament"));
        var built = FirmamentBuildAndExport.Run(source);
        Assert.True(built.IsSuccess, string.Join(Environment.NewLine, built.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var imported = Step242Importer.ImportBody(built.Value.Export.StepText);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(imported.Value!, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        Assert.True(SurfaceMeshIrValidator.TryValidate(document, out var failure), failure);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var topology));
        Assert.True(topology.IsWatertight);
    }

    [Fact]
    public void OctagonalCoaxialFixture_ProvesConicTrimAndRootFilletAreNotBoltSpecific()
    {
        var fixture = ExactCoaxialPartBuilder.Create(new ExactCoaxialPartRecipe(
            "octagonal-root-fixture", 8, 13d, 5.3d, 12.35d, 25d, 0.2d,
            8d, 35d, 0.9375d, 6.125d, 10d, "fixture-zone", "m4-generic"));
        Assert.True(fixture.IsSuccess, string.Join(Environment.NewLine, fixture.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(fixture.Value.Body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        Assert.Equal(8, document.SharedBoundaries.Count(boundary => boundary.CurveKind == CurveGeometryKind.Hyperbola3));
        Assert.Equal(2, document.Patches.Count(patch => patch.Support.Kind == SurfaceMeshSupportKind.Torus));
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var topology));
        Assert.True(topology.IsWatertight);
    }
}
