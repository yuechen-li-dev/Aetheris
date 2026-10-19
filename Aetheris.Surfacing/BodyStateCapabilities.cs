namespace Aetheris.Surfacing;

internal static class BodyStateCapabilities
{
    internal static SculptDiagnostic? ImportedReconstruction(BodyState input, string operation) =>
        input.Body.Bindings.FaceBindings.Any(binding => binding.SourceStepEntityId.HasValue)
            ? new("bodystate-imported-reconstruction-unsupported",
                $"{operation} rebuilds a housing and cannot preserve the imported face provenance of '{input.AuthoredName}'. " +
                "Use the explicit imported-face graft API for its admitted operation; the predecessor remains unchanged.", input.AuthoredName)
            : null;
    internal static SculptDiagnostic? UnretainedSectionChain(BodyState input, string operation)
    {
        var retained = input.ConstructionAuthority?.Operations.FirstOrDefault(o => o.Payload is AddSectionChainOperation or RemoveSectionChainOperation);
        return retained is null ? null : new("bodystate-history-capability-unsupported",
            $"{operation} cannot retain prior {retained.OperationKind} '{retained.OperationId}' with the current materializer. " +
            "The operation was rejected before reconstruction; the predecessor remains authoritative.", input.AuthoredName);
    }

    internal static SculptDiagnostic HousingRequired(BodyState input, string operation) => new(
        "bodystate-operation-capability-unsupported",
        $"{operation} cannot edit engineering body '{input.AuthoredName}' with the current materializer. " +
        "This operation rebuilds the rectangular housing construction; arbitrary body regions are not supported. " +
        "Use an admitted engineering feature on the source body. The input state remains unchanged.", input.AuthoredName);
}
