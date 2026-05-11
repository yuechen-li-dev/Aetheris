using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class RetainedLoopTrimOracleIntegrator
{
    internal static List<FacePatchCandidate> Attach(CirNode root, List<FacePatchCandidate> candidates, List<string> globalDiagnostics)
    {
        if (root is not CirSubtractNode subtract) return candidates;

        var next = new List<FacePatchCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var sourceSide = candidate.CandidateRole == "base-surface-candidate" ? SubtractOperandSide.Left : SubtractOperandSide.Right;
            var sourceNode = sourceSide == SubtractOperandSide.Left ? subtract.Left : subtract.Right;
            var source = SourceSurfaceExtractor.Extract(sourceNode).Descriptors.FirstOrDefault(d => d.Provenance == candidate.SourceSurface.Provenance);
            if (source is null)
            {
                next.Add(candidate with { Diagnostics = candidate.Diagnostics.Concat(["trim-oracle: source-surface-not-found-for-candidate"]).ToArray() });
                continue;
            }

            var loops = new List<RetainedRegionLoopDescriptor>(candidate.RetainedRegionLoops.Count);
            foreach (var loop in candidate.RetainedRegionLoops)
            {
                var oracle = BuildLoopRepresentation(subtract, source, sourceSide, loop.OppositeSurfaceProvenance, globalDiagnostics);
                loops.Add(loop with { OracleTrimRepresentation = oracle });
            }

            var diag = new List<string>(candidate.Diagnostics);
            foreach (var l in loops.Where(l => l.OracleTrimRepresentation is not null))
            {
                var r = l.OracleTrimRepresentation!;
                diag.Add($"trim-oracle: kind={r.Kind}");
                diag.Add($"trim-oracle: export-capability={r.ExportCapability}");
                if (r.Circle is not null) diag.Add("trim-oracle: analytic-circle-candidate");
                if (r.NumericalContour is not null) diag.Add("trim-oracle: numerical-contour-present");
                diag.Add("trim-oracle: exact-step-export-false");
                diag.Add("trim-oracle: b-rep-topology-not-emitted");
                if (l.OppositeSurfaceFamily == SurfacePatchFamily.Toroidal) diag.Add("trim-oracle: torus-generic-exactness-not-claimed");
            }

            next.Add(candidate with { RetainedRegionLoops = loops, Diagnostics = diag.Distinct().ToArray() });
        }

        return next;
    }

    private static TieredTrimCurveRepresentation? BuildLoopRepresentation(CirSubtractNode root, SourceSurfaceDescriptor source, SubtractOperandSide sourceSide, string oppositeProvenance, List<string> globalDiagnostics)
    {
        var field = SurfaceRestrictedFieldFactory.ForSubtractSource(root, source, sourceSide);
        globalDiagnostics.Add($"trim-oracle: attached-for-opposite-provenance={oppositeProvenance}");

        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(65, 65));
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        var options = new RestrictedContourSnapOptions(0.06d, 0.02d, 8);
        var selection = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        var rep = TieredTrimRepresentationBuilder.Build(selection, stitched, field).Representation;
        return rep;
    }
}
