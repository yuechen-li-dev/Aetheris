using Aetheris.Modules;

namespace Aetheris.Humanoid;

public static class HumanoidModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.Humanoid");
    public static readonly ModuleVersion Version = new(0, 1, 0);
    public static AetherisModule Definition { get; } = new(Id, "Humanoid", Version,
        [
            Capability("Humanoid.CanonicalSurface", "Versioned adult organic surface with stable topology, regions, symmetry, components, and binding triangulation."),
            Capability("Humanoid.Skeleton", "Canonical semantic skeleton, rest/bind transforms, linear blend skinning, and bounded pose evaluation."),
            Capability("Humanoid.Landmarks", "Typed surface, joint, measurement, and attachment landmark semantics."),
            Capability("Humanoid.Measurements", "Versioned adult measurement protocols with explicit pose and operands."),
            Capability("Humanoid.Morphs", "Bounded stature and limb proportion controls with coherent skeleton and binding updates."),
            Capability("Humanoid.Registration", "Deterministic provenance-gated reference fitting and structured validation evidence.")
        ],
        ["ImportedHumanoid", "CanonicalHumanoid", "HumanoidSurface", "HumanoidSkeleton", "HumanoidLandmark", "HumanoidMeasurementProtocol", "HumanoidAttachmentSite"],
        ["CanonicalAdultStandardV1"],
        ["authored implicit template -> canonical surface -> screened registration -> shaped skeleton/bind -> pose/display mesh"],
        Enum.GetNames<HumanoidDiagnosticCode>(),
        [new(CoreModule.Id, new(1, 0, 0))],
        new("docs/release/HUMANOID-X0.md"));

    private static ModuleCapability Capability(string id, string description) => new(id, Id, Version, description);
}
