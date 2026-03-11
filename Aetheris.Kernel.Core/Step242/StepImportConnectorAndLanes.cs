using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Step242;

internal sealed class StepParsedSourceDocument(Step242ParsedDocument document) : IParsedSourceDocument
{
    public Step242ParsedDocument Document { get; } = document;

    public string SourceFamily => "STEP/AP242";
}

internal sealed class StepSourceConnector : ISourceConnector
{
    public string Name => "step-ap242-connector";

    public bool CanOpen(ImportRequest request)
    {
        return request.SourceText.Contains("ISO-10303-21", StringComparison.OrdinalIgnoreCase)
            || request.SourceText.Contains("DATA;", StringComparison.OrdinalIgnoreCase);
    }

    public KernelResult<IParsedSourceDocument> Parse(ImportRequest request)
    {
        var parseResult = Step242SubsetParser.Parse(request.SourceText);
        if (!parseResult.IsSuccess)
        {
            return KernelResult<IParsedSourceDocument>.Failure(parseResult.Diagnostics);
        }

        return KernelResult<IParsedSourceDocument>.Success(new StepParsedSourceDocument(parseResult.Value));
    }
}

internal sealed class Step242ExactBRepImportLane : IImportLane
{
    public string Name => "step-ap242-exact-brep";

    public ImportLaneKind Kind => ImportLaneKind.ExactBRep;

    public bool CanImport(IParsedSourceDocument document, ImportPolicy policy)
    {
        if (document is not StepParsedSourceDocument stepDocument)
        {
            return false;
        }

        return !Step242TessellatedImportLane.HasTessellatedRoot(stepDocument.Document);
    }

    public KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy)
    {
        var stepDocument = (StepParsedSourceDocument)document;
        return ImportExactBrep(stepDocument.Document);
    }

    internal static KernelResult<BrepBody> ImportExactBrep(Step242ParsedDocument document)
    {
        try
        {
            return Step242Importer.ImportExactBrepCore(document);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Importer rejected parseable STEP input: {ex.Message}",
                    "Importer.Guardrail")
            ]);
        }
    }
}

internal sealed class Step242TessellatedImportLane : IImportLane
{
    public string Name => "step-ap242-tessellated";

    public ImportLaneKind Kind => ImportLaneKind.Tessellated;

    public bool CanImport(IParsedSourceDocument document, ImportPolicy policy)
    {
        if (document is not StepParsedSourceDocument stepDocument)
        {
            return false;
        }

        return HasTessellatedRoot(stepDocument.Document);
    }

    public KernelResult<BrepBody> Import(IParsedSourceDocument document, ImportPolicy policy)
    {
        var stepDocument = (StepParsedSourceDocument)document;

        return ImportFromParsedDocument(stepDocument.Document);
    }

    internal static KernelResult<BrepBody> ImportFromParsedDocument(Step242ParsedDocument document)
    {
        try
        {
            return ImportTessellated(document);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return KernelResult<BrepBody>.Failure([
                new KernelDiagnostic(
                    KernelDiagnosticCode.InvalidArgument,
                    KernelDiagnosticSeverity.Error,
                    $"Importer rejected parseable STEP input: {ex.Message}",
                    "Importer.Guardrail")
            ]);
        }
    }

    internal static bool HasTessellatedRoot(Step242ParsedDocument document)
    {
        return document.Entities.Any(e => string.Equals(e.Name, "TESSELLATED_SOLID", StringComparison.OrdinalIgnoreCase));
    }

    private static KernelResult<BrepBody> ImportTessellated(Step242ParsedDocument document)
    {
        var tessellatedSolidResult = TryImportTessellatedSolid(document);
        if (tessellatedSolidResult is not null)
        {
            return tessellatedSolidResult;
        }

        return Failure("Requested tessellated import lane, but no TESSELLATED_SOLID root was found.", "Importer.TessellatedSolid.MissingRoot");
    }

