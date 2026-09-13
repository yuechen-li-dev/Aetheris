namespace Aetheris.Kernel.StandardLibrary;

/// <summary>Discoverable public metadata for compiler-owned Firmament gear families.</summary>
public sealed record GearFamilyDescriptor(
    string Name,
    string Category,
    string Qualification,
    IReadOnlyList<string> RequiredParameters,
    IReadOnlyList<string> OptionalParameters,
    string Construction);

public static class GearFamilyCatalog
{
    public static IReadOnlyList<GearFamilyDescriptor> Families { get; } =
    [
        new("SpurGear", "PowerTransmission.Gears", "Supported", ["Module", "Teeth", "PressureAngle", "FaceWidth"], ["BoreDiameter", "Backlash", "Phase"], "Standard full-depth external involute"),
        new("InternalSpurGear", "PowerTransmission.Gears", "Supported", ["Module", "Teeth", "PressureAngle", "FaceWidth", "OutsideDiameter"], ["Backlash", "Phase"], "Standard full-depth internal involute ring"),
        new("BevelGear", "PowerTransmission.Gears", "Bounded", ["Module", "Teeth", "PressureAngle", "FaceWidth", "PitchConeAngle"], ["BoreDiameter", "Phase"], "Straight bevel pitch-cone ruled loft"),
        new("MiterGear", "PowerTransmission.Gears", "Bounded", ["Module", "Teeth", "PressureAngle", "FaceWidth"], ["PitchConeAngle", "BoreDiameter", "Phase"], "Equal-pair straight bevel specialization"),
        new("RatchetGear", "PowerTransmission.Ratchets", "Bounded", ["Teeth", "OutsideDiameter", "FaceWidth"], ["BoreDiameter", "DriveFaceAngle", "Phase"], "Asymmetric line/arc radial tooth profile"),
        new("Pawl", "PowerTransmission.Ratchets", "Bounded", ["Width", "Length", "Thickness", "PivotDiameter", "NoseLength"], ["EngagementAngle"], "Prismatic pawl with axial pivot bore")
    ];
}
