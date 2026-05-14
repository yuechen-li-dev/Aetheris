namespace Aetheris.Kernel.Firmament.Materializer;

public enum FrepMaterializerPolicyCategory
{
    SemanticExact,
    CirOnlyFallback
}

public sealed record FrepMaterializerPolicyRegistration(
    IFrepMaterializerPolicy Policy,
    FrepMaterializerPolicyCategory Category,
    string Description);

public sealed record FrepMaterializerPolicyCatalogSnapshot(
    IReadOnlyList<string> PolicyNames,
    IReadOnlyList<string> Diagnostics);

public static class FrepMaterializerPolicyCatalog
{
    private static readonly IReadOnlyList<FrepMaterializerPolicyRegistration> DefaultRegistrationsValue =
    [
        new(new ThroughHoleRecoveryPolicy(), FrepMaterializerPolicyCategory.SemanticExact, "Canonical rectangular-box minus cylindrical through-hole exact BRep recovery."),
        new(new CirOnlyFallbackPolicy(), FrepMaterializerPolicyCategory.CirOnlyFallback, "Intent-preserving fallback when no exact semantic BRep policy is admissible.")
    ];

    public static IReadOnlyList<FrepMaterializerPolicyRegistration> DefaultRegistrations() => DefaultRegistrationsValue;

    public static IReadOnlyList<IFrepMaterializerPolicy> Default() => DefaultRegistrationsValue.Select(r => r.Policy).ToArray();

    public static FrepMaterializerPolicyCatalogSnapshot SnapshotDefault()
    {
        var diagnostics = new List<string>
        {
            "FrepMaterializerPolicyCatalog default catalog built.",
            $"Policy registration count: {DefaultRegistrationsValue.Count}."
        };

        diagnostics.AddRange(DefaultRegistrationsValue.Select((registration, index) =>
            $"[{index}] {registration.Policy.Name} ({registration.Category}) - {registration.Description}"));

        return new(DefaultRegistrationsValue.Select(r => r.Policy.Name).ToArray(), diagnostics);
    }
}

public sealed class CirOnlyFallbackPolicy : IFrepMaterializerPolicy
{
    private const double CirOnlyFallbackScore = 1d;

    public string Name => nameof(CirOnlyFallbackPolicy);

    public FrepMaterializerPolicyEvaluation Evaluate(FrepMaterializerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return FrepMaterializerPolicyEvaluation.Admitted(
            Name,
            CirOnlyFallbackScore,
            FrepMaterializerCapability.CirOnly,
            ["cir-only-fallback", "intent-preserving"],
            [
                "Exact semantic BRep recovery unavailable for current input.",
                "Fallback selected to preserve CIR intent without BRep emission."
            ],
            plan: null);
    }
}
