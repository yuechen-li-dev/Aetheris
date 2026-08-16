using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Server.Contracts;

namespace Aetheris.Server.Api;

/// <summary>
/// Adapts imported AP242 product-definition semantics into Cadmata's existing
/// inspectable application model. This layer publishes semantic identity and
/// associations only; camera-facing placement remains presentation state.
/// </summary>
internal static class CadmataStepSemanticBridge
{
    public static CadmataVisualizationArtifactDto? Build(string stepText, string sourceName, BrepBody body)
    {
        var inspection = Step242SemanticPmiInspector.Inspect(stepText);
        if (!inspection.Success || inspection.Items.Count == 0) return null;

        var sourceFaceToBrepFace = body.Bindings.FaceBindings
            .Where(binding => binding.SourceStepEntityId.HasValue)
            .ToDictionary(binding => binding.SourceStepEntityId!.Value, binding => binding.FaceId.Value);
        var bodyAnchor = BodyAnchor(body);
        var entities = new List<CadmataVisualizationEntityDto>();
        var targetIds = inspection.Items
            .Select(item => item.Target)
            .Where(target => !string.IsNullOrWhiteSpace(target))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(target => target, StringComparer.Ordinal)
            .ToDictionary(target => target, TargetId, StringComparer.Ordinal);
        var datumTargetIds = inspection.Items
            .Where(item => item.Kind == "Datum")
            .ToDictionary(item => item.Name, item => targetIds.GetValueOrDefault(item.Target), StringComparer.Ordinal);

        entities.Add(Entity(
            "step:product",
            "Body",
            sourceName,
            "SemanticProductDefinition",
            Point(bodyAnchor),
            childIds: targetIds.Values.ToArray(),
            metadata: new Dictionary<string, string>
            {
                ["authority"] = "STEP AP242 semantic product definition",
                ["presentationAuthority"] = "Cadmata derived presentation"
            }));

        foreach (var (target, stableId) in targetIds)
        {
            var items = inspection.Items.Where(item => item.Target == target).ToArray();
            var faceIds = ResolveFaceIds(items.SelectMany(item => item.GeometricFaceEntityIds), sourceFaceToBrepFace);
            entities.Add(Entity(
                stableId,
                "EngineeringTarget",
                DisplayTarget(target),
                "SemanticTarget",
                Point(Anchor(body, faceIds, bodyAnchor)),
                parentIds: ["step:product"],
                topology: Topology(faceIds),
                metadata: new Dictionary<string, string>
                {
                    ["semanticPath"] = target,
                    ["associationAuthority"] = faceIds.Count > 0 ? "GEOMETRIC_ITEM_SPECIFIC_USAGE" : "product target"
                }));
        }

        foreach (var item in inspection.Items)
        {
            var targetId = targetIds.GetValueOrDefault(item.Target) ?? "step:product";
            var faceIds = ResolveFaceIds(item.GeometricFaceEntityIds, sourceFaceToBrepFace);
            var references = item.Kind == "Position"
                ? item.DatumReferences.Select(reference => datumTargetIds.GetValueOrDefault(reference))
                    .Where(reference => reference is not null).Cast<string>().ToArray()
                : [];
            var metadata = new Dictionary<string, string>
            {
                ["target"] = item.Target,
                ["targetSemanticId"] = targetId,
                ["semanticKind"] = item.Kind,
                ["provenance"] = $"STEP AP242 entity #{item.EntityId}",
                ["sourceEntityId"] = item.EntityId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["presentation"] = "derived; imported annotation transforms are non-authoritative"
            };
            Add(metadata, "nominal", item.Value);
            Add(metadata, "tolerancePlus", item.TolerancePlus);
            Add(metadata, "toleranceMinus", item.ToleranceMinus);
            Add(metadata, "quantity", item.Quantity);
            if (item.Value.HasValue) metadata["unit"] = "mm";
            if (item.DatumReferences.Count > 0) metadata["datumRefs"] = string.Join(" | ", item.DatumReferences);
            if (!string.IsNullOrWhiteSpace(item.Text)) metadata["text"] = item.Text;

            entities.Add(Entity(
                $"step-pmi:{item.EntityId}",
                item.Kind,
                DisplayLabel(item),
                Role(item.Kind),
                Point(Anchor(body, faceIds, bodyAnchor)),
                sourceSpan: $"STEP #{item.EntityId}",
                parentIds: [targetId],
                topology: Topology(faceIds),
                selectionIds: references,
                metadata: metadata));
        }

        var targetOwnersByFace = entities
            .Where(entity => entity.Kind == "EngineeringTarget" && entity.Topology?.FaceIds is { Count: > 0 })
            .SelectMany(entity => entity.Topology!.FaceIds!.Select(faceId => (faceId, entity.StableId)))
            .GroupBy(item => item.faceId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.StableId).Distinct().ToArray());
        foreach (var binding in body.Bindings.FaceBindings.OrderBy(binding => binding.FaceId.Value))
        {
            var faceId = binding.FaceId.Value;
            entities.Add(Entity(
                $"brep:face:{faceId}",
                "BRepFace",
                $"Face {faceId}",
                "MaterialFace",
                null,
                parentIds: targetOwnersByFace.GetValueOrDefault(faceId),
                topology: new CadmataTopologyDto([faceId]),
                metadata: binding.SourceStepEntityId is { } sourceId
                    ? new Dictionary<string, string> { ["stepEntityId"] = sourceId.ToString(System.Globalization.CultureInfo.InvariantCulture) }
                    : null));
        }

