using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

internal static class BrepBooleanCylinderOpenSlotBuilder
{
    public static KernelResult<BrepBody> Build(SafeBooleanComposition composition, ToleranceContext tolerance)
    {
        if (composition.RootDescriptor.Cylinder is not RecognizedCylinder rootCylinder
            || composition.OpenSlots is not { Count: 1 }
            || composition.Holes.Count != 0)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    "Boolean Subtract: bounded cylinder-root open-slot rebuild requires one recognized root cylinder and one supported rectangular through-slot tool.",
                    "BrepBooleanCylinderOpenSlotBuilder.Build"),
            ]);
        }

        var slot = composition.OpenSlots[0].ToolExtents;
        var centerX = (rootCylinder.MinCenter.X + rootCylinder.MaxCenter.X) * 0.5d;
        var centerY = (rootCylinder.MinCenter.Y + rootCylinder.MaxCenter.Y) * 0.5d;
        var minZ = System.Math.Min(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);
        var maxZ = System.Math.Max(rootCylinder.MinCenter.Z, rootCylinder.MaxCenter.Z);

        var yMin = slot.MinY;
        var yMax = slot.MaxY;
        var floorX = slot.MinX;
        var radius = rootCylinder.Radius;

        var dyMin = yMin - centerY;
        var dyMax = yMax - centerY;
        var xOnCylinderAtMinY = centerX + System.Math.Sqrt(System.Math.Max(0d, (radius * radius) - (dyMin * dyMin)));
        var xOnCylinderAtMaxY = centerX + System.Math.Sqrt(System.Math.Max(0d, (radius * radius) - (dyMax * dyMax)));

        if (xOnCylinderAtMinY <= floorX + tolerance.Linear || xOnCylinderAtMaxY <= floorX + tolerance.Linear)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.NotImplemented,
                    KernelDiagnosticSeverity.Error,
                    "Boolean Subtract: bounded cylinder-root open-slot rebuild requires strictly positive slot side-wall span between the floor plane and cylindrical wall.",
                    "BrepBooleanCylinderOpenSlotBuilder.Build"),
            ]);
        }

        var topArcMax = new Point3D(xOnCylinderAtMaxY, yMax, maxZ);
        var topArcMin = new Point3D(xOnCylinderAtMinY, yMin, maxZ);
        var bottomArcMax = new Point3D(xOnCylinderAtMaxY, yMax, minZ);
        var bottomArcMin = new Point3D(xOnCylinderAtMinY, yMin, minZ);

        var topFloorMax = new Point3D(floorX, yMax, maxZ);
        var topFloorMin = new Point3D(floorX, yMin, maxZ);
        var bottomFloorMax = new Point3D(floorX, yMax, minZ);
        var bottomFloorMin = new Point3D(floorX, yMin, minZ);

        var builder = new TopologyBuilder();
        var vTopArcMax = builder.AddVertex();
        var vTopArcMin = builder.AddVertex();
        var vBottomArcMax = builder.AddVertex();
        var vBottomArcMin = builder.AddVertex();
        var vTopFloorMax = builder.AddVertex();
        var vTopFloorMin = builder.AddVertex();
        var vBottomFloorMax = builder.AddVertex();
        var vBottomFloorMin = builder.AddVertex();

        var eTopArc = builder.AddEdge(vTopArcMax, vTopArcMin);
        var eBottomArc = builder.AddEdge(vBottomArcMin, vBottomArcMax);
        var eCylMax = builder.AddEdge(vBottomArcMax, vTopArcMax);
        var eCylMin = builder.AddEdge(vTopArcMin, vBottomArcMin);
        var eTopFloor = builder.AddEdge(vTopFloorMax, vTopFloorMin);
        var eBottomFloor = builder.AddEdge(vBottomFloorMin, vBottomFloorMax);
        var eFloorMax = builder.AddEdge(vBottomFloorMax, vTopFloorMax);
        var eFloorMin = builder.AddEdge(vTopFloorMin, vBottomFloorMin);
        var eTopRadialMax = builder.AddEdge(vTopArcMax, vTopFloorMax);
        var eTopRadialMin = builder.AddEdge(vTopFloorMin, vTopArcMin);
        var eBottomRadialMax = builder.AddEdge(vBottomFloorMax, vBottomArcMax);
        var eBottomRadialMin = builder.AddEdge(vBottomArcMin, vBottomFloorMin);

        var sideCylinderFace = AddFaceWithLoop(builder, [
            EdgeUse.Forward(eCylMax),
            EdgeUse.Forward(eTopArc),
            EdgeUse.Forward(eCylMin),
            EdgeUse.Forward(eBottomArc),
        ]);

        var topCapFace = AddFaceWithLoop(builder, [
            EdgeUse.Reversed(eTopArc),
            EdgeUse.Forward(eTopRadialMax),
            EdgeUse.Forward(eTopFloor),
            EdgeUse.Forward(eTopRadialMin),
        ]);

        var bottomCapFace = AddFaceWithLoop(builder, [
            EdgeUse.Reversed(eBottomArc),
            EdgeUse.Reversed(eBottomRadialMin),
            EdgeUse.Forward(eBottomFloor),
            EdgeUse.Forward(eBottomRadialMax),
        ]);

        var floorFace = AddFaceWithLoop(builder, [
            EdgeUse.Reversed(eTopFloor),
            EdgeUse.Forward(eFloorMax),
            EdgeUse.Forward(eBottomFloor),
            EdgeUse.Reversed(eFloorMin),
        ]);

        var sideMaxFace = AddFaceWithLoop(builder, [
            EdgeUse.Reversed(eTopRadialMax),
            EdgeUse.Reversed(eCylMax),
            EdgeUse.Reversed(eBottomRadialMax),
            EdgeUse.Forward(eFloorMax),
        ]);

        var sideMinFace = AddFaceWithLoop(builder, [
            EdgeUse.Reversed(eTopRadialMin),
            EdgeUse.Forward(eCylMin),
            EdgeUse.Reversed(eBottomRadialMin),
            EdgeUse.Reversed(eFloorMin),
        ]);

        var shell = builder.AddShell([sideCylinderFace, topCapFace, bottomCapFace, floorFace, sideMaxFace, sideMinFace]);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));

        var topCenter = new Point3D(centerX, centerY, maxZ);
        var bottomCenter = new Point3D(centerX, centerY, minZ);

        var thetaMax = NormalizeAngle(System.Math.Atan2(yMax - centerY, xOnCylinderAtMaxY - centerX));
        var thetaMin = NormalizeAngle(System.Math.Atan2(yMin - centerY, xOnCylinderAtMinY - centerX));
        var topArcEnd = thetaMin < thetaMax ? thetaMin + (2d * double.Pi) : thetaMin;
        var bottomArcEnd = thetaMax < thetaMin ? thetaMax + (2d * double.Pi) : thetaMax;

        geometry.AddCurve(new CurveGeometryId(1), CurveGeometry.FromCircle(new Circle3Curve(topCenter, zAxis, radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(2), CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, zAxis, radius, xAxis)));
        geometry.AddCurve(new CurveGeometryId(3), CurveGeometry.FromLine(new Line3Curve(bottomArcMax, zAxis)));
        geometry.AddCurve(new CurveGeometryId(4), CurveGeometry.FromLine(new Line3Curve(topArcMin, Direction3D.Create(new Vector3D(0d, 0d, -1d)))));
        geometry.AddCurve(new CurveGeometryId(5), CurveGeometry.FromLine(new Line3Curve(topFloorMax, Direction3D.Create(new Vector3D(0d, -1d, 0d)))));
        geometry.AddCurve(new CurveGeometryId(6), CurveGeometry.FromLine(new Line3Curve(bottomFloorMin, yAxis)));
        geometry.AddCurve(new CurveGeometryId(7), CurveGeometry.FromLine(new Line3Curve(bottomFloorMax, zAxis)));
        geometry.AddCurve(new CurveGeometryId(8), CurveGeometry.FromLine(new Line3Curve(topFloorMin, Direction3D.Create(new Vector3D(0d, 0d, -1d)))));
        geometry.AddCurve(new CurveGeometryId(9), CurveGeometry.FromLine(new Line3Curve(topArcMax, Direction3D.Create(new Vector3D(floorX - xOnCylinderAtMaxY, 0d, 0d)))));
        geometry.AddCurve(new CurveGeometryId(10), CurveGeometry.FromLine(new Line3Curve(topFloorMin, Direction3D.Create(new Vector3D(xOnCylinderAtMinY - floorX, 0d, 0d)))));
        geometry.AddCurve(new CurveGeometryId(11), CurveGeometry.FromLine(new Line3Curve(bottomFloorMax, Direction3D.Create(new Vector3D(xOnCylinderAtMaxY - floorX, 0d, 0d)))));
        geometry.AddCurve(new CurveGeometryId(12), CurveGeometry.FromLine(new Line3Curve(bottomArcMin, Direction3D.Create(new Vector3D(floorX - xOnCylinderAtMinY, 0d, 0d)))));

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromCylinder(new CylinderSurface(new Point3D(centerX, centerY, minZ), zAxis, radius, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(topCenter, zAxis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(bottomCenter, Direction3D.Create(new Vector3D(0d, 0d, -1d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(floorX, centerY, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(centerX, yMax, 0d), yAxis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(centerX, yMin, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eTopArc, new CurveGeometryId(1), new ParameterInterval(thetaMax, topArcEnd)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eBottomArc, new CurveGeometryId(2), new ParameterInterval(thetaMin, bottomArcEnd)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eCylMax, new CurveGeometryId(3), new ParameterInterval(0d, maxZ - minZ)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eCylMin, new CurveGeometryId(4), new ParameterInterval(0d, maxZ - minZ)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eTopFloor, new CurveGeometryId(5), new ParameterInterval(0d, yMax - yMin)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eBottomFloor, new CurveGeometryId(6), new ParameterInterval(0d, yMax - yMin)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eFloorMax, new CurveGeometryId(7), new ParameterInterval(0d, maxZ - minZ)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eFloorMin, new CurveGeometryId(8), new ParameterInterval(0d, maxZ - minZ)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eTopRadialMax, new CurveGeometryId(9), new ParameterInterval(0d, xOnCylinderAtMaxY - floorX)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eTopRadialMin, new CurveGeometryId(10), new ParameterInterval(0d, xOnCylinderAtMinY - floorX)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eBottomRadialMax, new CurveGeometryId(11), new ParameterInterval(0d, xOnCylinderAtMaxY - floorX)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(eBottomRadialMin, new CurveGeometryId(12), new ParameterInterval(0d, xOnCylinderAtMinY - floorX)));

        bindings.AddFaceBinding(new FaceGeometryBinding(sideCylinderFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topCapFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomCapFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(floorFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(sideMaxFace, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(sideMinFace, new SurfaceGeometryId(6)));

        var points = new Dictionary<VertexId, Point3D>
        {
            [vTopArcMax] = topArcMax,
            [vTopArcMin] = topArcMin,
            [vBottomArcMax] = bottomArcMax,
            [vBottomArcMin] = bottomArcMin,
            [vTopFloorMax] = topFloorMax,
            [vTopFloorMin] = topFloorMin,
            [vBottomFloorMax] = bottomFloorMax,
            [vBottomFloorMin] = bottomFloorMin,
        };

        var body = new BrepBody(builder.Model, geometry, bindings, points, composition);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static FaceId AddFaceWithLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
    {
        var loopId = builder.AllocateLoopId();
        var coedgeIds = new CoedgeId[edgeUses.Count];
        for (var i = 0; i < edgeUses.Count; i++)
        {
            coedgeIds[i] = builder.AllocateCoedgeId();
        }

        for (var i = 0; i < edgeUses.Count; i++)
        {
            var next = coedgeIds[(i + 1) % edgeUses.Count];
            var prev = coedgeIds[(i + edgeUses.Count - 1) % edgeUses.Count];
            builder.AddCoedge(new Coedge(coedgeIds[i], edgeUses[i].EdgeId, loopId, next, prev, edgeUses[i].IsReversed));
        }

        builder.AddLoop(new Loop(loopId, coedgeIds));
        return builder.AddFace([loopId]);
    }

    private static double NormalizeAngle(double value)
    {
        var angle = value % (2d * double.Pi);
        return angle < 0d ? angle + (2d * double.Pi) : angle;
    }

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }
}
