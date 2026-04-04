using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Boolean;

public static class BrepBooleanBoxCylinderHoleBuilder
{
    public static KernelResult<BrepBody> BuildAnalyticHole(AxisAlignedBoxExtents outer, AnalyticSurface surface, ToleranceContext tolerance)
    {
        var composition = new SafeBooleanComposition(outer, []);
        if (!BrepBooleanSafeComposition.TryAppend(composition, surface, tolerance, out var updatedComposition, out var diagnostic))
        {
            return KernelResult<BrepBody>.Failure([diagnostic!.ToKernelDiagnostic()]);
        }

        return BuildComposition(updatedComposition, tolerance);
    }

    public static KernelResult<BrepBody> BuildComposition(SafeBooleanComposition composition, ToleranceContext tolerance)
    {
        ArgumentNullException.ThrowIfNull(composition);
        _ = tolerance;
        if (composition.Holes.Count == 1
            && composition.Holes[0].Surface.Kind == AnalyticSurfaceKind.Sphere
            && composition.Holes[0].Surface.Sphere is RecognizedSphere sphere)
        {
            return CreateComposedContainedSphereCavityBody(composition.OuterBox, sphere);
        }

        if (composition.Holes.Any(hole => hole.Surface.Kind == AnalyticSurfaceKind.Sphere))
        {
            return KernelResult<BrepBody>.Failure([
                new BooleanDiagnostic(
                    BooleanDiagnosticCode.UnsupportedAnalyticSurfaceKind,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        null,
                        "safe subtract composition only supports one contained spherical cavity when no prior holes have been accepted."),
                    "BrepBoolean.AnalyticHole.UnsupportedAnalyticSurfaceKind").ToKernelDiagnostic(),
            ]);
        }

