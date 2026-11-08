namespace Aetheris.PlasticShell;

/// <summary>
/// Default molding rules per material: guideline values from common resin design guides, meant as starting
/// policy and overridden per Mold (<c>minimumDraft:</c>) or by a typed policy Static. Draft for texture follows the usual
/// rule of 1° per 0.025 mm of texture depth.
/// </summary>
public static class MoldingMaterials
{
    private const double TextureDraft = 40d; // degrees per millimetre of texture depth

    public static IReadOnlyDictionary<string, MoldingPolicy> Defaults { get; } = new[]
    {
        //          name        draft  texture       wall min/max  L/t   shrink  density g/cm³
        new MoldingPolicy("ABS",       1.0, TextureDraft, 1.14, 3.56, 150, 0.006, 1.05),
        new MoldingPolicy("PC",        1.0, TextureDraft, 1.00, 3.80, 100, 0.006, 1.20),
        new MoldingPolicy("PC_ABS",    1.0, TextureDraft, 1.00, 3.50, 120, 0.006, 1.15),
        new MoldingPolicy("PP",        1.0, TextureDraft, 0.64, 3.80, 250, 0.015, 0.90),
        new MoldingPolicy("HDPE",      1.0, TextureDraft, 0.75, 5.00, 250, 0.025, 0.95),
        new MoldingPolicy("PA66",      0.5, TextureDraft, 0.75, 3.00, 200, 0.015, 1.14),
        new MoldingPolicy("PA66_GF30", 0.5, TextureDraft, 0.75, 3.00, 150, 0.005, 1.37),
        new MoldingPolicy("POM",       0.5, TextureDraft, 0.75, 3.00, 150, 0.020, 1.41),
        new MoldingPolicy("PMMA",      1.0, TextureDraft, 0.64, 3.80, 130, 0.004, 1.18),
    }.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
}
