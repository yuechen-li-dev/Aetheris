using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Brep.Features;

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
        if (composition.RootDescriptor.Kind == SafeBooleanRootKind.Cylinder)
        {
            return CreateBoundedCylinderRootHoleChainBody(composition, tolerance);
        }

        if (composition.Holes.Count == 1
            && composition.Holes[0].Surface.Kind == AnalyticSurfaceKind.Sphere
            && composition.Holes[0].Surface.Sphere is RecognizedSphere sphere)
        {
            return CreateComposedContainedSphereCavityBody(composition.OuterBox, sphere);
        }

        if (composition.Holes.Count == 1
            && composition.Holes[0].SpanKind == SupportedBooleanHoleSpanKind.Contained
            && composition.Holes[0].Surface.Kind == AnalyticSurfaceKind.Cylinder
            && composition.Holes[0].Surface.Cylinder is RecognizedCylinder containedCylinder)
        {
            return CreateComposedContainedCylinderCavityBody(composition.OuterBox, containedCylinder, composition);
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

        if (composition.Holes.Count == 2 && composition.Holes.Any(h => h.IsBlind))
        {
            if (BrepBooleanCoaxialSubtractStackFamily.TryClassifyPair(
                composition.OuterBox,
                composition.Holes[0],
                composition.Holes[1],
                tolerance,
                out var steppedProfile,
                out var steppedDiagnostic))
            {
                return CreateSteppedCoaxialCylinderBody(composition, steppedProfile);
            }

            if (steppedDiagnostic is not null)
            {
                return KernelResult<BrepBody>.Failure([steppedDiagnostic.ToKernelDiagnostic()]);
            }
        }

        return CreateComposedThroughHoleBody(composition);
    }

    private static KernelResult<BrepBody> CreateBoundedCylinderRootHoleChainBody(SafeBooleanComposition composition, ToleranceContext tolerance)
    {
        if (composition.RootDescriptor.Cylinder is not RecognizedCylinder rootCylinder
            || composition.Holes.Count < 1
            || composition.Holes.Any(hole => hole.Surface.Kind != AnalyticSurfaceKind.Cylinder))
        {
            return KernelResult<BrepBody>.Failure([
                new BooleanDiagnostic(
                    BooleanDiagnosticCode.NotFullySpanning,
                    BrepBooleanCylinderRecognition.CreateBooleanMessage(
                        BooleanOperation.Subtract.ToString(),
                        null,
                        "cylinder-root safe rebuild in F3 requires a through-hole cylinder chain."),
                    "BrepBoolean.AnalyticHole.CylinderRootUnsupportedComposition").ToKernelDiagnostic(),
            ]);
        }

        var rootCenter = new Point3D(
            (rootCylinder.MinCenter.X + rootCylinder.MaxCenter.X) * 0.5d,
            (rootCylinder.MinCenter.Y + rootCylinder.MaxCenter.Y) * 0.5d,
            (rootCylinder.MinCenter.Z + rootCylinder.MaxCenter.Z) * 0.5d);

        var outer = BrepPrimitives.CreateCylinder(rootCylinder.Radius, rootCylinder.Height);
        if (!outer.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(outer.Diagnostics);
        }

        var innerShells = new List<BrepBody>(composition.Holes.Count);
        foreach (var hole in composition.Holes)
        {
            var localCenter = new Point3D(hole.CenterX - rootCenter.X, hole.CenterY - rootCenter.Y, rootCylinder.Height * 0.5d);
            var holeShell = BrepPrimitives.CreateCylinder(hole.BottomRadius, rootCylinder.Height);
            if (!holeShell.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(holeShell.Diagnostics);
            }

            var transformedHoleShell = TranslateBody(holeShell.Value, new Vector3D(localCenter.X, localCenter.Y, localCenter.Z));
            innerShells.Add(transformedHoleShell);
        }

        var merged = MergeOuterAndInnerShells(outer.Value, innerShells);
        var bodyWithComposition = TranslateMergedBody(merged, rootCenter, composition);
        var validation = BrepBindingValidator.Validate(bodyWithComposition, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(bodyWithComposition, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
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

    private static KernelResult<BrepBody> CreateComposedContainedCylinderCavityBody(
        AxisAlignedBoxExtents outerBox,
        in RecognizedCylinder cylinder,
        SafeBooleanComposition composition)
    {
        var outer = BrepBooleanBoxRecognition.CreateBoxFromExtents(outerBox);
        if (!outer.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(outer.Diagnostics);
        }

        var height = System.Math.Abs(cylinder.MaxAxisParameter - cylinder.MinAxisParameter);
        var innerResult = BrepPrimitives.CreateCylinder(cylinder.Radius, height);
        if (!innerResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(innerResult.Diagnostics);
        }

        var center = new Point3D(
            (cylinder.MinCenter.X + cylinder.MaxCenter.X) * 0.5d,
            (cylinder.MinCenter.Y + cylinder.MaxCenter.Y) * 0.5d,
            (cylinder.MinCenter.Z + cylinder.MaxCenter.Z) * 0.5d);
        var inner = TranslateBody(innerResult.Value, new Vector3D(center.X, center.Y, center.Z));
        var body = MergeOuterAndInnerShell(outer.Value, inner, composition);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static BrepBody MergeOuterAndInnerShell(BrepBody outer, BrepBody inner, SafeBooleanComposition? composition = null)
        => MergeOuterAndInnerShells(outer, [inner], composition);

    private static BrepBody MergeOuterAndInnerShells(BrepBody outer, IReadOnlyList<BrepBody> innerShells, SafeBooleanComposition? composition = null)
    {
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

        var remappedInnerShellIds = new List<ShellId>(innerShells.Count);
        for (var innerIndex = 0; innerIndex < innerShells.Count; innerIndex++)
        {
            var inner = innerShells[innerIndex];
            var idOffset = 1000 * (innerIndex + 1);
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

            remappedInnerShellIds.Add(innerShellMap[AssertSingleShellId(inner.Topology)]);
        }

        var outerShellId = AssertSingleShellId(outer.Topology);
        topology.AddBody(new Body(new BodyId(1), [outerShellId, .. remappedInnerShellIds]));

        var geometry = new BrepGeometryStore();
        foreach (var curve in outer.Geometry.Curves)
        {
            geometry.AddCurve(curve.Key, curve.Value);
        }

        foreach (var surface in outer.Geometry.Surfaces)
        {
            geometry.AddSurface(surface.Key, surface.Value);
        }

        for (var innerIndex = 0; innerIndex < innerShells.Count; innerIndex++)
        {
            var inner = innerShells[innerIndex];
            var idOffset = 1000 * (innerIndex + 1);
            foreach (var curve in inner.Geometry.Curves)
            {
                geometry.AddCurve(new CurveGeometryId(curve.Key.Value + idOffset), curve.Value);
            }

            foreach (var surface in inner.Geometry.Surfaces)
            {
                geometry.AddSurface(new SurfaceGeometryId(surface.Key.Value + idOffset), surface.Value);
            }
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

        for (var innerIndex = 0; innerIndex < innerShells.Count; innerIndex++)
        {
            var inner = innerShells[innerIndex];
            var idOffset = 1000 * (innerIndex + 1);
            var innerEdgeMap = inner.Topology.Edges.ToDictionary(e => e.Id, e => new EdgeId(e.Id.Value + idOffset));
            var innerFaceMap = inner.Topology.Faces.ToDictionary(f => f.Id, f => new FaceId(f.Id.Value + idOffset));

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
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in outer.Topology.Vertices.OrderBy(v => v.Id.Value))
        {
            if (outer.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = point;
            }
        }

        for (var innerIndex = 0; innerIndex < innerShells.Count; innerIndex++)
        {
            var inner = innerShells[innerIndex];
            var idOffset = 1000 * (innerIndex + 1);
            foreach (var vertex in inner.Topology.Vertices.OrderBy(v => v.Id.Value))
            {
                if (inner.TryGetVertexPoint(vertex.Id, out var point))
                {
                    vertexPoints[new VertexId(vertex.Id.Value + idOffset)] = point;
                }
            }
        }

        return new BrepBody(
            topology,
            geometry,
            bindings,
            vertexPoints,
            safeBooleanComposition: composition,
            shellRepresentation: new BrepBodyShellRepresentation(outerShellId, remappedInnerShellIds));
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
                    blindBottomCenter,
                    Direction3D.Create(topology.Hole.Axis.ToVector() * -1d),
                    topology.Hole.ReferenceAxis)));
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

    private static KernelResult<BrepBody> CreateSteppedCoaxialCylinderBody(
        SafeBooleanComposition composition,
        in CoaxialCylinderSubtractStackProfile steppedProfile)
    {
        var box = composition.OuterBox;
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

        var entryVertex = builder.AddVertex();
        var shoulderOuterVertex = builder.AddVertex();
        var shoulderInnerVertex = builder.AddVertex();
        var deepEndVertex = builder.AddVertex();
        var outerSeamStartVertex = builder.AddVertex();
        var outerSeamEndVertex = builder.AddVertex();
        var innerSeamStartVertex = builder.AddVertex();
        var innerSeamEndVertex = builder.AddVertex();

        var entryCircle = builder.AddEdge(entryVertex, entryVertex);
        var shoulderOuterCircle = builder.AddEdge(shoulderOuterVertex, shoulderOuterVertex);
        var shoulderInnerCircle = builder.AddEdge(shoulderInnerVertex, shoulderInnerVertex);
        var deepEndCircle = builder.AddEdge(deepEndVertex, deepEndVertex);
        var outerSeam = builder.AddEdge(outerSeamStartVertex, outerSeamEndVertex);
        var innerSeam = builder.AddEdge(innerSeamStartVertex, innerSeamEndVertex);

        var bottomOuterLoop = AddLoop(builder, [Forward(e1), Forward(e2), Forward(e3), Forward(e4)]);
        var topOuterLoop = AddLoop(builder, [Forward(e5), Forward(e6), Forward(e7), Forward(e8)]);
        var bottomLoops = new List<LoopId>(2) { bottomOuterLoop };
        var topLoops = new List<LoopId>(2) { topOuterLoop };
        if (steppedProfile.EntryFromTop)
        {
            topLoops.Add(AddLoop(builder, [Forward(entryCircle)]));
            if (steppedProfile.DeepHole.SpanKind == SupportedBooleanHoleSpanKind.Through)
            {
                bottomLoops.Add(AddLoop(builder, [Reversed(deepEndCircle)]));
            }
        }
        else
        {
            bottomLoops.Add(AddLoop(builder, [Reversed(entryCircle)]));
            if (steppedProfile.DeepHole.SpanKind == SupportedBooleanHoleSpanKind.Through)
            {
                topLoops.Add(AddLoop(builder, [Forward(deepEndCircle)]));
            }
        }

        var bottomFace = builder.AddFace(bottomLoops);
        var topFace = builder.AddFace(topLoops);
        var xMinFace = builder.AddFace([AddLoop(builder, [Forward(e1), Forward(e10), Reversed(e5), Reversed(e9)])]);
        var xMaxFace = builder.AddFace([AddLoop(builder, [Forward(e2), Forward(e11), Reversed(e6), Reversed(e10)])]);
        var yMaxFace = builder.AddFace([AddLoop(builder, [Forward(e3), Forward(e12), Reversed(e7), Reversed(e11)])]);
        var yMinFace = builder.AddFace([AddLoop(builder, [Forward(e4), Forward(e9), Reversed(e8), Reversed(e12)])]);

        var outerWallFace = builder.AddFace([
            AddLoop(builder, [Forward(outerSeam), Reversed(entryCircle), Reversed(outerSeam), Forward(shoulderOuterCircle)])
        ]);
        var shoulderFace = builder.AddFace([
            AddLoop(builder, [Forward(shoulderOuterCircle)]),
            AddLoop(builder, [Reversed(shoulderInnerCircle)])
        ]);
        var innerWallFace = builder.AddFace([
            AddLoop(builder, [Forward(innerSeam), Reversed(shoulderInnerCircle), Reversed(innerSeam), Forward(deepEndCircle)])
        ]);

        FaceId? deepBottomFace = null;
        if (steppedProfile.DeepHole.IsBlind)
        {
            deepBottomFace = builder.AddFace([AddLoop(builder, [Forward(deepEndCircle)])]);
        }

        var shellFaces = new List<FaceId>
        {
            bottomFace,
            topFace,
            xMinFace,
            xMaxFace,
            yMaxFace,
            yMinFace,
            outerWallFace,
            shoulderFace,
            innerWallFace,
        };
        if (deepBottomFace is FaceId deepBottom)
        {
            shellFaces.Add(deepBottom);
        }

        var shell = builder.AddShell(shellFaces);
        builder.AddBody([shell]);

        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));
        var yAxis = Direction3D.Create(new Vector3D(0d, 1d, 0d));
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

        var entryZ = steppedProfile.EntryFromTop ? box.MaxZ : box.MinZ;
        var shoulderZ = steppedProfile.ShoulderZ;
        var deepEndZ = steppedProfile.DeepHole.SpanKind == SupportedBooleanHoleSpanKind.Through
            ? (steppedProfile.EntryFromTop ? box.MinZ : box.MaxZ)
            : steppedProfile.DeepHole.EndCenter.Z;
        var center = new Point3D(steppedProfile.EntryHole.CenterX, steppedProfile.EntryHole.CenterY, 0d);
        var entryCenter = new Point3D(center.X, center.Y, entryZ);
        var shoulderCenter = new Point3D(center.X, center.Y, shoulderZ);
        var deepEndCenter = new Point3D(center.X, center.Y, deepEndZ);
        var entryRadius = steppedProfile.EntryHole.BottomRadius;
        var deepRadius = steppedProfile.DeepHole.BottomRadius;
        var seamAxis = steppedProfile.EntryFromTop ? Direction3D.Create(new Vector3D(0d, 0d, -1d)) : Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var outerSeamStart = entryCenter + (xAxis.ToVector() * entryRadius);
        var outerSeamEnd = shoulderCenter + (xAxis.ToVector() * entryRadius);
        var innerSeamStart = shoulderCenter + (xAxis.ToVector() * deepRadius);
        var innerSeamEnd = deepEndCenter + (xAxis.ToVector() * deepRadius);

        var geometry = new BrepGeometryStore();
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

        var entryCurveId = new CurveGeometryId(13);
        var shoulderOuterCurveId = new CurveGeometryId(14);
        var shoulderInnerCurveId = new CurveGeometryId(15);
        var deepEndCurveId = new CurveGeometryId(16);
        var outerSeamCurveId = new CurveGeometryId(17);
        var innerSeamCurveId = new CurveGeometryId(18);
        geometry.AddCurve(entryCurveId, CurveGeometry.FromCircle(new Circle3Curve(entryCenter, zAxis, entryRadius, xAxis)));
        geometry.AddCurve(shoulderOuterCurveId, CurveGeometry.FromCircle(new Circle3Curve(shoulderCenter, zAxis, entryRadius, xAxis)));
        geometry.AddCurve(shoulderInnerCurveId, CurveGeometry.FromCircle(new Circle3Curve(shoulderCenter, zAxis, deepRadius, xAxis)));
        geometry.AddCurve(deepEndCurveId, CurveGeometry.FromCircle(new Circle3Curve(deepEndCenter, zAxis, deepRadius, xAxis)));
        geometry.AddCurve(outerSeamCurveId, CurveGeometry.FromLine(new Line3Curve(outerSeamStart, seamAxis)));
        geometry.AddCurve(innerSeamCurveId, CurveGeometry.FromLine(new Line3Curve(innerSeamStart, seamAxis)));

        geometry.AddSurface(new SurfaceGeometryId(1), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MinZ), Direction3D.Create(new Vector3D(0d, 0d, -1d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(2), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, box.MaxZ), zAxis, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(3), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MinY, 0d), Direction3D.Create(new Vector3D(0d, -1d, 0d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(4), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MaxX, 0d, 0d), Direction3D.Create(new Vector3D(1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(5), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, box.MaxY, 0d), Direction3D.Create(new Vector3D(0d, 1d, 0d)), Direction3D.Create(new Vector3D(-1d, 0d, 0d)))));
        geometry.AddSurface(new SurfaceGeometryId(6), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(box.MinX, 0d, 0d), Direction3D.Create(new Vector3D(-1d, 0d, 0d)), yAxis)));
        geometry.AddSurface(new SurfaceGeometryId(7), SurfaceGeometry.FromCylinder(new CylinderSurface(shoulderCenter, zAxis, entryRadius, xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(8), SurfaceGeometry.FromPlane(new PlaneSurface(shoulderCenter, Direction3D.Create(new Vector3D(0d, 0d, steppedProfile.EntryFromTop ? -1d : 1d)), xAxis)));
        geometry.AddSurface(new SurfaceGeometryId(9), SurfaceGeometry.FromCylinder(new CylinderSurface(deepEndCenter, zAxis, deepRadius, xAxis)));
        if (deepBottomFace is not null)
        {
            geometry.AddSurface(new SurfaceGeometryId(10), SurfaceGeometry.FromPlane(new PlaneSurface(deepEndCenter, Direction3D.Create(new Vector3D(0d, 0d, steppedProfile.EntryFromTop ? -1d : 1d)), xAxis)));
        }

        var bindings = new BrepBindingModel();
        for (var i = 0; i < 12; i++)
        {
            bindings.AddEdgeBinding(new EdgeGeometryBinding(new EdgeId(i + 1), new CurveGeometryId(i + 1), new ParameterInterval(0d, 1d)));
        }

        bindings.AddEdgeBinding(new EdgeGeometryBinding(entryCircle, entryCurveId, new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(shoulderOuterCircle, shoulderOuterCurveId, new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(shoulderInnerCircle, shoulderInnerCurveId, new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(deepEndCircle, deepEndCurveId, new ParameterInterval(0d, 2d * double.Pi)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(outerSeam, outerSeamCurveId, new ParameterInterval(0d, (outerSeamEnd - outerSeamStart).Length)));
        bindings.AddEdgeBinding(new EdgeGeometryBinding(innerSeam, innerSeamCurveId, new ParameterInterval(0d, (innerSeamEnd - innerSeamStart).Length)));

        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(1)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(2)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMinFace, new SurfaceGeometryId(3)));
        bindings.AddFaceBinding(new FaceGeometryBinding(xMaxFace, new SurfaceGeometryId(4)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yMaxFace, new SurfaceGeometryId(5)));
        bindings.AddFaceBinding(new FaceGeometryBinding(yMinFace, new SurfaceGeometryId(6)));
        bindings.AddFaceBinding(new FaceGeometryBinding(outerWallFace, new SurfaceGeometryId(7)));
        bindings.AddFaceBinding(new FaceGeometryBinding(shoulderFace, new SurfaceGeometryId(8)));
        bindings.AddFaceBinding(new FaceGeometryBinding(innerWallFace, new SurfaceGeometryId(9)));
        if (deepBottomFace is FaceId deepBottomBinding)
        {
            bindings.AddFaceBinding(new FaceGeometryBinding(deepBottomBinding, new SurfaceGeometryId(10)));
        }

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
            [entryVertex] = outerSeamStart,
            [shoulderOuterVertex] = outerSeamEnd,
            [shoulderInnerVertex] = innerSeamStart,
            [deepEndVertex] = innerSeamEnd,
            [outerSeamStartVertex] = outerSeamStart,
            [outerSeamEndVertex] = outerSeamEnd,
            [innerSeamStartVertex] = innerSeamStart,
            [innerSeamEndVertex] = innerSeamEnd,
        };

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
        var topCenter = hole.StartCenter.Z >= hole.EndCenter.Z ? hole.StartCenter : hole.EndCenter;
        var bottomCenter = hole.StartCenter.Z < hole.EndCenter.Z ? hole.StartCenter : hole.EndCenter;
        var axis = hole.Axis;
        var referenceAxis = hole.ReferenceAxis;
        var topEllipse = hole.SpanKind != SupportedBooleanHoleSpanKind.BlindFromBottom;
        var bottomEllipse = hole.SpanKind != SupportedBooleanHoleSpanKind.BlindFromTop;
        var (topCurve, seamTopPoint) = CreateSectionCurve(topCenter, cylinder.Radius, axis, referenceAxis, topEllipse, zAxis);
        var (bottomCurve, seamBottomPoint) = CreateSectionCurve(bottomCenter, cylinder.Radius, axis, referenceAxis, bottomEllipse, zAxis);

        return new HoleGeometryData(
            topCurve,
            bottomCurve,
            CurveGeometry.FromLine(new Line3Curve(seamTopPoint, Direction3D.Create(seamBottomPoint - seamTopPoint))),
            SurfaceGeometry.FromCylinder(new CylinderSurface(bottomCenter, axis, cylinder.Radius, referenceAxis)),
            topCenter,
            bottomCenter,
            seamTopPoint,
            seamBottomPoint,
            (seamBottomPoint - seamTopPoint).Length);
    }

    private static HoleGeometryData CreateConeHoleGeometry(SupportedBooleanHole hole, Direction3D xAxis, Direction3D zAxis, in RecognizedCone cone)
    {
        var startIsTop = hole.StartCenter.Z >= hole.EndCenter.Z;
        var topCenter = startIsTop ? hole.StartCenter : hole.EndCenter;
        var bottomCenter = startIsTop ? hole.EndCenter : hole.StartCenter;
        var topRadius = startIsTop ? hole.BottomRadius : hole.TopRadius;
        var bottomRadius = startIsTop ? hole.TopRadius : hole.BottomRadius;
        var axis = hole.Axis;
        var referenceAxis = hole.ReferenceAxis;
        var topEllipse = hole.SpanKind != SupportedBooleanHoleSpanKind.BlindFromBottom;
        var bottomEllipse = hole.SpanKind != SupportedBooleanHoleSpanKind.BlindFromTop;
        var (topCurve, seamTopPoint) = CreateSectionCurve(topCenter, topRadius, axis, referenceAxis, topEllipse, zAxis);
        var (bottomCurve, seamBottomPoint) = CreateSectionCurve(bottomCenter, bottomRadius, axis, referenceAxis, bottomEllipse, zAxis);
        var innerMinCenter = cone.PointAtAxisParameter(System.Math.Min(cone.MinAxisParameter, cone.MaxAxisParameter));
        var innerMinRadius = cone.RadiusAtAxisParameter(System.Math.Min(cone.MinAxisParameter, cone.MaxAxisParameter));

        return new HoleGeometryData(
            topCurve,
            bottomCurve,
            CurveGeometry.FromLine(new Line3Curve(seamTopPoint, Direction3D.Create(seamBottomPoint - seamTopPoint))),
            SurfaceGeometry.FromCone(new ConeSurface(innerMinCenter, cone.Axis, innerMinRadius, cone.SemiAngleRadians, referenceAxis)),
            topCenter,
            bottomCenter,
            seamTopPoint,
            seamBottomPoint,
            (seamBottomPoint - seamTopPoint).Length);
    }

    private static (CurveGeometry Curve, Point3D SeamPoint) CreateSectionCurve(
        Point3D center,
        double radius,
        Direction3D axis,
        Direction3D referenceAxis,
        bool forceEllipse,
        Direction3D zAxis)
    {
        var axisDotZ = System.Math.Abs(axis.ToVector().Dot(zAxis.ToVector()));
        if (forceEllipse && axisDotZ < 0.999d)
        {
            var majorRadius = radius / axisDotZ;
            var projected = axis.ToVector() - (zAxis.ToVector() * axis.ToVector().Dot(zAxis.ToVector()));
            var majorAxis = Direction3D.Create(projected);
            var seamPoint = center + (majorAxis.ToVector() * majorRadius);
            return (CurveGeometry.FromEllipse(new Ellipse3Curve(center, zAxis, majorRadius, radius, majorAxis)), seamPoint);
        }

        var seam = center + (referenceAxis.ToVector() * radius);
        return (CurveGeometry.FromCircle(new Circle3Curve(center, axis, radius, referenceAxis)), seam);
    }

    private static double AxisParameterAtZ(in RecognizedCone cone, double z)
    {
        var axisZ = cone.Axis.ToVector().Z;
        return (z - cone.AxisOrigin.Z) / axisZ;
    }

    private static BrepBody TranslateMergedBody(BrepBody body, Point3D center, SafeBooleanComposition composition)
    {
        var translation = new Vector3D(center.X, center.Y, center.Z);
        if (translation == Vector3D.Zero)
        {
            return new BrepBody(body.Topology, body.Geometry, body.Bindings, vertexPoints: null, composition, body.ShellRepresentation);
        }

        var translatedGeometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            translatedGeometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(curveEntry.Value.Line3!.Value.Origin + translation, curveEntry.Value.Line3.Value.Direction)),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(curveEntry.Value.Circle3!.Value.Center + translation, curveEntry.Value.Circle3.Value.Normal, curveEntry.Value.Circle3.Value.Radius, curveEntry.Value.Circle3.Value.XAxis)),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            translatedGeometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(surfaceEntry.Value.Plane!.Value.Origin + translation, surfaceEntry.Value.Plane.Value.Normal, surfaceEntry.Value.Plane.Value.UAxis)),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(surfaceEntry.Value.Cylinder!.Value.Origin + translation, surfaceEntry.Value.Cylinder.Value.Axis, surfaceEntry.Value.Cylinder.Value.Radius, surfaceEntry.Value.Cylinder.Value.XAxis)),
                _ => surfaceEntry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = point + translation;
            }
        }

        return new BrepBody(body.Topology, translatedGeometry, body.Bindings, vertexPoints, composition, body.ShellRepresentation);
    }

    private static BrepBody TranslateBody(BrepBody body, Vector3D translation)
    {
        if (translation == Vector3D.Zero)
        {
            return body;
        }

        var translatedGeometry = new BrepGeometryStore();
        foreach (var curveEntry in body.Geometry.Curves)
        {
            translatedGeometry.AddCurve(curveEntry.Key, curveEntry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(curveEntry.Value.Line3!.Value.Origin + translation, curveEntry.Value.Line3.Value.Direction)),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(curveEntry.Value.Circle3!.Value.Center + translation, curveEntry.Value.Circle3.Value.Normal, curveEntry.Value.Circle3.Value.Radius, curveEntry.Value.Circle3.Value.XAxis)),
                _ => curveEntry.Value
            });
        }

        foreach (var surfaceEntry in body.Geometry.Surfaces)
        {
            translatedGeometry.AddSurface(surfaceEntry.Key, surfaceEntry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(surfaceEntry.Value.Plane!.Value.Origin + translation, surfaceEntry.Value.Plane.Value.Normal, surfaceEntry.Value.Plane.Value.UAxis)),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(surfaceEntry.Value.Cylinder!.Value.Origin + translation, surfaceEntry.Value.Cylinder.Value.Axis, surfaceEntry.Value.Cylinder.Value.Radius, surfaceEntry.Value.Cylinder.Value.XAxis)),
                _ => surfaceEntry.Value
            });
        }

        Dictionary<VertexId, Point3D>? vertexPoints = null;
        if (body.Topology.Vertices.Any())
        {
            vertexPoints = [];
            foreach (var vertex in body.Topology.Vertices)
            {
                if (body.TryGetVertexPoint(vertex.Id, out var point))
                {
                    vertexPoints[vertex.Id] = point + translation;
                }
            }
        }

        return new BrepBody(body.Topology, translatedGeometry, body.Bindings, vertexPoints, body.SafeBooleanComposition, body.ShellRepresentation);
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