        return CreateComposedThroughHoleBody(composition);
    }

    private static KernelResult<BrepBody> CreateComposedContainedSphereCavityBody(AxisAlignedBoxExtents outerBox, in RecognizedSphere sphere)
    {
        var outer = BrepBooleanBoxRecognition.CreateBoxFromExtents(outerBox);
        if (!outer.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(outer.Diagnostics);
        }

        var innerResult = CreateSphereShell(sphere);
        if (!innerResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(innerResult.Diagnostics);
        }

        var merged = MergeOuterAndInnerShell(outer.Value, innerResult.Value);
        var validation = BrepBindingValidator.Validate(merged, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(merged, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static BrepBody MergeOuterAndInnerShell(BrepBody outer, BrepBody inner)
    {
        const int idOffset = 1000;
        var topology = new TopologyModel();

        foreach (var vertex in outer.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            topology.AddVertex(vertex);
        }

        foreach (var edge in outer.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            topology.AddEdge(edge);
        }

        foreach (var coedge in outer.Topology.Coedges.OrderBy(c => c.Id.Value))
        {
            topology.AddCoedge(coedge);
        }

        foreach (var loop in outer.Topology.Loops.OrderBy(l => l.Id.Value))
        {
            topology.AddLoop(loop);
        }

        foreach (var face in outer.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            topology.AddFace(face);
        }

        foreach (var shell in outer.Topology.Shells.OrderBy(s => s.Id.Value))
        {
            topology.AddShell(shell);
        }

        var innerVertexMap = inner.Topology.Vertices.ToDictionary(v => v.Id, v => new VertexId(v.Id.Value + idOffset));
        var innerEdgeMap = inner.Topology.Edges.ToDictionary(e => e.Id, e => new EdgeId(e.Id.Value + idOffset));
        var innerLoopMap = inner.Topology.Loops.ToDictionary(l => l.Id, l => new LoopId(l.Id.Value + idOffset));
        var innerCoedgeMap = inner.Topology.Coedges.ToDictionary(c => c.Id, c => new CoedgeId(c.Id.Value + idOffset));
        var innerFaceMap = inner.Topology.Faces.ToDictionary(f => f.Id, f => new FaceId(f.Id.Value + idOffset));
        var innerShellMap = inner.Topology.Shells.ToDictionary(s => s.Id, s => new ShellId(s.Id.Value + idOffset));

        foreach (var vertex in inner.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            topology.AddVertex(new Vertex(innerVertexMap[vertex.Id]));
        }

        foreach (var edge in inner.Topology.Edges.OrderBy(e => e.Id.Value))
        {
            topology.AddEdge(new Edge(innerEdgeMap[edge.Id], innerVertexMap[edge.StartVertexId], innerVertexMap[edge.EndVertexId]));
        }

        foreach (var coedge in inner.Topology.Coedges.OrderBy(c => c.Id.Value))
        {
            topology.AddCoedge(new Coedge(
                innerCoedgeMap[coedge.Id],
                innerEdgeMap[coedge.EdgeId],
                innerLoopMap[coedge.LoopId],
                innerCoedgeMap[coedge.NextCoedgeId],
                innerCoedgeMap[coedge.PrevCoedgeId],
                coedge.IsReversed));
        }

        foreach (var loop in inner.Topology.Loops.OrderBy(l => l.Id.Value))
        {
            topology.AddLoop(new Loop(innerLoopMap[loop.Id], loop.CoedgeIds.Select(id => innerCoedgeMap[id]).ToArray()));
        }

        foreach (var face in inner.Topology.Faces.OrderBy(f => f.Id.Value))
        {
            topology.AddFace(new Face(innerFaceMap[face.Id], face.LoopIds.Select(id => innerLoopMap[id]).ToArray()));
        }

        foreach (var shell in inner.Topology.Shells.OrderBy(s => s.Id.Value))
        {
            topology.AddShell(new Shell(innerShellMap[shell.Id], shell.FaceIds.Select(id => innerFaceMap[id]).ToArray()));
        }

        var outerShellId = AssertSingleShellId(outer.Topology);
        var remappedInnerShellId = innerShellMap[AssertSingleShellId(inner.Topology)];
        topology.AddBody(new Body(new BodyId(1), [outerShellId, remappedInnerShellId]));

        var geometry = new BrepGeometryStore();
        foreach (var curve in outer.Geometry.Curves)
        {
            geometry.AddCurve(curve.Key, curve.Value);
        }

        foreach (var surface in outer.Geometry.Surfaces)
        {
            geometry.AddSurface(surface.Key, surface.Value);
        }

        foreach (var curve in inner.Geometry.Curves)
        {
            geometry.AddCurve(new CurveGeometryId(curve.Key.Value + idOffset), curve.Value);
        }

        foreach (var surface in inner.Geometry.Surfaces)
        {
            geometry.AddSurface(new SurfaceGeometryId(surface.Key.Value + idOffset), surface.Value);
        }

        var bindings = new BrepBindingModel();
        foreach (var edgeBinding in outer.Bindings.EdgeBindings.OrderBy(binding => binding.EdgeId.Value))
        {
            bindings.AddEdgeBinding(edgeBinding);
        }

        foreach (var faceBinding in outer.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            bindings.AddFaceBinding(faceBinding);
        }

        foreach (var edgeBinding in inner.Bindings.EdgeBindings.OrderBy(binding => binding.EdgeId.Value))
        {
            bindings.AddEdgeBinding(edgeBinding with
            {
                EdgeId = innerEdgeMap[edgeBinding.EdgeId],
                CurveGeometryId = new CurveGeometryId(edgeBinding.CurveGeometryId.Value + idOffset),
            });
        }

        foreach (var faceBinding in inner.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            bindings.AddFaceBinding(faceBinding with
            {
                FaceId = innerFaceMap[faceBinding.FaceId],
                SurfaceGeometryId = new SurfaceGeometryId(faceBinding.SurfaceGeometryId.Value + idOffset),
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in outer.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            if (outer.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = point;
            }
        }

        foreach (var vertex in inner.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            if (inner.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[innerVertexMap[vertex.Id]] = point;
            }
        }

        return new BrepBody(
            topology,
            geometry,
            bindings,
            vertexPoints,
            safeBooleanComposition: null,
            shellRepresentation: new BrepBodyShellRepresentation(outerShellId, [remappedInnerShellId]));
    }

    private static ShellId AssertSingleShellId(TopologyModel topology)
        => topology.Bodies.Single().ShellIds.Single();

    private static KernelResult<BrepBody> CreateSphereShell(in RecognizedSphere sphere)
    {
        var builder = new TopologyBuilder();
        var sphereFace = builder.AddFace([]);
        var sphereShell = builder.AddShell([sphereFace]);
        builder.AddBody([sphereShell]);

        var geometry = new BrepGeometryStore();
        geometry.AddSurface(
            new SurfaceGeometryId(1),
            SurfaceGeometry.FromSphere(new SphereSurface(
                sphere.Center,
                Direction3D.Create(new Vector3D(0d, 0d, 1d)),
                sphere.Radius,
                Direction3D.Create(new Vector3D(1d, 0d, 0d)))));

        var bindings = new BrepBindingModel();
        bindings.AddFaceBinding(new FaceGeometryBinding(sphereFace, new SurfaceGeometryId(1)));

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints: null, safeBooleanComposition: null);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static KernelResult<BrepBody> CreateComposedThroughHoleBody(SafeBooleanComposition composition)
    {
        var box = composition.OuterBox;
        var holes = composition.Holes;
        if (holes.Count > 1 && holes.Any(h => h.IsBlind))
        {
            return KernelResult<BrepBody>.Failure([
                new BooleanDiagnostic(
                    BooleanDiagnosticCode.UnsupportedBlindHoleComposition,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        null,
                        "cannot rebuild a composition with more than one blind analytic hole in B1; blind-hole support is limited to single-feature subtracts."),
                    "BrepBoolean.AnalyticHole.UnsupportedBlindHoleComposition").ToKernelDiagnostic(),
            ]);
        }

        var builder = new TopologyBuilder();

        var v1 = builder.AddVertex();
        var v2 = builder.AddVertex();
        var v3 = builder.AddVertex();
        var v4 = builder.AddVertex();
        var v5 = builder.AddVertex();
        var v6 = builder.AddVertex();
        var v7 = builder.AddVertex();
        var v8 = builder.AddVertex();

        var e1 = builder.AddEdge(v1, v2);
        var e2 = builder.AddEdge(v2, v3);
        var e3 = builder.AddEdge(v3, v4);
        var e4 = builder.AddEdge(v4, v1);
        var e5 = builder.AddEdge(v5, v6);
        var e6 = builder.AddEdge(v6, v7);
        var e7 = builder.AddEdge(v7, v8);
        var e8 = builder.AddEdge(v8, v5);
        var e9 = builder.AddEdge(v1, v5);
        var e10 = builder.AddEdge(v2, v6);
        var e11 = builder.AddEdge(v3, v7);
        var e12 = builder.AddEdge(v4, v8);

        var holeTopology = new List<HoleTopology>(holes.Count);
        foreach (var hole in holes)
        {
            var topHoleVertex = builder.AddVertex();
            var bottomHoleVertex = builder.AddVertex();
            var seamTopVertex = builder.AddVertex();
            var seamBottomVertex = builder.AddVertex();

            var topCircle = builder.AddEdge(topHoleVertex, topHoleVertex);
            var bottomCircle = builder.AddEdge(bottomHoleVertex, bottomHoleVertex);
            var seam = builder.AddEdge(seamTopVertex, seamBottomVertex);

            holeTopology.Add(new HoleTopology(hole, topHoleVertex, bottomHoleVertex, seamTopVertex, seamBottomVertex, topCircle, bottomCircle, seam));
        }

        var bottomOuterLoop = AddLoop(builder, [Forward(e1), Forward(e2), Forward(e3), Forward(e4)]);
        var topOuterLoop = AddLoop(builder, [Forward(e5), Forward(e6), Forward(e7), Forward(e8)]);
        var bottomLoops = new List<LoopId>(holes.Count + 1) { bottomOuterLoop };
        var topLoops = new List<LoopId>(holes.Count + 1) { topOuterLoop };
        foreach (var hole in holeTopology)
        {
            if (hole.Hole.SpanKind == SupportedBooleanHoleSpanKind.Through)
            {
                topLoops.Add(AddLoop(builder, [Forward(hole.TopCircle)]));
                bottomLoops.Add(AddLoop(builder, [Reversed(hole.BottomCircle)]));
            }
            else if (hole.Hole.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop)
            {
                topLoops.Add(AddLoop(builder, [Forward(hole.TopCircle)]));
            }
            else
            {
                bottomLoops.Add(AddLoop(builder, [Reversed(hole.BottomCircle)]));
            }
        }

        var bottomFace = builder.AddFace(bottomLoops);
        var topFace = builder.AddFace(topLoops);
        var xMinFace = builder.AddFace([AddLoop(builder, [Forward(e1), Forward(e10), Reversed(e5), Reversed(e9)])]);
        var xMaxFace = builder.AddFace([AddLoop(builder, [Forward(e2), Forward(e11), Reversed(e6), Reversed(e10)])]);
        var yMaxFace = builder.AddFace([AddLoop(builder, [Forward(e3), Forward(e12), Reversed(e7), Reversed(e11)])]);
        var yMinFace = builder.AddFace([AddLoop(builder, [Forward(e4), Forward(e9), Reversed(e8), Reversed(e12)])]);

        var holeFaces = new List<FaceId>(holes.Count);
        foreach (var hole in holeTopology)
        {
            holeFaces.Add(builder.AddFace([
                AddLoop(builder, [Forward(hole.Seam), Reversed(hole.TopCircle), Reversed(hole.Seam), Forward(hole.BottomCircle)])
            ]));
        }

        var blindBottomFaces = new List<FaceId>();
        foreach (var hole in holeTopology.Where(h => h.Hole.IsBlind))
        {
            var terminationEdge = hole.Hole.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop ? hole.BottomCircle : hole.TopCircle;
            blindBottomFaces.Add(builder.AddFace([AddLoop(builder, [Forward(terminationEdge)])]));
        }

        var shellFaces = new List<FaceId>(6 + holeFaces.Count)
        {
            bottomFace,
            topFace,
            xMinFace,
            xMaxFace,
            yMaxFace,
            yMinFace,
        };
        shellFaces.AddRange(holeFaces);
        shellFaces.AddRange(blindBottomFaces);

        var shell = builder.AddShell(shellFaces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var width = box.MaxX - box.MinX;
        var depth = box.MaxY - box.MinY;
        var height = box.MaxZ - box.MinZ;

        var p1 = new Point3D(box.MinX, box.MinY, box.MinZ);
        var p2 = new Point3D(box.MaxX, box.MinY, box.MinZ);
        var p3 = new Point3D(box.MaxX, box.MaxY, box.MinZ);
        var p4 = new Point3D(box.MinX, box.MaxY, box.MinZ);
        var p5 = new Point3D(box.MinX, box.MinY, box.MaxZ);
        var p6 = new Point3D(box.MaxX, box.MinY, box.MaxZ);
        var p7 = new Point3D(box.MaxX, box.MaxY, box.MaxZ);
        var p8 = new Point3D(box.MinX, box.MaxY, box.MaxZ);

        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));

        var lineCurves = new[]
        {
            (p1, new Vector3D(width, 0d, 0d)),
            (p2, new Vector3D(0d, depth, 0d)),
            (p3, new Vector3D(-width, 0d, 0d)),
            (p4, new Vector3D(0d, -depth, 0d)),
            (p5, new Vector3D(width, 0d, 0d)),
            (p6, new Vector3D(0d, depth, 0d)),
            (p7, new Vector3D(-width, 0d, 0d)),
            (p8, new Vector3D(0d, -depth, 0d)),
            (p1, new Vector3D(0d, 0d, height)),
            (p2, new Vector3D(0d, 0d, height)),
            (p3, new Vector3D(0d, 0d, height)),
            (p4, new Vector3D(0d, 0d, height)),
        };

        for (var i = 0; i < lineCurves.Length; i++)
        {
            geometry.AddCurve(new CurveGeometryId(i + 1), CurveGeometry.FromLine(new Line3Curve(lineCurves[i].Item1, Direction3D.Create(lineCurves[i].Item2))));
        }

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MinZ), Direction3D.Create(new Vector3D(0d, 0d, -1d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MaxZ), zAxis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MinY, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MaxX, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MaxY, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MinX, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)), yAxis)));

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 12; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(i + 1), new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        }

        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMinFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMaxFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yMaxFace, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yMinFace, new SurfaceGeometryId(6)));

        var vertexPoints = new Dictionary<VertexId, Point3D>
        {
            [v1] = p1,
            [v2] = p2,
            [v3] = p3,
            [v4] = p4,
            [v5] = p5,
            [v6] = p6,
            [v7] = p7,
            [v8] = p8,
        };

        var nextCurveId = 13;
        var nextSurfaceId = 7;
        var blindBottomFaceIndex = 0;
        for (var i = 0; i < holeTopology.Count; i++)
        {
            var topology = holeTopology[i];
            var geometryData = CreateHoleGeometry(topology.Hole, box, zAxis, xAxis);
            var topCurveId = new CurveGeometryId(nextCurveId++);
            var bottomCurveId = new CurveGeometryId(nextCurveId++);
            var seamCurveId = new CurveGeometryId(nextCurveId++);
            var surfaceId = new SurfaceGeometryId(nextSurfaceId++);

            geometry.AddCurve(topCurveId, geometryData.TopCircle);
            geometry.AddCurve(bottomCurveId, geometryData.BottomCircle);
            geometry.AddCurve(seamCurveId, geometryData.Seam);
            geometry.AddSurface(surfaceId, geometryData.Surface);

            bindings.AddEdgeBinding(new EdgeGeometryBinding(topology.TopCircle, topCurveId, new ParameterInterval(0d, 2d * double.Pi)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(topology.BottomCircle, bottomCurveId, new ParameterInterval(0d, 2d * double.Pi)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(topology.Seam, seamCurveId, new ParameterInterval(0d, geometryData.SeamLength)));
            bindings.AddFaceBinding(new FaceGeometryBinding(holeFaces[i], surfaceId));

            if (topology.Hole.IsBlind)
            {
                var blindBottomCenter = topology.Hole.SpanKind == SupportedBooleanHoleSpanKind.BlindFromTop
                    ? geometryData.BottomCenter
                    : geometryData.TopCenter;
                var blindBottomSurfaceId = new SurfaceGeometryId(nextSurfaceId++);
                geometry.AddSurface(blindBottomSurfaceId, SurfaceGeometry.FromPlane(new PlaneSurface(
                    new Point3D(blindBottomCenter.X, blindBottomCenter.Y, blindBottomCenter.Z),
                    Direction3D.Create(new Vector3D(0d, 0d, -1d)),
                    xAxis)));
                bindings.AddFaceBinding(new FaceGeometryBinding(blindBottomFaces[blindBottomFaceIndex++], blindBottomSurfaceId));
            }

            vertexPoints[topology.TopHoleVertex] = geometryData.SeamTopPoint;
            vertexPoints[topology.BottomHoleVertex] = geometryData.SeamBottomPoint;
            vertexPoints[topology.SeamTopVertex] = geometryData.SeamTopPoint;
            vertexPoints[topology.SeamBottomVertex] = geometryData.SeamBottomPoint;
        }

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints, composition);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static HoleGeometryData CreateHoleGeometry(SupportedBooleanHole hole, AxisAlignedBoxExtents box, Direction3D zAxis, Direction3D xAxis)
    {
        return hole.Surface.Kind switch
        { 
            AnalyticSurfaceKind.Cylinder when hole.Surface.Cylinder is RecognizedCylinder cylinder => CreateCylinderHoleGeometry(hole, xAxis, zAxis, cylinder),
            AnalyticSurfaceKind.Cone when hole.Surface.Cone is RecognizedCone cone => CreateConeHoleGeometry(hole, xAxis, zAxis, cone),
            _ => throw new InvalidOperationException($"Unsupported hole surface kind '{hole.Surface.Kind}'."),
        };
    }

    private static HoleGeometryData CreateCylinderHoleGeometry(SupportedBooleanHole hole, Direction3D xAxis, Direction3D zAxis, in RecognizedCylinder cylinder)
    {
        var centerX = cylinder.MinCenter.X;
        var centerY = cylinder.MinCenter.Y;
        var topZ = System.Math.Max(hole.StartZ, hole.EndZ);
        var bottomZ = System.Math.Min(hole.StartZ, hole.EndZ);
        var topCenter = new Point3D(centerX, centerY, topZ);
        var bottomCenter = new Point3D(centerX, centerY, bottomZ);
        var seamBottomPoint = new Point3D(centerX + cylinder.Radius, centerY, bottomZ);
        var seamTopPoint = new Point3D(centerX + cylinder.Radius, centerY, topZ);

        return new HoleGeometryData(
            CurveGeometry.FromCircle(new Circle3Curve(topCenter, zAxis, cylinder.Radius, xAxis)),
            CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, zAxis, cylinder.Radius, xAxis)),
            CurveGeometry.FromLine(new Line3Curve(seamTopPoint, Direction3D.Create(seamBottomPoint - seamTopPoint))),
            SurfaceGeometry.FromCylinder(new CylinderSurface(bottomCenter, zAxis, cylinder.Radius, xAxis)),
            topCenter,
            bottomCenter,
            seamTopPoint,
            seamBottomPoint,
            (seamBottomPoint - seamTopPoint).Length);
    }

    private static HoleGeometryData CreateConeHoleGeometry(SupportedBooleanHole hole, Direction3D xAxis, Direction3D zAxis, in RecognizedCone cone)
    {
        var bottomZ = System.Math.Min(hole.StartZ, hole.EndZ);
        var topZ = System.Math.Max(hole.StartZ, hole.EndZ);
        var bottomAxisParameter = AxisParameterAtZ(cone, bottomZ);
        var topAxisParameter = AxisParameterAtZ(cone, topZ);
        var bottomCenter = cone.PointAtAxisParameter(bottomAxisParameter);
        var topCenter = cone.PointAtAxisParameter(topAxisParameter);
        var bottomRadius = hole.BottomRadius;
        var topRadius = hole.TopRadius;
        var seamBottomPoint = new Point3D(bottomCenter.X + bottomRadius, bottomCenter.Y, bottomCenter.Z);
        var seamTopPoint = new Point3D(topCenter.X + topRadius, topCenter.Y, topCenter.Z);
        var innerMinAxisParameter = System.Math.Min(bottomAxisParameter, topAxisParameter);
        var innerMinCenter = cone.PointAtAxisParameter(innerMinAxisParameter);
        var innerMinRadius = cone.RadiusAtAxisParameter(innerMinAxisParameter);

        return new HoleGeometryData(
            CurveGeometry.FromCircle(new Circle3Curve(topCenter, zAxis, topRadius, xAxis)),
            CurveGeometry.FromCircle(new Circle3Curve(bottomCenter, zAxis, bottomRadius, xAxis)),
            CurveGeometry.FromLine(new Line3Curve(seamTopPoint, Direction3D.Create(seamBottomPoint - seamTopPoint))),
            SurfaceGeometry.FromCone(new ConeSurface(innerMinCenter, cone.Axis, innerMinRadius, cone.SemiAngleRadians, xAxis)),
            topCenter,
            bottomCenter,
            seamTopPoint,
            seamBottomPoint,
            (seamBottomPoint - seamTopPoint).Length);
    }

    private static double AxisParameterAtZ(in RecognizedCone cone, double z)
    {
        var axisZ = cone.Axis.ToVector().Z;
        return (z - cone.AxisOrigin.Z) / axisZ;
    }

    private static LoopId AddLoop(TopologyBuilder builder, IReadOnlyList<EdgeUse> edgeUses)
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
        return loopId;
    }

    private readonly record struct HoleTopology(
        SupportedBooleanHole Hole,
        VertexId TopHoleVertex,
        VertexId BottomHoleVertex,
        VertexId SeamTopVertex,
        VertexId SeamBottomVertex,
        EdgeId TopCircle,
        EdgeId BottomCircle,
        EdgeId Seam);

    private readonly record struct HoleGeometryData(
        CurveGeometry TopCircle,
        CurveGeometry BottomCircle,
        CurveGeometry Seam,
        SurfaceGeometry Surface,
        Point3D TopCenter,
        Point3D BottomCenter,
        Point3D SeamTopPoint,
        Point3D SeamBottomPoint,
        double SeamLength);

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed);

    private static EdgeUse Forward(EdgeId edgeId) => new(edgeId, IsReversed: false);

    private static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, IsReversed: true);
}
