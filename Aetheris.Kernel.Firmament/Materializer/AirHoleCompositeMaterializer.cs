using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;

namespace Aetheris.Kernel.Firmament.Materializer;

internal sealed record AirHoleCompositeMaterializationResult(
    bool Succeeded,
    BrepBody? Body,
    IReadOnlyList<AirHoleSimpleShaftMaterializationPlan> Plans,
    IReadOnlyList<string> Diagnostics);

internal static class AirHoleCompositeMaterializer
{
    private const double Tolerance = 1e-9;

    public static AirHoleCompositeMaterializationResult Execute(IReadOnlyList<AirHoleFeature> features, AirHoleSimpleShaftHost host)
    {
        var diagnostics = new List<string> { "air-hole-x4 composite semantic hole planner started." };
        if (features.Count == 0)
        {
            diagnostics.Add("air-hole-x4 rejected: composite materialization requires at least one semantic AirHoleFeature.");
            return new(false, null, [], diagnostics);
        }

        var plans = new List<AirHoleSimpleShaftMaterializationPlan>(features.Count);
        foreach (var feature in features)
        {
            var planResult = AirHoleSimpleShaftMaterializer.TryCreatePlan(feature, host);
            diagnostics.AddRange(planResult.Diagnostics.Select(d => $"{feature.FeatureId}: {d}"));
            if (!planResult.Succeeded || planResult.Plan is null)
            {
                diagnostics.Add($"air-hole-x4 rejected: semantic feature {feature.FeatureId} cannot enter bounded composite materialization.");
                return new(false, null, plans, diagnostics);
            }

            if (planResult.Plan.StackKind != AirHoleStackKind.SimpleShaft)
            {
                diagnostics.Add($"air-hole-x4 rejected: composite materialization is limited to circular shaft holes for STEP-V2-X4; feature {feature.FeatureId} is {planResult.Plan.StackKind}.");
                return new(false, null, plans, diagnostics);
            }

            plans.Add(planResult.Plan);
        }

        var zAxis = Direction3D.Create(new Vector3D(0, 0, 1));
        var xAxis = Direction3D.Create(new Vector3D(1, 0, 0));
        var extents = new AxisAlignedBoxExtents(-host.Width / 2d, host.Width / 2d, -host.Depth / 2d, host.Depth / 2d, host.ZMin, host.ZMax);
        var composition = new SafeBooleanComposition(extents, [], SafeBooleanRootDescriptor.FromBox(extents));

        foreach (var plan in plans)
        {
            var span = plan.CutZMin <= host.ZMin + Tolerance && plan.CutZMax >= host.ZMax - Tolerance
                ? SupportedBooleanHoleSpanKind.Through
                : (Math.Abs(plan.CutZMax - host.ZMax) < Tolerance ? SupportedBooleanHoleSpanKind.BlindFromTop : SupportedBooleanHoleSpanKind.BlindFromBottom);
            var cylinder = new RecognizedCylinder(new Point3D(plan.CenterU, plan.CenterV, 0), zAxis, plan.Radius, plan.CutZMin, plan.CutZMax);
            var surface = new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cylinder);
            if (!BrepBooleanSafeCompositionGraphValidator.TryValidateNextSubtract(composition, surface, ToleranceContext.Default, out var updated, out var diagnostic, plan.SemanticFeatureId))
            {
                diagnostics.Add("firmament-v2-semantic-hole-overlap: " + (diagnostic?.Message ?? $"feature {plan.SemanticFeatureId} overlaps or conflicts with a previously accepted semantic hole."));
                return new(false, null, plans, diagnostics);
            }

            // Preserve the end-condition span chosen by AirHoleFeature planning while using the graph validator's narrow same-axis interference policy.
            var last = updated.Holes[^1] with { SpanKind = span, StartZ = plan.CutZMin, EndZ = plan.CutZMax };
            composition = updated with { Holes = [.. updated.Holes.Take(updated.Holes.Count - 1), last] };
            diagnostics.Add($"air-hole-x4 accepted semantic feature {plan.SemanticFeatureId} center=({plan.CenterU:0.###},{plan.CenterV:0.###}) radius={plan.Radius:0.###}.");
        }

        var built = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(composition, ToleranceContext.Default);
        if (!built.IsSuccess || built.Value is null)
        {
            diagnostics.Add("air-hole-x4 composite BRep build failed.");
            diagnostics.AddRange(built.Diagnostics.Select(d => d.Message));
            return new(false, null, plans, diagnostics);
        }

        diagnostics.Add("air-hole-x4 composite materialization succeeded through semantic AirHoleFeature plans and safe boolean AP242 BRep composition.");
        return new(true, built.Value, plans, diagnostics);
    }
}
