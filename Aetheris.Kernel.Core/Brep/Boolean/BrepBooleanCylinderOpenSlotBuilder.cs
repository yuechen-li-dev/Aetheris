using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Brep.Surgery;
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

        // This bounded recipe knows the retained cylinder arc, slot floor, two
        // radial walls, and both caps before editing begins. Surgery realizes
        // those explicit cycles and never searches for an affected feature.
        var sideCylinderFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Forward(eCylMax),
            BrepEdgeUse.Forward(eTopArc),
            BrepEdgeUse.Forward(eCylMin),
            BrepEdgeUse.Forward(eBottomArc),
        ]);
        var topCapFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Reversed(eTopArc),
            BrepEdgeUse.Forward(eTopRadialMax),
            BrepEdgeUse.Forward(eTopFloor),
            BrepEdgeUse.Forward(eTopRadialMin),
        ]);
        var bottomCapFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Reversed(eBottomArc),
            BrepEdgeUse.Reversed(eBottomRadialMin),
            BrepEdgeUse.Forward(eBottomFloor),
            BrepEdgeUse.Forward(eBottomRadialMax),
        ]);
        var floorFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Reversed(eTopFloor),
            BrepEdgeUse.Forward(eFloorMax),
            BrepEdgeUse.Forward(eBottomFloor),
            BrepEdgeUse.Reversed(eFloorMin),
        ]);
        var sideMaxFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Reversed(eTopRadialMax),
            BrepEdgeUse.Reversed(eCylMax),
            BrepEdgeUse.Reversed(eBottomRadialMax),
            BrepEdgeUse.Forward(eFloorMax),
        ]);
        var sideMinFace = AddFaceWithLegacyLoopSense(builder, [
            BrepEdgeUse.Reversed(eTopRadialMin),
            BrepEdgeUse.Forward(eCylMin),
            BrepEdgeUse.Reversed(eBottomRadialMin),
            BrepEdgeUse.Reversed(eFloorMin),
        ]);

        var faceResults = new[] { sideCylinderFace, topCapFace, bottomCapFace, floorFace, sideMaxFace, sideMinFace };
        var failedFace = faceResults.FirstOrDefault(result => !result.IsSuccess);
        if (failedFace is not null)
        {
            return KernelResult<BrepBody>.Failure(failedFace.Diagnostics);
        }

        var faceIds = faceResults.Select(result => result.Value).ToArray();
        var assembly = BrepShellAssembler.CreateClosedBody(builder, faceIds);
        if (!assembly.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(assembly.Diagnostics);
        }

        var sideCylinderFaceId = faceIds[0];
        var topCapFaceId = faceIds[1];
        var bottomCapFaceId = faceIds[2];
        var floorFaceId = faceIds[3];
        var sideMaxFaceId = faceIds[4];
        var sideMinFaceId = faceIds[5];

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

        bindings.AddFaceBinding(new FaceGeometryBinding(sideCylinderFaceId, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topCapFaceId, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomCapFaceId, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(floorFaceId, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(sideMaxFaceId, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(sideMinFaceId, new SurfaceGeometryId(6)));

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
        var validation = BrepSurgeryValidation.ValidateBody(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static double NormalizeAngle(double value)
    {
        var angle = value % (2d * double.Pi);
        return angle < 0d ? angle + (2d * double.Pi) : angle;
    }

    // M3 preserves the established keyway coedge senses byte-for-byte. The
    // recipe still hands the completed, explicitly selected loop to Surgery;
    // strict new callers use BrepLoopBuilder instead of this control seam.
    private static KernelResult<FaceId> AddFaceWithLegacyLoopSense(TopologyBuilder builder, IReadOnlyList<BrepEdgeUse> edgeUses)
    {
        var loop = BrepLoopBuilder.CreateKnownLoopPreservingLegacySense(builder, edgeUses);
        return loop.IsSuccess
            ? BrepFaceBuilder.CreateKnownFaceFromLoops(builder, loop.Value)
            : KernelResult<FaceId>.Failure(loop.Diagnostics);
    }

}
