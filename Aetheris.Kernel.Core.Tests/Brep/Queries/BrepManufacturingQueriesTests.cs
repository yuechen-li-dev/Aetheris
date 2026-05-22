using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class BrepManufacturingQueriesTests
{
    [Fact]
    public void FirstHit_BoxRay_ReturnsNearestHitWithFaceAndNormal()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var ray = new Ray3D(new Point3D(-3d, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepManufacturingQueries.FirstHit(box, ray);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2d, result.Value!.Value.T, 9);
        Assert.Equal(new Point3D(-1d, 0d, 0d), result.Value.Value.Point);
        Assert.NotNull(result.Value.Value.FaceId);
        Assert.NotNull(result.Value.Value.Normal);
        Assert.Equal(-1d, result.Value.Value.Normal!.Value.X, 9);
    }

    [Fact]
    public void FirstHit_UnsupportedBody_ReturnsNotImplemented()
    {
        var body = new BrepBody(new TopologyModel(), new BrepGeometryStore(), new BrepBindingModel());
        var ray = new Ray3D(Point3D.Origin, Direction3D.Create(new Vector3D(1d, 0d, 0d)));

        var result = BrepManufacturingQueries.FirstHit(body, ray);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
    }

    [Fact]
    public void LocalThickness_BoxPlanarFaceNormalInward_ReturnsOppositeBoundaryDistance()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var xPositiveFaceId = ResolveFaceByNormal(box, new Vector3D(1d, 0d, 0d));

        var result = BrepManufacturingQueries.ProbeLocalThickness(
            box,
            new LocalThicknessProbe(xPositiveFaceId, new Point3D(1d, 0d, 0d)),
            ToleranceContext.Default);

        Assert.True(result.IsSuccess);
        Assert.Equal(2d, result.Value.Thickness, 6);
        Assert.Equal(xPositiveFaceId, result.Value.SourceFaceId);
        Assert.NotEqual(result.Value.SourceFaceId, result.Value.OppositeFaceId);
        Assert.Equal(-1d, result.Value.OppositePoint.X, 6);
        Assert.Equal(0d, result.Value.OppositePoint.Y, 6);
        Assert.Equal(0d, result.Value.OppositePoint.Z, 6);
    }

    [Fact]
    public void LocalThickness_NonPlanarFace_ReturnsNotImplemented()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;
        var sphereFace = sphere.Topology.Faces.Single().Id;

        var result = BrepManufacturingQueries.ProbeLocalThickness(
            sphere,
            new LocalThicknessProbe(sphereFace, new Point3D(2d, 0d, 0d)));

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
    }

    [Fact]
    public void EnumerateInternalConcaveEdges_OccupiedCellLShape_ReturnsSharpConcaveFacts()
    {
        var lShaped = CreateSupportedLShapedAdditiveBody();

        var result = BrepManufacturingQueries.EnumerateInternalConcaveEdges(lShaped);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value);
        Assert.All(result.Value, fact =>
        {
            Assert.Equal(ConcaveManufacturingEdgeGeometryClass.PlanarPlanarLinearSharp, fact.GeometryClass);
            Assert.True(fact.RequiresFiniteToolRadius);
            Assert.Equal(0d, fact.MinimumToolRadiusLowerBound, 9);
            Assert.NotEqual(fact.FirstFaceId, fact.SecondFaceId);
        });
    }

    [Fact]
    public void EnumerateInternalConcaveEdges_WithoutOccupiedCells_ReturnsNotImplemented()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;

        var result = BrepManufacturingQueries.EnumerateInternalConcaveEdges(box);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == KernelDiagnosticCode.NotImplemented);
    }

    private static BrepBody CreateSupportedLShapedAdditiveBody()
    {
        var cells = new[]
        {
            new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(0d, 2d, 2d, 4d, 0d, 1d),
        };

        var built = BrepBooleanOrthogonalUnionBuilder.BuildFromCells(cells);
        Assert.True(built.IsSuccess);

        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 1d),
            [],
            SafeBooleanRootDescriptor.FromBox(new AxisAlignedBoxExtents(0d, 4d, 0d, 4d, 0d, 1d)),
            OccupiedCells: cells);

        return new BrepBody(
            built.Value.Topology,
            built.Value.Geometry,
            built.Value.Bindings,
            built.Value.Topology.Vertices
                .Where(v => built.Value.TryGetVertexPoint(v.Id, out _))
                .ToDictionary(v => v.Id, v =>
                {
                    built.Value.TryGetVertexPoint(v.Id, out var point);
                    return point;
                }),
            composition,
            built.Value.ShellRepresentation);
    }

    private static FaceId ResolveFaceByNormal(BrepBody body, Vector3D normal)
    {
        foreach (var binding in body.Bindings.FaceBindings)
        {
            var surface = body.Geometry.GetSurface(binding.SurfaceGeometryId);
            if (surface.Kind != SurfaceGeometryKind.Plane || surface.Plane is null)
            {
                continue;
            }

            var candidate = surface.Plane.Value.Normal.ToVector();
            if (System.Math.Abs(candidate.X - normal.X) < 1e-9d
                && System.Math.Abs(candidate.Y - normal.Y) < 1e-9d
                && System.Math.Abs(candidate.Z - normal.Z) < 1e-9d)
            {
                return binding.FaceId;
            }
        }

        throw new InvalidOperationException($"No planar face found with normal {normal}.");
    }
}
