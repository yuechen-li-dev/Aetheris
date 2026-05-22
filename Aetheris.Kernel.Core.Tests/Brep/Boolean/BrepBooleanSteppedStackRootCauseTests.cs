using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Boolean;

public sealed class BrepBooleanSteppedStackRootCauseTests
{
    [Fact]
    public void SteppedThirdSubtract_NoLongerFailsWithHoleInterference()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-15d, 15d, -15d, 15d, -10d, 10d)).Value;
        var through = BrepPrimitives.CreateCylinder(2d, 30d).Value;
        var mediumBlind = Translate(BrepPrimitives.CreateCylinder(3d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 7d)));
        var shallowBlind = Translate(BrepPrimitives.CreateCylinder(4d, 3d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 8.5d)));

        var first = BrepBoolean.Subtract(baseBox, through);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, mediumBlind);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value.SafeBooleanComposition?.Holes.Count);

        var third = BrepBoolean.Subtract(second.Value, shallowBlind);
        if (!third.IsSuccess)
        {
            var diagnostic = Assert.Single(third.Diagnostics);
            Assert.NotEqual("BrepBoolean.AnalyticHole.HoleInterference", diagnostic.Source);
        }
    }

    [Fact]
    public void NLevelCoaxialBuilderProbe_ObservedBehavior_IsSuccessfulBuild()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-15d, 15d, -15d, 15d, -10d, 10d)).Value;
        var through = BrepPrimitives.CreateCylinder(2d, 30d).Value;
        var mediumBlind = Translate(BrepPrimitives.CreateCylinder(3d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 7d)));

        var first = BrepBoolean.Subtract(baseBox, through);
        Assert.True(first.IsSuccess);
        var second = BrepBoolean.Subtract(first.Value, mediumBlind);
        Assert.True(second.IsSuccess);

        var composition = second.Value.SafeBooleanComposition!;
        var seedHole = composition.Holes[1];
        var thirdHole = seedHole with
        {
            BottomRadius = 4d,
            TopRadius = 4d,
            EndCenter = new Point3D(seedHole.EndCenter.X, seedHole.EndCenter.Y, 7d),
            EndZ = 7d,
        };
        composition = composition with { Holes = [composition.Holes[0], composition.Holes[1], thirdHole] };

        var build = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(composition, ToleranceContext.Default);
        Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(d => d.Message)));
        Assert.True(BrepBindingValidator.Validate(build.Value, requireAllEdgeAndFaceBindings: true).IsSuccess);
    }

    [Fact]
    public void CounterboreTwoLevel_StillSucceeds()
    {
        var baseBox = BrepBooleanBoxRecognition.CreateBoxFromExtents(new AxisAlignedBoxExtents(-35d, 35d, -20d, 20d, -8d, 8d)).Value;
        var pocket = Translate(BrepPrimitives.CreateCylinder(5d, 6d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 0d, 5d)));
        var through = BrepPrimitives.CreateCylinder(3d, 24d).Value;

        var first = BrepBoolean.Subtract(baseBox, pocket);
        Assert.True(first.IsSuccess);

        var second = BrepBoolean.Subtract(first.Value, through);
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, second.Value.SafeBooleanComposition?.Holes.Count);
    }

    private static BrepBody Translate(BrepBody body, Transform3D transform)
    {
        var geometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            geometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(transform.Apply(curveEntry.Value.Line3!.Value.Origin), transform.Apply(curveEntry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(transform.Apply(curveEntry.Value.Circle3!.Value.Center), transform.Apply(curveEntry.Value.Circle3.Value.Normal), curveEntry.Value.Circle3.Value.Radius, transform.Apply(curveEntry.Value.Circle3.Value.XAxis))),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(transform.Apply(surfaceEntry.Value.Plane!.Value.Origin), transform.Apply(surfaceEntry.Value.Plane.Value.Normal), transform.Apply(surfaceEntry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(transform.Apply(surfaceEntry.Value.Cylinder!.Value.Origin), transform.Apply(surfaceEntry.Value.Cylinder.Value.Axis), surfaceEntry.Value.Cylinder.Value.Radius, transform.Apply(surfaceEntry.Value.Cylinder.Value.XAxis))),
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints, body.SafeBooleanComposition);
    }
}
