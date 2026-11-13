using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static SemanticTopologyCorrespondence? ProfileDisplayCorrespondence(ResolvedProfile2D profile, LineArcProfileExtrudeResult emitted, string bodyName)
    {
        if (emitted.Correspondence is not { } correspondence) return null;
        var loop = profile.Loops.First(l => l.IsOuter);
        var uniqueNames = loop.Segments.Select(s => s.Name).Distinct(StringComparer.Ordinal).Count() == loop.Segments.Count;
        var corners = (uniqueNames ? ProfileJunctionClassifier.Classify(profile) : []).Where(j => j.LoopId == loop.Name && (j.Classification is ProfileJunctionKind.ConvexProfileJunction or ProfileJunctionKind.ReflexProfileJunction)
            && loop.Segments.Single(s => s.Name == j.PredecessorSegmentId).Geometry is LineArcLineSegment2D
            && loop.Segments.Single(s => s.Name == j.SuccessorSegmentId).Geometry is LineArcLineSegment2D)
            .ToDictionary(j => $"profile:{profile.Name}.{j.LoopId}.{j.SuccessorSegmentId}", j => j.Classification);
        var sideSources = correspondence.Descendants.Where(d => d.Role == SemanticTopologyRole.ExtrusionSideFace && d.Face is not null)
            .GroupBy(d => d.SourceStableId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        // The two caps: the planar faces across the extrusion axis, named like a Compose operation's, Top at its end.
        var axis = profile.EffectiveConstructionPlane.AxisZ.ToVector();
        var sideFaces = correspondence.Descendants.Where(d => d.Role == SemanticTopologyRole.ExtrusionSideFace && d.Face is not null).Select(d => d.Face!.Value).ToHashSet();
        var caps = emitted.Body!.Topology.Faces.Where(f => !sideFaces.Contains(f.Id)).Select(f => (f.Id, Plane: emitted.Body.TryGetFaceSurfaceGeometry(f.Id, out var s) ? s?.Plane : null))
            .Where(f => f.Plane is { } plane && Math.Abs(Math.Abs(plane.Normal.ToVector().Dot(axis)) - 1) < 1e-9)
            .Select(f => (f.Id, Height: (f.Plane!.Value.Origin - Point3D.Origin).Dot(axis))).OrderBy(f => f.Height).ToArray();
        var capDescendants = caps.Length == 2 && caps[1].Height - caps[0].Height > 1e-9
            ? new[] { (caps[0].Id, Port: "Bottom", Role: SemanticTopologyRole.Unknown), (caps[1].Id, Port: "Top", Role: SemanticTopologyRole.Unknown) }
                .Select(c => new SemanticTopologyDescendant($"plan:{bodyName}:cap:{c.Port.ToLowerInvariant()}:face:{c.Id.Value}", "Face", c.Role, $"extrude:{bodyName}",
                    Face: c.Id, FirmamentSelector: $"face({bodyName}.{c.Port})", Addressability: SemanticTopologyAddressability.DerivedStable)).ToArray()
            : [];
        return correspondence with { BodyStableId = bodyName, Descendants = correspondence.Descendants.Where(d =>
            d.Role is SemanticTopologyRole.ExtrusionSideFace or SemanticTopologyRole.TopBoundary or SemanticTopologyRole.BottomBoundary
            || d.Role == SemanticTopologyRole.VerticalExtrusionEdge && d.StableId.EndsWith(":vertical-start", StringComparison.Ordinal) && corners.ContainsKey(d.SourceStableId))
            .Select(d => d.Role == SemanticTopologyRole.VerticalExtrusionEdge
                ? d with { Role = corners[d.SourceStableId] == ProfileJunctionKind.ReflexProfileJunction ? SemanticTopologyRole.ReflexLongitudinalEdge : SemanticTopologyRole.ConvexLongitudinalEdge,
                    Addressability = SemanticTopologyAddressability.DerivedStable }
                // Every planar side face from one profile segment is addressable (PMI, holes); a hole additionally
                // needs PlanarFaceThroughHole.Support, which inspection reports separately.
                : d.Face is { } id && d.Role == SemanticTopologyRole.ExtrusionSideFace && sideSources[d.SourceStableId] == 1
                    && emitted.Body!.TryGetFaceSurfaceGeometry(id, out var surface) && surface?.Plane is not null
                    ? d with { FirmamentSelector = $"face({bodyName}.Side({System.Text.Json.JsonSerializer.Serialize(d.SourceStableId)}))", Addressability = SemanticTopologyAddressability.DerivedStable }
                    : d).Concat(capDescendants).ToArray() };
    }

    private static KernelResult<FirmamentStepExportResult> ExportLongitudinalFinish(string source, ResolvedProfile2D profile, double height, string bodyName, string? diagnosticSource)
        => ExportProfileCornerHost(source, profile, height, bodyName, diagnosticSource, null);

    // A single prism and source-side holes share the profile plan. General Compose
    // histories still belong to their existing planners and are never flattened here.
    private static KernelResult<FirmamentStepExportResult>? TryExportProfileCornerHost(string source,
        PrismaticProfileCompositionParseResult parsed, string? diagnosticSource, FirmamentV2Document? document)
    {
        var feature = parsed.Feature;
        if (feature is null) return null;
        var requested = ProfileLongitudinalEdgeFinish.Requested(source);
        var operation = feature.Operations.Count == 1 ? feature.Operations[0] : null;
        var qualified = operation is { Intent: PrismaticProfileIntent.Base, From: 0 } && !feature.Placement.IsExplicit
            && feature.Hollow is null && (feature.ShaftHoles?.Count ?? 0) == 0
            && (feature.CounterboreHoles?.Count ?? 0) == 0 && (feature.CountersinkHoles?.Count ?? 0) == 0
            && (feature.ConstructionPlaneBlindDrills?.Count ?? 0) == 0 && !feature.AllSlotProfiles.Any()
            && (feature.Bosses?.Count ?? 0) == 0 && (feature.Pockets?.Count ?? 0) == 0 && (feature.MaterialOffsets?.Count ?? 0) == 0;
        if (!qualified)
            return requested ? KernelResult<FirmamentStepExportResult>.Failure([new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                "longitudinal-edge-finish: This history needs a single profile extrusion with source-side through holes. Other Compose operations are not supported by this finish planner.", "FirmamentV2.EdgeFinish")]) : null;
        if (!parsed.Profiles.TryGetValue(operation!.ProfileReference, out var profile)) return null;
        // Resolved Compose operands carry a plane, while their axial interval
        // belongs to Base. Publish that interval to the profile plan explicitly;
        // otherwise its compatibility default centers the extrusion on the plane.
        profile = profile with { LocalStartDepth = operation.From, LocalEndDepth = operation.To };
        if (!requested && (feature.FaceHoles.Count == 0 || source.Contains("EdgeFinish", StringComparison.Ordinal)
            || profile.Loops.Count != 1 || profile.Loops[0].Segments.Any(s => s.Geometry is not LineArcLineSegment2D))) return null;
        return ExportProfileCornerHost(source, profile, operation.To, feature.Name, diagnosticSource, feature, document);
    }

    private static KernelResult<FirmamentStepExportResult> ExportProfileCornerHost(string source, ResolvedProfile2D profile,
        double height, string bodyName, string? diagnosticSource, PrismaticProfileCompositionFeature? feature, FirmamentV2Document? document = null)
    {
        var result = ProfileLongitudinalEdgeFinish.ApplyAll(source, profile, bodyName);
        KernelResult<FirmamentStepExportResult> Fail(string message)
        {
            var span = ProfileBoundaryChamferSourceBinder.FinishDeclarations(diagnosticSource ?? source).FirstOrDefault()?.Span;
            return KernelResult<FirmamentStepExportResult>.Failure([new KernelDiagnostic(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "FirmamentV2.EdgeFinish")
                { SourceSpan = span is { } s ? new(s.Start, s.Length) : null }]);
        }
        if (result.Profile is null) return Fail(result.Diagnostic!);
        var emitted = ResolvedProfile2DValidator.Extrude(result.Profile, height);
        if (emitted.Body is null || emitted.BRepPlan is null) return Fail(string.Join("; ", emitted.Diagnostics));
        var correspondence = ProfileDisplayCorrespondence(result.Profile, emitted, bodyName)!;
        var original = ResolvedProfile2DValidator.Extrude(profile, height);
        if (original.Body is null || original.Correspondence is null) return Fail("longitudinal-edge-finish: Missing original support plan.");
        var anchors = original.Correspondence.Descendants.Where(d => d.Face is not null && d.Role == SemanticTopologyRole.ExtrusionSideFace)
            .Select(d => (d.SourceStableId, Support: PlanarFaceThroughHole.Support(original.Body, d.Face!.Value)))
            .Where(d => d.Support is not null).ToDictionary(d => d.SourceStableId, d => d.Support!.Origin, StringComparer.Ordinal);
        correspondence = correspondence with {
            Descendants = correspondence.Descendants.Select(d => d.Face is not null && anchors.TryGetValue(d.SourceStableId, out var origin)
                ? d with { SupportOrigin = origin } : d).ToArray(),
            ProvenanceChain = correspondence.ProvenanceChain.Append("ProfileCornerFinishHost").ToArray()
        };
        var completed = PlanarFaceThroughHole.FromProfile(emitted, correspondence);
        foreach (var hole in feature?.FaceHoles ?? [])
        {
            completed = PlanarFaceThroughHole.Apply(completed, hole);
            if (completed.Body is null) return Fail(string.Join("; ", completed.Diagnostics));
        }
        var body = completed.Body!;
        correspondence = completed.Correspondence!;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid || !FirmamentManifoldChecker.IsManifold(body)) return Fail("longitudinal-edge-finish: The resulting body did not pass closed-manifold verification.");
        var semanticPmi = Array.Empty<Step242SemanticPmi>();
        if (document is not null && feature is not null)
        {
            var support = ValidateV2PmiExportSupport(document);
            if (!support.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(support.Diagnostics);
            semanticPmi = BuildV2SemanticPmi(document, [], bodyName, feature.FaceHoles.ToDictionary(h => h.Name, h => h.StableId, StringComparer.Ordinal),
                correspondence, body, ComposeOperationCapFaces(feature, body, correspondence), feature).ToArray();
        }
        var step = Step242Exporter.ExportBody(body, semanticPmi, new Step242ExportOptions { ProductName = bodyName,
            ApplicationName = "Aetheris.Firmament.FullLengthEdgeFinish", BrepExportPreflightMode = BrepExportPreflightMode.Enforce });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess || !FirmamentManifoldChecker.IsManifold(imported.Value)) return Fail("longitudinal-edge-finish: STEP reimport did not preserve a closed manifold.");
        var receipts = result.Finishes.SelectMany(f => FirmamentFeatureAccounting.ProfileFinish(source, f.Name!, bodyName))
            .Concat((feature?.FaceHoles ?? []).Select(h => new FirmamentFeatureReceipt(bodyName, "Hole", h.Name))).ToArray();
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, bodyName, 0, "profile-longitudinal-edge-finish", "profile-corner-host",
            Features: feature is null ? [] : CompositionHoleReports(feature, body, correspondence)) {
            RuntimeBody = body, RuntimeBodySourceName = bodyName,
            RuntimeCorrespondence = correspondence,
            MaterializedFeatures = receipts,
            EngineeringFeatures = result.Finishes.Select(f => new FirmamentEngineeringFeatureReport(
                f.Name!, "EdgeFinish", "edgefinish:" + f.Name, bodyName, "Longitudinal", f.Target!, f.Size,
                f.Kind == "Fillet" ? "Radius" : "Distance", f.AddsMaterial ? "Add" : "Remove", PolicySource: f.Kind, MaterializationRoute: "FullLengthProfileCorner")).ToArray()
        });
    }
}
