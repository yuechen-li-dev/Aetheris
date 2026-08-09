using Aetheris.Forge.Sdk;

namespace MyCompany.SecretGeometry.Generated;

// Focused M1 generated-binding proof. A future generator owns this deterministic shape.
public sealed record SecretCouponSpec(double WidthMillimeters, double DepthMillimeters, double HeightMillimeters);

public static class ForgeTemplates
{
    public static ForgeInvocation SecretCoupon(
        ForgeModule module,
        SecretCouponSpec spec,
        string instanceName = "SecretCoupon") =>
        module.ResolveTemplate("SecretCoupon")
            .Invoke(instanceName)
            .Bind("Spec", new ForgeRecord(
                "SecretCouponSpec",
                new Dictionary<string, ForgeValue>(StringComparer.Ordinal)
                {
                    ["Width"] = new ForgeLength(spec.WidthMillimeters),
                    ["Depth"] = new ForgeLength(spec.DepthMillimeters),
                    ["Height"] = new ForgeLength(spec.HeightMillimeters),
                }));
}
