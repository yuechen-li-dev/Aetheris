using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.EdgeFinishing;

/// <summary>
/// F0/F1 bounded constant-radius cylindrical fillet builder for explicit internal concave planar-planar vertical edges.
/// </summary>
public static class BrepBoundedFillet
{
    private const string SingleEdgeCylindricalFilletCandidate = "single_edge_cylindrical_fillet";
    private const string ChainedSameRadiusCylindricalFilletCandidate = "chained_same_radius_cylindrical_fillets";
    private const string ChainedSameRadiusCylindricalTerminationCandidate = "chained_same_radius_fillet_with_cylindrical_termination";
    private const string RejectCandidate = "reject";

    public static KernelResult<BrepBody> FilletTrustedPolyhedralSingleInternalConcaveEdge(
        BrepBody sourceBody,
        BoundedManufacturingFilletSelection selection,
        double radius)
    {
        var contextResult = BrepBoundedFilletContext.TryCreate(sourceBody, selection, radius);
        if (!contextResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(contextResult.Diagnostics);
        }

        var context = contextResult.Value;
        var engine = new JudgmentEngine<BrepBoundedFilletContext>();
        var judgment = engine.Evaluate(context, BuildCandidates());
        if (!judgment.IsSuccess || !judgment.Selection.HasValue || judgment.Selection.Value.Candidate.Name == RejectCandidate)
        {
            var reason = judgment.Rejections.Count == 0
                ? BuildRejectReason(context)
                : string.Join(" ", judgment.Rejections.Select(rejection => $"{rejection.CandidateName}: {rejection.Reason}."));
            return KernelResult<BrepBody>.Failure([Failure($"Bounded fillet edge resolution rejected: {reason}", "firmament.fillet-bounded")]);
        }

        return judgment.Selection.Value.Candidate.Name switch
        {
            SingleEdgeCylindricalFilletCandidate => BuildConcaveFilletBody(sourceBody, context),
            ChainedSameRadiusCylindricalFilletCandidate => BuildConcaveFilletBody(sourceBody, context),
            ChainedSameRadiusCylindricalTerminationCandidate => KernelResult<BrepBody>.Failure([Failure(
                "Bounded chained same-radius fillet with cylindrical-context termination is recognized but not yet supported; the current local extrusion rewrite cannot preserve neighboring cylindrical context while terminating the chain.",
                "firmament.fillet-bounded")]),
            _ => KernelResult<BrepBody>.Failure([Failure($"Bounded fillet selected unsupported candidate '{judgment.Selection.Value.Candidate.Name}'.", "firmament.fillet-bounded")])
        };
    }