    private static KernelResult<BrepBody>? TryImportTessellatedSolid(Step242ParsedDocument document)
    {
        var tessellatedSolids = document.Entities
            .Where(e => string.Equals(e.Name, "TESSELLATED_SOLID", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (tessellatedSolids.Count == 0)
        {
            return null;
        }

        if (tessellatedSolids.Count > 1)
        {
            return Failure("Multiple TESSELLATED_SOLID roots are unsupported in M118 tessellated import subset.", "Importer.TessellatedSolid.SingleRoot");
        }

        var shellRefsResult = Step242SubsetDecoder.ReadReferenceList(tessellatedSolids[0], 1, "TESSELLATED_SOLID shells");
        if (!shellRefsResult.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(shellRefsResult.Diagnostics);
        }

        var faceEntityIds = new List<int>();
        foreach (var shellRef in shellRefsResult.Value)
        {
            var shellResult = document.TryGetEntity(shellRef.TargetId);
            if (!shellResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(shellResult.Diagnostics);
            }

            if (string.Equals(shellResult.Value.Name, "COMPLEX_TRIANGULATED_FACE", StringComparison.OrdinalIgnoreCase))
            {
                faceEntityIds.Add(shellResult.Value.Id);
                continue;
            }

            if (!string.Equals(shellResult.Value.Name, "TESSELLATED_SHELL", StringComparison.OrdinalIgnoreCase))
            {
                return Failure($"TESSELLATED_SOLID supports TESSELLATED_SHELL or direct COMPLEX_TRIANGULATED_FACE items in M118 subset; got '{shellResult.Value.Name}'.", "Importer.TessellatedSolid.ShellType");
            }

            var shellFaceRefsResult = Step242SubsetDecoder.ReadReferenceList(shellResult.Value, 1, "TESSELLATED_SHELL faces");
            if (!shellFaceRefsResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(shellFaceRefsResult.Diagnostics);
            }

            faceEntityIds.AddRange(shellFaceRefsResult.Value.Select(r => r.TargetId));
        }

        var builder = new TopologyBuilder();
        var geometry = new BrepGeometryStore();
        var bindings = new BrepBindingModel();
        var allFaceIds = new List<FaceId>();
        var coedges = new List<Coedge>();
        var nextCurveGeometryId = 1;
        var nextSurfaceGeometryId = 1;

        for (var faceIndex = 0; faceIndex < faceEntityIds.Count; faceIndex++)
        {
            var faceEntityResult = document.TryGetEntity(faceEntityIds[faceIndex]);
            if (!faceEntityResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(faceEntityResult.Diagnostics);
            }

            if (!string.Equals(faceEntityResult.Value.Name, "COMPLEX_TRIANGULATED_FACE", StringComparison.OrdinalIgnoreCase))
            {
                return Failure(
                    $"TESSELLATED_SOLID supports only COMPLEX_TRIANGULATED_FACE in M118 subset; got '{faceEntityResult.Value.Name}'.",
                    "Importer.TessellatedSolid.FaceFamily");
            }

            var trianglesResult = DecodeComplexTriangulatedFaceTriangles(document, faceEntityResult.Value);
            if (!trianglesResult.IsSuccess)
            {
                return KernelResult<BrepBody>.Failure(trianglesResult.Diagnostics);
            }

            foreach (var triangle in trianglesResult.Value)
            {
                var buildResult = AppendTriangleFace(builder, geometry, bindings, coedges, triangle, ref nextCurveGeometryId, ref nextSurfaceGeometryId);
                if (!buildResult.IsSuccess)
                {
                    return KernelResult<BrepBody>.Failure(buildResult.Diagnostics);
                }

                if (buildResult.Value is FaceId faceId)
                {
                    allFaceIds.Add(faceId);
                }
            }
        }

        foreach (var coedge in coedges)
        {
            builder.AddCoedge(coedge);
        }

        if (allFaceIds.Count == 0)
        {
            return Failure("TESSELLATED_SOLID contains no triangulated polygons to import in M118 subset.", "Importer.TessellatedSolid.Empty");
        }

        var shellId = builder.AddShell(allFaceIds);
        builder.AddBody([shellId]);

        var body = new BrepBody(builder.Model, geometry, bindings);
        var validation = BrepBindingValidator.Validate(body, requireAllEdgeAndFaceBindings: true);
        if (!validation.IsSuccess)
        {
            return KernelResult<BrepBody>.Failure(validation.Diagnostics);
        }

        return KernelResult<BrepBody>.Success(body, validation.Diagnostics);
    }

    private static KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>> DecodeComplexTriangulatedFaceTriangles(
        Step242ParsedDocument document,
        Step242ParsedEntity faceEntity)
    {
        if (faceEntity.Arguments.Count < 7)
        {
            return FailureTessellatedFaceTriangles("COMPLEX_TRIANGULATED_FACE requires at least 7 arguments.", "Importer.TessellatedSolid.ComplexTriangulatedFace");
        }

        var coordinatesRefResult = Step242SubsetDecoder.ReadReference(faceEntity, 1, "COMPLEX_TRIANGULATED_FACE coordinates");
        if (!coordinatesRefResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure(coordinatesRefResult.Diagnostics);
        }

        var coordinatesEntityResult = document.TryGetEntity(coordinatesRefResult.Value.TargetId, "COORDINATES_LIST");
        if (!coordinatesEntityResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure(coordinatesEntityResult.Diagnostics);
        }

        var coordinatesResult = ReadCoordinatesListPoints(coordinatesEntityResult.Value);
        if (!coordinatesResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure(coordinatesResult.Diagnostics);
        }

        var pointNodeIndicesResult = ReadOneBasedIntegerList(faceEntity, 5, "COMPLEX_TRIANGULATED_FACE pnindex", "Importer.TessellatedSolid.ComplexTriangulatedFace");
        if (!pointNodeIndicesResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure(pointNodeIndicesResult.Diagnostics);
        }

        var polygonsResult = ReadOneBasedIntegerListList(faceEntity, 6, "COMPLEX_TRIANGULATED_FACE polygon loops", "Importer.TessellatedSolid.ComplexTriangulatedFace");
        if (!polygonsResult.IsSuccess)
        {
            return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure(polygonsResult.Diagnostics);
        }

        var triangles = new List<(Point3D A, Point3D B, Point3D C)>();
        var pnindex = pointNodeIndicesResult.Value;
        var coordinates = coordinatesResult.Value;

        foreach (var polygon in polygonsResult.Value)
        {
            if (polygon.Count < 3)
            {
                continue;
            }

            var polygonCoordinates = new List<Point3D>(polygon.Count);
            for (var i = 0; i < polygon.Count; i++)
            {
                var polygonNodeIndex = polygon[i] - 1;
                if (polygonNodeIndex < 0 || polygonNodeIndex >= pnindex.Count)
                {
                    return FailureTessellatedFaceTriangles("COMPLEX_TRIANGULATED_FACE polygon index is outside pnindex range.", "Importer.TessellatedSolid.ComplexTriangulatedFace");
                }

                var coordinateIndex = pnindex[polygonNodeIndex] - 1;
                if (coordinateIndex < 0 || coordinateIndex >= coordinates.Count)
                {
                    return FailureTessellatedFaceTriangles("COMPLEX_TRIANGULATED_FACE pnindex value is outside COORDINATES_LIST range.", "Importer.TessellatedSolid.ComplexTriangulatedFace");
                }

                polygonCoordinates.Add(coordinates[coordinateIndex]);
            }

            for (var i = 1; i < polygonCoordinates.Count - 1; i++)
            {
                triangles.Add((polygonCoordinates[0], polygonCoordinates[i], polygonCoordinates[i + 1]));
            }
        }

        return KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Success(triangles);
    }

    private static KernelResult<FaceId?> AppendTriangleFace(
        TopologyBuilder builder,
        BrepGeometryStore geometry,
        BrepBindingModel bindings,
        ICollection<Coedge> coedges,
        (Point3D A, Point3D B, Point3D C) triangle,
        ref int nextCurveGeometryId,
        ref int nextSurfaceGeometryId)
    {
        var ab = triangle.B - triangle.A;
        var ac = triangle.C - triangle.A;
        var normal = ab.Cross(ac);
        if (!Direction3D.TryCreate(normal, out var normalDirection))
        {
            return KernelResult<FaceId?>.Success(null);
        }

        var uAxis = Direction3D.Create(ab);
        var surfaceGeometryId = new SurfaceGeometryId(nextSurfaceGeometryId++);
        geometry.AddSurface(surfaceGeometryId, SurfaceGeometry.FromPlane(new PlaneSurface(triangle.A, normalDirection, uAxis)));

        var va = builder.AddVertex();
        var vb = builder.AddVertex();
        var vc = builder.AddVertex();

        var eab = builder.AddEdge(va, vb);
        var ebc = builder.AddEdge(vb, vc);
        var eca = builder.AddEdge(vc, va);

        var edges = new[]
        {
            (Edge: eab, Start: triangle.A, End: triangle.B),
            (Edge: ebc, Start: triangle.B, End: triangle.C),
            (Edge: eca, Start: triangle.C, End: triangle.A)
        };

        foreach (var edge in edges)
        {
            var edgeVector = edge.End - edge.Start;
            if (!Direction3D.TryCreate(edgeVector, out var edgeDirection))
            {
                return KernelResult<FaceId?>.Success(null);
            }

            var length = edgeVector.Length;
            var curveGeometryId = new CurveGeometryId(nextCurveGeometryId++);
            geometry.AddCurve(curveGeometryId, CurveGeometry.FromLine(new Line3Curve(edge.Start, edgeDirection)));
            bindings.AddEdgeBinding(new EdgeGeometryBinding(edge.Edge, curveGeometryId, new ParameterInterval(0d, length), OrientedEdgeSense: true));
        }

        var loopId = builder.AllocateLoopId();
        var c1 = builder.AllocateCoedgeId();
        var c2 = builder.AllocateCoedgeId();
        var c3 = builder.AllocateCoedgeId();

        coedges.Add(new Coedge(c1, eab, loopId, c2, c3, IsReversed: false));
        coedges.Add(new Coedge(c2, ebc, loopId, c3, c1, IsReversed: false));
        coedges.Add(new Coedge(c3, eca, loopId, c1, c2, IsReversed: false));

        builder.AddLoop(new Loop(loopId, [c1, c2, c3]));
        var faceId = builder.AddFace([loopId]);
        bindings.AddFaceBinding(new FaceGeometryBinding(faceId, surfaceGeometryId));
        return KernelResult<FaceId?>.Success(faceId);
    }

    private static KernelResult<IReadOnlyList<Point3D>> ReadCoordinatesListPoints(Step242ParsedEntity coordinatesEntity)
    {
        if (coordinatesEntity.Arguments.Count < 3 || coordinatesEntity.Arguments[2] is not Step242ListValue points)
        {
            return FailureTessellatedFacePoints("COORDINATES_LIST points argument is missing or malformed.", "Importer.TessellatedSolid.CoordinatesList");
        }

        var result = new List<Point3D>(points.Items.Count);
        for (var i = 0; i < points.Items.Count; i++)
        {
            if (points.Items[i] is not Step242ListValue tuple || tuple.Items.Count != 3
                || tuple.Items[0] is not Step242NumberValue x
                || tuple.Items[1] is not Step242NumberValue y
                || tuple.Items[2] is not Step242NumberValue z)
            {
                return FailureTessellatedFacePoints("COORDINATES_LIST contains a non-XYZ tuple.", "Importer.TessellatedSolid.CoordinatesList");
            }

            result.Add(new Point3D(x.Value, y.Value, z.Value));
        }

        return KernelResult<IReadOnlyList<Point3D>>.Success(result);
    }

    private static KernelResult<IReadOnlyList<int>> ReadOneBasedIntegerList(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count || entity.Arguments[argumentIndex] is not Step242ListValue list)
        {
            return FailureIntegerList($"{context}: expected integer list argument.", source);
        }

        var values = new List<int>(list.Items.Count);
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i] is not Step242NumberValue number || number.Value <= 0 || number.Value != System.Math.Floor(number.Value))
            {
                return FailureIntegerList($"{context}: list item {i} must be a positive integer.", source);
            }

