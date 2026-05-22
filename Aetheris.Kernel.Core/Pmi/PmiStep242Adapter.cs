using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Pmi;

/// <summary>
/// Bounded adapter from kernel semantic PMI entities into current STEP242 semantic PMI payload records.
/// This is the migration seam while STEP export still consumes legacy Step242SemanticPmi records.
/// </summary>
public static class PmiStep242Adapter
{
    public static IReadOnlyList<Step242SemanticPmi> ToStep242SemanticPmi(PmiModel model, IReadOnlyList<Step242SemanticPmiNote>? passthroughNotes = null)
    {
        ArgumentNullException.ThrowIfNull(model);

        var result = new List<Step242SemanticPmi>(model.DatumFeatures.Count + model.Dimensions.Count + (passthroughNotes?.Count ?? 0));

        foreach (var datum in model.DatumFeatures)
        {
            if (datum.Kind == PmiDatumFeatureKind.Planar
                && datum.Target is PmiPlanarFaceReference planar)
            {
                result.Add(new Step242SemanticPmiDatum(datum.DatumId, "plane", datum.Label, planar.Selector));
            }
        }

        foreach (var dimension in model.Dimensions)
        {
            if (dimension.Kind == PmiDimensionKind.Diameter
                && dimension.PrimaryReference is PmiCylindricalFeatureReference cylinder)
            {
                result.Add(new Step242SemanticPmiHole(
                    cylinder.FeatureId,
                    dimension.NominalValue,
                    null,
                    cylinder.Family,
                    null,
                    null));
            }
        }

        if (passthroughNotes is not null)
        {
            result.AddRange(passthroughNotes);
        }

        return result;
    }
}
