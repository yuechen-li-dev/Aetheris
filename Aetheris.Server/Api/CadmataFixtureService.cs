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
        ["split-compose-chamfer"] = "fixtures/FirmamentV2/ProfileComposition/valid/semantic-split-compose-chamfer.firmament",
        ["semantic-shaft-hole"] = "fixtures/FirmamentV2/Hole/valid/semantic-shaft-selection.firmament",
        ["ctc-01-x3"] = "testdata/firmament/reconstructions/nist_ctc_01/ctc01_prismatic_blockout_x3.firmament",
    };

    public static bool TryLoad(string fixtureId, DocumentSession document, out CadmataFixtureLoadResponseDto? response, out string error)
    {
        response = null; error = string.Empty;
        if (!FixturePaths.TryGetValue(fixtureId, out var relative)) { error = $"Cadmata fixture '{fixtureId}' is not available."; return false; }
        var sourcePath = Path.Combine(FindRepositoryRoot(), relative);
        if (!File.Exists(sourcePath)) { error = $"Cadmata fixture source is unavailable: {relative}."; return false; }
        var source = File.ReadAllText(sourcePath);
        BrepBody? body = null; SemanticTopologyCorrespondence? correspondence = null; IReadOnlyList<ResolvedProfile2D> profiles = [];
        var diagnostics = new List<string>();
        if (fixtureId == "semantic-shaft-hole")
        {
            var parsed = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(sourcePath));
            if (!parsed.IsSuccess || parsed.Document is null) { error = string.Join("; ", parsed.Diagnostics); return false; }
            var inspected = SemanticHoleInspection.Inspect(parsed.Document);
            body = inspected.Body; correspondence = inspected.Correspondence; diagnostics.AddRange(inspected.Diagnostics);
        }
        else if (PrismaticProfileCompositionParser.IsCompositionSource(source))
        {
            var parsed = PrismaticProfileCompositionParser.Parse(source);
            var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var normalizationDiagnostics);
            if (stack is null) { error = string.Join("; ", normalizationDiagnostics); return false; }
            var emitted = PrismaticSectionStackEmitter.Emit(stack);
            body = emitted.Body; correspondence = emitted.Correspondence; profiles = parsed.Profiles.Values.ToArray(); diagnostics.AddRange(emitted.Diagnostics);
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
        var artifact = BuildArtifact(fixtureId, relative, profiles, correspondence, diagnostics, body);
        response = new(document.Id.ToString(), added.OccurrenceId.ToString(), added.DefinitionId.ToString(), fixtureId, artifact);
        return true;
    }

    private static CadmataVisualizationArtifactDto BuildArtifact(string fixtureId, string sourcePath, IReadOnlyList<ResolvedProfile2D> profiles, SemanticTopologyCorrespondence? correspondence, IReadOnlyList<string> diagnostics, BrepBody body)
    {
        var entities = new List<CadmataVisualizationEntityDto>();
        foreach (var profile in profiles)
        foreach (var segment in profile.Loops.SelectMany(loop => loop.Segments))
        {
            var guideId = segment.Provenance.ConceptStableId;
            entities.Add(new(guideId, "ConceptGuide", guideId.Replace("concept:", "", StringComparison.Ordinal), "profileGuides", "ConstructionGuide", Geometry(segment.Geometry), segment.Provenance.SourceSpan, null, [segment.Provenance.StableId], null, null, null, null, null, null, null));
            var descendants = correspondence?.Descendants.Where(d => d.SourceStableId == segment.Provenance.StableId).ToArray() ?? [];
            entities.Add(new(segment.Provenance.StableId, "ProfileSegment", segment.Name, "profileLoops", "ProfileBoundary", Geometry(segment.Geometry), segment.Provenance.SourceSpan, [guideId], null, descendants.Where(d => d.Kind == "ArrangementFragment").Select(d => d.StableId).ToArray(), descendants.Where(d => d.Kind != "ArrangementFragment").Select(d => d.StableId).ToArray(), null, null, null, null, new Dictionary<string, string> { ["derivation"] = segment.Provenance.Derivation }));
        }
        if (fixtureId == "semantic-shaft-hole")
        {
            const string id = "hole:base.mount";
            entities.Add(new(id, "HoleFeature", "mount", "conceptAxes", "SemanticHole", new("circle", Center: new(1.5, -1, 5), Radius: 2), "offset:0", null, null, null, correspondence?.Descendants.Select(d => d.StableId).ToArray(), null, null, "AirHoleSimpleShaftMaterializer", null, new Dictionary<string, string> { ["diameter"] = "4 mm", ["axis"] = "+Z", ["endCondition"] = "throughAll" }));
            entities.Add(new("concept:base.mount.axis", "ConceptAxis", "mount axis", "conceptAxes", "HoleAxis", new("polyline", [new(1.5, -1, -5), new(1.5, -1, 5)]), "offset:0", [id], null, null, null, null, null, null, null, null));
        }
        foreach (var descendant in correspondence?.Descendants ?? [])
        {
            entities.Add(new(descendant.StableId, $"BRep{descendant.Kind}", descendant.Role.ToString(), descendant.Kind == "Face" ? "selections" : "brepEdges", descendant.Role.ToString(), null, null, [descendant.SourceStableId], null, null, null, new(descendant.Face is { } face ? [face.Value] : null, descendant.Edge is { } edge ? [edge.Value] : null, descendant.Loop is { } loop ? [loop.Value] : null, descendant.Vertex is { } vertex ? [vertex.Value] : null), null, null, null, null));
        }
        var selectionSourceIds = fixtureId == "semantic-shaft-hole" ? new[] { "hole:base.mount" } : entities.Where(e => e.Kind == "ProfileSegment").Select(e => e.StableId).ToArray();
        var selection = new CadmataVisualizationSelectionDto($"selection:{fixtureId}", fixtureId == "semantic-shaft-hole" ? "mount semantic shaft" : "compiler-published profile boundary", fixtureId == "semantic-shaft-hole" ? "FaceSet" : "LoopSet", selectionSourceIds, selectionSourceIds, fixtureId != "semantic-shaft-hole", []);
        return new("cadmata-concept-viz-x1", fixtureId, sourcePath, entities, [selection], diagnostics.Select(d => new CadmataVisualizationDiagnosticDto("Compiler.Trace", d, "info")).ToArray(), new Dictionary<string, double> { ["entityCount"] = entities.Count, ["faceCount"] = body.Topology.Faces.Count(), ["edgeCount"] = body.Topology.Edges.Count() });
    }
    private static CadmataGeometryDto Geometry(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new("polyline", [new(line.Start.X, line.Start.Y, 0), new(line.End.X, line.End.Y, 0)]),
        LineArcCircularArc2D arc => new("polyline", ArcPoints(arc)),
        _ => new("polyline", []),
    };
    private static IReadOnlyList<CadmataPointDto> ArcPoints(LineArcCircularArc2D arc) => Enumerable.Range(0, 25).Select(i => { var a = arc.StartAngleRadians + arc.SweepAngleRadians * i / 24d; return new CadmataPointDto(arc.Center.X + arc.Radius * Math.Cos(a), arc.Center.Y + arc.Radius * Math.Sin(a), 0); }).ToArray();
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null) { if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) return directory.FullName; directory = directory.Parent; }
        throw new InvalidOperationException("Aetheris repository root was not found.");
    }
}
