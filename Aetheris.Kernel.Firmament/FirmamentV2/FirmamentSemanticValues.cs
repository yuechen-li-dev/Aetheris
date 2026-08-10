using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public sealed record ResolvedProfileSemanticBinding(ResolvedProfile2D Profile)
    : ExactProfileBinding("profile:" + Profile.Name)
{
    public override IReadOnlyList<string> ValidateExactProfile() =>
        ResolvedProfile2DValidator.Validate(Profile).Diagnostics;
}

/// <summary>Normalizes native, template-expanded, Concept Path, and Recognize producers.</summary>
public static class FirmamentSemanticValues
{
    public static IReadOnlyList<SemanticValue> FromRecognizedRegions(FirmamentV2Document document, string sourceIdentity, string source)
    {
        var output = new List<SemanticValue>();
        foreach (var bodyName in (document.RecognizedRegions ?? []).Select(region => region.BodyName).Distinct(StringComparer.Ordinal))
        {
            var solid = document.Solids.Single(item => item.Name == bodyName);
            var inline = solid.InlineStep ?? throw new ArgumentException($"Recognized body '{bodyName}' is not InlineStep.");
            var imported = Step242Importer.ImportBody(File.ReadAllText(inline.NormalizedPath));
            if (!imported.IsSuccess || imported.Value is null)
                throw new InvalidDataException($"Recognized body '{bodyName}' could not be re-imported through the exact STEP path: {string.Join("; ", imported.Diagnostics.Select(item => item.Message))}");
            var map = new Dictionary<string, FaceId>(StringComparer.Ordinal);
            foreach (var pair in inline.TopologyMap.FaceEntityToFaceId)
            {
                var token = pair.Value.StartsWith("face-", StringComparison.Ordinal) ? pair.Value[5..] : pair.Value;
                if (!int.TryParse(token, out var faceValue)) throw new InvalidDataException($"Imported face association '{pair.Value}' is not a stable FaceId.");
                var face = new FaceId(faceValue);
                if (!imported.Value.Topology.TryGetFace(face, out _)) throw new InvalidDataException($"Imported face association '{pair.Key}' -> '{pair.Value}' is stale.");
                map[pair.Key] = face;
            }
            var match = Regex.Match(source, $@"\bRecognize\s+{Regex.Escape(bodyName)}\s*\{{", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var span = new SemanticSourceSpan(sourceIdentity, match.Success ? match.Index : 0, match.Success ? DeclarationLength(source, match.Index) : 0);
            var subset = document with { RecognizedRegions = document.RecognizedRegions!.Where(region => region.BodyName == bodyName).ToArray() };
            output.AddRange(FromRecognizedRegions(subset, imported.Value, map, inline.ContentHash, span));
        }
        return output;
    }

    public static IReadOnlyList<SemanticValue> FromProfilesAndConceptPaths(string source, string sourceIdentity = "<memory>", ICollection<string>? reportedDiagnostics = null)
    {
        var expansionDiagnostics = new List<string>();
        var expansion = FirmamentV2TemplateExpansion.Expand(source, expansionDiagnostics);
        foreach (var diagnostic in expansionDiagnostics) reportedDiagnostics?.Add(diagnostic);
        var expanded = expansion?.Source ?? source;
        var bindDiagnostics = new List<string>();
        var profiles = ProfileAuthoringParser.BindPathDerivedProfiles(expanded, bindDiagnostics);
        foreach (var diagnostic in bindDiagnostics) reportedDiagnostics?.Add(diagnostic);
        var inspections = ProfileAuthoringParser.InspectConceptPaths(source);
        var values = new List<SemanticValue>();
        foreach (var inspection in inspections.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            var match = Regex.Match(expanded, $@"\bConcept\s+Path\s+{Regex.Escape(inspection.Name)}\s*\{{", RegexOptions.CultureInvariant);
            var span = new SemanticSourceSpan(sourceIdentity, match.Success ? match.Index : 0, match.Success ? DeclarationLength(expanded, match.Index) : 0);
            var authoredSpan = expansion?.Instantiations.FirstOrDefault() is { } instantiation
                ? new SemanticSourceSpan(sourceIdentity, instantiation.ApplicationSourceSpan.Start, instantiation.ApplicationSourceSpan.Length)
                : span;
            var members = new List<SemanticValue>
            {
                new($"concept-path:{inspection.Name}.Start", new("Point2"), provenance: [new("concept-path", inspection.Name, "validated start", authoredSpan)], authoredSourceSpan: authoredSpan, generatedSourceSpan: expansion is null ? null : span, exposedName: "Start")
            };
            foreach (var entry in inspection.Entries)
            {
                var stepMatch = Regex.Match(expanded, $@"\b(?:Line|Arc|Close)\s+{Regex.Escape(entry.Name)}\b", RegexOptions.CultureInvariant);
                var stepSpan = new SemanticSourceSpan(sourceIdentity, stepMatch.Success ? stepMatch.Index : span.Start, stepMatch.Success ? DeclarationLength(expanded, stepMatch.Index) : span.Length);
                var memberAuthoredSpan = expansion is null ? stepSpan : authoredSpan;
                var end = new SemanticValue(entry.EndpointId, new("Point2"), provenance: [new("concept-path-segment", entry.Name, "validated endpoint", memberAuthoredSpan)], authoredSourceSpan: memberAuthoredSpan, generatedSourceSpan: expansion is null ? null : stepSpan, exposedName: "End");
                members.Add(new SemanticValue(entry.GuideId, new(entry.Kind + "2"), exposedMembers: [end], provenance: [new("concept-path-segment", entry.Name, "validated ordered planar guide", memberAuthoredSpan)], authoredSourceSpan: memberAuthoredSpan, generatedSourceSpan: expansion is null ? null : stepSpan, exposedName: entry.Name));
            }
            var capabilities = new List<ISemanticCapability>();
            var bindings = new List<SemanticBinding>();
            if (profiles.Values.FirstOrDefault(profile => inspection.Consumers?.Any(consumer => consumer.Kind == "Profile" && consumer.Name == profile.Name) == true) is { } profile)
            {
                capabilities.AddRange([new ProfileCapability(), new SelectableCapability(), new ExactGeometryCapability(), new ComposeOperandCapability()]);
                bindings.Add(new ResolvedProfileSemanticBinding(profile));
            }
            var provenance = new List<SemanticProvenance> { new("authored", inspection.Name, "Concept Path", authoredSpan) };
            if (expansion is not null)
                provenance.AddRange(expansion.Instantiations.Select(item => new SemanticProvenance("template-specialization", item.SpecializationIdentity, "expanded before semantic normalization", new(sourceIdentity, item.ApplicationSourceSpan.Start, item.ApplicationSourceSpan.Length))));
            values.Add(new SemanticValue(inspection.Provenance ?? "concept-path:" + inspection.Name, new("ConceptPath"), capabilities, bindings, members, provenance, authoredSpan, expansion is null ? null : span));
        }
        return values;
    }

    public static SemanticValue FromProfile(ResolvedProfile2D profile, SemanticSourceSpan span, IEnumerable<SemanticProvenance>? provenance = null, string? exposedName = null) =>
        new("profile:" + profile.Name, new("Profile2D"),
            [new ProfileCapability(), new SelectableCapability(), new ExactGeometryCapability(), new ComposeOperandCapability(), new ModifyTargetCapability()],
            [new ResolvedProfileSemanticBinding(profile)], provenance: provenance, authoredSourceSpan: span, exposedName: exposedName);

    public static IReadOnlyList<SemanticValue> FromConceptIr(ConceptIrDocument document, string sourceIdentity = "<memory>")
    {
        SemanticValue Scalar(ConceptIrValue value, string? exposedName = null) => new(
            value.StableId, new(value.Kind.ToString()), provenance: [new("concept-ir", value.StableId, value.Provenance)], exposedName: exposedName);
        var values = document.ResolvedValues.Select(value => Scalar(value)).ToList();
        foreach (var item in document.Structs)
        {
            var span = new SemanticSourceSpan(sourceIdentity, item.SourceSpan.Start, item.SourceSpan.Length);
            var members = item.Members.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => Scalar(pair.Value, pair.Key)).ToArray();
            values.Add(new SemanticValue("concept-struct:" + item.Name, new("ConceptStruct"), exposedMembers: members,
                provenance: [new("authored", item.Name, string.Join(",", item.Satisfies), span)], authoredSourceSpan: span));
        }
        return values;
    }

