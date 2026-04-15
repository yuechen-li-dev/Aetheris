using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Assembly;

public sealed record FirmasmExecutedInstance(
    string InstanceId,
    string PartName,
    string ResolvedPath,
    BrepBody Body,
    Transform3D WorldTransform);

public sealed record FirmasmExecutionResult(
    FirmasmLoadedAssembly LoadedAssembly,
    IReadOnlyList<FirmasmExecutedInstance> Instances,
    BrepBody ComposedBody);

public sealed class FirmasmAssemblyExecutor
{
    private readonly FirmasmManifestLoader _loader = new();

    public KernelResult<FirmasmExecutionResult> ExecuteFromFile(string manifestPath)
    {
        var loadResult = _loader.LoadFromFile(manifestPath);
        if (!loadResult.IsSuccess)
        {
            return KernelResult<FirmasmExecutionResult>.Failure(loadResult.Diagnostics);
        }

        return Execute(loadResult.Value);
    }

    public KernelResult<FirmasmExecutionResult> Execute(FirmasmLoadedAssembly loadedAssembly)
    {
        ArgumentNullException.ThrowIfNull(loadedAssembly);

        var instances = new List<FirmasmExecutedInstance>(loadedAssembly.Manifest.Instances.Count);
        foreach (var instance in loadedAssembly.Manifest.Instances)
        {
            if (!loadedAssembly.LoadedParts.TryGetValue(instance.Part, out var loadedPart))
            {
                return Failure($"Instance '{instance.Id}' references unloaded part '{instance.Part}'.");
            }

            if (loadedPart is not FirmasmLoadedOpaqueStepPart stepPart)
            {
                return Failure($"Instance '{instance.Id}' part '{instance.Part}' has kind '{loadedPart.GetType().Name}' which is not executable in ASM-A3. Only STEP parts are currently supported.");
            }

            var transform = BuildRigidTransform(instance.Transform);
            var transformedBody = TransformBody(stepPart.ImportedBody, transform);
            instances.Add(new FirmasmExecutedInstance(instance.Id, instance.Part, stepPart.ResolvedPath, transformedBody, transform));
        }

        var composedBody = Compose(instances.Select(i => i.Body).ToArray());
        var validation = BrepBindingValidator.Validate(composedBody, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<FirmasmExecutionResult>.Failure(validation.Diagnostics);
        }

        return KernelResult<FirmasmExecutionResult>.Success(
            new FirmasmExecutionResult(loadedAssembly, instances, composedBody),
            validation.Diagnostics);
    }

    internal static Transform3D BuildRigidTransform(FirmasmRigidTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        var world = Transform3D.Identity;
        if (transform.RotateDegXyz is { Count: 3 } rotate)
        {
            world = world
                * Transform3D.CreateRotationX(ToRadians(rotate[0]))
                * Transform3D.CreateRotationY(ToRadians(rotate[1]))
                * Transform3D.CreateRotationZ(ToRadians(rotate[2]));
        }

        var translation = new Vector3D(transform.Translate[0], transform.Translate[1], transform.Translate[2]);
        world = world * Transform3D.CreateTranslation(translation);
        return world;
    }

    private static BrepBody TransformBody(BrepBody sourceBody, Transform3D transform)
    {
        var transformedGeometry = new BrepGeometryStore();
        foreach (var (curveId, curve) in sourceBody.Geometry.Curves)
        {
            transformedGeometry.AddCurve(curveId, TransformCurve(curve, transform));
        }

        foreach (var (surfaceId, surface) in sourceBody.Geometry.Surfaces)
        {
            transformedGeometry.AddSurface(surfaceId, TransformSurface(surface, transform));
        }

        var transformedVertices = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in sourceBody.Topology.Vertices)
        {
            if (sourceBody.TryGetVertexPoint(vertex.Id, out var point))
            {
                transformedVertices[vertex.Id] = transform.Apply(point);
            }
        }

