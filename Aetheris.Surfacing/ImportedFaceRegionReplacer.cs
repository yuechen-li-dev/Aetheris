using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Surfacing;

public sealed record ImportedFaceReplacementEvidence(
    int SourceStepFaceEntityId,
    int CurrentFaceId,
    IReadOnlyList<int> PreservedNeighborSourceStepEntityIds,
    int InnerLoopCount,
    BrepPcurveEvidence Pcurves);

public sealed record ImportedFaceReplacementResult(
    bool IsSuccess,
    BodyState? OutputState,
    ImportedFaceReplacementEvidence? Evidence,
    IReadOnlyList<SculptDiagnostic> Diagnostics);

/// <summary>
/// Bounded imported-face graft: topology, edge curves, vertices, and neighboring face bindings are
/// retained from the imported BRep; only the selected ADVANCED_FACE support binding is succeeded.
/// </summary>
public static class ImportedFaceRegionReplacer
{
    public static BodyState AdoptImportedBody(BodyState semanticSource, BrepBody importedBody, string authoredName)
    {
        ArgumentNullException.ThrowIfNull(semanticSource); ArgumentNullException.ThrowIfNull(importedBody);
        if (semanticSource.Construction is null) throw new ArgumentException("The imported housing adapter requires an explicit housing semantic source.", nameof(semanticSource));
        var geometry = Step242Exporter.ExportBody(importedBody);
        if (!geometry.IsSuccess) throw new ArgumentException("Imported body cannot be retained: " + string.Join("; ", geometry.Diagnostics.Select(d => d.Message)), nameof(importedBody));
        var associations = SculptedHousingFactory.PersistentAssociations(importedBody, semanticSource.Construction);
        return semanticSource with
        {
            StateId = BodyStateId.Derive($"{semanticSource.StateId.Value}|ImportedStep|{geometry.Value}"),
            PredecessorStateId = semanticSource.StateId,
            AuthoredName = authoredName,
            Body = importedBody,
            ConstructionAuthority = null, // This adapter has no source/import recipe to replay.
            GeometryAssociations = associations,
            SemanticPmi = SculptedHousingFactory.SemanticPmi(associations, semanticSource.Construction, importedBody),
            AssemblyInterfaces = SculptedHousingFactory.AssemblyInterfaces(associations)
        };
    }

