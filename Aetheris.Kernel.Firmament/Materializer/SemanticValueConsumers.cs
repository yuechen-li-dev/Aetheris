using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Materializer;

public sealed record FirmamentTopologySelectionBinding(
    BrepBody Body,
    SemanticTopologyCorrespondence Correspondence,
    IReadOnlyList<string> SourceStableIds)
    : ExactSelectionBinding("selection-source:" + Correspondence.BodyStableId + ":" + string.Join(",", SourceStableIds.Order(StringComparer.Ordinal)));

public sealed record SemanticConsumerResult<T>(T? Value, IReadOnlyList<SemanticDiagnostic> Diagnostics) where T : class
{
    public bool IsSuccess => Value is not null && Diagnostics.Count == 0;
}

/// <summary>One profile contract shared by Profile, Compose, and admitted Modify consumers.</summary>
public static class ProfileSemanticConsumer
{
    public static SemanticConsumerResult<ResolvedProfile2D> RequireProfile(SemanticReference reference, string consumer)
    {
        var missing = SemanticValueValidator.Require<ProfileCapability>(reference);
        if (missing is not null) return new(null, [missing]);
        if (!reference.Value.TryBinding<ExactProfileBinding>(out var binding) || binding is not ResolvedProfileSemanticBinding firmament)
            return new(null, [new(SemanticValueValidator.NoExactBinding, $"{consumer} requires a validated ResolvedProfile2D binding.", reference.ConsumerSourceSpan)]);
        var validation = firmament.ValidateExactProfile();
        return validation.Count == 0
            ? new(firmament.Profile, [])
            : new(null, validation.Select(message => new SemanticDiagnostic(SemanticValueValidator.NoExactBinding, message, reference.ConsumerSourceSpan)).ToArray());
    }
}

public static class SemanticValueSelectionConsumer
{
    public static SemanticSelectionResolution Resolve(
        SemanticReference reference,
        string label,
        SemanticTopologyRole? role,
        SemanticSelectionRequirement requirement)
    {
        var request = new SemanticSelectionRequest("selection:" + label, label, BodyId(reference.Value), [reference.Value.StableIdentity], role, requirement,
            $"{reference.ConsumerSourceSpan.Source}:{reference.ConsumerSourceSpan.Start}", "SemanticValue");
        var missing = SemanticValueValidator.Require<SelectableCapability>(reference);
        if (missing is not null) return Failure(request, SemanticSelectionFailure.SelectionConsumerMismatch, missing);
        if (reference.Value.TryBinding<FirmamentTopologySelectionBinding>(out var topology))
        {
            request = request with { BodyStableId = topology.Correspondence.BodyStableId, SourceStableIds = topology.SourceStableIds };
            return SemanticTopologySelectionResolver.Resolve(topology.Body, topology.Correspondence, request);
        }
        if (reference.Value.TryBinding<ExactBrepFaceBinding>(out var face))
            return ExactFaces(request, face.Body, [face.Face]);
        if (reference.Value.TryBinding<ExactBrepRegionBinding>(out var region))
            return ExactFaces(request, region.Body, region.Faces);
        return Failure(request, SemanticSelectionFailure.SelectionConsumerMismatch,
            new(SemanticValueValidator.NoExactBinding, $"Selectable value '{reference.Value.StableIdentity}' has no exact selection binding.", reference.ConsumerSourceSpan));
    }

    private static SemanticSelectionResolution ExactFaces(SemanticSelectionRequest request, BrepBody body, IReadOnlyList<FaceId> faces)
    {
        if (request.Require is not (SemanticSelectionRequirement.ExactlyOne or SemanticSelectionRequirement.OneOrMore or SemanticSelectionRequirement.NonEmptyFaceSet))
            return Failure(request, SemanticSelectionFailure.SelectionConsumerMismatch,
                new(SemanticValueValidator.MissingCapability, "An exact face region does not prove an edge chain or loop contract."));
        if (request.Require == SemanticSelectionRequirement.ExactlyOne && faces.Count != 1)
            return Failure(request, SemanticSelectionFailure.SelectionCardinalityMismatch,
                new("semantic-selection-cardinality-mismatch", $"Expected one face; found {faces.Count}."));
        var descendants = faces.OrderBy(face => face.Value).Select(face => new SemanticTopologyDescendant(
            $"semantic-face:{face.Value}", "Face", SemanticTopologyRole.Unknown, request.SourceStableIds.Single(), Face: face)).ToArray();
        return new(true, SemanticSelectionFailure.None, request, descendants, [], false, false, []);
    }

    private static string BodyId(SemanticValue value) => value.TryBinding<ExactBrepBodyBinding>(out var body) ? body.BodyStableId
        : value.TryBinding<ExactBrepFaceBinding>(out var face) ? face.Body.Topology.Bodies.Single().Id.Value.ToString()
        : value.TryBinding<ExactBrepRegionBinding>(out var region) ? region.Body.Topology.Bodies.Single().Id.Value.ToString()
        : value.StableIdentity;

    private static SemanticSelectionResolution Failure(SemanticSelectionRequest request, SemanticSelectionFailure failure, SemanticDiagnostic diagnostic) =>
        new(false, failure, request, [], [], false, false,
            [new(diagnostic.Code, diagnostic.Message, request.SourceSpan, [])]);
}

public static class ModifySemanticConsumer
{
    public static SemanticDiagnostic? Admit(SemanticReference reference)
    {
        var missing = SemanticValueValidator.Require<ModifyTargetCapability>(reference);
        if (missing is not null) return missing;
        return reference.Value.Bindings.Any(binding => binding is ExactBrepBodyBinding or ExactBrepFaceBinding or ExactBrepRegionBinding or ExactProfileBinding)
            ? null
            : new(SemanticValueValidator.NoExactBinding, "Modify requires an exact body, face/region, or validated profile binding.", reference.ConsumerSourceSpan);
    }
}