        var diagnostics = inspection.Diagnostics
            .Select(message => new CadmataVisualizationDiagnosticDto("STEP.PMI.Inspection", message, "warning"))
            .ToArray();
        return new CadmataVisualizationArtifactDto(
            "cadmata-concept-viz-x1",
            sourceName,
            sourceName,
            entities,
            [],
            diagnostics,
            new Dictionary<string, double>
            {
                ["entityCount"] = entities.Count,
                ["datumCount"] = inspection.DatumCount,
                ["dimensionCount"] = inspection.DimensionCount,
                ["geometricToleranceCount"] = inspection.GeometricToleranceCount,
                ["annotationCount"] = inspection.AnnotationCount,
                ["repeatedFeaturePmiCount"] = inspection.Items.Count(item => item.Quantity is > 1)
            });
    }

    private static CadmataVisualizationEntityDto Entity(
        string stableId,
        string kind,
        string label,
        string role,
        CadmataGeometryDto? geometry,
        string? sourceSpan = null,
        IReadOnlyList<string>? parentIds = null,
        IReadOnlyList<string>? childIds = null,
        CadmataTopologyDto? topology = null,
        IReadOnlyList<string>? selectionIds = null,
        IReadOnlyDictionary<string, string>? metadata = null) =>
        new(stableId, kind, label, "selections", role, geometry, sourceSpan, parentIds, childIds, null, null, topology, selectionIds, null, null, metadata);

    private static string TargetId(string target) => $"step-target:{target}";
    private static string DisplayTarget(string target) => target.Split('.').LastOrDefault() ?? target;
    private static string Role(string kind) => kind switch
    {
        "Datum" => "Datum",
        "Dimension" or "Diameter" => "Dimension",
        "Position" => "GeometricTolerance",
        "Annotation" => "EngineeringAnnotation",
        _ => "SemanticPMI"
    };
    private static string DisplayLabel(Step242SemanticPmiInspectionItem item) => item.Kind switch
    {
        "Datum" => $"Datum {item.Name}",
        "Diameter" => DisplayTarget(item.Target),
        "Annotation" => item.Name,
        _ => item.Name
    };

    private static IReadOnlyList<int> ResolveFaceIds(IEnumerable<int> sourceIds, IReadOnlyDictionary<int, int> map) =>
        sourceIds.Where(map.ContainsKey).Select(sourceId => map[sourceId]).Distinct().OrderBy(id => id).ToArray();

    private static CadmataTopologyDto? Topology(IReadOnlyList<int> faceIds) => faceIds.Count == 0 ? null : new(faceIds);
    // CadmataGeometryDto's transport shape represents point-like evidence as a
    // one-sample polyline (the client already treats its final sample as an anchor).
    private static CadmataGeometryDto Point(Point3D point) => new("polyline", Points: [new(point.X, point.Y, point.Z)]);

    private static Point3D BodyAnchor(BrepBody body)
    {
        var points = body.Topology.Vertices
            .Select(vertex => body.TryGetVertexPoint(vertex.Id, out var point) ? (Point3D?)point : null)
            .Where(point => point.HasValue).Select(point => point!.Value).ToArray();
        if (points.Length == 0) return new Point3D(0, 0, 0);
        return new Point3D(
            (points.Min(point => point.X) + points.Max(point => point.X)) / 2,
            (points.Min(point => point.Y) + points.Max(point => point.Y)) / 2,
            (points.Min(point => point.Z) + points.Max(point => point.Z)) / 2);
    }

    private static Point3D Anchor(BrepBody body, IReadOnlyList<int> faceIds, Point3D fallback)
    {
        var vertices = new HashSet<VertexId>();
        foreach (var faceId in faceIds)
        {
            if (!body.Topology.TryGetFace(new FaceId(faceId), out var face) || face is null) continue;
            foreach (var loopId in face.LoopIds)
            {
                var loop = body.Topology.GetLoop(loopId);
                foreach (var coedgeId in loop.CoedgeIds)
                {
                    var edge = body.Topology.GetEdge(body.Topology.GetCoedge(coedgeId).EdgeId);
                    vertices.Add(edge.StartVertexId);
                    vertices.Add(edge.EndVertexId);
                }
            }
        }
        var points = vertices.Select(vertex => body.TryGetVertexPoint(vertex, out var point) ? (Point3D?)point : null)
            .Where(point => point.HasValue).Select(point => point!.Value).ToArray();
        if (points.Length == 0) return fallback;
        return new Point3D(points.Average(point => point.X), points.Average(point => point.Y), points.Average(point => point.Z));
    }

    private static void Add(IDictionary<string, string> metadata, string key, double? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Add(IDictionary<string, string> metadata, string key, int? value)
    {
        if (value.HasValue) metadata[key] = value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