    private static IReadOnlyList<JudgmentCandidate<BrepBoundedFilletContext>> BuildCandidates()
        =>
        [
            new JudgmentCandidate<BrepBoundedFilletContext>(
                Name: SingleEdgeCylindricalFilletCandidate,
                IsAdmissible: When.All<BrepBoundedFilletContext>(
                    context => context.IsTrustedSource,
                    context => context.SelectionCount == 1,
                    context => context.IsPlanarPlanar,
                    context => context.IsInternalConcave,
                    context => context.IsBoundedRadius),
                Score: context => context.IsInternalConcave ? 100d : 0d,
                RejectionReason: context => $"requires trusted single-edge internal concave planar-planar bounded-radius context (trusted={context.IsTrustedSource}, count={context.SelectionCount}, planar={context.IsPlanarPlanar}, concave={context.IsInternalConcave}, bounded={context.IsBoundedRadius})",
                TieBreakerPriority: 0),
            new JudgmentCandidate<BrepBoundedFilletContext>(
                Name: ChainedSameRadiusCylindricalFilletCandidate,
                IsAdmissible: When.All<BrepBoundedFilletContext>(
                    context => context.IsTrustedSource,
                    context => context.SelectionCount == 2,
                    context => context.IsPlanarPlanar,
                    context => context.IsInternalConcave,
                    context => context.IsBoundedRadius,
                    context => context.HasLocalChainedInteraction),
                Score: context => context.HasLocalChainedInteraction ? 150d : 0d,
                RejectionReason: context => $"requires trusted two-edge same-radius chained internal concave planar-planar bounded-radius context (trusted={context.IsTrustedSource}, count={context.SelectionCount}, planar={context.IsPlanarPlanar}, concave={context.IsInternalConcave}, bounded={context.IsBoundedRadius}, interacting={context.HasLocalChainedInteraction})",
                TieBreakerPriority: 1),
            new JudgmentCandidate<BrepBoundedFilletContext>(
                Name: ChainedSameRadiusCylindricalTerminationCandidate,
                IsAdmissible: When.All<BrepBoundedFilletContext>(
                    context => context.IsTrustedSource,
                    context => context.SelectionCount == 2,
                    context => context.IsInternalConcave,
                    context => context.IsBoundedRadius,
                    context => context.HasLocalChainedInteraction,
                    context => context.HasCylindricalSourceFaces,
                    context => context.IsCylindricalTerminationSupported),
                Score: context => context.HasCylindricalSourceFaces ? 175d : 0d,
                RejectionReason: context => $"requires supported chained same-radius cylindrical-context termination context (trusted={context.IsTrustedSource}, count={context.SelectionCount}, concave={context.IsInternalConcave}, bounded={context.IsBoundedRadius}, interacting={context.HasLocalChainedInteraction}, hasCylindricalSourceFaces={context.HasCylindricalSourceFaces}, supported={context.IsCylindricalTerminationSupported})",
                TieBreakerPriority: 2),
            new JudgmentCandidate<BrepBoundedFilletContext>(
                Name: RejectCandidate,
                IsAdmissible: _ => true,
                Score: _ => -1d,
                RejectionReason: _ => "bounded single-edge cylindrical fillet candidate is not admissible",
                TieBreakerPriority: 3)
        ];

    private static string BuildRejectReason(BrepBoundedFilletContext context)
    {
        if (context.SelectionCount == 2 && context.HasLocalChainedInteraction && context.HasCylindricalSourceFaces)
        {
            return "chained_same_radius_fillet_with_cylindrical_termination: requires supported chained same-radius cylindrical-context termination context (supported=False).";
        }

        return "No bounded single-edge/chained fillet candidate was admissible.";
    }

    private static KernelResult<BrepBody> BuildConcaveFilletBody(BrepBody sourceBody, BrepBoundedFilletContext context)
    {
        if (!TryBuildOrthogonalFootprintLoop(context.OccupiedCells, out var loop, out var loopFailure))
        {
            return KernelResult<BrepBody>.Failure([Failure(
                $"Bounded fillet local builder requires a single coherent orthogonal footprint loop: {loopFailure}",
                "firmament.fillet-bounded")]);
        }

        if (!TryApplyCornerFillets(loop, context.Selection.Corners, context.Radius, out var filletedLoop, out var filletFailure))
        {
            return KernelResult<BrepBody>.Failure([Failure(
                $"Bounded fillet local builder could not construct the selected internal-edge cylindrical cut chain: {filletFailure}",
                "firmament.fillet-bounded")]);
        }

        return BuildExtrudedBodyWithArcs(
            filletedLoop,
            context.Selection.MinZ,
            context.Selection.MaxZ,
            context.Radius,
            sourceBody.SafeBooleanComposition);
    }

