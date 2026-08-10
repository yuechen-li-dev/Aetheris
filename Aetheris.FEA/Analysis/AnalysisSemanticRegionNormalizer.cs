using Aetheris.Semantics;

namespace Aetheris.FEA.Analysis;

/// <summary>Erases the producer after checking the common boundary capability.</summary>
public static class AnalysisSemanticRegionNormalizer
{
    public static (SemanticRegionBinding? Region, SemanticDiagnostic? Diagnostic) Normalize(SemanticReference reference)
    {
        var missing = SemanticValueValidator.Require<BoundaryRegionCapability>(reference);
        if (missing is not null) return (null, missing);
        if (reference.Value.TryBinding<ExactAnalysisRegionBinding>(out var analysis))
            return (new(analysis.BodyStableId, analysis.RegionPath, analysis.ExactBrepFaceId, Provenance: Provenance(reference),
                SemanticStableId: reference.Value.StableIdentity,
                CapabilityEvidence: reference.Value.Capabilities.Values.Select(capability => capability.Name).ToArray(),
                ExactBindingKind: analysis.Kind), null);
        if (reference.Value.TryBinding<ExactBrepFaceBinding>(out var face))
            return (new(face.Body.Topology.Bodies.Single().Id.Value.ToString(), reference.Value.ExposedName ?? reference.Value.StableIdentity,
                face.Face.Value.ToString(), Provenance: Provenance(reference), SemanticStableId: reference.Value.StableIdentity,
                CapabilityEvidence: reference.Value.Capabilities.Values.Select(capability => capability.Name).ToArray(), ExactBindingKind: face.Kind), null);
        if (reference.Value.TryBinding<ExactBrepRegionBinding>(out var region))
            return (new(region.Body.Topology.Bodies.Single().Id.Value.ToString(), reference.Value.ExposedName ?? reference.Value.StableIdentity,
                RecognizedFaceIds: region.Faces.OrderBy(face => face.Value).Select(face => face.Value.ToString()).ToArray(), Provenance: Provenance(reference),
                SemanticStableId: reference.Value.StableIdentity,
                CapabilityEvidence: reference.Value.Capabilities.Values.Select(capability => capability.Name).ToArray(), ExactBindingKind: region.Kind), null);
        return (null, new(SemanticValueValidator.NoExactBinding,
            $"BoundaryRegionCapable value '{reference.Value.StableIdentity}' has no exact analysis/BRep region binding.", reference.ConsumerSourceSpan));
    }

    private static AnalysisProvenance Provenance(SemanticReference reference)
    {
        var span = reference.Value.AuthoredSourceSpan ?? reference.ConsumerSourceSpan;
        return new(span.Source, span.Start, span.Length,
            string.Join(" -> ", reference.Value.Provenance.Select(item => item.Stage + ":" + item.Identity)));
    }
}
