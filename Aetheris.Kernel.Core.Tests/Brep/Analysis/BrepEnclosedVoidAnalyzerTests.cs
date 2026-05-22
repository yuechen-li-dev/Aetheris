using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Analysis;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Tests.Brep.Analysis;

public sealed class BrepEnclosedVoidAnalyzerTests
{
    [Fact]
    public void Analyze_BoxSphereContainedSubtract_Detects_EnclosedVoid()
    {
        var baseBox = BrepPrimitives.CreateBox(40d, 30d, 12d).Value;
        var innerSphere = BrepPrimitives.CreateSphere(4d).Value;

        var result = BrepBoolean.Subtract(baseBox, innerSphere);

        Assert.True(result.IsSuccess);
        var facts = BrepEnclosedVoidAnalyzer.Analyze(result.Value);
        Assert.True(facts.HasEnclosedVoids);
        Assert.Equal(1, facts.EnclosedVoidCount);
        Assert.Single(facts.EnclosedVoidShellIds);
    }

    [Fact]
    public void Analyze_BoxCylinderThroughHole_DoesNotDetect_EnclosedVoid()
    {
        var baseBox = BrepPrimitives.CreateBox(40d, 30d, 12d).Value;
        var through = BrepPrimitives.CreateCylinder(4d, 20d).Value;

        var result = BrepBoolean.Subtract(baseBox, through);

        Assert.True(result.IsSuccess);
        var facts = BrepEnclosedVoidAnalyzer.Analyze(result.Value);
        Assert.False(facts.HasEnclosedVoids);
        Assert.Equal(0, facts.EnclosedVoidCount);
        Assert.Empty(facts.EnclosedVoidShellIds);
    }

    [Fact]
    public void Analyze_CylinderRootThroughHoleChain_DoesNotDetect_EnclosedVoid()
    {
        var root = BrepPrimitives.CreateCylinder(40d, 12d).Value;
        var centerBore = BrepPrimitives.CreateCylinder(12d, 24d).Value;
        var hole0 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(25d, 0d, 0d)));
        var hole90 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(0d, 25d, 0d)));
        var hole180 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(-25d, 0d, 0d)));
        var hole270 = TransformBody(BrepPrimitives.CreateCylinder(4d, 24d).Value, Transform3D.CreateTranslation(new Vector3D(0d, -25d, 0d)));

        var withCenter = BrepBoolean.Subtract(root, centerBore).Value;
        var withHole0 = BrepBoolean.Subtract(withCenter, hole0).Value;
        var withHole90 = BrepBoolean.Subtract(withHole0, hole90).Value;
        var withHole180 = BrepBoolean.Subtract(withHole90, hole180).Value;
        var result = BrepBoolean.Subtract(withHole180, hole270);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.ShellRepresentation);
        Assert.Equal(5, result.Value.ShellRepresentation!.InnerShellIds.Count);

        var facts = BrepEnclosedVoidAnalyzer.Analyze(result.Value);

        Assert.False(facts.HasEnclosedVoids);
        Assert.Equal(0, facts.EnclosedVoidCount);
        Assert.Empty(facts.EnclosedVoidShellIds);
    }

    private static BrepBody TransformBody(BrepBody body, Transform3D transform)
    {
        var geometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            geometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(
                    transform.Apply(curveEntry.Value.Line3!.Value.Origin),
                    transform.Apply(curveEntry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(
                    transform.Apply(curveEntry.Value.Circle3!.Value.Center),
                    transform.Apply(curveEntry.Value.Circle3.Value.Normal),
                    curveEntry.Value.Circle3.Value.Radius,
                    transform.Apply(curveEntry.Value.Circle3.Value.XAxis))),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(
                    transform.Apply(surfaceEntry.Value.Plane!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Plane.Value.Normal),
                    transform.Apply(surfaceEntry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(
                    transform.Apply(surfaceEntry.Value.Cylinder!.Value.Origin),
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.Axis),
                    surfaceEntry.Value.Cylinder.Value.Radius,
                    transform.Apply(surfaceEntry.Value.Cylinder.Value.XAxis))),
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
