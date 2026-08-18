using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Firmament;
using Aetheris.Server.Contracts;
using Aetheris.Server.Documents;

namespace Aetheris.Server.Api;

/// <summary>Development fixture bridge. Geometry and topology ids come from the same materializers used by the compiler; no browser-side BRep matching occurs.</summary>
internal static class CadmataFixtureService
{
    private static readonly IReadOnlyDictionary<string, string> FixturePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["direct-profile"] = "fixtures/Profile/valid/scaffold-rectangle.firmament",
        ["construction-plane-positive-x"] = "fixtures/Profile/valid/construction-plane-positive-x.firmament",
        ["split-compose-chamfer"] = "fixtures/ProfileComposition/valid/semantic-split-compose-chamfer.firmament",
        ["semantic-shaft-hole"] = "fixtures/Hole/valid/semantic-shaft-selection.firmament",
        ["construction-plane-through-hole"] = "fixtures/Hole/valid/construction-plane-through-hole.firmament",
        ["construction-plane-blind-drillpoint"] = "fixtures/Hole/valid/construction-plane-blind-drillpoint-shaft-depth.firmament",
        ["ctc-01-x3"] = "fixtures/LegacyV1/Reconstruction/nist_ctc_01/ctc01_prismatic_blockout_x3.firmament",
        ["ctc-01-x4"] = "fixtures/LegacyV1/Reconstruction/nist_ctc_01/ctc01_prismatic_blockout_x4.firmament",
        ["semantic-capsule-slot"] = "fixtures/ProfileComposition/valid/semantic-capsule-slot-through.firmament",
        ["profile-compose-l-bracket-counterbore-pmi"] = "fixtures/Canonical/valid/profile-compose-l-bracket-counterbore-pmi.firmament",
        ["pmi-projected-hole-diameter"] = "fixtures/Canonical/valid/pmi-projected-hole-diameter.firmament",
        ["hexbolt-m1"] = "fixtures/LegacyV1/Examples/mcmaster_91180a151_threadless_hex_bolt.firmament",
        ["hexbolt-m2"] = "fixtures/LegacyV1/Examples/hexbolt_template_m2.firmament",
    };

    public static bool TryLoad(string fixtureId, DocumentSession document, out CadmataFixtureLoadResponseDto? response, out string error)
    {
        response = null; error = string.Empty;
        if (!FixturePaths.TryGetValue(fixtureId, out var relative)) { error = $"Cadmata fixture '{fixtureId}' is not available."; return false; }
        var sourcePath = Path.Combine(FindRepositoryRoot(), relative);
        if (!File.Exists(sourcePath)) { error = $"Cadmata fixture source is unavailable: {relative}."; return false; }
        var source = File.ReadAllText(sourcePath);
        if (fixtureId == "pmi-projected-hole-diameter")
            return TryLoadProjectedPmiFixture(fixtureId, relative, sourcePath, source, document, out response, out error);
        if (fixtureId is "hexbolt-m1" or "hexbolt-m2")
            return TryLoadStandardPartFixture(fixtureId, relative, sourcePath, source, document, out response, out error);
        BrepBody? body = null; SemanticTopologyCorrespondence? correspondence = null; IReadOnlyList<ResolvedProfile2D> profiles = []; IReadOnlyList<PrismaticShaftHoleFeature> shaftHoles = []; IReadOnlyList<PrismaticCapsuleSlotFeature> capsuleSlots = []; IReadOnlyList<PrismaticRoundedRectangleSlotFeature> roundedRectangleSlots = [];
        var diagnostics = new List<string>(); SemanticHoleSourceInspectionEvidence? semanticHoleEvidence = null;
        if (fixtureId is "semantic-shaft-hole" or "construction-plane-through-hole" or "construction-plane-blind-drillpoint")
        {
            var parsed = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(sourcePath));
            if (!parsed.IsSuccess || parsed.Document is null) { error = string.Join("; ", parsed.Diagnostics); return false; }
            var inspected = SemanticHoleInspection.Inspect(parsed.Document);
            body = inspected.Body; correspondence = inspected.Correspondence; semanticHoleEvidence = inspected.Evidence; diagnostics.AddRange(inspected.Diagnostics);
        }
        else if (PrismaticProfileCompositionParser.IsCompositionSource(source))
        {
            var parsed = PrismaticProfileCompositionParser.Parse(source);
            var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var normalizationDiagnostics);
            if (stack is null) { error = string.Join("; ", normalizationDiagnostics); return false; }
            var emitted = PrismaticSectionStackEmitter.Emit(stack);
            shaftHoles = stack.Feature.ShaftHoles ?? [];
            capsuleSlots = stack.Feature.CapsuleSlots ?? [];
            roundedRectangleSlots = stack.Feature.RoundedRectangleSlots ?? [];
            // Synthetic circle Profiles are a compiler lowering detail.  Cadmata publishes the authored
            // HoleFeature and its exact descendants instead of duplicating four axis guides per hole.
            profiles = parsed.Profiles.Values.Where(profile => !shaftHoles.Any(hole => hole.ProfileReference == profile.Name) && !capsuleSlots.Any(slot => slot.ProfileReference == profile.Name) && !roundedRectangleSlots.Any(slot => slot.ProfileReference == profile.Name)).ToArray();
            body = emitted.Body; correspondence = emitted.Correspondence; diagnostics.AddRange(emitted.Diagnostics);
        }
        else
        {
            var parsed = ProfileAuthoringParser.Parse(source);
            if (parsed.Profile is null) { error = string.Join("; ", parsed.Diagnostics); return false; }
            var emitted = ResolvedProfile2DValidator.Extrude(parsed.Profile, parsed.Height);
            body = emitted.Body; correspondence = emitted.Correspondence; profiles = [parsed.Profile]; diagnostics.AddRange(emitted.Diagnostics);
        }
        if (body is null) { error = "The compiler did not materialize a body for this Cadmata fixture."; return false; }
        var added = document.AddBody(body, $"Cadmata: {fixtureId}");
        var pmi = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(sourcePath)).Document?.BoundPmi;
        var artifact = BuildArtifact(fixtureId, relative, profiles, shaftHoles, capsuleSlots, roundedRectangleSlots, correspondence, diagnostics, body, semanticHoleEvidence, pmi);
        response = new(document.Id.ToString(), added.OccurrenceId.ToString(), added.DefinitionId.ToString(), fixtureId, artifact);
        return true;
    }

    private static bool TryLoadStandardPartFixture(string fixtureId, string sourcePath, string fullSourcePath, string source, DocumentSession document, out CadmataFixtureLoadResponseDto? response, out string error)
    {
        response = null; error = string.Empty;
        var parsed = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(fullSourcePath));
        if (!parsed.IsSuccess || parsed.Document is null) { error = string.Join("; ", parsed.Diagnostics); return false; }
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-cadmata-{Guid.NewGuid():N}.step");
        try
        {
            var built = FirmamentBuildAndExport.Run(fullSourcePath, output);
            if (!built.IsSuccess || built.Value?.Export.StandardPart is not { } report) { error = string.Join("; ", built.Diagnostics.Select(item => item.Message)); return false; }
            var imported = Step242Importer.ImportBody(built.Value.Export.StepText);
            if (!imported.IsSuccess || imported.Value is null) { error = string.Join("; ", imported.Diagnostics.Select(item => item.Message)); return false; }
            var body = imported.Value;
            var added = document.AddBody(body, $"Cadmata: {fixtureId}");
            var artifact = BuildStandardPartArtifact(fixtureId, sourcePath, parsed.Document, body, report);
            response = new(document.Id.ToString(), added.OccurrenceId.ToString(), added.DefinitionId.ToString(), fixtureId, artifact);
            return true;
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    private static CadmataVisualizationArtifactDto BuildStandardPartArtifact(string fixtureId, string sourcePath, FirmamentV2Document document, BrepBody body, FirmamentStandardPartReport report)
    {
        var entities = new List<CadmataVisualizationEntityDto>();
        var descendants = report.SemanticDescendants;
        var instantiation = document.ConceptIr?.TemplateInstantiations?.SingleOrDefault();
        var recordArgument = instantiation?.RecordArguments?.SingleOrDefault();
        foreach (var semantic in descendants)
        {
            var children = descendants.Where(candidate => candidate.ParentStableId == semantic.StableId).Select(candidate => candidate.StableId).ToArray();
            var materialFaces = descendants.Where(candidate => candidate.FaceId is not null && (candidate.StableId == semantic.StableId || candidate.StableId.StartsWith(semantic.StableId + ".", StringComparison.Ordinal))).Select(candidate => candidate.FaceId!.Value).Distinct().Order().ToArray();
            var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["family"] = report.Family,
                ["template"] = report.Template ?? string.Empty,
                ["semanticKind"] = semantic.Kind,
                ["sourceForm"] = "FirmamentV2.Record/Static/Template"
            };
            if (instantiation is not null)
            {
                metadata["instance"] = instantiation.Instance;
                metadata["specializationIdentity"] = instantiation.SpecializationIdentity;
                metadata["recordParameter"] = recordArgument?.Key ?? string.Empty;
                metadata["recordType"] = recordArgument?.Value.RecordType ?? string.Empty;
                metadata["recordSource"] = recordArgument?.Value.StaticValue ?? string.Empty;
                metadata["recordProvenance"] = recordArgument?.Value.Provenance ?? string.Empty;
                foreach (var require in instantiation.RequireResults ?? new Dictionary<string, string>()) metadata["require." + require.Key] = require.Value;
            }
            if (semantic.Metadata is not null) metadata["engineeringMetadata"] = semantic.Metadata;
            if (semantic.Kind == "Part")
                foreach (var parameter in report.Parameters)
                {
                    metadata["parameter." + parameter.Key] = parameter.Value;
                    var recordType = recordArgument?.Value.RecordType;
                    var declared = document.StaticAuthoring?.RecordTypes.SingleOrDefault(type => type.Name == recordType)?.Fields.GetValueOrDefault(parameter.Key);
                    metadata["parameterType." + parameter.Key] = declared ?? "Unknown";
                    metadata["parameterSource." + parameter.Key] = recordArgument?.Value.StaticValue ?? "Authored";
                    metadata["parameterStatus." + parameter.Key] = instantiation?.DefaultedArguments.Contains(recordArgument?.Key ?? string.Empty) == true ? "Default" : "BoundStaticRecord";
                }
            entities.Add(new(
                semantic.StableId,
                semantic.Kind == "Part" ? "TemplateInstance" : semantic.Kind == "Region" ? "PartRegion" : "GeneratedFace",
                semantic.StableId[(semantic.StableId.LastIndexOf('.') + 1)..],
                semantic.Kind == "Face" ? "selections" : "conceptAxes",
                semantic.Kind,
                null,
                document.StaticAuthoring?.Templates.SingleOrDefault()?.SourceSpan.ToString(),
                semantic.ParentStableId is null ? null : [semantic.ParentStableId],
                children,
                null,
                children,
                materialFaces.Length == 0 ? null : new CadmataTopologyDto(materialFaces),
                null,
                report.Family == "ExactCoaxialPart" ? "Firmament.Template/ExactCoaxialPart" : "StandardLibrary.HexBoltBuilder",
                null,
                metadata));
        }
        foreach (var face in body.Topology.Faces.OrderBy(item => item.Id.Value))
        {
            var owners = descendants.Where(item => item.FaceId == face.Id.Value).Select(item => item.StableId).ToArray();
            entities.Add(new($"brep:face:{face.Id.Value}", "BRepFace", $"Face {face.Id.Value}", "brepFaces", "MaterialFace", null, null, owners, null, null, null, new CadmataTopologyDto([face.Id.Value]), null, null, null, null));
        }
        var regions = descendants.Where(item => item.Kind == "Region").Select(item => item.StableId).ToArray();
        var selection = new CadmataVisualizationSelectionDto($"selection:{fixtureId}", "generated StandardLibrary regions", "SemanticRegionSet", regions, regions, false, []);
        return new("cadmata-concept-viz-x1", fixtureId, sourcePath, entities, [selection], [], new Dictionary<string, double>
        {
            ["entityCount"] = entities.Count,
            ["faceCount"] = body.Topology.Faces.Count(),
            ["semanticDescendantCount"] = descendants.Count,
            ["templateCount"] = document.ConceptIr?.TemplateInstantiations?.Count ?? document.StaticAuthoring?.Templates.Count ?? 0
        });
    }

    private static bool TryLoadProjectedPmiFixture(string fixtureId, string sourcePath, string fullSourcePath, string source, DocumentSession document, out CadmataFixtureLoadResponseDto? response, out string error)
    {
        response = null; error = string.Empty;
        var parsed = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(fullSourcePath));
        if (!parsed.IsSuccess || parsed.Document is null) { error = string.Join("; ", parsed.Diagnostics); return false; }
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-cadmata-{Guid.NewGuid():N}.step");
        try
        {
            var built = FirmamentBuildAndExport.Run(fullSourcePath, output);
            if (!built.IsSuccess || built.Value is null) { error = string.Join("; ", built.Diagnostics.Select(item => item.Message)); return false; }
            var imported = Step242Importer.ImportBody(built.Value.Export.StepText);
            if (!imported.IsSuccess || imported.Value is null) { error = string.Join("; ", imported.Diagnostics.Select(item => item.Message)); return false; }
            var body = imported.Value;
            var feature = built.Value.Export.Features?.Single(item => item.Name == "Mount");
            if (feature is null) { error = "Projected PMI fixture did not publish its Mount semantic feature report."; return false; }
            var added = document.AddBody(body, $"Cadmata: {fixtureId}");
            var artifact = BuildProjectedPmiArtifact(fixtureId, sourcePath, parsed.Document, body, feature);
            response = new(document.Id.ToString(), added.OccurrenceId.ToString(), added.DefinitionId.ToString(), fixtureId, artifact);
            return true;
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    private static CadmataVisualizationArtifactDto BuildProjectedPmiArtifact(string fixtureId, string sourcePath, FirmamentV2Document document, BrepBody body, FirmamentHoleFeatureReport feature)
    {
        var entities = new List<CadmataVisualizationEntityDto>();
        var bodyId = $"body:{document.ModelName}.Base";
        var holeId = $"hole:{feature.FeatureId}";
        var radius = feature.Diameter / 2d;
        var center = feature.ResolvedPoint3 is { Count: 3 } point ? new CadmataPointDto(point[0], point[1], point[2]) : new(feature.LocalU, feature.LocalV, 6d);
        var holeFaces = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Cylinder && surface.Cylinder is { } cylinder && Math.Abs(cylinder.Radius - radius) < 1e-7).Select(face => face.Id.Value).Order().ToArray();
        var topFaces = body.Topology.Faces.Where(face => body.TryGetFaceSurfaceGeometry(face.Id, out var surface) && surface?.Kind == SurfaceGeometryKind.Plane && surface.Plane is { } plane && plane.Normal.ToVector().Z > 0.999).Select(face => face.Id.Value).Order().ToArray();
        var sourceSpan = feature.SourceSpan;
        entities.Add(new(bodyId, "Body", "Base", "material", "Body", null, null, null, [holeId], null, null, new(topFaces.Concat(holeFaces).Distinct().ToArray()), null, null, null, new Dictionary<string, string> { ["model"] = document.ModelName }));
        entities.Add(new(holeId, "HoleFeature", feature.Name, "conceptAxes", "SemanticHole", new("circle", Center: center, Radius: radius), sourceSpan, [bodyId], null, null, null, new(holeFaces), null, feature.MaterializationRoute, null, new Dictionary<string, string> { ["kind"] = "Hole<Shaft>", ["diameter"] = $"{feature.Diameter:R} mm", ["endCondition"] = "ThroughAll", ["sourceIdentity"] = feature.FeatureId }));
        foreach (var face in body.Topology.Faces.OrderBy(item => item.Id.Value))
        {
            var faceId = face.Id.Value;
            var isHoleFace = holeFaces.Contains(faceId);
            entities.Add(new($"brep:face:{faceId}", "BRepFace", $"Face {faceId}", "selections", isHoleFace ? "HoleShaftWall" : topFaces.Contains(faceId) ? "TopDatumRegion" : "BodyFace", null, null, [isHoleFace ? holeId : bodyId], null, null, null, new([faceId]), null, null, null, null));
        }

        var records = document.PmiBlock?.Records ?? [];
        var bound = (document.BoundPmi?.Datums ?? []).Concat(document.BoundPmi?.Dimensions ?? []).ToDictionary(item => item.Name, StringComparer.Ordinal);
        foreach (var record in records.Where(item => item.Kind is FirmamentV2PmiKind.DatumPlane or FirmamentV2PmiKind.HoleDiameter))
        {
            if (!bound.TryGetValue(record.Name, out var item)) continue;
            var isDatum = record.Kind == FirmamentV2PmiKind.DatumPlane;
            var id = isDatum ? $"pmi:datum:{record.Name}" : $"pmi:dimension:{record.Name}";
            var constraint = record.Projection is null ? null : (document.StaticAuthoring?.SemanticConstraints ?? []).SingleOrDefault(value => value.Id == record.Projection.SourceRequireId);
            var require = constraint is null ? null : document.StaticAuthoring?.Requires.SingleOrDefault(value => value.Name == constraint.Id);
            var tolerance = item.DimensionTolerance;
            var targetId = isDatum ? bodyId : holeId;
            var metadata = new Dictionary<string, string> {
                ["target"] = string.Join(", ", item.Targets), ["targetSemanticId"] = targetId,
                ["nominal"] = item.DimensionValue is { } nominal ? $"{nominal.NumericValue:R} {nominal.Unit}" : "",
                ["tolerancePlus"] = tolerance?.Plus is { } plus ? $"{plus:R} mm" : "", ["toleranceMinus"] = tolerance?.Minus is { } minus ? $"{minus:R} mm" : "",
                ["datumRefs"] = string.Join(", ", item.DatumRefs), ["projection"] = item.ProjectionSource ?? "manual",
                ["require"] = constraint?.Id ?? "", ["subject"] = constraint is null ? "" : $"{constraint.Subject}.{constraint.Property}",
                ["expected"] = require?.Expected ?? "", ["expectedProvenance"] = constraint?.ExpectedProvenance ?? require?.Provenance ?? "", ["toleranceSource"] = require?.ToleranceSource ?? ""
            };
            entities.Add(new(id, isDatum ? "Datum" : "HoleDiameter", record.Name, isDatum ? "conceptPlanes" : "conceptPoints", isDatum ? "Datum" : "HoleDiameter", new("circle", Center: center, Radius: isDatum ? radius * 1.5 : radius), record.SourceSpan.ToString(), [targetId], null, null, null, new(isDatum ? topFaces : holeFaces), null, null, null, metadata));
        }
        return new("cadmata-concept-viz-x1", fixtureId, sourcePath, entities, [], [], new Dictionary<string, double> { ["entityCount"] = entities.Count, ["faceCount"] = body.Topology.Faces.Count(), ["pmiCount"] = entities.Count(item => item.Kind is "Datum" or "HoleDiameter") });
    }

    private static CadmataVisualizationArtifactDto BuildArtifact(string fixtureId, string sourcePath, IReadOnlyList<ResolvedProfile2D> profiles, IReadOnlyList<PrismaticShaftHoleFeature> shaftHoles, IReadOnlyList<PrismaticCapsuleSlotFeature> capsuleSlots, IReadOnlyList<PrismaticRoundedRectangleSlotFeature> roundedRectangleSlots, SemanticTopologyCorrespondence? correspondence, IReadOnlyList<string> diagnostics, BrepBody body, SemanticHoleSourceInspectionEvidence? semanticHoleEvidence = null, FirmamentV2BoundPmiBlock? pmi = null)
    {
        var entities = new List<CadmataVisualizationEntityDto>();
        foreach (var profile in profiles)
        {
            var frame = profile.EffectiveConstructionPlane;
            var axes = new[] { frame.Origin, frame.Origin + frame.AxisX.ToVector() * 8, frame.Origin, frame.Origin + frame.AxisY.ToVector() * 8, frame.Origin, frame.Origin + frame.AxisZ.ToVector() * 8 }
                .Select(p => new CadmataPointDto(p.X, p.Y, p.Z)).ToArray();
            if (!entities.Any(entity => entity.StableId == frame.StableId))
                entities.Add(new(frame.StableId, "ConstructionPlane", profile.PlaneFrame, "constructionPlanes", "ConstructionFrame", new("polyline", axes), frame.SourceSpan, [frame.SourceConceptId], null, null, null, null, null, null, null, new Dictionary<string, string> { ["sourceConceptId"] = frame.SourceConceptId, ["handedness"] = frame.Handedness, ["determinant"] = frame.Determinant.ToString("R") }));
            if (!entities.Any(entity => entity.StableId == frame.SourceConceptId))
                entities.Add(new(frame.SourceConceptId, "ConceptPlane", frame.SourceConceptId, "conceptPlanes", "ConceptGuide", new("polyline", axes), frame.SourceSpan, null, null, null, [frame.StableId], null, null, null, null, null));
        foreach (var segment in profile.Loops.SelectMany(loop => loop.Segments))
        {
            var guideId = segment.Provenance.ConceptStableId;
            entities.Add(new(guideId, "ConceptGuide", guideId.Replace("concept:", "", StringComparison.Ordinal), "profileGuides", "ConstructionGuide", Geometry(segment.Geometry, frame), segment.Provenance.SourceSpan, [frame.StableId], [segment.Provenance.StableId], null, null, null, null, null, null, null));
            var descendants = correspondence?.Descendants.Where(d => d.SourceStableId == segment.Provenance.StableId).ToArray() ?? [];
            entities.Add(new(segment.Provenance.StableId, "ProfileSegment", segment.Name, "profileLoops", "ProfileBoundary", Geometry(segment.Geometry, frame), segment.Provenance.SourceSpan, [guideId, frame.StableId], null, descendants.Where(d => d.Kind == "ArrangementFragment").Select(d => d.StableId).ToArray(), descendants.Where(d => d.Kind != "ArrangementFragment").Select(d => d.StableId).ToArray(), null, null, null, null, new Dictionary<string, string> { ["derivation"] = segment.Provenance.Derivation, ["constructionPlaneId"] = frame.StableId }));
        }
        }
        if (fixtureId == "semantic-shaft-hole")
        {
            const string id = "hole:base.mount";
            entities.Add(new(id, "HoleFeature", "mount", "conceptAxes", "SemanticHole", new("circle", Center: new(1.5, -1, 5), Radius: 2), "offset:0", null, null, null, correspondence?.Descendants.Select(d => d.StableId).ToArray(), null, null, "SemanticShaftHole", null, new Dictionary<string, string> { ["diameter"] = "4 mm", ["axis"] = "+Z", ["endCondition"] = "throughAll" }));
            entities.Add(new("concept:base.mount.axis", "ConceptAxis", "mount axis", "conceptAxes", "HoleAxis", new("polyline", [new(1.5, -1, -5), new(1.5, -1, 5)]), "offset:0", [id], null, null, null, null, null, null, null, null));
        }
        if (semanticHoleEvidence is { PlacementKind: "ConstructionPlane", FrameOrigin: { } origin, AxisX: { } axisX, AxisY: { } axisY, AxisZ: { } axisZ, WorldMouthCenter: { } mouth, HostInterval: { } interval })
        {
            CadmataPointDto P(double[] v) => new(v[0], v[1], v[2]);
            var frameOrigin = P(origin); var x = P(axisX); var y = P(axisY); var z = P(axisZ); var mouthPoint = P(mouth);
            CadmataPointDto Add(CadmataPointDto a, CadmataPointDto direction, double scale) => new(a.X + direction.X * scale, a.Y + direction.Y * scale, a.Z + direction.Z * scale);
            var featureId = "hole:" + semanticHoleEvidence.FeatureId;
            var frameAxes = new[] { frameOrigin, Add(frameOrigin, x, 8), frameOrigin, Add(frameOrigin, y, 8), frameOrigin, Add(frameOrigin, z, 8) };
            entities.Add(new(semanticHoleEvidence.ConstructionPlaneId!, "ConstructionPlane", semanticHoleEvidence.ConstructionPlaneId!, "constructionPlanes", "ConstructionFrame", new("polyline", frameAxes), semanticHoleEvidence.SourceSpan, [semanticHoleEvidence.SourceConceptPlaneId!], [featureId], null, null, null, null, null, null, new Dictionary<string, string> { ["sourceConceptId"] = semanticHoleEvidence.SourceConceptPlaneId! }));
            entities.Add(new(semanticHoleEvidence.SourceConceptPlaneId!, "ConceptPlane", semanticHoleEvidence.SourceConceptPlaneId!, "conceptPlanes", "ConceptGuide", new("polyline", frameAxes), semanticHoleEvidence.SourceSpan, null, [semanticHoleEvidence.ConstructionPlaneId!], null, null, null, null, null, null, null));
            var descendants = correspondence?.Descendants.Select(d => d.StableId).ToArray() ?? [];
            entities.Add(new(featureId, "HoleFeature", semanticHoleEvidence.FeatureId, "conceptAxes", "SemanticHole", new("circle", Center: mouthPoint, Radius: semanticHoleEvidence.Radius), semanticHoleEvidence.SourceSpan, [semanticHoleEvidence.ConstructionPlaneId!], null, null, descendants, null, null, "LocalFrameHoleBRepPlan", null, new Dictionary<string, string> { ["placementKind"] = "ConstructionPlane", ["extent"] = semanticHoleEvidence.Extent, ["hostInterval"] = $"{interval[0]:R}..{interval[1]:R}" }));
            entities.Add(new(featureId + ".axis", "ConceptAxis", "drilling axis", "conceptAxes", "HoleAxis", new("polyline", [mouthPoint, Add(mouthPoint, z, interval[1] - interval[0])]), semanticHoleEvidence.SourceSpan, [featureId], null, null, null, null, null, null, null, null));
            if (semanticHoleEvidence.PointAngle is { } angle && semanticHoleEvidence.ShaftDepth is { } shaftDepth && semanticHoleEvidence.TotalDepth is { } totalDepth)
            {
                var transition = Add(mouthPoint, z, shaftDepth); var tip = Add(mouthPoint, z, totalDepth);
                var pointDescendants = correspondence?.Descendants.Where(d => d.Role is SemanticTopologyRole.HoleDrillPointFace or SemanticTopologyRole.HoleTipVertex).Select(d => d.StableId).ToArray() ?? [];
                entities.Add(new(featureId + ".shaft-envelope", "ShaftEnvelope", "shaft envelope", "conceptAxes", "Shaft", new("polyline", [mouthPoint, transition]), semanticHoleEvidence.SourceSpan, [featureId], null, null, null, null, null, null, null, new Dictionary<string, string> { ["radius"] = semanticHoleEvidence.Radius.ToString("R"), ["shaftDepth"] = shaftDepth.ToString("R") }));
                entities.Add(new(featureId + ".transition", "TransitionLoop", "shaft-to-DrillPoint transition", "profileLoops", "ShaftToDrillPointLoop", new("circle", Center: transition, Radius: semanticHoleEvidence.Radius), semanticHoleEvidence.SourceSpan, [featureId], null, null, correspondence?.Descendants.Where(d => d.Role == SemanticTopologyRole.HoleShaftToDrillPointLoop).Select(d => d.StableId).ToArray(), null, null, "LocalFrameHoleBRepPlan", null, null));
                entities.Add(new(featureId + ".drill-point", "DrillPoint", "exact DrillPoint cone", "conceptAxes", "DrillPoint", new("polyline", [transition, tip]), semanticHoleEvidence.SourceSpan, [featureId], null, null, pointDescendants, null, null, "ConeSurface", null, new Dictionary<string, string> { ["includedPointAngle"] = $"{angle:R} deg", ["tipLength"] = semanticHoleEvidence.TipLength?.ToString("R") ?? string.Empty, ["analytic"] = "ConeSurface" }));
                entities.Add(new(featureId + ".tip", "TipVertex", "DrillPoint tip", "conceptPoints", "TipVertex", new("polyline", [tip]), semanticHoleEvidence.SourceSpan, [featureId + ".drill-point"], null, null, correspondence?.Descendants.Where(d => d.Role == SemanticTopologyRole.HoleTipVertex).Select(d => d.StableId).ToArray(), null, null, "LocalFrameHoleBRepPlan", null, null));
            }
        }
        foreach (var hole in shaftHoles)
        {
            double BoundaryZ(SemanticTopologyRole role, double fallback)
            {
                var loopId = correspondence?.Descendants.FirstOrDefault(d => d.SourceStableId == hole.StableId && d.Role == role)?.Loop;
                if (loopId is null) return fallback;
                var loop = body.Topology.Loops.Single(x => x.Id == loopId.Value);
                var coedge = body.Topology.Coedges.Single(x => x.Id == loop.CoedgeIds[0]);
                var edge = body.Topology.Edges.Single(x => x.Id == coedge.EdgeId);
                return body.TryGetVertexPoint(edge.StartVertexId, out var point) ? point.Z : fallback;
            }
            var entryZ = BoundaryZ(SemanticTopologyRole.HoleEntryLoop, hole.To);
            var exitZ = BoundaryZ(SemanticTopologyRole.HoleExitLoop, hole.From);
            var descendants = correspondence?.Descendants.Where(d => d.SourceStableId == hole.StableId).Select(d => d.StableId).ToArray() ?? [];
            entities.Add(new(hole.StableId, "HoleFeature", hole.Name, "conceptAxes", "SemanticHole", new("circle", Center: new(hole.CenterX, hole.CenterY, entryZ), Radius: hole.Diameter / 2d), hole.SourceSpan, null, null, null, descendants, null, null, "SemanticHoleComposeLowering", null, new Dictionary<string, string> { ["diameter"] = $"{hole.Diameter:R} mm", ["axis"] = "+Z", ["endCondition"] = "throughAll", ["materialInterval"] = $"{exitZ:R}..{entryZ:R} mm" }));
            entities.Add(new($"{hole.StableId}.axis", "ConceptAxis", $"{hole.Name} axis", "conceptAxes", "HoleAxis", new("polyline", [new(hole.CenterX, hole.CenterY, exitZ), new(hole.CenterX, hole.CenterY, entryZ)]), hole.SourceSpan, [hole.StableId], null, null, null, null, null, null, null, null));
        }
        foreach (var slot in capsuleSlots)
        {
            var z = slot.To; var descendants = correspondence?.Descendants.Where(d => d.SourceStableId == slot.StableId).Select(d => d.StableId).ToArray() ?? [];
            var h=slot.StraightSpan/2d; var a=new CadmataPointDto(slot.CenterX-slot.DirectionX*h,slot.CenterY-slot.DirectionY*h,z); var b=new CadmataPointDto(slot.CenterX+slot.DirectionX*h,slot.CenterY+slot.DirectionY*h,z);
            entities.Add(new(slot.StableId,"SlotFeature",slot.Name,"conceptAxes","SemanticSlotCapsule",new("polyline",[a,b]),slot.SourceSpan,null,null,null,descendants,null,null,"SemanticSlotCapsuleComposeLowering",null,new Dictionary<string,string> { ["length"]=$"{slot.Length:R} mm", ["width"]=$"{slot.Width:R} mm", ["radius"]=$"{slot.Radius:R} mm", ["extent"]=slot.Extent }));
            entities.Add(new($"{slot.StableId}.axis","ConceptAxis",$"{slot.Name} major axis","conceptAxes","SlotMajorAxis",new("polyline",[a,b]),slot.SourceSpan,[slot.StableId],null,null,null,null,null,null,null,null));
        }
        foreach (var slot in roundedRectangleSlots)
        {
            var h=slot.Length/2d; var z=slot.To; var a=new CadmataPointDto(slot.CenterX-slot.DirectionX*h,slot.CenterY-slot.DirectionY*h,z); var b=new CadmataPointDto(slot.CenterX+slot.DirectionX*h,slot.CenterY+slot.DirectionY*h,z); var descendants=correspondence?.Descendants.Where(d=>d.SourceStableId==slot.StableId).Select(d=>d.StableId).ToArray() ?? [];
            entities.Add(new(slot.StableId,"SlotFeature",slot.Name,"conceptAxes","SemanticSlotRoundedRectangle",new("polyline",[a,b]),slot.SourceSpan,null,null,null,descendants,null,null,"SemanticSlotComposeLowering",null,new Dictionary<string,string> { ["length"]=$"{slot.Length:R} mm",["width"]=$"{slot.Width:R} mm",["cornerRadius"]=$"{slot.CornerRadius:R} mm",["extent"]=slot.Extent }));
        }
        foreach (var descendant in correspondence?.Descendants ?? [])
        {
            entities.Add(new(descendant.StableId, $"BRep{descendant.Kind}", descendant.Role.ToString(), descendant.Kind == "Face" ? "selections" : "brepEdges", descendant.Role.ToString(), null, null, [descendant.SourceStableId], null, null, null, new(descendant.Face is { } face ? [face.Value] : null, descendant.Edge is { } edge ? [edge.Value] : null, descendant.Loop is { } loop ? [loop.Value] : null, descendant.Vertex is { } vertex ? [vertex.Value] : null), null, null, null, null));
        }
        // PMI is published as an inspectable semantic entity, not a renderer-specific decoration.
        // This fixture's counterbore mouth is part of its authored source and provides a stable initial anchor.
        if (fixtureId == "profile-compose-l-bracket-counterbore-pmi" && pmi is not null)
        {
            var anchor = new CadmataGeometryDto("circle", Center: new(-30, -10, 12), Radius: 4);
            foreach (var datum in pmi.Datums)
                entities.Add(new($"pmi:datum:{datum.Name}", "Datum", datum.Name, "conceptPlanes", "Datum", anchor, datum.SourceSpan.ToString(), null, null, null, null, null, null, null, null, new Dictionary<string, string> { ["target"] = string.Join(", ", datum.Targets), ["pmiKind"] = "Datum" }));
            foreach (var dimension in pmi.Dimensions.Where(item => item.Kind == FirmamentV2PmiKind.HoleDiameter))
            {
                var tolerance = dimension.DimensionTolerance;
                entities.Add(new($"pmi:dimension:{dimension.Name}", "HoleDiameter", dimension.Name, "conceptPoints", "HoleDiameter", anchor, dimension.SourceSpan.ToString(), null, null, null, null, null, null, null, null, new Dictionary<string, string> {
                    ["target"] = string.Join(", ", dimension.Targets), ["nominal"] = $"{dimension.DimensionValue?.NumericValue:R} mm", ["tolerancePlus"] = tolerance?.Plus is { } plus ? $"{plus:R} mm" : "", ["toleranceMinus"] = tolerance?.Minus is { } minus ? $"{minus:R} mm" : "", ["datumRefs"] = string.Join(", ", dimension.DatumRefs), ["projection"] = dimension.ProjectionSource ?? "manual" }));
            }
        }
        var hasSemanticFeatures = fixtureId is "semantic-shaft-hole" or "construction-plane-through-hole" or "construction-plane-blind-drillpoint" || shaftHoles.Count > 0 || capsuleSlots.Count > 0 || roundedRectangleSlots.Count > 0;
        var selectionSourceIds = fixtureId == "semantic-shaft-hole" ? new[] { "hole:base.mount" } : (fixtureId is "construction-plane-through-hole" or "construction-plane-blind-drillpoint") && semanticHoleEvidence is not null ? new[] { "hole:" + semanticHoleEvidence.FeatureId } : capsuleSlots.Count > 0 ? capsuleSlots.Select(slot => slot.StableId).ToArray() : roundedRectangleSlots.Count > 0 ? roundedRectangleSlots.Select(slot => slot.StableId).ToArray() : shaftHoles.Count > 0 ? shaftHoles.Select(hole => hole.StableId).ToArray() : entities.Where(e => e.Kind == "ProfileSegment").Select(e => e.StableId).ToArray();
        var selection = new CadmataVisualizationSelectionDto($"selection:{fixtureId}", hasSemanticFeatures ? "semantic feature descendants" : "compiler-published profile boundary", hasSemanticFeatures ? "FaceSet" : "LoopSet", selectionSourceIds, selectionSourceIds, !hasSemanticFeatures, []);
        return new("cadmata-concept-viz-x1", fixtureId, sourcePath, entities, [selection], diagnostics.Select(d => new CadmataVisualizationDiagnosticDto("Compiler.Trace", d, "info")).ToArray(), new Dictionary<string, double> { ["entityCount"] = entities.Count, ["faceCount"] = body.Topology.Faces.Count(), ["edgeCount"] = body.Topology.Edges.Count() });
    }
    private static CadmataGeometryDto Geometry(LineArcProfileCurve2D curve, ConstructionPlane frame) => curve switch
    {
        LineArcLineSegment2D line => new("polyline", [World(line.Start, frame), World(line.End, frame)]),
        LineArcCircularArc2D arc => new("polyline", ArcPoints(arc).Select(p => World((p.X, p.Y), frame)).ToArray()),
        _ => new("polyline", []),
    };
    private static CadmataPointDto World((double X, double Y) local, ConstructionPlane frame) { var point = frame.ToWorld(local); return new(point.X, point.Y, point.Z); }
    private static IReadOnlyList<CadmataPointDto> ArcPoints(LineArcCircularArc2D arc) => Enumerable.Range(0, 25).Select(i => { var a = arc.StartAngleRadians + arc.SweepAngleRadians * i / 24d; return new CadmataPointDto(arc.Center.X + arc.Radius * Math.Cos(a), arc.Center.Y + arc.Radius * Math.Sin(a), 0); }).ToArray();
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null) { if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) return directory.FullName; directory = directory.Parent; }
        throw new InvalidOperationException("Aetheris repository root was not found.");
    }
}
