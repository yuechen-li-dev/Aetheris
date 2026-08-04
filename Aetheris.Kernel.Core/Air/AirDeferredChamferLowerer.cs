namespace Aetheris.Kernel.Core.Air;

internal enum AirDeferredChamferFamily
{
    RectangularConcavePocketRim,
    SingleStraightConvexEdge,
    AdjacentEdgeJunction,
}

internal sealed record AirDeferredChamferRequest(
    string FeatureId,
    AirDeferredChamferFamily Family,
    string SemanticSelection,
    double Distance,
    IReadOnlyList<string> AttemptedWitness);

internal sealed record AirDeferredChamferConstruction(string ConstructionId);

/// <summary>
/// Executable boundary fixtures for chamfer families whose required topology witness is not yet
/// representable. These failures intentionally occur before BRepPlan or BRep emission.
/// </summary>
internal static class AirDeferredChamferLowerer
{
    public static ChamferLoweringResult<AirDeferredChamferConstruction> Lower(AirDeferredChamferRequest request)
    {
        if (!double.IsFinite(request.Distance) || request.Distance <= 0)
            return Error(ChamferLoweringErrorKind.InvalidAuthoredInput, "chamfer-invalid-distance:must-be-positive", "Chamfer distance must be finite and positive.", request);
        return request.Family switch
        {
            AirDeferredChamferFamily.RectangularConcavePocketRim => Error(
                ChamferLoweringErrorKind.MissingConstructionWitness,
                "chamfer-missing-construction-witness:section-transition-does-not-support-holes",
                "Current SectionTransition admits one outer loop only; it cannot own retained host regions plus a transitioning inner loop.", request),
            AirDeferredChamferFamily.SingleStraightConvexEdge => Error(
                ChamferLoweringErrorKind.MissingConstructionWitness,
                "chamfer-missing-construction-witness:localized-planar-replacement-not-implemented",
                "A bounded support plane is insufficient without an authoritative retained/replacement region topology plan.", request),
            AirDeferredChamferFamily.AdjacentEdgeJunction => Error(
                ChamferLoweringErrorKind.CornerPolicyRequired,
                "chamfer-corner-policy-required:multiple-valid-corner-patches",
                "The two edge strips admit multiple valid junction patches (miter, setback, or explicit corner face).", request),
            _ => Error(ChamferLoweringErrorKind.UnsupportedJunction, "chamfer-unsupported-junction", "No bounded construction family is registered.", request),
        };
    }

    private static ChamferLoweringResult<AirDeferredChamferConstruction> Error(
        ChamferLoweringErrorKind kind,
        string code,
        string message,
        AirDeferredChamferRequest request) =>
        ChamferLoweringResult<AirDeferredChamferConstruction>.Err(new(kind, code, message, "FeatureAIR->ConstructionAIR",
            [$"selection={request.SemanticSelection}", .. request.AttemptedWitness]));
}
