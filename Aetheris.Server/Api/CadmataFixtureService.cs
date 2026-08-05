using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Server.Contracts;
using Aetheris.Server.Documents;

namespace Aetheris.Server.Api;

/// <summary>Development fixture bridge. Geometry and topology ids come from the same materializers used by the compiler; no browser-side BRep matching occurs.</summary>
internal static class CadmataFixtureService
{
    private static readonly IReadOnlyDictionary<string, string> FixturePaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["direct-profile"] = "fixtures/FirmamentV2/Profile/valid/scaffold-rectangle.firmament",
        ["construction-plane-positive-x"] = "fixtures/FirmamentV2/Profile/valid/construction-plane-positive-x.firmament",
        ["split-compose-chamfer"] = "fixtures/FirmamentV2/ProfileComposition/valid/semantic-split-compose-chamfer.firmament",
        ["semantic-shaft-hole"] = "fixtures/FirmamentV2/Hole/valid/semantic-shaft-selection.firmament",
        ["construction-plane-through-hole"] = "fixtures/FirmamentV2/Hole/valid/construction-plane-through-hole.firmament",
        ["ctc-01-x3"] = "testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x3.firmament",
        ["ctc-01-x4"] = "testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x4.firmament",
        ["semantic-capsule-slot"] = "fixtures/FirmamentV2/ProfileComposition/valid/semantic-capsule-slot-through.firmament",
    };

    public static bool TryLoad(string fixtureId, DocumentSession document, out CadmataFixtureLoadResponseDto? response, out string error)
    {
        response = null; error = string.Empty;
        if (!FixturePaths.TryGetValue(fixtureId, out var relative)) { error = $"Cadmata fixture '{fixtureId}' is not available."; return false; }
        var sourcePath = Path.Combine(FindRepositoryRoot(), relative);
        if (!File.Exists(sourcePath)) { error = $"Cadmata fixture source is unavailable: {relative}."; return false; }
        var source = File.ReadAllText(sourcePath);
        BrepBody? body = null; SemanticTopologyCorrespondence? correspondence = null; IReadOnlyList<ResolvedProfile2D> profiles = []; IReadOnlyList<PrismaticShaftHoleFeature> shaftHoles = []; IReadOnlyList<PrismaticCapsuleSlotFeature> capsuleSlots = []; IReadOnlyList<PrismaticRoundedRectangleSlotFeature> roundedRectangleSlots = [];
        var diagnostics = new List<string>(); SemanticHoleSourceInspectionEvidence? semanticHoleEvidence = null;
        if (fixtureId is "semantic-shaft-hole" or "construction-plane-through-hole")
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
        var artifact = BuildArtifact(fixtureId, relative, profiles, shaftHoles, capsuleSlots, roundedRectangleSlots, correspondence, diagnostics, body, semanticHoleEvidence);
        response = new(document.Id.ToString(), added.OccurrenceId.ToString(), added.DefinitionId.ToString(), fixtureId, artifact);
        return true;
    }

    private static CadmataVisualizationArtifactDto BuildArtifact(string fixtureId, string sourcePath, IReadOnlyList<ResolvedProfile2D> profiles, IReadOnlyList<PrismaticShaftHoleFeature> shaftHoles, IReadOnlyList<PrismaticCapsuleSlotFeature> capsuleSlots, IReadOnlyList<PrismaticRoundedRectangleSlotFeature> roundedRectangleSlots, SemanticTopologyCorrespondence? correspondence, IReadOnlyList<string> diagnostics, BrepBody body, SemanticHoleSourceInspectionEvidence? semanticHoleEvidence = null)
    {
        var entities = new List<CadmataVisualizationEntityDto>();
        foreach (var profile in profiles)
        {
            var frame = profile.EffectiveConstructionPlane;
            var axes = new[] { frame.Origin, frame.Origin + frame.AxisX.ToVector() * 8, frame.Origin, frame.Origin + frame.AxisY.ToVector() * 8, frame.Origin, frame.Origin + frame.AxisZ.ToVector() * 8 }
                .Select(p => new CadmataPointDto(p.X, p.Y, p.Z)).ToArray();
            entities.Add(new(frame.StableId, "ConstructionPlane", profile.PlaneFrame, "constructionPlanes", "ConstructionFrame", new("polyline", axes), frame.SourceSpan, [frame.SourceConceptId], null, null, null, null, null, null, null, new Dictionary<string, string> { ["sourceConceptId"] = frame.SourceConceptId, ["handedness"] = frame.Handedness, ["determinant"] = frame.Determinant.ToString("R") }));
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
            entities.Add(new(id, "HoleFeature", "mount", "conceptAxes", "SemanticHole", new("circle", Center: new(1.5, -1, 5), Radius: 2), "offset:0", null, null, null, correspondence?.Descendants.Select(d => d.StableId).ToArray(), null, null, "AirHoleSimpleShaftMaterializer", null, new Dictionary<string, string> { ["diameter"] = "4 mm", ["axis"] = "+Z", ["endCondition"] = "throughAll" }));
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
        var hasSemanticFeatures = fixtureId is "semantic-shaft-hole" or "construction-plane-through-hole" || shaftHoles.Count > 0 || capsuleSlots.Count > 0 || roundedRectangleSlots.Count > 0;
        var selectionSourceIds = fixtureId == "semantic-shaft-hole" ? new[] { "hole:base.mount" } : fixtureId == "construction-plane-through-hole" && semanticHoleEvidence is not null ? new[] { "hole:" + semanticHoleEvidence.FeatureId } : capsuleSlots.Count > 0 ? capsuleSlots.Select(slot => slot.StableId).ToArray() : roundedRectangleSlots.Count > 0 ? roundedRectangleSlots.Select(slot => slot.StableId).ToArray() : shaftHoles.Count > 0 ? shaftHoles.Select(hole => hole.StableId).ToArray() : entities.Where(e => e.Kind == "ProfileSegment").Select(e => e.StableId).ToArray();
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
