using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

internal static class FirmamentV2DfmEnforcement
{
    public static KernelResult<object> Validate(FirmamentV2Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var diagnostics = new List<KernelDiagnostic>();
        foreach (var template in document.Templates ?? [])
        {
            if (!string.Equals(template.Process, "CNC", StringComparison.Ordinal)) continue;
            foreach (var concept in template.Concepts.Where(c => string.Equals(c.Name, "minimumToolRadius", StringComparison.Ordinal)))
            {
                if (!string.Equals(concept.Unit, document.Units, StringComparison.Ordinal))
                {
                    diagnostics.Add(Error($"{FirmamentV2Parser.DfmConceptUnitMismatch}: template '{template.Name}' concept '{concept.Name}' expects length unit '{document.Units}' but found '{concept.Unit ?? "<unitless>"}' in '{concept.RawValue}'."));
                    continue;
                }

                foreach (var (featureName, radius) in EnumerateHoleRadii(document))
                {
                    if (radius < concept.NumericValue)
                    {
                        diagnostics.Add(Error($"{FirmamentV2Parser.DfmMinimumToolRadiusViolation}: template '{template.Name}' concept '{concept.Name}' requires minimum tool radius {concept.NumericValue:0.###}{document.Units}; feature '{featureName}' has radius {radius:0.###}{document.Units}."));
                    }
                }
            }
        }

        foreach (var fill in document.LatticeFills ?? [])
        {
            var host = document.Solids.SingleOrDefault(s => string.Equals(s.Name, fill.Host, StringComparison.Ordinal));
            if (host?.Box is null || host.Box.Size.Count != 3)
            {
                diagnostics.Add(Error("lattice-fill-history-known-host-required: M9 admits one history-known Box host; imported STEP bodies remain deferred."));
                continue;
            }

            var template = (document.Templates ?? []).SingleOrDefault(t => string.Equals(t.Process, "Additive", StringComparison.OrdinalIgnoreCase));
            if (template is null)
            {
                diagnostics.Add(Error("additive-template-required: Lattice Fill requires one Template<Additive> manufacturing context."));
                continue;
            }

            double Concept(string name)
            {
                var concept = template.Concepts.SingleOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (concept is null || !string.Equals(concept.Unit, document.Units, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(Error($"additive-concept-required: template '{template.Name}' requires typed length concept '{name}' in '{document.Units}'."));
                    return double.NaN;
                }
                return concept.NumericValue;
            }

            var context = new AdditiveManufacturingContext(template.Name, template.Process,
                Concept("MinimumWallThickness"), Concept("MinimumStrutDiameter"), Concept("MinimumBondDiameter"), Concept("MinimumHoleDiameter"),
                $"Template<{template.Process}> {template.Name}");
            if (!double.IsFinite(context.MinimumWallThickness) || !double.IsFinite(context.MinimumStrutDiameter) || !double.IsFinite(context.MinimumBondDiameter) || !double.IsFinite(context.MinimumHoleDiameter)) continue;

            var hole = (document.ModifyBlocks ?? []).Where(m => string.Equals(m.TargetSolid, fill.Host, StringComparison.Ordinal)).SelectMany(m => m.SemanticHoles).SingleOrDefault(h => h.EndCondition.Kind == FirmamentV2SemanticHoleEndKind.ThroughAll && h.EntryFace.Axis is "+Z" or "-Z");
            if (hole is null)
            {
                diagnostics.Add(Error("lattice-fill-through-hole-required: M9 history-known proof route requires exactly one Z through-hole."));
                continue;
            }

            var r = fill.Region;
            var bounds = new AxisAlignedBoxExtents(r.Center[0] - r.Size[0] / 2d, r.Center[0] + r.Size[0] / 2d, r.Center[1] - r.Size[1] / 2d, r.Center[1] + r.Size[1] / 2d, r.Center[2] - r.Size[2] / 2d, r.Center[2] + r.Size[2] / 2d);
            var hostBounds = new AxisAlignedBoxExtents(-host.Box.Size[0] / 2d, host.Box.Size[0] / 2d, -host.Box.Size[1] / 2d, host.Box.Size[1] / 2d, -host.Box.Size[2] / 2d, host.Box.Size[2] / 2d);
            var feature = new LatticeFillFeature($"{fill.Host}.{fill.Name}", fill.Host, new LatticeFillRegion(fill.Name, bounds, $"{fill.Name}@{fill.SourceSpan.Start}"), LatticePatternKind.OctetTruss, fill.CellSize, fill.StrutRadius, LatticeBoundaryPolicy.Bond, context, new LatticeFillProvenance("FirmamentV2", fill.Name, $"{fill.SourceSpan.Start}:{fill.SourceSpan.Length}", []));
            foreach (var diagnostic in LatticeFillM9.Validate(feature, hostBounds, hole.ShaftDiameter, new Point3D(hole.Center.U, hole.Center.V, 0d)))
            {
                diagnostics.Add(Error(diagnostic));
            }
        }

        return diagnostics.Count == 0
            ? KernelResult<object>.Success(new object())
            : KernelResult<object>.Failure(diagnostics);
    }

    private static IEnumerable<(string FeatureName, double Radius)> EnumerateHoleRadii(FirmamentV2Document document)
    {
        foreach (var modify in document.ModifyBlocks ?? [])
        foreach (var hole in modify.SemanticHoles)
        {
            yield return ($"{modify.TargetSolid}.{hole.Name}.shaft", hole.ShaftDiameter / 2d);
            if (hole.CounterboreDiameter is { } cb) yield return ($"{modify.TargetSolid}.{hole.Name}.counterbore", cb / 2d);
            if (hole.CountersinkDiameter is { } cs) yield return ($"{modify.TargetSolid}.{hole.Name}.countersink", cs / 2d);
        }
    }

    private static KernelDiagnostic Error(string message) =>
        new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error, message, "FirmamentV2.DfmEnforcement");
}
