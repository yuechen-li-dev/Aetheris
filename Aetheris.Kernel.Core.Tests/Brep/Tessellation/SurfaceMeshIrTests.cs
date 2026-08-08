using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class SurfaceMeshIrTests
{
    [Fact]
    public void Box_UsesSharedLinePlans_AndMostlyQuadPatchCells()
    {
        var box = BrepPrimitives.CreateBox(3d, 2d, 1d).Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(box, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        Assert.Equal(12, document.SharedBoundaries.Count);
        Assert.Equal(6, document.Patches.Count);
        Assert.All(document.Patches, patch => Assert.IsType<QuadCell>(Assert.Single(patch.Cells)));
        Assert.True(SurfaceMeshIrValidator.TryValidate(document, out var failure), failure);
    }

    [Fact]
    public void Cylinder_ReusesCircularBoundaries_AndHasExplicitPeriodicQuadStrip()
    {
        var cylinder = BrepPrimitives.CreateCylinder(1.5d, 4d).Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(cylinder, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        var side = Assert.Single(document.Patches, patch => patch.Support.Kind == SurfaceMeshSupportKind.Cylinder);
        Assert.True(side.HasPeriodicUSeam);
        Assert.All(side.Cells, cell => Assert.IsType<QuadCell>(cell));
        Assert.True(document.SharedBoundaries.Count(plan => plan.CurveKind == CurveGeometryKind.Circle3 && plan.IsClosed) >= 2);
    }

    [Fact]
    public void Lowering_IsDeterministic_AndReportsSurfaceMeshPipeline()
    {
        var body = BrepPrimitives.CreateCylinder(1d, 2d).Value;
        Assert.True(SurfaceMeshIrTessellator.TryTessellate(body, DisplayTessellationOptions.Default, out var first));
        Assert.True(SurfaceMeshIrTessellator.TryTessellate(body, DisplayTessellationOptions.Default, out var second));
        Assert.Equal(DisplayMeshPipeline.SurfaceMeshIr, first.MeshPipeline);
        Assert.NotNull(first.SurfaceMeshMetrics);
        Assert.Equal(first.SurfaceMeshMetrics!.DeterministicHash, second.SurfaceMeshMetrics!.DeterministicHash);
        Assert.Equal(first.FacePatches.SelectMany(p => p.TriangleIndices), second.FacePatches.SelectMany(p => p.TriangleIndices));
    }

    [Fact]
    public void LegacyOracle_RemainsExplicitlyAvailableForMigrationComparison()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var legacy = BrepDisplayTessellator.TessellateLegacyForComparison(box);
        var modern = BrepDisplayTessellator.TessellateSurfaceMeshIr(box);
        Assert.True(legacy.IsSuccess);
        Assert.True(modern.IsSuccess);
        Assert.Equal(DisplayMeshPipeline.LegacyTessellator, legacy.Value.MeshPipeline);
        Assert.Equal(DisplayMeshPipeline.SurfaceMeshIr, modern.Value.MeshPipeline);
    }

    [Fact]
    public void CylinderLowering_UsesExactRadialNormals()
    {
        var body = BrepPrimitives.CreateCylinder(2d, 3d).Value;
        var result = BrepDisplayTessellator.TessellateSurfaceMeshIr(body);
        Assert.True(result.IsSuccess);
        var cylindrical = Assert.Single(result.Value.FacePatches, patch => patch.Positions.Count > 10 && patch.TriangleIndices.Count == 72 * 3);
        for (var i = 0; i < cylindrical.Positions.Count; i++)
        {
            var position = cylindrical.Positions[i];
            var radialLength = double.Sqrt((position.X * position.X) + (position.Y * position.Y));
            Assert.True(double.Abs(cylindrical.Normals[i].Z) < 1e-9d);
            Assert.True(double.Abs(cylindrical.Normals[i].X - (position.X / radialLength)) < 1e-9d);
            Assert.True(double.Abs(cylindrical.Normals[i].Y - (position.Y / radialLength)) < 1e-9d);
        }
    }

    [Fact]
    public void ThroughHolePlate_UsesMultiLoopPlanarBands_AndLowersToWatertightMesh()
    {
        var plate = BrepPrimitives.CreateBox(40d, 30d, 4d).Value;
        var cutter = BrepPrimitives.CreateCylinder(3d, 10d).Value;
        var body = BrepBoolean.Subtract(plate, cutter);
        Assert.True(body.IsSuccess, string.Join(Environment.NewLine, body.Diagnostics.Select(d => d.Message)));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body.Value, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        var caps = document.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Plane && patch.TrimLoops.Count == 2).ToArray();
        Assert.Equal(2, caps.Length);
        Assert.All(caps, cap => Assert.All(cap.Cells, cell => Assert.IsType<QuadCell>(cell)));
        var wall = Assert.Single(document.Patches, patch => patch.Support.Kind == SurfaceMeshSupportKind.Cylinder);
        Assert.Equal(36, wall.Cells.Count); // angular refinement only; one axial row
        var rings = document.SharedBoundaries.Where(boundary => boundary.CurveKind == CurveGeometryKind.Circle3 && boundary.Uses.Count == 2).ToArray();
        Assert.Equal(2, rings.Length);
        Assert.All(rings, ring => Assert.Equal(ring.Samples[0].Id, ring.Samples[^1].Id));
        Assert.True(SurfaceMeshIrValidator.TryValidate(document, out var irFailure), irFailure);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out var topology));
        Assert.True(topology.IsWatertight);
        Assert.True(topology.IsOutwardOriented);
        Assert.NotEmpty(mesh.HardEdges);
        var legacy = BrepDisplayTessellator.TessellateLegacyForComparison(body.Value);
        Assert.True(legacy.IsSuccess);
        Assert.True(topology.TriangleCount < legacy.Value.FacePatches.Sum(patch => patch.TriangleIndices.Count / 3));
        // Focused evidence assertion keeps the comparison on the real legacy route.

        var displayed = BrepDisplayTessellator.TessellateSurfaceMeshIr(body.Value);
        Assert.True(displayed.IsSuccess);
        var capFaceIds = caps.Select(cap => cap.FaceId).ToHashSet();
        foreach (var cap in displayed.Value.FacePatches.Where(patch => capFaceIds.Contains(patch.FaceId)))
        {
            for (var i = 0; i < cap.TriangleIndices.Count; i += 3)
            {
                var a = cap.Positions[cap.TriangleIndices[i]];
                var b = cap.Positions[cap.TriangleIndices[i + 1]];
                var c = cap.Positions[cap.TriangleIndices[i + 2]];
                var centroidX = (a.X + b.X + c.X) / 3d;
                var centroidY = (a.Y + b.Y + c.Y) / 3d;
                Assert.True(double.Sqrt((centroidX * centroidX) + (centroidY * centroidY)) > 2.8d,
                    $"Cap triangle {i / 3} crossed the radius-3 trim: centroid=({centroidX:R},{centroidY:R}).");
            }
        }
    }

    [Fact]
    public void BinaryStlExporter_WritesExactTriangleRecordCount()
    {
        var body = BrepBoolean.Subtract(BrepPrimitives.CreateBox(20d, 20d, 2d).Value, BrepPrimitives.CreateCylinder(2d, 6d).Value).Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out _));
        var path = Path.Combine(Path.GetTempPath(), $"aetheris-{Guid.NewGuid():N}.stl");
        try
        {
            BinaryStlExporter.Export(path, mesh);
            Assert.Equal(84L + (50L * mesh.TriangleIndices.Count / 3), new FileInfo(path).Length);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