    private static KernelResult<BrepBody> BuildExtrudedBodyWithArcs(
        IReadOnlyList<LoopSegment2D> segments,
        double minZ,
        double maxZ,
        double radius,
        SafeBooleanComposition? safeBooleanComposition)
    {
        var height = maxZ - minZ;
        if (height <= 0d)
        {
            return KernelResult<BrepBody>.Failure([Failure("Bounded fillet local builder requires a positive source height.", "firmament.fillet-bounded")]);
        }

        var builder = new TopologyBuilder();
        var vertexCount = segments.Count;
        var bottomVertices = new VertexId[vertexCount];
        var topVertices = new VertexId[vertexCount];
        var nodePoints = new (double X, double Y)[vertexCount];

        for (var i = 0; i < vertexCount; i++)
        {
            var start = segments[i].Start;
            nodePoints[i] = start;
            bottomVertices[i] = builder.AddVertex();
            topVertices[i] = builder.AddVertex();
        }

        var bottomEdges = new EdgeId[vertexCount];
        var topEdges = new EdgeId[vertexCount];
        var verticalEdges = new EdgeId[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var next = (i + 1) % vertexCount;
            bottomEdges[i] = builder.AddEdge(bottomVertices[i], bottomVertices[next]);
            topEdges[i] = builder.AddEdge(topVertices[i], topVertices[next]);
            verticalEdges[i] = builder.AddEdge(bottomVertices[i], topVertices[i]);
        }

        var sideFaces = new FaceId[vertexCount];
        for (var i = 0; i < vertexCount; i++)
        {
            var next = (i + 1) % vertexCount;
            sideFaces[i] = AddFaceWithLoop(builder,
            [
                EdgeUse.Forward(bottomEdges[i]),
                EdgeUse.Forward(verticalEdges[next]),
                EdgeUse.Reversed(topEdges[i]),
                EdgeUse.Reversed(verticalEdges[i])
            ]);
        }

        var bottomFace = AddFaceWithLoop(builder, Enumerable.Range(0, vertexCount).Select(i => EdgeUse.Forward(bottomEdges[i])).ToArray());
        var topFace = AddFaceWithLoop(builder,
            Enumerable.Range(0, vertexCount)
                .Reverse()
                .Select(i => EdgeUse.Reversed(topEdges[i]))
                .ToArray());

        var shellFaces = sideFaces.Concat([bottomFace, topFace]).ToArray();
        var shell = builder.AddShell(shellFaces);
        builder.AddBody([shell]);

        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var zAxis = Direction3D.Create(new Vector3D(0d, 0d, 1d));
        var xAxis = Direction3D.Create(new Vector3D(1d, 0d, 0d));

        var curveId = 1;
        for (var i = 0; i < vertexCount; i++)
        {
            var segment = segments[i];
            var bottomEdge = bottomEdges[i];
            var topEdge = topEdges[i];

            if (segment.Kind == LoopSegmentKind.Line)
            {
                var lineDirection = Direction3D.Create(new Vector3D(segment.End.X - segment.Start.X, segment.End.Y - segment.Start.Y, 0d));
                var bottomOrigin = new Point3D(segment.Start.X, segment.Start.Y, minZ);
                var topOrigin = new Point3D(segment.Start.X, segment.Start.Y, maxZ);

                geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(bottomOrigin, lineDirection)));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomEdge, new CurveGeometryId(curveId), new ParameterInterval(0d, segment.Length)));
                curveId++;

                geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(topOrigin, lineDirection)));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(topEdge, new CurveGeometryId(curveId), new ParameterInterval(0d, segment.Length)));
                curveId++;
            }
            else
            {
                var centerBottom = new Point3D(segment.Center!.Value.X, segment.Center.Value.Y, minZ);
                var centerTop = new Point3D(segment.Center!.Value.X, segment.Center.Value.Y, maxZ);
                var startVector = new Vector3D(segment.Start.X - segment.Center.Value.X, segment.Start.Y - segment.Center.Value.Y, 0d);
                var referenceAxis = Direction3D.Create(startVector);

                var (startAngle, endAngle) = ComputeArcParameterRange(segment.Center.Value, segment.Start, segment.End, segment.IsClockwise);
                geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromCircle(new Circle3Curve(centerBottom, zAxis, radius, referenceAxis)));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(bottomEdge, new CurveGeometryId(curveId), new ParameterInterval(startAngle, endAngle)));
                curveId++;

                geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromCircle(new Circle3Curve(centerTop, zAxis, radius, referenceAxis)));
                bindings.AddEdgeBinding(new EdgeGeometryBinding(topEdge, new CurveGeometryId(curveId), new ParameterInterval(startAngle, endAngle)));
                curveId++;
            }
        }

        for (var i = 0; i < vertexCount; i++)
        {
            var start = nodePoints[i];
            var verticalOrigin = new Point3D(start.X, start.Y, minZ);
            geometry.AddCurve(new CurveGeometryId(curveId), CurveGeometry.FromLine(new Line3Curve(verticalOrigin, zAxis)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(verticalEdges[i], new CurveGeometryId(curveId), new ParameterInterval(0d, height)));
            curveId++;
        }

        var surfaceId = 1;
        for (var i = 0; i < vertexCount; i++)
        {
            var segment = segments[i];
            if (segment.Kind == LoopSegmentKind.Line)
            {
                var direction = Direction3D.Create(new Vector3D(segment.End.X - segment.Start.X, segment.End.Y - segment.Start.Y, 0d));
                var outwardNormal = Direction3D.Create(new Vector3D(segment.End.Y - segment.Start.Y, -(segment.End.X - segment.Start.X), 0d));
                geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(segment.Start.X, segment.Start.Y, minZ), outwardNormal, direction)));
            }
            else
            {
                var center = segment.Center!.Value;
                var origin = new Point3D(center.X, center.Y, minZ);
                var referenceAxis = Direction3D.Create(new Vector3D(segment.Start.X - center.X, segment.Start.Y - center.Y, 0d));
                geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromCylinder(new CylinderSurface(origin, zAxis, radius, referenceAxis)));
            }

            bindings.AddFaceBinding(new FaceGeometryBinding(sideFaces[i], new SurfaceGeometryId(surfaceId)));
            surfaceId++;
        }

        geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, minZ), Direction3D.Create(new Vector3D(0d, 0d, -1d)), xAxis)));
        bindings.AddFaceBinding(new FaceGeometryBinding(bottomFace, new SurfaceGeometryId(surfaceId)));
        surfaceId++;
        geometry.AddSurface(new SurfaceGeometryId(surfaceId), SurfaceGeometry.FromPlane(new PlaneSurface(new Point3D(0d, 0d, maxZ), zAxis, xAxis)));
        bindings.AddFaceBinding(new FaceGeometryBinding(topFace, new SurfaceGeometryId(surfaceId)));

        var body = new BrepBody(builder.Model, geometry, bindings, vertexPoints: null, safeBooleanComposition: safeBooleanComposition);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        return validation.IsSuccess
            ? KernelResult<BrepBody>.Success(body, validation.Diagnostics)
            : KernelResult<BrepBody>.Failure(validation.Diagnostics);
    }

    private static (double Start, double End) ComputeArcParameterRange((double X, double Y) center, (double X, double Y) start, (double X, double Y) end, bool clockwise)
    {
        var startAngle = System.Math.Atan2(start.Y - center.Y, start.X - center.X);
        var endAngle = System.Math.Atan2(end.Y - center.Y, end.X - center.X);
        if (startAngle < 0d)
        {
            startAngle += 2d * double.Pi;
        }

        if (endAngle < 0d)
        {
            endAngle += 2d * double.Pi;
        }

        if (clockwise)
        {
            if (endAngle > startAngle)
            {
                endAngle -= 2d * double.Pi;
            }
        }
        else if (endAngle < startAngle)
        {
            endAngle += 2d * double.Pi;
        }

        return (startAngle, endAngle);
    }

    private static bool TryApplyCornerFillets(
        IReadOnlyList<(double X, double Y)> loop,
        IReadOnlyList<BoundedManufacturingFilletCornerSelection> corners,
        double radius,
        out List<LoopSegment2D> filletedLoop,
        out string failure)
    {
        filletedLoop = [];
        failure = string.Empty;

        if (corners.Count is < 1 or > 2)
        {
            failure = "selected bounded fillet corner set must contain one or two corners";
            return false;
        }

        var signedArea = ComputeSignedArea(loop);
        var isCounterClockwise = signedArea > 0d;
        var cornerDataByIndex = new Dictionary<int, ((double X, double Y) CutA, (double X, double Y) CutB, (double X, double Y) Center)>();
        foreach (var cornerSelection in corners)
        {
            var cornerIndices = Enumerable.Range(0, loop.Count)
                .Where(index => NearlyEqual(loop[index].X, cornerSelection.EdgeX) && NearlyEqual(loop[index].Y, cornerSelection.EdgeY))
                .ToArray();
            if (cornerIndices.Length != 1)
            {
                failure = "selected internal corner was not uniquely present on the boundary loop";
                return false;
            }

            var indexToFillet = cornerIndices[0];
            if (!IsConcaveCorner(loop, indexToFillet, isCounterClockwise))
            {
                failure = "selected corner is not a local concave corner on this footprint";
                return false;
            }

            var prevIndex = (indexToFillet + loop.Count - 1) % loop.Count;
            var nextIndex = (indexToFillet + 1) % loop.Count;
            var corner = loop[indexToFillet];
            var prev = loop[prevIndex];
            var next = loop[nextIndex];

            var incoming = (X: prev.X - corner.X, Y: prev.Y - corner.Y);
            var outgoing = (X: next.X - corner.X, Y: next.Y - corner.Y);
            if (!IsAxisAligned(incoming) || !IsAxisAligned(outgoing))
            {
                failure = "selected corner neighborhood is not axis-aligned";
                return false;
            }

            var incomingLength = System.Math.Sqrt(incoming.X * incoming.X + incoming.Y * incoming.Y);
            var outgoingLength = System.Math.Sqrt(outgoing.X * outgoing.X + outgoing.Y * outgoing.Y);
            if (radius >= incomingLength || radius >= outgoingLength)
            {
                failure = "radius exceeds one of the local orthogonal edge spans";
                return false;
            }

            var cutA = (corner.X + incoming.X / incomingLength * radius, corner.Y + incoming.Y / incomingLength * radius);
            var cutB = (corner.X + outgoing.X / outgoingLength * radius, corner.Y + outgoing.Y / outgoingLength * radius);
            var center = (corner.X + incoming.X / incomingLength * radius + outgoing.X / outgoingLength * radius,
                corner.Y + incoming.Y / incomingLength * radius + outgoing.Y / outgoingLength * radius);
            cornerDataByIndex[indexToFillet] = (cutA, cutB, center);
        }

        foreach (var index in cornerDataByIndex.Keys)
        {
            var nextIndex = (index + 1) % loop.Count;
            if (!cornerDataByIndex.ContainsKey(nextIndex))
            {
                continue;
            }

            var first = cornerDataByIndex[index].CutB;
            var second = cornerDataByIndex[nextIndex].CutA;
            if (LoopSegment2D.Line(first, second).Length <= 1e-8)
            {
                failure = "chained corner interaction collapsed an inter-fillet span";
                return false;
            }
        }

        var arcCenters = cornerDataByIndex.Values.ToDictionary(
            data => (data.CutA, data.CutB),
            data => data.Center);

        var transformed = new List<(double X, double Y)>();
        for (var i = 0; i < loop.Count; i++)
        {
            if (cornerDataByIndex.TryGetValue(i, out var data))
            {
                transformed.Add(data.CutA);
                transformed.Add(data.CutB);
                continue;
            }

            transformed.Add(loop[i]);
        }

        for (var i = 0; i < transformed.Count; i++)
        {
            var start = transformed[i];
            var end = transformed[(i + 1) % transformed.Count];
            if (arcCenters.TryGetValue((start, end), out var center))
            {
                filletedLoop.Add(LoopSegment2D.Arc(start, end, center, isClockwise: false));
            }
            else
            {
                filletedLoop.Add(LoopSegment2D.Line(start, end));
            }
        }

        return true;
    }

    private static bool TryBuildOrthogonalFootprintLoop(
        IReadOnlyList<AxisAlignedBoxExtents> cells,
        out List<(double X, double Y)> loop,
        out string failure)
    {
        loop = [];
        failure = string.Empty;

        var xCoords = cells.SelectMany(cell => new[] { cell.MinX, cell.MaxX }).Distinct().OrderBy(v => v).ToArray();
        var yCoords = cells.SelectMany(cell => new[] { cell.MinY, cell.MaxY }).Distinct().OrderBy(v => v).ToArray();
        if (xCoords.Length < 2 || yCoords.Length < 2)
        {
            failure = "occupied-cell grid is degenerate";
            return false;
        }

        var occupied = new bool[xCoords.Length - 1, yCoords.Length - 1];
        for (var xi = 0; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 0; yi < yCoords.Length - 1; yi++)
            {
                var cx = 0.5d * (xCoords[xi] + xCoords[xi + 1]);
                var cy = 0.5d * (yCoords[yi] + yCoords[yi + 1]);
                occupied[xi, yi] = cells.Any(cell =>
                    cx > cell.MinX && cx < cell.MaxX &&
                    cy > cell.MinY && cy < cell.MaxY);
            }
        }

        var segments = new List<((double X, double Y) Start, (double X, double Y) End)>();
        for (var xi = 0; xi < xCoords.Length - 1; xi++)
        {
            for (var yi = 0; yi < yCoords.Length - 1; yi++)
            {
                if (!occupied[xi, yi])
                {
                    continue;
                }

                var xMin = xCoords[xi];
                var xMax = xCoords[xi + 1];
                var yMin = yCoords[yi];
                var yMax = yCoords[yi + 1];

                if (yi == 0 || !occupied[xi, yi - 1])
                {
                    segments.Add(((xMin, yMin), (xMax, yMin)));
                }

                if (xi == xCoords.Length - 2 || !occupied[xi + 1, yi])
                {
                    segments.Add(((xMax, yMin), (xMax, yMax)));
                }

                if (yi == yCoords.Length - 2 || !occupied[xi, yi + 1])
                {
                    segments.Add(((xMax, yMax), (xMin, yMax)));
                }

                if (xi == 0 || !occupied[xi - 1, yi])
                {
                    segments.Add(((xMin, yMax), (xMin, yMin)));
                }
            }
        }

        if (segments.Count == 0)
        {
            failure = "occupied-cell boundary extraction produced no perimeter segments";
            return false;
        }

        var outgoing = segments.GroupBy(segment => segment.Start).ToDictionary(group => group.Key, group => group.ToList());
        if (outgoing.Any(entry => entry.Value.Count != 1))
        {
            failure = "occupied-cell perimeter is not a single simple loop";
            return false;
        }

        var start = segments[0].Start;
        var current = start;
        var visited = new HashSet<((double X, double Y) Start, (double X, double Y) End)>();
        while (true)
        {
            loop.Add(current);
            if (!outgoing.TryGetValue(current, out var nextSegments) || nextSegments.Count != 1)
            {
                failure = "occupied-cell perimeter stitching failed";
                return false;
            }

            var nextSegment = nextSegments[0];
            if (!visited.Add(nextSegment))
            {
                failure = "occupied-cell perimeter contains repeated segments";
                return false;
            }

            current = nextSegment.End;
            if (NearlyEqual(current.X, start.X) && NearlyEqual(current.Y, start.Y))
            {
                break;
            }
        }

        if (visited.Count != segments.Count)
        {
            failure = "occupied-cell perimeter contains multiple disjoint loops or holes";
            return false;
        }

        var normalized = SimplifyCollinear(loop);
        var area = ComputeSignedArea(normalized);
        if (area < 0d)
        {
            normalized.Reverse();
        }

        loop = normalized;
        return loop.Count >= 4;
    }

    private static List<(double X, double Y)> SimplifyCollinear(IReadOnlyList<(double X, double Y)> loop)
    {
        var simplified = new List<(double X, double Y)>();
        for (var i = 0; i < loop.Count; i++)
        {
            var prev = loop[(i + loop.Count - 1) % loop.Count];
            var current = loop[i];
            var next = loop[(i + 1) % loop.Count];

            var v1 = (X: current.X - prev.X, Y: current.Y - prev.Y);
            var v2 = (X: next.X - current.X, Y: next.Y - current.Y);
            if (IsAxisAligned(v1) && IsAxisAligned(v2) && (NearlyEqual(v1.X * v2.Y - v1.Y * v2.X, 0d)))
            {
                continue;
            }

            simplified.Add(current);
        }

        return simplified;
    }

    private static bool IsConcaveCorner(IReadOnlyList<(double X, double Y)> loop, int index, bool isCounterClockwise)
    {
        var prev = loop[(index + loop.Count - 1) % loop.Count];
        var current = loop[index];
        var next = loop[(index + 1) % loop.Count];
        var cross = (current.X - prev.X) * (next.Y - current.Y) - (current.Y - prev.Y) * (next.X - current.X);
        return isCounterClockwise ? cross < 0d : cross > 0d;
    }

    private static double ComputeSignedArea(IReadOnlyList<(double X, double Y)> polygon)
    {
        double twiceArea = 0d;
        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            twiceArea += (a.X * b.Y) - (b.X * a.Y);
        }

        return twiceArea * 0.5d;
    }

    private static bool IsAxisAligned((double X, double Y) vector)
        => NearlyEqual(vector.X, 0d) || NearlyEqual(vector.Y, 0d);

    private static bool NearlyEqual(double a, double b)
        => System.Math.Abs(a - b) <= 1e-9;

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

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);

    private readonly record struct EdgeUse(EdgeId EdgeId, bool IsReversed)
    {
        public static EdgeUse Forward(EdgeId edgeId) => new(edgeId, false);
        public static EdgeUse Reversed(EdgeId edgeId) => new(edgeId, true);
    }

    private enum LoopSegmentKind
    {
        Line,
        Arc
    }

    private readonly record struct LoopSegment2D(
        LoopSegmentKind Kind,
        (double X, double Y) Start,
        (double X, double Y) End,
        (double X, double Y)? Center,
        bool IsClockwise,
        double Length)
    {
        public static LoopSegment2D Line((double X, double Y) start, (double X, double Y) end)
            => new(LoopSegmentKind.Line, start, end, null, false, System.Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y)));

        public static LoopSegment2D Arc((double X, double Y) start, (double X, double Y) end, (double X, double Y) center, bool isClockwise)
            => new(LoopSegmentKind.Arc, start, end, center, isClockwise, 0.5d * double.Pi * System.Math.Sqrt((start.X - center.X) * (start.X - center.X) + (start.Y - center.Y) * (start.Y - center.Y)));
    }
}

