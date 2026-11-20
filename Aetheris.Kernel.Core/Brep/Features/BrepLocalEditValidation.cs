using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Brep.Features;

internal static class BrepLocalEditValidation
{
    internal static void RequireQualified(BrepBody body, double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance <= 0 || tolerance > .1)
            throw new ArgumentException("Import tolerance must be positive, finite and at most 0.1 mm.");
        if (!TopologyGraphValidator.Validate(body.Topology).IsSuccess || body.Topology.Bodies.Count() != 1 || body.Topology.Shells.Count() != 1
            || body.Topology.Coedges.GroupBy(c => c.EdgeId).Any(g => g.Count() != 2 || g.First().IsReversed == g.Last().IsReversed))
            throw new ArgumentException("A single closed oriented manifold body is required.");
        var pcurves = BrepPcurveValidator.Validate(body, tolerance, requireEveryCoedge: true, samples: 257);
        if (!pcurves.IsValid || pcurves.MaximumReconstructionDeviation > tolerance || !BrepExportPreflight.Validate(body, tolerance).IsValid)
            throw new ArgumentException("The imported or revised body is not qualified at the requested tolerance; required trim or boundary checks failed.");
    }
}