    public static IReadOnlyList<SemanticValue> FromRecognizedRegions(
        FirmamentV2Document document,
        BrepBody body,
        IReadOnlyDictionary<string, FaceId> importedFaceMap,
        string importedSourceIdentity,
        SemanticSourceSpan recognizeSpan)
    {
        var output = new List<SemanticValue>();
        foreach (var bodyGroup in (document.RecognizedRegions ?? []).GroupBy(region => region.BodyName, StringComparer.Ordinal))
        {
            var members = new List<SemanticValue>();
            foreach (var region in bodyGroup.OrderBy(item => item.RegionName, StringComparer.Ordinal))
            {
                var faceIds = region.FaceRefs.Distinct(StringComparer.Ordinal).Select(reference =>
                    importedFaceMap.TryGetValue(reference, out var face) ? face : throw new ArgumentException($"Recognized face '{reference}' has no exact imported BRep association.")).ToArray();
                if (faceIds.Length == 0) continue;
                var stableId = $"recognize:{importedSourceIdentity}:{bodyGroup.Key}.{region.RegionName}:{string.Join(",", region.FaceRefs.Order(StringComparer.Ordinal))}";
                var exact = faceIds.Length == 1
                    ? (SemanticBinding)new ExactBrepFaceBinding(body, faceIds[0], stableId)
                    : new ExactBrepRegionBinding(body, faceIds, stableId);
                members.Add(new SemanticValue(stableId, new("RecognizedRegion"),
                    [new BoundaryRegionCapability(), new SelectableCapability(), new ExactGeometryCapability(), new AnalysisRegionCapability(), new ModifyTargetCapability()],
                    [exact, new ImportedEntityBinding(importedSourceIdentity, region.FaceRefs)],
                    provenance:
                    [
                        new("inline-step", importedSourceIdentity, "canonical imported source"),
                        new("recognize", bodyGroup.Key + "." + region.RegionName, $"kind={region.Kind};confidence={region.Confidence}", recognizeSpan),
                    ],
                    authoredSourceSpan: recognizeSpan,
                    exposedName: region.RegionName));
            }
            var rootId = $"inline-step:{importedSourceIdentity}:{bodyGroup.Key}";
            output.Add(new SemanticValue(rootId, new("ImportedBody"),
                [new BodyCapability(), new ExactGeometryCapability(), new SelectableCapability(), new ModifyTargetCapability()],
                [new ExactBrepBodyBinding(body, rootId), new ImportedEntityBinding(importedSourceIdentity, [])],
                members,
                [new("inline-step", importedSourceIdentity, "exact canonical BRep import", recognizeSpan)], recognizeSpan));
        }
        return output;
    }

    private static int DeclarationLength(string source, int start)
    {
        var open = source.IndexOf('{', start);
        if (open < 0)
        {
            var end = source.IndexOfAny(['\r', '\n'], start);
            return Math.Max(0, end >= 0 ? end - start : source.Length - start);
        }
        var depth = 0;
        for (var index = open; index < source.Length; index++)
        {
            if (source[index] == '{') depth++;
            else if (source[index] == '}' && --depth == 0) return index - start + 1;
        }
        return 0;
    }
}