internal readonly record struct BrepBoundedFilletContext(
    bool IsTrustedSource,
    int SelectionCount,
    bool HasLocalChainedInteraction,
    bool IsPlanarPlanar,
    bool HasCylindricalSourceFaces,
    bool IsCylindricalTerminationSupported,
    bool IsInternalConcave,
    bool IsBoundedRadius,
    BoundedManufacturingFilletSelection Selection,
    double Radius,
    IReadOnlyList<AxisAlignedBoxExtents> OccupiedCells)
{
    public static KernelResult<BrepBoundedFilletContext> TryCreate(BrepBody sourceBody, BoundedManufacturingFilletSelection selection, double radius)
    {
        if (sourceBody.SafeBooleanComposition?.OccupiedCells is not { Count: > 0 } occupiedCells)
        {
            return KernelResult<BrepBoundedFilletContext>.Failure([Failure(
                "Bounded fillet local builder requires occupied-cell safe-composition metadata.",
                "firmament.fillet-bounded")]);
        }

        if (!double.IsFinite(radius) || radius <= 0d)
        {
            return KernelResult<BrepBoundedFilletContext>.Failure([Failure(
                "Bounded fillet radius must be finite and greater than 0.",
                "firmament.fillet-bounded")]);
        }

        var planar = sourceBody.Bindings.FaceBindings.All(binding => sourceBody.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.Plane);
        var hasCylindricalFaces = sourceBody.Bindings.FaceBindings.Any(binding => sourceBody.Geometry.GetSurface(binding.SurfaceGeometryId).Kind == SurfaceGeometryKind.Cylinder);
        var bounded = radius < selection.MaxAllowedRadius;
        return KernelResult<BrepBoundedFilletContext>.Success(new BrepBoundedFilletContext(
            IsTrustedSource: true,
            SelectionCount: selection.Corners.Count,
            HasLocalChainedInteraction: selection.HasLocalInteraction,
            IsPlanarPlanar: planar,
            HasCylindricalSourceFaces: hasCylindricalFaces,
            IsCylindricalTerminationSupported: false,
            IsInternalConcave: true,
            IsBoundedRadius: bounded,
            Selection: selection,
            Radius: radius,
            OccupiedCells: occupiedCells));
    }

    private static KernelDiagnostic Failure(string message, string source)
        => new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, Source: source);
}