            values.Add((int)number.Value);
        }

        return KernelResult<IReadOnlyList<int>>.Success(values);
    }

    private static KernelResult<IReadOnlyList<IReadOnlyList<int>>> ReadOneBasedIntegerListList(Step242ParsedEntity entity, int argumentIndex, string context, string source)
    {
        if (argumentIndex < 0 || argumentIndex >= entity.Arguments.Count || entity.Arguments[argumentIndex] is not Step242ListValue outer)
        {
            return FailureIntegerListList($"{context}: expected list-of-list integer argument.", source);
        }

        var lists = new List<IReadOnlyList<int>>(outer.Items.Count);
        for (var i = 0; i < outer.Items.Count; i++)
        {
            if (outer.Items[i] is not Step242ListValue inner)
            {
                return FailureIntegerListList($"{context}: item {i} must be an integer list.", source);
            }

            var values = new List<int>(inner.Items.Count);
            for (var j = 0; j < inner.Items.Count; j++)
            {
                if (inner.Items[j] is not Step242NumberValue number || number.Value <= 0 || number.Value != System.Math.Floor(number.Value))
                {
                    return FailureIntegerListList($"{context}: item {i} value {j} must be a positive integer.", source);
                }

                values.Add((int)number.Value);
            }

            lists.Add(values);
        }

        return KernelResult<IReadOnlyList<IReadOnlyList<int>>>.Success(lists);
    }

    private static KernelResult<BrepBody> Failure(string message, string source) =>
        KernelResult<BrepBody>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>> FailureTessellatedFaceTriangles(string message, string source) =>
        KernelResult<IReadOnlyList<(Point3D A, Point3D B, Point3D C)>>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<IReadOnlyList<Point3D>> FailureTessellatedFacePoints(string message, string source) =>
        KernelResult<IReadOnlyList<Point3D>>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<IReadOnlyList<int>> FailureIntegerList(string message, string source) =>
        KernelResult<IReadOnlyList<int>>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);

    private static KernelResult<IReadOnlyList<IReadOnlyList<int>>> FailureIntegerListList(string message, string source) =>
        KernelResult<IReadOnlyList<IReadOnlyList<int>>>.Failure([new KernelDiagnostic(KernelDiagnosticCode.NotImplemented, KernelDiagnosticSeverity.Error, message, source)]);
}