    public static ImportedFaceReplacementResult Apply(
        BodyState importedState,
        int sourceStepFaceEntityId,
        BSplineSurfacePatch replacementPatch,
        string outputName,
        double tolerance = 1e-5)
    {
        if (importedState.Construction is null) return new(false, null, null, [BodyStateCapabilities.HousingRequired(importedState, "ImportedFaceReplacement")]);
        var historical = importedState.Delta?.Correspondence.FirstOrDefault(item => item.Change == GeometricChangeKind.Replaced
            && item.InputEntity == $"ADVANCED_FACE:{sourceStepFaceEntityId}");
        if (historical is not null)
            return Failure("surf-selector-target-replaced", $"Imported ADVANCED_FACE #{sourceStepFaceEntityId} was replaced by {string.Join(", ", historical.OutputEntities)} in state {importedState.StateId.Value}; select current geometry.");
        var selectedBindings = importedState.Body.Bindings.FaceBindings.Where(binding => binding.SourceStepEntityId == sourceStepFaceEntityId).ToArray();
        if (selectedBindings.Length != 1)
            return Failure("surf-imported-selector-unresolved", $"Imported ADVANCED_FACE #{sourceStepFaceEntityId} resolved to {selectedBindings.Length} current faces.");
        var selected = selectedBindings[0];
        var face = importedState.Body.Topology.GetFace(selected.FaceId);
        if (face.LoopIds.Count == 0) return Failure("surf-inner-loop-invalid", "Selected imported face has no outer loop.");

        // Preserve this compatibility API's historical resolved-orientation contract.
        var graft = BoundedFaceGraft.Apply(importedState.Body, selected.FaceId, replacementPatch, tolerance, new(replacementPatch.ReversedOrientation));
        if (graft.Body is null) return new(false, null, null, graft.Diagnostics);
        var body = graft.Body;
        var pcurveEvidence = graft.Pcurves!;

        var construction = importedState.Construction with
        {
            ReplacementPatch = replacementPatch,
            CrownWidth = importedState.Construction.Width,
            CrownDepth = importedState.Construction.Depth,
            CrownOffset = SampleMaximumZ(replacementPatch) - importedState.Construction.BaseHeight
        };
        var inventory = importedState.SemanticInventory.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        inventory.Remove(SculptedHousingFactory.CrownRegion);
        inventory[replacementPatch.PatchId] = new(replacementPatch.PatchId, SculptEntityKind.Surface,
            replacementPatch.GeometryFingerprint,
            "Imported ADVANCED_FACE semantic successor.");
        var outputId = BodyStateId.Derive($"{importedState.StateId.Value}|ImportedReplaceRegion|{sourceStepFaceEntityId}|{replacementPatch.PatchId}|{replacementPatch.GeometryFingerprint}|{replacementPatch.ReversedOrientation}");
        var delta = new GeometricDelta(importedState.StateId, outputId, [$"ADVANCED_FACE:{sourceStepFaceEntityId}"],
            [SculptedHousingFactory.BottomMountingInterface, SculptedHousingFactory.MountingHolePattern], [SculptedHousingFactory.CrownRegion], [], [replacementPatch.PatchId],
            [SculptedHousingFactory.CrownRegion], new(-construction.Width / 2d, -construction.Depth / 2d, construction.BaseHeight, construction.Width / 2d, construction.Depth / 2d, construction.FinalHeight),
            [new(SculptedHousingFactory.BottomMountingInterface, GeometricChangeKind.Preserved, [SculptedHousingFactory.BottomMountingInterface], "Explicit imported bottom-interface face association retained."),
             new(SculptedHousingFactory.MountingHolePattern, GeometricChangeKind.Preserved, [SculptedHousingFactory.MountingHolePattern], "Explicit imported cylindrical-face associations retained."),
             new(SculptedHousingFactory.CrownRegion, GeometricChangeKind.Replaced, [replacementPatch.PatchId], $"Explicit imported provenance ADVANCED_FACE #{sourceStepFaceEntityId}."),
             new($"ADVANCED_FACE:{sourceStepFaceEntityId}", GeometricChangeKind.Replaced, [replacementPatch.PatchId], "Imported selector succession is explicit and stale-safe.")]);
        var associationRemap = SculptedHousingFactory.RemapPersistentAssociations(importedState, body, delta);
        if (!associationRemap.IsSuccess) return new(false, null, null, associationRemap.Diagnostics);
        var associations = associationRemap.Associations;
        var evidence = new ImportedFaceReplacementEvidence(sourceStepFaceEntityId, selected.FaceId.Value,
            body.Bindings.FaceBindings.Where(binding => binding.FaceId != selected.FaceId && binding.SourceStepEntityId.HasValue).Select(binding => binding.SourceStepEntityId!.Value).Order().ToArray(),
            face.LoopIds.Count - 1, pcurveEvidence);
        var validations = SculptedHousingFactory.ValidateBody(body, tolerance).Append(new SculptValidationEvidence("ImportedFaceReplacement", true,
            LocalityEvidenceLevel.CertifiedBounded, 0d, tolerance, $"Retained topology and {evidence.PreservedNeighborSourceStepEntityIds.Count} neighboring imported face provenances; replaced support on current face {selected.FaceId.Value}.")).ToArray();
        var output = new BodyState(outputId, importedState.StateId, importedState.BodyStableId, outputName, body, construction, inventory, delta, validations,
            associations, SculptedHousingFactory.SemanticPmi(associations, construction, body), SculptedHousingFactory.AssemblyInterfaces(associations));
        return new(true, output, evidence, []);
    }

    private static double SampleMaximumZ(BSplineSurfacePatch patch)
    {
        var d = patch.ParameterDomain;
        return Enumerable.Range(0, 17).SelectMany(i => Enumerable.Range(0, 17).Select(j => patch.Evaluate(
            d.UMin + ((d.UMax - d.UMin) * i / 16d), d.VMin + ((d.VMax - d.VMin) * j / 16d)).Z)).Max();
    }
    private static ImportedFaceReplacementResult Failure(string code, string message) => new(false, null, null, [new(code, message)]);
}
