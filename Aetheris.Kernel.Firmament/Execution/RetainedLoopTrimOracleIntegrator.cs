using Aetheris.Kernel.Core.Cir;

namespace Aetheris.Kernel.Firmament.Execution;

internal static class RetainedLoopTrimOracleIntegrator
{
    internal static List<FacePatchCandidate> Attach(CirNode root, List<FacePatchCandidate> candidates, List<string> globalDiagnostics)
    {
        if (root is not CirSubtractNode subtract) return candidates;

        var leftDescriptors = SourceSurfaceExtractor.Extract(subtract.Left).Descriptors;
        var rightDescriptors = SourceSurfaceExtractor.Extract(subtract.Right).Descriptors;

        var next = new List<FacePatchCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var sourceSide = candidate.CandidateRole == "base-surface-candidate" ? SubtractOperandSide.Left : SubtractOperandSide.Right;
            var sourceSet = sourceSide == SubtractOperandSide.Left ? leftDescriptors : rightDescriptors;
            var oppositeSet = sourceSide == SubtractOperandSide.Left ? rightDescriptors : leftDescriptors;
            var source = sourceSet.FirstOrDefault(d => d.Provenance == candidate.SourceSurface.Provenance);
            if (source is null)
            {
                next.Add(candidate with { Diagnostics = candidate.Diagnostics.Concat(["oracle-trim: source-surface-not-found-for-candidate"]).ToArray() });
                continue;
            }

            var loops = new List<RetainedRegionLoopDescriptor>(candidate.RetainedRegionLoops.Count);
            foreach (var loop in candidate.RetainedRegionLoops)
            {
                var matches = oppositeSet.Where(o => o.Provenance == loop.OppositeSurfaceProvenance && o.Family == loop.OppositeSurfaceFamily).ToArray();
                if (matches.Length == 1)
                {
                    var selected = matches[0];
                    var build = SelectedOppositeFieldBuilder.TryBuild(selected);
                    if (build.Status == SelectedOppositeFieldBuildStatus.Success && build.Node is not null)
                    {
                        var oracle = BuildLoopRepresentation(source, build.Node, "selected-opposite-field-used");
                        loops.Add(loop with { OracleTrimRepresentation = oracle, OracleTrimStrongEvidence = true, OracleTrimRoutingDiagnostic = "oracle-trim: selected-opposite-field-used" });
                    }
                    else
                    {
                        loops.Add(loop with
                        {
                            OracleTrimRepresentation = null,
                            OracleTrimStrongEvidence = false,
                            OracleTrimRoutingDiagnostic = $"oracle-trim: selected-opposite-field-deferred:{build.Diagnostic}"
                        });
                    }

                    continue;
                }

                if (matches.Length > 1)
                {
                    loops.Add(loop with { OracleTrimRepresentation = null, OracleTrimStrongEvidence = false, OracleTrimRoutingDiagnostic = "oracle-trim: multiple-opposite-sources-deferred" });
                    continue;
                }

                var familyMatches = oppositeSet.Where(o => o.Family == loop.OppositeSurfaceFamily).ToArray();
                var routing = familyMatches.Length > 0 ? "oracle-trim: broad-opposite-field-only" : "oracle-trim: missing-opposite-source";
                var oracleBroad = familyMatches.Length > 0 ? BuildLoopRepresentation(source, sourceSide == SubtractOperandSide.Left ? subtract.Right : subtract.Left, "broad-opposite-field-only") : null;
                loops.Add(loop with { OracleTrimRepresentation = oracleBroad, OracleTrimStrongEvidence = false, OracleTrimRoutingDiagnostic = routing });
            }

            var diag = new List<string>(candidate.Diagnostics);
            diag.AddRange(loops.Select(l => l.OracleTrimRoutingDiagnostic));
            foreach (var l in loops.Where(l => l.OracleTrimRepresentation is not null))
            {
                var r = l.OracleTrimRepresentation!;
                diag.Add(l.OracleTrimStrongEvidence ? "oracle-trim: strong-oracle-evidence-attached" : "oracle-trim: diagnostic-only-oracle-evidence-attached");
                if (r.Circle is not null) diag.Add("oracle-trim: analytic-circle-candidate");
                if (r.NumericalContour is not null) diag.Add("oracle-trim: numerical-contour-present");
                diag.Add("oracle-trim: exact-step-export-false");
                diag.Add("oracle-trim: b-rep-topology-not-emitted");
                if (l.OppositeSurfaceFamily == SurfacePatchFamily.Toroidal) diag.Add("oracle-trim: torus-generic-exactness-not-claimed");
            }

            next.Add(candidate with { RetainedRegionLoops = loops, Diagnostics = diag.Distinct().ToArray() });
        }

        globalDiagnostics.Add("oracle-trim: deterministic-opposite-routing-applied");
        return next;
    }

    private static TieredTrimCurveRepresentation BuildLoopRepresentation(SourceSurfaceDescriptor source, CirNode opposite, string routing)
    {
        var field = SurfaceRestrictedFieldFactory.ForSourceAndOpposite(source, opposite, routing);
        var grid = RestrictedFieldGridSampler.Sample(field, new RestrictedFieldGridOptions(65, 65));
        var extraction = RestrictedFieldMarchingSquaresExtractor.Extract(grid, field.Parameterization);
        var stitched = SurfaceTrimContourStitcher.Stitch(extraction);
        var options = new RestrictedContourSnapOptions(0.06d, 0.02d, 8);
        var selection = RestrictedContourSnapSelector.Select(stitched, RestrictedContourSnapAnalyzer.Analyze(stitched, options), options);
        return TieredTrimRepresentationBuilder.Build(selection, stitched, field).Representation;
    }
}
