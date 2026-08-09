using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

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
    public void Cone_UsesSharedCircularRings_AndAngularGeneratorQuadStrip()
    {
        var body = BrepRevolve.Create(
            [new ProfilePoint2D(4d, -2d), new ProfilePoint2D(2d, 2d)],
            new ExtrudeFrame3D(Point3D.Origin, Direction3D.Create(new Vector3D(0d, 0d, 1d)), Direction3D.Create(new Vector3D(1d, 0d, 0d))),
            new RevolveAxis3D(Point3D.Origin, new Vector3D(0d, 0d, 1d))).Value;

        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        var side = Assert.Single(document.Patches, patch => patch.Support.Kind == SurfaceMeshSupportKind.Cone);
        Assert.True(side.HasPeriodicUSeam);
        Assert.All(side.Cells, cell => Assert.IsType<QuadCell>(cell));
        Assert.Equal(36, side.Cells.Count);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var topology));
        Assert.True(topology.IsWatertight);
    }

    [Fact]
    public void Sphere_UsesSixStructuredCharts_WithSharedSeamVertices_AndExactNormals()
    {
        var body = BrepPrimitives.CreateSphere(3d).Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        var charts = document.Patches.Where(patch => patch.Support.Kind == SurfaceMeshSupportKind.Sphere).ToArray();
        Assert.Equal(6, charts.Length);
        Assert.All(charts, chart => Assert.All(chart.Cells, cell => Assert.IsType<QuadCell>(cell)));
        Assert.Contains(charts.SelectMany(chart => chart.Cells).SelectMany(cell => cell.VertexIds).GroupBy(id => id), group => group.Count() >= 4);
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out var mesh, out var topology));
        Assert.True(topology.IsWatertight);
        Assert.All(mesh.Normals, normal => Assert.True(double.Abs(normal.Length - 1d) < 1e-9d));
    }

    [Fact]
    public void Torus_UsesDoublyPeriodicStructuredQuads_AndReusesBrepSeamSamples()
    {
        var body = BrepPrimitives.CreateTorus(6d, 1.5d).Value;
        Assert.True(SurfaceMeshIrTessellator.TryBuild(body, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document));
        var patch = Assert.Single(document.Patches, candidate => candidate.Support.Kind == SurfaceMeshSupportKind.Torus);
        Assert.True(patch.HasPeriodicUSeam);
        Assert.True(patch.HasPeriodicVSeam);
        Assert.All(patch.Cells, cell => Assert.IsType<QuadCell>(cell));
        var patchIds = patch.Cells.SelectMany(cell => cell.VertexIds).ToHashSet();
        Assert.All(document.SharedBoundaries.SelectMany(boundary => boundary.Samples), sample => Assert.Contains(sample.Id, patchIds));
        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var topology));
        Assert.True(topology.IsWatertight);
    }

    [Theory]
    [InlineData("sphere")]
    [InlineData("torus")]
    public void M3Primitive_DisplayRoute_IsSurfaceMeshIr_AndDeterministic(string fixture)
    {
        var body = fixture == "sphere"
            ? BrepPrimitives.CreateSphere(2d).Value
            : BrepPrimitives.CreateTorus(5d, 1d).Value;
        var first = BrepDisplayTessellator.TessellateSurfaceMeshIr(body);
        var second = BrepDisplayTessellator.TessellateSurfaceMeshIr(body);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(DisplayMeshPipeline.SurfaceMeshIr, first.Value.MeshPipeline);
        Assert.Equal(first.Value.SurfaceMeshMetrics!.DeterministicHash, second.Value.SurfaceMeshMetrics!.DeterministicHash);
        Assert.Equal(
            first.Value.FacePatches.SelectMany(patch => patch.TriangleIndices),
            second.Value.FacePatches.SelectMany(patch => patch.TriangleIndices));
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
    public void Ctc01_SplineBoundedCylinderPatches_FollowTheExactSupportWithoutLongChords()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var step = File.ReadAllText(Path.Combine(root!.FullName, "testdata", "step242", "nist", "CTC", "nist_ctc_01_asme1_ap242-e1.stp"));
        var imported = Step242Importer.ImportBody(step);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.True(SurfaceMeshIrTessellator.TryBuild(imported.Value, SurfaceMeshPolicy.FromDisplayOptions(DisplayTessellationOptions.Default), out var document, out var failure), failure);

        var affectedFaces = new HashSet<int> { 10, 20, 30, 44, 65, 66, 69, 70 };
        var patches = document.Patches.Where(patch => affectedFaces.Contains(patch.FaceId.Value)).ToArray();
        Assert.Equal(affectedFaces.Count, patches.Length);
        foreach (var patch in patches)
        {
            var trim = Assert.Single(patch.TrimLoopData!);
            var authoritativeBoundary = trim.VertexIds.ToHashSet();
            var cylinder = patch.Support.Cylinder!.Value;
            var vertexById = document.Vertices.ToDictionary(vertex => vertex.Id);
            var localById = trim.VertexIds.Select((id, index) => (id, local: trim.LocalCoordinates[index]))
                .ToDictionary(item => item.id, item => item.local);
            foreach (var vertexId in patch.Cells.SelectMany(cell => cell.VertexIds).Distinct())
            {
                var vertex = vertexById[vertexId];
                if (!localById.ContainsKey(vertexId))
                {
                    Assert.NotNull(vertex.U);
                    Assert.NotNull(vertex.V);
                    localById[vertexId] = (vertex.U!.Value, vertex.V!.Value);
                }
                var projected = cylinder.Evaluate(localById[vertexId].U, localById[vertexId].V);
                var supportError = (projected - vertex.Position).Length;
                var tolerance = authoritativeBoundary.Contains(vertexId) ? DisplayTessellationOptions.Default.ChordTolerance : 1e-9d;
                Assert.True(supportError <= tolerance, $"Face {patch.FaceId.Value} vertex {vertexId} is {supportError:R} off its exact cylinder.");
            }

            Assert.Contains(patch.Cells, cell => cell is QuadCell);
            Assert.Contains(patch.Cells.SelectMany(cell => cell.VertexIds), id => !authoritativeBoundary.Contains(id));
            Assert.True(authoritativeBoundary.IsSubsetOf(patch.Cells.SelectMany(cell => cell.VertexIds)));
            Assert.All(patch.Cells, cell =>
            {
                var uSpan = cell.VertexIds.Max(id => localById[id].U) - cell.VertexIds.Min(id => localById[id].U);
                Assert.True(uSpan < 0.2d, $"Face {patch.FaceId.Value} still contains a long cylindrical chord spanning {uSpan:R} radians.");
                var a = vertexById[cell.VertexIds[0]].Position;
                var b = vertexById[cell.VertexIds[1]].Position;
                var c = vertexById[cell.VertexIds[2]].Position;
                var centroid = new Point3D((a.X + b.X + c.X) / 3d, (a.Y + b.Y + c.Y) / 3d, (a.Z + b.Z + c.Z) / 3d);
                var offset = centroid - cylinder.Origin;
                var angle = double.Atan2(offset.Dot(cylinder.YAxis.ToVector()), offset.Dot(cylinder.XAxis.ToVector()));
                var exactNormal = cylinder.Normal(angle).ToVector();
                if (!patch.SameSense) exactNormal = -exactNormal;
                Assert.True((b - a).Cross(c - a).Dot(exactNormal) > 0d, $"Face {patch.FaceId.Value} contains a reversed trim cell.");
            });
        }

        foreach (var patch in document.Patches.Where(patch => patch.FaceId.Value is 48 or 50 or 55 or 56))
        {
            var loops = patch.TrimLoopData!;
            var outer = loops.MaxBy(loop => double.Abs(loop.SignedArea))!;
            var holes = loops.Where(loop => loop.LoopId != outer.LoopId).ToArray();
            var localById = loops.SelectMany(loop => loop.VertexIds.Select((id, index) => (id, local: loop.LocalCoordinates[index])))
                .GroupBy(item => item.id).ToDictionary(group => group.Key, group => group.First().local);
            Assert.All(patch.Cells, cell =>
            {
                var centroid = (
                    U: cell.VertexIds.Average(id => localById[id].U),
                    V: cell.VertexIds.Average(id => localById[id].V));
                Assert.True(IsInside(centroid, outer.LocalCoordinates), $"Face {patch.FaceId.Value} cell crossed its outer trim.");
                Assert.DoesNotContain(holes, hole => IsInside(centroid, hole.LocalCoordinates));
            });
        }

        Assert.True(SurfaceMeshIrTessellator.TryLowerToTriangleMesh(document, out _, out var topology));
        Assert.True(topology.IsWatertight);
        Assert.Equal(0, topology.ZeroAreaTriangleCount);
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

    private static bool IsInside((double U, double V) point, IReadOnlyList<(double U, double V)> polygon)
    {
        var inside = false;
        for (int current = 0, previous = polygon.Count - 1; current < polygon.Count; previous = current++)
        {
            var a = polygon[current];
            var b = polygon[previous];
            if ((a.V > point.V) != (b.V > point.V)
                && point.U < ((b.U - a.U) * (point.V - a.V) / (b.V - a.V)) + a.U)
                inside = !inside;
        }
        return inside;
    }
}
