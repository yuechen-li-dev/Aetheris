using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>
/// Production consumer of the X7 contact/incidence plan.  All vertices and
/// shared edges are allocated by stable planned identity before their face
/// uses are emitted; faces never rediscover coincident topology.
/// </summary>
public static class ProfileFilletContactShellMaterializer
{
    private const double Tolerance = 1e-8;

    public static ProfileFilletShellPlanResult TryMaterialize(
        ResolvedProfile2D profile,
        ProfileBoundaryChamferTarget target,
        ProfileEdgeFinishMixedShellPlan mixed,
        ProfileFilletContactShellPlan contacts)
    {
        ProfileFilletShellPlanResult Fail(string diagnostic) => new(false, null, null, null, null, [diagnostic]);
        if (target.Side != ProfileBoundaryChamferSide.Top) return Fail("ProfileFilletContactShellBottomNotImplemented");
        if (mixed.FinishKind != ProfileEdgeFinishKind.Fillet) return Fail("ProfileFilletContactShellFilletPlanRequired");
        if (!ReferenceEquals(target, contacts.Target) && target.StableId != contacts.Target.StableId) return Fail("ProfileFilletContactShellTargetMismatch");
        var contactValidation = ProfileFilletContactGraphValidator.Validate(contacts);
        if (!contactValidation.Succeeded) return Fail(contactValidation.Diagnostics[0]);
        var loop = profile.Loops.SingleOrDefault(candidate => candidate.Name == target.LoopId);
        if (loop is null || loop.Segments.Count != mixed.OrderedPatches.Count) return Fail("ProfileFilletContactShellProfileMismatch");
        var area = SignedArea(loop);
        if (Math.Abs(area) <= Tolerance) return Fail("ProfileFilletContactShellProfileDegenerate");

        var frame = profile.EffectiveConstructionPlane;
        var lowerDepth = profile.LocalStartDepth ?? -1d;
        var capDepth = profile.LocalEndDepth ?? 1d;
        var transitionDepth = capDepth - mixed.FinishSize;
        var radius = mixed.FinishSize;
        var capOut = frame.AxisZ;
        var axialInto = -capOut.ToVector();
        var count = loop.Segments.Count;
        var classifications = ProfileJunctionClassifier.Classify(profile, loop)
            .ToDictionary(item => (item.PredecessorSegmentId, item.SuccessorSegmentId));
        var junctions = new Junction[count];

        for (var i = 0; i < count; i++)
        {
            var previousIndex = (i + count - 1) % count;
            var previous = loop.Segments[previousIndex];
            var current = loop.Segments[i];
            var source = frame.ToWorld(Start(current.Geometry), capDepth);
            var side = frame.ToWorld(Start(current.Geometry), transitionDepth);
            var cap = CapPoint(current.Geometry, atEnd: false, area, radius, capDepth, frame);
            var incomingCenter = RollingCenter(previous.Geometry, atEnd: true, area, radius, transitionDepth, frame);
            var outgoingCenter = RollingCenter(current.Geometry, atEnd: false, area, radius, transitionDepth, frame);
            if (previous.Geometry is not LineArcLineSegment2D previousLine || current.Geometry is not LineArcLineSegment2D currentLine)
            {
                junctions[i] = Junction.Smooth(source, side, cap, incomingCenter, outgoingCenter);
                continue;
            }

            var classification = classifications[(previous.Name, current.Name)];
            var ta = Direction(previousLine, frame);
            var na = Inward(previousLine, area, frame);
            var nb = Inward(currentLine, area, frame);
            var depth = source + axialInto * radius;
            if (classification.Classification == ProfileJunctionKind.ConvexProfileJunction)
            {
                var center = source + na.ToVector() * radius + nb.ToVector() * radius + axialInto * radius;
                var capJunction = center + capOut.ToVector() * radius;
                var majorAxis = Direction3D.Create(depth - center);
                var planeNormal = Direction3D.Create(majorAxis.ToVector().Cross(capOut.ToVector()));
                junctions[i] = new(JunctionKind.ConvexMiter, source, depth, depth, depth, depth, depth,
                    capJunction, capJunction, center, center, center, classification.VertexId, majorAxis, planeNormal);
            }
            else if (classification.Classification == ProfileJunctionKind.ReflexProfileJunction && target.ReflexJunctionStyle == ProfileReflexJunctionStyle.SphereSeamCompatibility)
            {
                var center = source + na.ToVector() * radius + nb.ToVector() * radius + axialInto * radius;
                var capJunction = center + capOut.ToVector() * radius;
                var sideA = center - na.ToVector() * radius;
                var sideB = center - nb.ToVector() * radius;
                junctions[i] = Junction.Spherical(JunctionKind.ReflexCompatibilitySphere, source, depth, depth, depth,
                    sideA, sideB, capJunction, center, center, center, classification.VertexId);
            }
            else if (classification.Classification == ProfileJunctionKind.ReflexProfileJunction)
            {
                var centerA = depth + na.ToVector() * radius;
                var centerB = depth + nb.ToVector() * radius;
                var capA = source + na.ToVector() * radius;
                var capB = source + nb.ToVector() * radius;
                junctions[i] = new(JunctionKind.ReflexHornTorus, source, depth, depth, depth, depth, depth, capA, capB,
                    centerA, centerB, depth, classification.VertexId, na, nb);
            }
            else return Fail($"ProfileFilletContactShellSharpJunctionUnsupported:vertex={classification.VertexId}:kind={classification.Classification}");
        }

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var points = new Dictionary<VertexId, Point3D>();
        var vertices = new Dictionary<string, VertexId>(StringComparer.Ordinal);
        var edges = new Dictionary<string, EdgeId>(StringComparer.Ordinal);
        var edgeUses = new Dictionary<EdgeId, List<bool>>();
        var descendants = new List<SemanticTopologyDescendant>();
        var curveId = 1;
        var surfaceId = 1;

        VertexId Vertex(string key, Point3D point)
        {
            if (vertices.TryGetValue(key, out var existing)) return existing;
            var vertex = builder.AddVertex(); vertices.Add(key, vertex); points.Add(vertex, point); return vertex;
        }
        EdgeId Edge(string key, VertexId start, VertexId end, CurveGeometry curve, ParameterInterval trim, bool sameSense)
        {
            if (edges.TryGetValue(key, out var existing)) return existing;
            if (start == end || (points[end] - points[start]).Length <= Tolerance)
                throw new InvalidOperationException($"ProfileFilletContactShellZeroLengthEdge:edge={key}");
            var edge = builder.AddEdge(start, end); edges.Add(key, edge);
            var curveGeometry = new CurveGeometryId(curveId++); geometry.AddCurve(curveGeometry, curve);
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge, curveGeometry, trim, sameSense));
            edgeUses.Add(edge, []);
            return edge;
        }
        EdgeId LineEdge(string key, VertexId start, VertexId end) => Edge(key, start, end,
            CurveGeometry.FromLine(new Line3Curve(points[start], Direction3D.Create(points[end] - points[start]))),
            new ParameterInterval(0d, (points[end] - points[start]).Length), true);
        EdgeId ArcEdge(string key, VertexId start, VertexId end, Point3D center)
        {
            if (edges.TryGetValue(key, out var existing)) return existing;
            var x = points[start] - center; var y = points[end] - center;
            var normal = x.Cross(y);
            if (normal.Length <= Tolerance) throw new InvalidOperationException($"ProfileFilletContactShellArcDegenerate:edge={key}");
            var angle = Math.Acos(Math.Clamp(x.Dot(y) / (x.Length * y.Length), -1d, 1d));
            return Edge(key, start, end, CurveGeometry.FromCircle(new Circle3Curve(center, Direction3D.Create(normal), x.Length, Direction3D.Create(x))),
                new ParameterInterval(0d, angle), true);
        }
        EdgeId EllipseEdge(string key, VertexId start, VertexId end, Point3D center,
            Direction3D majorAxis, Direction3D planeNormal, double minorRadius) => Edge(key, start, end,
            CurveGeometry.FromEllipse(new Ellipse3Curve(center, planeNormal, minorRadius * Math.Sqrt(2d), minorRadius, majorAxis)),
            new ParameterInterval(0d, Math.PI / 2d), true);
        Use UseFrom(EdgeId edge, VertexId start)
        {
            var reverse = builder.Model.Edges.Single(item => item.Id == edge).StartVertexId != start;
            return new(edge, reverse);
        }
        FaceId Face(string stableId, IReadOnlyList<Use> uses, SurfaceGeometry surface, SemanticTopologyRole role, string source)
        {
            if (uses.Count < 3) throw new InvalidOperationException($"ProfileFilletContactShellFaceBoundaryInvalid:face={stableId}");
            var loopId = builder.AllocateLoopId(); var coedges = uses.Select(_ => builder.AllocateCoedgeId()).ToArray();
            for (var index = 0; index < uses.Count; index++)
            {
                var use = uses[index]; edgeUses[use.Edge].Add(use.Reverse);
                builder.AddCoedge(new Coedge(coedges[index], use.Edge, loopId, coedges[(index + 1) % uses.Count], coedges[(index + uses.Count - 1) % uses.Count], use.Reverse));
            }
            builder.AddLoop(new Loop(loopId, coedges)); var face = builder.AddFace([loopId]);
            var surfaceGeometry = new SurfaceGeometryId(surfaceId++); geometry.AddSurface(surfaceGeometry, surface);
            bindings.AddFaceBinding(new FaceGeometryBinding(face, surfaceGeometry, true));
            descendants.Add(new(stableId, "Face", role, source, Face: face, ParentStableId: target.StableId));
            return face;
        }

        try
        {
            var lowerVertices = new VertexId[count];
            var sideStarts = new VertexId[count]; var sideEnds = new VertexId[count];
            var capStarts = new VertexId[count]; var capEnds = new VertexId[count];
            var bottomEdges = new EdgeId[count]; var sideContacts = new EdgeId[count]; var capContacts = new EdgeId?[count];
            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                lowerVertices[i] = Vertex($"source-vertex:{i}:lower", frame.ToWorld(Start(loop.Segments[i].Geometry), lowerDepth));
                sideStarts[i] = Vertex($"component:{i}:side:start", junctions[i].OutgoingSide);
                sideEnds[i] = Vertex($"component:{i}:side:end", junctions[next].IncomingSide);
                capStarts[i] = Vertex($"component:{i}:cap:start", junctions[i].OutgoingCap);
                capEnds[i] = Vertex($"component:{i}:cap:end", junctions[next].IncomingCap);
                bottomEdges[i] = ProfileEdge($"source:{i}:lower", loop.Segments[i].Geometry, lowerVertices[i],
                    Vertex($"source-vertex:{next}:lower", frame.ToWorld(End(loop.Segments[i].Geometry), lowerDepth)), lowerDepth, frame);
                var plannedSide = contacts.SideContactChains[i].OrderedContacts.OfType<ProfileFilletSideContactEdge>().Single();
                sideContacts[i] = loop.Segments[i].Geometry is LineArcCircularArc2D
                    ? Edge(plannedSide.StableId, sideStarts[i], sideEnds[i], plannedSide.Curve, plannedSide.Trim, plannedSide.TraversesWithCurveParameter)
                    : ProfileEdge(plannedSide.StableId, loop.Segments[i].Geometry, sideStarts[i], sideEnds[i], transitionDepth, frame);
                var inset = Offset(loop.Segments[i].Geometry, area, radius);
                if (inset is not null)
                {
                    var plannedCap = contacts.OrderedCapContacts[i];
                    capContacts[i] = loop.Segments[i].Geometry is LineArcCircularArc2D
                        ? Edge(plannedCap.StableId, capStarts[i], capEnds[i], plannedCap.Curve, plannedCap.Trim, plannedCap.TraversesWithCurveParameter)
                        : ProfileEdge(plannedCap.StableId, inset, capStarts[i], capEnds[i], capDepth, frame);
                }
            }

            EdgeId ProfileEdge(string key, LineArcProfileCurve2D curve, VertexId a, VertexId b, double depth, ConstructionPlane constructionPlane)
            {
                if (curve is LineArcLineSegment2D) return LineEdge(key, a, b);
                var arc = (LineArcCircularArc2D)curve;
                return Edge(key, a, b, CurveGeometry.FromCircle(new Circle3Curve(constructionPlane.ToWorld(arc.Center, depth), constructionPlane.AxisZ, arc.Radius, constructionPlane.AxisX)),
                    new ParameterInterval(Math.Min(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians), Math.Max(arc.StartAngleRadians, arc.StartAngleRadians + arc.SweepAngleRadians)), arc.SweepAngleRadians >= 0d);
            }

            var incomingInterfaces = new EdgeId[count]; var outgoingInterfaces = new EdgeId[count];
            var supportIncoming = new EdgeId?[count]; var supportOutgoing = new EdgeId?[count];
            var junctionCap = new EdgeId?[count]; var junctionSide = new EdgeId?[count];
            for (var i = 0; i < count; i++)
            {
                var previous = (i + count - 1) % count;
                var junction = junctions[i];
                var incomingPatchSide = junction.Kind == JunctionKind.ReflexCompatibilitySphere
                    ? Vertex($"junction:{i}:incoming-patch-side", junction.IncomingPatchSide)
                    : sideEnds[previous];
                var outgoingPatchSide = junction.Kind == JunctionKind.ReflexCompatibilitySphere
                    ? Vertex($"junction:{i}:outgoing-patch-side", junction.OutgoingPatchSide)
                    : sideStarts[i];
                incomingInterfaces[i] = junction.Kind == JunctionKind.ConvexMiter
                    ? EllipseEdge($"junction:{i}:convex-miter", incomingPatchSide, capEnds[previous], junction.SurfaceCenter, junction.MajorStart, junction.MajorEnd, radius)
                    : ArcEdge($"junction:{i}:incoming-interface", incomingPatchSide, capEnds[previous], junction.IncomingCenter);
                outgoingInterfaces[i] = junction.Kind is JunctionKind.Smooth or JunctionKind.ConvexMiter
                    ? incomingInterfaces[i]
                    : ArcEdge($"junction:{i}:outgoing-interface", capStarts[i], outgoingPatchSide, junction.OutgoingCenter);
                if (junction.HasSupportPatch)
                {
                    var depth = Vertex($"junction:{i}:depth", junction.Depth);
                    supportIncoming[i] = LineEdge($"junction:{i}:incoming-support", incomingPatchSide, depth);
                    supportOutgoing[i] = LineEdge($"junction:{i}:outgoing-support", depth, outgoingPatchSide);
                    junctionSide[i] = ArcEdge($"junction:{i}:sphere-side", outgoingPatchSide, incomingPatchSide, junction.SurfaceCenter);
                }
                if (junction.Kind == JunctionKind.ReflexHornTorus)
                    junctionCap[i] = ArcEdge($"junction:{i}:cap-contact", capEnds[previous], capStarts[i], junction.Source);
            }

            Face($"{target.StableId}:bottom-cap", Enumerable.Range(0, count).Reverse().Select(i => UseFrom(bottomEdges[i], lowerVertices[(i + 1) % count])).ToArray(),
                SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), lowerDepth), Direction3D.Create(-frame.AxisZ.ToVector()), frame.AxisX)),
                SemanticTopologyRole.BottomFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}");

            var topUses = new List<Use>();
            for (var i = 0; i < count; i++)
            {
                if (capContacts[i] is { } capEdge) topUses.Add(UseFrom(capEdge, capStarts[i]));
                var next = (i + 1) % count;
                if (junctionCap[next] is { } capJunction) topUses.Add(UseFrom(capJunction, capEnds[i]));
            }
            Face($"{target.StableId}:top-cap", topUses,
                SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld((0d, 0d), capDepth), frame.AxisZ, frame.AxisX)),
                SemanticTopologyRole.TopFaceBoundaryLoop, $"profile:{profile.Name}.{loop.Name}");

            for (var i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var startDepth = junctions[i].ParentHasSupport ? Vertex($"junction:{i}:depth", junctions[i].Depth) : sideStarts[i];
                var endDepth = junctions[next].ParentHasSupport ? Vertex($"junction:{next}:depth", junctions[next].Depth) : sideEnds[i];
                var verticalEnd = LineEdge($"junction:{next}:vertical-side", lowerVertices[next], endDepth);
                var verticalStart = LineEdge($"junction:{i}:vertical-side", lowerVertices[i], startDepth);
                var sideUses = new List<Use> { UseFrom(bottomEdges[i], lowerVertices[i]), UseFrom(verticalEnd, lowerVertices[next]) };
                if (junctions[next].ParentHasSupport && supportIncoming[next] is { } incomingSupport) sideUses.Add(UseFrom(incomingSupport, endDepth));
                sideUses.Add(UseFrom(sideContacts[i], sideEnds[i]));
                if (junctions[i].ParentHasSupport && supportOutgoing[i] is { } outgoingSupport) sideUses.Add(UseFrom(outgoingSupport, sideStarts[i]));
                sideUses.Add(UseFrom(verticalStart, startDepth));
                Face($"{target.StableId}:side:{loop.Segments[i].Name}:fragment:0", sideUses,
                    SideSurface(loop.Segments[i].Geometry, lowerDepth, frame), SemanticTopologyRole.ExtrusionSideFace, loop.Segments[i].Provenance.StableId);

                var patchUses = new List<Use> { UseFrom(sideContacts[i], sideStarts[i]), UseFrom(incomingInterfaces[next], sideEnds[i]) };
                if (junctions[next].Kind == JunctionKind.ReflexCompatibilitySphere)
                {
                    patchUses.RemoveAt(patchUses.Count - 1);
                    patchUses.Add(UseFrom(supportIncoming[next]!.Value, sideEnds[i]));
                    patchUses.Add(UseFrom(incomingInterfaces[next], Vertex($"junction:{next}:incoming-patch-side", junctions[next].IncomingPatchSide)));
                }
                if (capContacts[i] is { } capEdge) patchUses.Add(UseFrom(capEdge, capEnds[i]));
                patchUses.Add(UseFrom(outgoingInterfaces[i], capStarts[i]));
                if (junctions[i].Kind == JunctionKind.ReflexCompatibilitySphere)
                    patchUses.Add(UseFrom(supportOutgoing[i]!.Value, Vertex($"junction:{i}:outgoing-patch-side", junctions[i].OutgoingPatchSide)));
                Face($"{target.StableId}:fillet:{loop.Segments[i].Name}", patchUses, PatchSurface(mixed.OrderedPatches[i]),
                    SemanticTopologyRole.FilletSurface, loop.Segments[i].Provenance.StableId);
            }

            for (var i = 0; i < count; i++)
            {
                var junction = junctions[i];
                if (junction.Kind is JunctionKind.Smooth or JunctionKind.ConvexMiter) continue;
                var previous = (i + count - 1) % count;
                if (junction.Kind == JunctionKind.ReflexHornTorus)
                {
                    Face($"{target.StableId}:junction:{junction.SourceId}",
                        [UseFrom(incomingInterfaces[i], capEnds[previous]), UseFrom(outgoingInterfaces[i], sideStarts[i]), UseFrom(junctionCap[i]!.Value, capStarts[i])],
                        SurfaceGeometry.FromTorus(new TorusSurface(junction.SurfaceCenter, capOut, radius, radius, junction.MajorStart)),
                        SemanticTopologyRole.ReflexJunctionPatch, junction.SourceId);
                }
                else
                {
                    var incomingPatchSide = junction.Kind == JunctionKind.ReflexCompatibilitySphere
                        ? Vertex($"junction:{i}:incoming-patch-side", junction.IncomingPatchSide)
                        : sideEnds[previous];
                    var outgoingPatchSide = junction.Kind == JunctionKind.ReflexCompatibilitySphere
                        ? Vertex($"junction:{i}:outgoing-patch-side", junction.OutgoingPatchSide)
                        : sideStarts[i];
                    Face($"{target.StableId}:junction:{junction.SourceId}",
                        [UseFrom(incomingInterfaces[i], capEnds[previous]), UseFrom(junctionSide[i]!.Value, incomingPatchSide), UseFrom(outgoingInterfaces[i], outgoingPatchSide)],
                        SurfaceGeometry.FromSphere(new SphereSurface(junction.SurfaceCenter, capOut, radius, Direction3D.Create(points[outgoingPatchSide] - junction.SurfaceCenter))),
                        SemanticTopologyRole.ReflexJunctionPatch, junction.SourceId);
                    var depth = Vertex($"junction:{i}:depth", junction.Depth);
                    Face($"{target.StableId}:junction-support:{junction.SourceId}",
                        [UseFrom(supportOutgoing[i]!.Value, depth), UseFrom(junctionSide[i]!.Value, outgoingPatchSide), UseFrom(supportIncoming[i]!.Value, incomingPatchSide)],
                        SurfaceGeometry.FromPlane(new PlaneSurface(junction.Depth, capOut, Direction(loop.Segments[previous].Geometry as LineArcLineSegment2D ?? throw new InvalidOperationException(), frame))),
                        SemanticTopologyRole.EdgeFinishReplacementFace, junction.SourceId);
                }
            }

            var incidenceFailure = edgeUses.Where(pair => pair.Value.Count != 2 || pair.Value[0] == pair.Value[1]).OrderBy(pair => pair.Key.Value).FirstOrDefault();
            if (!incidenceFailure.Equals(default(KeyValuePair<EdgeId, List<bool>>)))
                return Fail($"ProfileFilletContactShellIncidenceInvalid:edge={edges.Single(pair => pair.Value == incidenceFailure.Key).Key}:uses={incidenceFailure.Value.Count}:orientations={string.Join(',', incidenceFailure.Value)}");

            var shell = builder.AddShell(builder.Model.Faces.Select(face => face.Id).ToArray()); builder.AddBody([shell]);
            var body = new BrepBody(builder.Model, geometry, bindings, points);
            var bindingValidation = BrepBindingValidator.Validate(body, true);
            if (!bindingValidation.IsSuccess) return Fail("ProfileFilletContactShellBrepBindingInvalid");
            var correspondence = new SemanticTopologyCorrespondence(target.HostBodyId, descendants,
                ["ProfileFilletContactShellPlan", "PreallocatedSharedEdges", "SourceSideFragments", "ContactDrivenCap", "AnalyticFilletPatches", "IncidencePrecheck"]);
            return new(true, body, correspondence, null, null,
                ["ProfileFilletContactShellEmitterX8", "ClosedLoop", "EndpointTerminationCount=0", "NurbsCount=0"]);
        }
        catch (InvalidOperationException exception)
        {
            return Fail(exception.Message);
        }
    }

    private static SurfaceGeometry PatchSurface(AnalyticEdgeFinishPatch patch) => patch switch
    {
        CylindricalFilletPatch cylinder => SurfaceGeometry.FromCylinder(cylinder.Surface),
        SphericalFilletPatch sphere => SurfaceGeometry.FromSphere(sphere.Surface),
        ToroidalFilletPatch torus => SurfaceGeometry.FromTorus(torus.Surface),
        _ => throw new InvalidOperationException($"ProfileFilletContactShellPatchUnsupported:{patch.GetType().Name}")
    };

    private static SurfaceGeometry SideSurface(LineArcProfileCurve2D curve, double depth, ConstructionPlane frame) => curve switch
    {
        LineArcLineSegment2D line => SurfaceGeometry.FromPlane(new PlaneSurface(frame.ToWorld(line.Start, depth), Direction3D.Create(Direction(line, frame).ToVector().Cross(frame.AxisZ.ToVector())), Direction(line, frame))),
        LineArcCircularArc2D arc => SurfaceGeometry.FromCylinder(new CylinderSurface(frame.ToWorld(arc.Center, depth), frame.AxisZ, arc.Radius, frame.AxisX)),
        _ => throw new NotSupportedException()
    };

    private static LineArcProfileCurve2D? Offset(LineArcProfileCurve2D curve, double area, double radius) => curve switch
    {
        LineArcLineSegment2D line => OffsetLine(line, area, radius),
        LineArcCircularArc2D arc => OffsetArc(arc, area, radius),
        _ => null
    };

    private static LineArcLineSegment2D OffsetLine(LineArcLineSegment2D line, double area, double radius)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        var nx = area > 0d ? -dy / length : dy / length; var ny = area > 0d ? dx / length : -dx / length;
        return new((line.Start.X + nx * radius, line.Start.Y + ny * radius), (line.End.X + nx * radius, line.End.Y + ny * radius));
    }

    private static LineArcCircularArc2D? OffsetArc(LineArcCircularArc2D arc, double area, double radius)
    {
        var convex = Math.Sign(arc.SweepAngleRadians) * Math.Sign(area) >= 0d;
        var result = convex ? arc.Radius - radius : arc.Radius + radius;
        return result <= Tolerance ? null : new(arc.Center, result, arc.StartAngleRadians, arc.SweepAngleRadians);
    }

    private static Point3D CapPoint(LineArcProfileCurve2D curve, bool atEnd, double area, double radius, double depth, ConstructionPlane frame)
    {
        var offset = Offset(curve, area, radius);
        if (offset is null && curve is LineArcCircularArc2D arc) return frame.ToWorld(arc.Center, depth);
        return frame.ToWorld(atEnd ? End(offset!) : Start(offset!), depth);
    }

    private static Point3D RollingCenter(LineArcProfileCurve2D curve, bool atEnd, double area, double radius, double transitionDepth, ConstructionPlane frame) =>
        CapPoint(curve, atEnd, area, radius, transitionDepth, frame);

    private static (double X, double Y) Start(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.Start,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)),
        _ => throw new NotSupportedException()
    };

    private static (double X, double Y) End(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => line.End,
        LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians)),
        _ => throw new NotSupportedException()
    };

    private static Direction3D Direction(LineArcLineSegment2D line, ConstructionPlane frame) =>
        Direction3D.Create(frame.ToWorldDirection(new Vector3D(line.End.X - line.Start.X, line.End.Y - line.Start.Y, 0d)));

    private static Direction3D Inward(LineArcLineSegment2D line, double area, ConstructionPlane frame)
    {
        var dx = line.End.X - line.Start.X; var dy = line.End.Y - line.Start.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        var normal = area > 0d ? new Vector3D(-dy / length, dx / length, 0d) : new Vector3D(dy / length, -dx / length, 0d);
        return Direction3D.Create(frame.ToWorldDirection(normal));
    }

    private static double SignedArea(ResolvedProfileLoop2D loop) => loop.Segments.Sum(segment => segment.Geometry switch
    {
        LineArcLineSegment2D line => line.Start.X * line.End.Y - line.End.X * line.Start.Y,
        LineArcCircularArc2D arc => 2d * (arc.Center.X * arc.Radius * (Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Sin(arc.StartAngleRadians)) - arc.Center.Y * arc.Radius * (Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians) - Math.Cos(arc.StartAngleRadians)) + arc.Radius * arc.Radius * arc.SweepAngleRadians),
        _ => 0d
    }) * .5d;

    private enum JunctionKind { Smooth, ConvexMiter, ReflexHornTorus, ReflexCompatibilitySphere }

    private sealed record Junction(
        JunctionKind Kind,
        Point3D Source,
        Point3D Depth,
        Point3D IncomingSide,
        Point3D OutgoingSide,
        Point3D IncomingPatchSide,
        Point3D OutgoingPatchSide,
        Point3D IncomingCap,
        Point3D OutgoingCap,
        Point3D IncomingCenter,
        Point3D OutgoingCenter,
        Point3D SurfaceCenter,
        string SourceId,
        Direction3D MajorStart,
        Direction3D MajorEnd)
    {
        public bool HasSupportPatch => Kind == JunctionKind.ReflexCompatibilitySphere;
        public bool ParentHasSupport => false;

        public static Junction Smooth(Point3D source, Point3D side, Point3D cap, Point3D incomingCenter, Point3D outgoingCenter) =>
            new(JunctionKind.Smooth, source, side, side, side, side, side, cap, cap, incomingCenter, outgoingCenter, incomingCenter,
                "smooth", Direction3D.Create(cap - incomingCenter), Direction3D.Create(cap - outgoingCenter));

        public static Junction Spherical(JunctionKind kind, Point3D source, Point3D depth, Point3D incomingSide, Point3D outgoingSide,
            Point3D incomingPatchSide, Point3D outgoingPatchSide,
            Point3D cap, Point3D incomingCenter, Point3D outgoingCenter, Point3D sphereCenter, string sourceId) =>
            new(kind, source, depth, incomingSide, outgoingSide, incomingPatchSide, outgoingPatchSide, cap, cap, incomingCenter, outgoingCenter, sphereCenter, sourceId,
                Direction3D.Create(cap - sphereCenter), Direction3D.Create(outgoingPatchSide - sphereCenter));
    }

    private readonly record struct Use(EdgeId Edge, bool Reverse);
}
