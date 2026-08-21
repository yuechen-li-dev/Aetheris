using Aetheris.Modules;
using Aetheris.Surfacing;

namespace Aetheris.PlasticShell;

public static class PlasticShellModule
{
    public static readonly AetherisModuleId Id = new("Aetheris.PlasticShell");
    public static readonly ModuleVersion Version = new(0, 1, 0);
    public static AetherisModule Definition { get; } = new(Id, "Plastic Shell", Version,
        [
            Capability("PlasticShell.Intent", "First-class molded-product intent retaining exterior authority, wall policy, tooling, parting, gates, standoffs, ejectors, and AutoRib."),
            Capability("PlasticShell.Wall", "Exact paired-support wall realization and independent analytic thickness evidence for the bounded frustum family."),
            Capability("PlasticShell.Tooling", "Exact tooling direction, planar parting, signed draft classification, and bounded directional pullability evidence."),
            Capability("PlasticShell.FlowProxy", "Explicitly non-physical geometric distance proxy from semantic gates."),
            Capability("PlasticShell.AutoRib", "Eligibility-first deterministic rib-network selection through the shared Judgment Engine."),
            Capability("PlasticShell.Step", "Closed-manifold AP242 product body with semantic manufacturing notes and zero rational product surfaces.")
        ],
        ["PlasticShell", "MoldedProduct", "ToolingIntent", "ConstantWall"],
        ["PlasticShell<Exterior,WallPolicy,Tooling,Parting,ManufacturingFeatures>"],
        ["Firmament PlasticShell -> PlasticShellIr -> molding constraints -> exact bounded body -> evidence -> AP242"],
        typeof(PlasticDiagnosticCodes).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToArray(),
        [new(CoreModule.Id, new(1, 0, 0)), new(SurfacingModule.Id, new(0, 1, 0))],
        new("docs/public/firmament/plastic-shell.md"));

    private static ModuleCapability Capability(string id, string description) => new(id, Id, Version, description);
}