        return new BrepBody(
            sourceBody.Topology,
            transformedGeometry,
            sourceBody.Bindings,
            transformedVertices,
            sourceBody.SafeBooleanComposition,
            sourceBody.ShellRepresentation);
    }

    private static CurveGeometry TransformCurve(CurveGeometry curve, Transform3D transform)
    {
        return curve.Kind switch
        {
            CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(
                transform.Apply(curve.Line3!.Value.Origin),
                transform.Apply(curve.Line3.Value.Direction, ToleranceContext.Default))),
            CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(
                transform.Apply(curve.Circle3!.Value.Center),
                transform.Apply(curve.Circle3.Value.Normal, ToleranceContext.Default),
                curve.Circle3.Value.Radius,
                transform.Apply(curve.Circle3.Value.XAxis, ToleranceContext.Default))),
            CurveGeometryKind.BSpline3 => CurveGeometry.FromBSpline(new BSpline3Curve(
                curve.BSpline3!.Value.Degree,
                curve.BSpline3.Value.ControlPoints.Select(transform.Apply).ToArray(),
                curve.BSpline3.Value.KnotMultiplicities,
                curve.BSpline3.Value.KnotValues,
                curve.BSpline3.Value.CurveForm,
                curve.BSpline3.Value.ClosedCurve,
                curve.BSpline3.Value.SelfIntersect,
                curve.BSpline3.Value.KnotSpec)),
            CurveGeometryKind.Ellipse3 => CurveGeometry.FromEllipse(new Ellipse3Curve(
                transform.Apply(curve.Ellipse3!.Value.Center),
                transform.Apply(curve.Ellipse3.Value.Normal, ToleranceContext.Default),
                curve.Ellipse3.Value.MajorRadius,
                curve.Ellipse3.Value.MinorRadius,
                transform.Apply(curve.Ellipse3.Value.XAxis, ToleranceContext.Default))),
            _ => curve
        };
    }

    private static SurfaceGeometry TransformSurface(SurfaceGeometry surface, Transform3D transform)
    {
        return surface.Kind switch
        {
            SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(
                transform.Apply(surface.Plane!.Value.Origin),
                transform.Apply(surface.Plane.Value.Normal, ToleranceContext.Default),
                transform.Apply(surface.Plane.Value.UAxis, ToleranceContext.Default))),
            SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(
                transform.Apply(surface.Cylinder!.Value.Origin),
                transform.Apply(surface.Cylinder.Value.Axis, ToleranceContext.Default),
                surface.Cylinder.Value.Radius,
                transform.Apply(surface.Cylinder.Value.XAxis, ToleranceContext.Default))),
            SurfaceGeometryKind.Cone => SurfaceGeometry.FromCone(new ConeSurface(
                transform.Apply(surface.Cone!.Value.PlacementOrigin),
                transform.Apply(surface.Cone.Value.Axis, ToleranceContext.Default),
                surface.Cone.Value.PlacementRadius,
                surface.Cone.Value.SemiAngleRadians,
                transform.Apply(surface.Cone.Value.ReferenceAxis, ToleranceContext.Default))),
            SurfaceGeometryKind.Sphere => SurfaceGeometry.FromSphere(new SphereSurface(
                transform.Apply(surface.Sphere!.Value.Center),
                transform.Apply(surface.Sphere.Value.Axis, ToleranceContext.Default),
                surface.Sphere.Value.Radius,
                transform.Apply(surface.Sphere.Value.XAxis, ToleranceContext.Default))),
            SurfaceGeometryKind.Torus => SurfaceGeometry.FromTorus(new TorusSurface(
                transform.Apply(surface.Torus!.Value.Center),
                transform.Apply(surface.Torus.Value.Axis, ToleranceContext.Default),
                surface.Torus.Value.MajorRadius,
                surface.Torus.Value.MinorRadius,
                transform.Apply(surface.Torus.Value.XAxis, ToleranceContext.Default))),
            SurfaceGeometryKind.BSplineSurfaceWithKnots => SurfaceGeometry.FromBSplineSurfaceWithKnots(new BSplineSurfaceWithKnots(
                surface.BSplineSurfaceWithKnots!.DegreeU,
                surface.BSplineSurfaceWithKnots.DegreeV,
                surface.BSplineSurfaceWithKnots.ControlPoints.Select(row => row.Select(transform.Apply).ToArray()).ToArray(),
                surface.BSplineSurfaceWithKnots.SurfaceForm,
                surface.BSplineSurfaceWithKnots.UClosed,
                surface.BSplineSurfaceWithKnots.VClosed,
                surface.BSplineSurfaceWithKnots.SelfIntersect,
                surface.BSplineSurfaceWithKnots.KnotMultiplicitiesU,
                surface.BSplineSurfaceWithKnots.KnotMultiplicitiesV,
                surface.BSplineSurfaceWithKnots.KnotValuesU,
                surface.BSplineSurfaceWithKnots.KnotValuesV,
                surface.BSplineSurfaceWithKnots.KnotSpec)),
            _ => surface
        };
    }

    private static BrepBody Compose(IReadOnlyList<BrepBody> bodies)
    {
        if (bodies.Count == 0)
        {
            throw new InvalidOperationException("At least one body must be provided for assembly composition.");
        }

        var topology = new TopologyModel();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var vertexPoints = new Dictionary<VertexId, Point3D>();

        var nextVertexId = 1;
        var nextEdgeId = 1;
        var nextCoedgeId = 1;
        var nextLoopId = 1;
        var nextFaceId = 1;
        var nextShellId = 1;
        var nextBodyId = 1;
        var nextCurveId = 1;
        var nextSurfaceId = 1;

        foreach (var sourceBody in bodies)
        {
            var vertexMap = sourceBody.Topology.Vertices
                .OrderBy(v => v.Id.Value)
                .ToDictionary(v => v.Id, _ => new VertexId(nextVertexId++));
            foreach (var (oldVertexId, newVertexId) in vertexMap)
            {
                topology.AddVertex(new Vertex(newVertexId));
                if (sourceBody.TryGetVertexPoint(oldVertexId, out var point))
                {
                    vertexPoints[newVertexId] = point;
                }
            }

            var edgeMap = sourceBody.Topology.Edges
                .OrderBy(e => e.Id.Value)
                .ToDictionary(e => e.Id, _ => new EdgeId(nextEdgeId++));
            foreach (var edge in sourceBody.Topology.Edges.OrderBy(e => e.Id.Value))
            {
                topology.AddEdge(new Edge(edgeMap[edge.Id], vertexMap[edge.StartVertexId], vertexMap[edge.EndVertexId]));
            }

            var coedgeMap = sourceBody.Topology.Coedges
                .OrderBy(c => c.Id.Value)
                .ToDictionary(c => c.Id, _ => new CoedgeId(nextCoedgeId++));
            var loopMap = sourceBody.Topology.Loops
                .OrderBy(l => l.Id.Value)
                .ToDictionary(l => l.Id, _ => new LoopId(nextLoopId++));
            var faceMap = sourceBody.Topology.Faces
                .OrderBy(f => f.Id.Value)
                .ToDictionary(f => f.Id, _ => new FaceId(nextFaceId++));
            var shellMap = sourceBody.Topology.Shells
                .OrderBy(s => s.Id.Value)
                .ToDictionary(s => s.Id, _ => new ShellId(nextShellId++));
            var bodyMap = sourceBody.Topology.Bodies
                .OrderBy(b => b.Id.Value)
                .ToDictionary(b => b.Id, _ => new BodyId(nextBodyId++));

            foreach (var coedge in sourceBody.Topology.Coedges.OrderBy(c => c.Id.Value))
            {
                topology.AddCoedge(new Coedge(
                    coedgeMap[coedge.Id],
                    edgeMap[coedge.EdgeId],
                    loopMap[coedge.LoopId],
                    coedgeMap[coedge.NextCoedgeId],
                    coedgeMap[coedge.PrevCoedgeId],
                    coedge.IsReversed));
            }

            foreach (var loop in sourceBody.Topology.Loops.OrderBy(l => l.Id.Value))
            {
                topology.AddLoop(new Loop(loopMap[loop.Id], loop.CoedgeIds.Select(id => coedgeMap[id]).ToArray()));
            }

            foreach (var face in sourceBody.Topology.Faces.OrderBy(f => f.Id.Value))
            {
                topology.AddFace(new Face(faceMap[face.Id], face.LoopIds.Select(id => loopMap[id]).ToArray()));
            }

            foreach (var shell in sourceBody.Topology.Shells.OrderBy(s => s.Id.Value))
            {
                topology.AddShell(new Shell(shellMap[shell.Id], shell.FaceIds.Select(id => faceMap[id]).ToArray()));
            }

            foreach (var body in sourceBody.Topology.Bodies.OrderBy(b => b.Id.Value))
            {
                topology.AddBody(new Body(bodyMap[body.Id], body.ShellIds.Select(id => shellMap[id]).ToArray()));
            }

            var curveMap = sourceBody.Geometry.Curves
                .OrderBy(c => c.Key.Value)
                .ToDictionary(c => c.Key, _ => new CurveGeometryId(nextCurveId++));
            foreach (var (curveId, curve) in sourceBody.Geometry.Curves.OrderBy(c => c.Key.Value))
            {
                geometry.AddCurve(curveMap[curveId], curve);
            }

            var surfaceMap = sourceBody.Geometry.Surfaces
                .OrderBy(s => s.Key.Value)
                .ToDictionary(s => s.Key, _ => new SurfaceGeometryId(nextSurfaceId++));
            foreach (var (surfaceId, surface) in sourceBody.Geometry.Surfaces.OrderBy(s => s.Key.Value))
            {
                geometry.AddSurface(surfaceMap[surfaceId], surface);
            }

            foreach (var edgeBinding in sourceBody.Bindings.EdgeBindings.OrderBy(b => b.EdgeId.Value))
            {
                bindings.AddEdgeBinding(new EdgeGeometryBinding(
                    edgeMap[edgeBinding.EdgeId],
                    curveMap[edgeBinding.CurveGeometryId],
                    edgeBinding.TrimInterval,
                    edgeBinding.OrientedEdgeSense));
            }

            foreach (var faceBinding in sourceBody.Bindings.FaceBindings.OrderBy(b => b.FaceId.Value))
            {
                bindings.AddFaceBinding(new FaceGeometryBinding(faceMap[faceBinding.FaceId], surfaceMap[faceBinding.SurfaceGeometryId]));
            }
        }

        return new BrepBody(topology, geometry, bindings, vertexPoints);
    }

    private static double ToRadians(double degrees) => degrees * (double.Pi / 180d);

    private static KernelResult<FirmasmExecutionResult> Failure(string message)
    {
        return KernelResult<FirmasmExecutionResult>.Failure([
            new KernelDiagnostic(
                KernelDiagnosticCode.ValidationFailed,
                KernelDiagnosticSeverity.Error,
                message,
                Source: "firmasm")
        ]);
    }
}
