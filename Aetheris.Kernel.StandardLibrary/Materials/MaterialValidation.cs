using Aetheris.Kernel.StandardLibrary.Materials.Database;

namespace Aetheris.Kernel.StandardLibrary.Materials;

public static class MaterialDataValidator
{
    public static IReadOnlyList<string> Validate(IEnumerable<MaterialEntity> materials)
    {
        var entries = materials.ToArray();
        var errors = new List<string>();
        foreach (var duplicate in entries.GroupBy(x => (x.CatalogId, x.StableId)).Where(x => x.Count() > 1))
            errors.Add($"Duplicate stable material identity '{duplicate.Key.CatalogId}:{duplicate.Key.StableId}'.");
        foreach (var duplicate in entries.GroupBy(x => x.FirmamentPath, StringComparer.Ordinal).Where(x => x.Count() > 1))
            errors.Add($"Duplicate Firmament material path '{duplicate.Key}'.");

        foreach (var material in entries)
        {
            foreach (var duplicate in material.Properties.GroupBy(x => x.Kind).Where(x => x.Count() > 1))
                errors.Add($"{material.StableId}:duplicate property {duplicate.Key}.");
            var properties = material.Properties.GroupBy(x => x.Kind).ToDictionary(x => x.Key, x => x.First());
            foreach (var property in properties.Values.Where(x => !double.IsFinite(x.ValueSi)))
                errors.Add($"{material.StableId}:{property.Kind} must be finite.");
            Positive(MaterialPropertyKind.Density);
            Positive(MaterialPropertyKind.YoungsModulus);
            Positive(MaterialPropertyKind.ShearModulus);
            Positive(MaterialPropertyKind.YieldStrength);
            Positive(MaterialPropertyKind.UltimateTensileStrength);
            Positive(MaterialPropertyKind.ThermalConductivity);
            Positive(MaterialPropertyKind.SpecificHeat);
            if (properties.TryGetValue(MaterialPropertyKind.PoissonsRatio, out var poisson) && material.ConstitutiveClass == MaterialConstitutiveClass.LinearElasticIsotropic && (poisson.ValueSi <= -1 || poisson.ValueSi >= 0.5))
                errors.Add($"{material.StableId}:PoissonsRatio must be in (-1, 0.5) for an isotropic material.");
            if (properties.TryGetValue(MaterialPropertyKind.YieldStrength, out var yieldStrength) && properties.TryGetValue(MaterialPropertyKind.UltimateTensileStrength, out var ultimate) && ultimate.ValueSi < yieldStrength.ValueSi)
                errors.Add($"{material.StableId}:UltimateTensileStrength must be at least YieldStrength.");
            if (properties.TryGetValue(MaterialPropertyKind.CoefficientOfThermalExpansion, out var expansion) && expansion.ValueSi <= 0)
                errors.Add($"{material.StableId}:CoefficientOfThermalExpansion must be positive where supplied.");

            void Positive(MaterialPropertyKind kind)
            {
                if (properties.TryGetValue(kind, out var value) && value.ValueSi <= 0)
                    errors.Add($"{material.StableId}:{kind} must be positive where supplied.");
            }
        }
        return errors;
    }
}
