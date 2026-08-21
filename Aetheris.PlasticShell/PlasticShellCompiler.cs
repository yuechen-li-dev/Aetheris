using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Judgment;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Surfacing;

namespace Aetheris.PlasticShell;

public static class PlasticShellCompiler
{
    public static PlasticShellCompileResult Compile(PlasticShellIr intent, string modelName)
    {
        ArgumentNullException.ThrowIfNull(intent);
        var diagnostics = ValidateIntent(intent).ToList();
        if (diagnostics.Any(x => x.Severity == PlasticDiagnosticSeverity.Error)) return new(false, modelName, intent, null, diagnostics);

        var realized = ThinWalledBodyBRepPlanner.CreateFrustum(intent.Exterior.BottomRadius, intent.Exterior.TopRadius, intent.Exterior.Height, intent.WallPolicy.NominalThickness);
        if (!realized.IsSuccess)
        {
            diagnostics.Add(new(PlasticDiagnosticCodes.WallOffsetCollapse, PlasticDiagnosticSeverity.Error,
                "The exact parallel conical interior collapsed or failed closed-manifold admission for the requested wall.", intent.Exterior.StableId));
            return new(false, modelName, intent, null, diagnostics);
        }

        var thickness = VerifyThickness(intent, realized.Value);
        if (thickness.Violations.Count > 0)
            diagnostics.Add(new(PlasticDiagnosticCodes.WallThicknessViolation, PlasticDiagnosticSeverity.Error, string.Join("; ", thickness.Violations), intent.Exterior.StableId));
        var draft = AnalyzeDraft(intent);
        foreach (var failure in draft.Where(x => !x.Satisfied && x.DraftAngleDegrees > 0d))
            diagnostics.Add(new(intent.Exterior.Protected ? PlasticDiagnosticCodes.DraftConflict : PlasticDiagnosticCodes.DraftInsufficient, PlasticDiagnosticSeverity.Error,
                $"Region '{failure.Region}' realizes {failure.DraftAngleDegrees:G6} degrees; {failure.RequiredDegrees:G6} degrees is required.", failure.Region));
        var pullability = AnalyzePullability(intent, draft);
        if (pullability.Undercuts.Count > 0)
            foreach (var region in pullability.Undercuts) diagnostics.Add(new(PlasticDiagnosticCodes.Undercut, PlasticDiagnosticSeverity.Error, $"Region '{region}' is not directionally accessible from its required pull side.", region));
        var gates = AnalyzeGates(intent, diagnostics);
        var rib = intent.AutoRib is null ? null : JudgeRibs(intent, diagnostics);
        var ejectors = AnalyzeEjectors(intent, rib, diagnostics);
        if (diagnostics.Any(x => x.Severity == PlasticDiagnosticSeverity.Error)) return new(false, modelName, intent, null, diagnostics);

        var materialized = MoldedFeatureMaterializer.Materialize(intent, rib, realized.Value.Body);
        diagnostics.AddRange(materialized.Diagnostics);
        if (!materialized.IsSuccess || diagnostics.Any(x => x.Severity == PlasticDiagnosticSeverity.Error)) return new(false, modelName, intent, null, diagnostics);
        if (rib?.SelectedCandidate is { } materiallySelected)
            rib = rib with
            {
                OriginalSelectedCandidate = rib.OriginalSelectedCandidate ?? materiallySelected,
                MaterializationGates = rib.Candidates.Select(c => c.CandidateId == materiallySelected
                    ? new RibMaterializationGateEvidence(c.CandidateId, "evaluated", true, [])
                    : new RibMaterializationGateEvidence(c.CandidateId, "not-evaluated-after-semantic-loss", null, ["Lower semantic utility; selected candidate passed all materialization gates."])).ToArray()
            };
        var finalBody = materialized.Body ?? realized.Value.Body;
        if (materialized.Evidence is { } materialization)
        {
            if (materialization.Features.Any(f => f.MinimumDraftAngleDegrees + 1e-9 < intent.MinimumDraftAngleDegrees))
                diagnostics.Add(new(PlasticDiagnosticCodes.ConstantSectionFeatureZeroDraft, PlasticDiagnosticSeverity.Warning,
                    $"Constant-section rib and annular-standoff walls are intentionally vertical and realize 0 degrees of release draft, below the requested {intent.MinimumDraftAngleDegrees:G6} degrees. The exact B-rep is pull-direction single-valued but requires an explicit molding-process decision.", intent.AutoRib?.RequestId ?? intent.PlasticShellId));
            draft = [.. draft, .. materialization.Features.Select(f => new PlasticDraftRegionEvidence(
                f.FeatureId, MoldPullSide.CoreSide, f.MinimumDraftAngleDegrees, intent.MinimumDraftAngleDegrees,
                f.MinimumDraftAngleDegrees + 1e-9 >= intent.MinimumDraftAngleDegrees, f.Strength,
                f.Kind == "ConstantThicknessWallRib"
                    ? "Exact planar B-rep: parallel vertical side faces preserve constant shell thickness and the top is a flat pull-normal plane."
                    : "Exact analytic annular B-rep: vertical cylindrical walls and a flat pull-normal top annulus."))];
            pullability = new(
                [.. pullability.CoreAccessible, .. materialization.Features.Select(f => f.FeatureId)],
                pullability.CavityAccessible, pullability.PartingBoundary, pullability.Undercuts,
                "Exact monotone frustum classification plus exact prismatic/cylindrical B-rep additions along +Z; no hidden vertical interval or side action. Zero rib release draft is reported separately.",
                PlasticEvidenceStrength.CertifiedBounded);
        }

        var stateId = BodyStateId.Derive(Canonical(intent, rib?.SelectedCandidate));
        var introduced = new List<string> { "InnerWall", "DraftedOuterWall", "DraftedInnerWall", intent.PartingPlane.StableId };
        introduced.AddRange(intent.Gates.Select(x => $"Gate:{x.GateId}"));
        introduced.AddRange(intent.Standoffs.Select(x => $"Standoff:{x.StandoffId}"));
        introduced.AddRange(intent.Ejectors.Select(x => $"EjectorPin:{x.EjectorId}"));
        if (rib?.SelectedCandidate is { } selected) introduced.Add($"RibNetwork:{selected}");
        if (materialized.Evidence is { } me) introduced.AddRange(me.Junctions.Select(j => j.JunctionId));
        var envelope = new SpatialInfluenceEnvelope(-intent.Exterior.TopRadius, -intent.Exterior.TopRadius, 0, intent.Exterior.TopRadius, intent.Exterior.TopRadius, intent.Exterior.Height);
        var delta = new GeometricDelta(
            BodyStateId.Derive($"{intent.PlasticShellId}|design-authority"), stateId,
            [intent.Exterior.StableId, intent.PartingPlane.StableId], [.. intent.PreservedEntities, intent.PartingPlane.StableId], ["InnerBottom"], [], introduced,
            [intent.Exterior.StableId, "GeneratedInterior", "GeneratedManufacturingFeatures"], envelope,
            [new(intent.Exterior.StableId, GeometricChangeKind.Preserved, [intent.Exterior.StableId], "Exterior analytic support parameters are retained exactly."),
             new("InnerBottom", GeometricChangeKind.Replaced, materialized.Evidence?.Features.Select(f => f.FeatureId).ToArray() ?? ["InnerBottom"], "Only exact feature footprints are removed from the planar cavity floor and replaced by shared-edge molded-feature faces."),
             new("<molded-additions>", GeometricChangeKind.Introduced, introduced, "Standoffs and the selected AutoRib network are grafted into the single product boundary; tooling contacts remain semantic.")]);
        var classification = new Dictionary<string, MoldPullSide>(StringComparer.Ordinal)
        {
            ["OuterConicalWall"] = MoldPullSide.CavitySide,
            ["InnerConicalWall"] = MoldPullSide.CoreSide,
            ["TopAnnularRim"] = MoldPullSide.PartingBoundary,
            ["OuterBottom"] = MoldPullSide.CavitySide,
            ["InnerBottom"] = MoldPullSide.CoreSide
        };
        if (materialized.Evidence is { } molded)
            foreach (var feature in molded.Features) classification[feature.FeatureId] = MoldPullSide.CoreSide;
        var evidence = new PlasticShellEvidence(thickness, draft, pullability, gates, ejectors, rib, materialized.Evidence, classification,
            $"Plane '{intent.PartingPlane.StableId}' at ({intent.PartingPlane.Origin.X:R},{intent.PartingPlane.Origin.Y:R},{intent.PartingPlane.Origin.Z:R}); normal parallel to tooling direction.",
            $"One BRep body, {finalBody.Topology.Shells.Count()} closed shell, {finalBody.Topology.Faces.Count()} faces; topology-authoritative molded additions and exact analytic exterior.");
        return new(true, modelName, intent, new(stateId, intent.PlasticShellId, finalBody, intent, delta, evidence), diagnostics);
    }

    private static IEnumerable<PlasticDiagnostic> ValidateIntent(PlasticShellIr x)
    {
        static bool P(double value) => double.IsFinite(value) && value > 0;
        if (!P(x.Exterior.BottomRadius) || !P(x.Exterior.TopRadius) || !P(x.Exterior.Height)) yield return new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "Exterior radii and height must be finite and positive.", x.Exterior.StableId);
        if (Math.Abs(x.Exterior.TopRadius - x.Exterior.BottomRadius) <= 1e-9) yield return new(PlasticDiagnosticCodes.DraftConflict, PlasticDiagnosticSeverity.Error, "The bounded X0 frustum exterior must carry non-zero draft; top and bottom radii cannot be equal.", x.Exterior.StableId);
        var w = x.WallPolicy;
        if (!P(w.NominalThickness) || !P(w.MinimumThickness) || !P(w.MaximumThickness) || w.MinimumThickness > w.NominalThickness || w.NominalThickness > w.MaximumThickness || w.ThicknessTolerance < 0)
            yield return new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "WallPolicy requires 0 < minimum <= nominal <= maximum and a non-negative tolerance.", x.PlasticShellId);
        if (x.Exterior.BottomRadius <= w.NominalThickness || x.Exterior.TopRadius <= w.NominalThickness || x.Exterior.Height <= w.NominalThickness)
            yield return new(PlasticDiagnosticCodes.WallOffsetCollapse, PlasticDiagnosticSeverity.Error, $"Requested {w.NominalThickness:G6} mm wall leaves no admissible bounded interior.", x.Exterior.StableId);
        var alignment = Math.Abs(x.PartingPlane.Normal.ToVector().Dot(x.ToolingDirection.ToVector()));
        if (alignment < 1d - 1e-9 || Math.Abs(x.PartingPlane.Origin.Z - x.Exterior.Height) > 1e-6)
            yield return new(PlasticDiagnosticCodes.InvalidParting, PlasticDiagnosticSeverity.Error, "X0 requires a parting plane normal parallel to ToolingDirection and located at the enclosure rim.", x.PartingPlane.StableId);
        if (Math.Abs(x.ToolingDirection.X) > 1e-9 || Math.Abs(x.ToolingDirection.Y) > 1e-9 || x.ToolingDirection.Z < 1d - 1e-9)
            yield return new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "The bounded X0 realization currently admits +Z ToolingDirection only.", x.PlasticShellId);
        if (!double.IsFinite(x.MinimumDraftAngleDegrees) || x.MinimumDraftAngleDegrees < 0 || x.MinimumDraftAngleDegrees >= 45)
            yield return new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "MinimumDraftAngle must be in [0,45) degrees.", x.PlasticShellId);
        foreach (var s in x.Standoffs)
        {
            if (!P(s.Height) || !P(s.OuterDiameter) || s.HoleDiameter is <= 0 || s.HoleDiameter >= s.OuterDiameter)
                yield return new(PlasticDiagnosticCodes.SourceInvalid, PlasticDiagnosticSeverity.Error, "Standoff dimensions are invalid.", s.StandoffId);
            var localRatio = (s.OuterDiameter - (s.HoleDiameter ?? 0d)) / (2d * x.WallPolicy.NominalThickness);
            if (localRatio > 1.5d) yield return new(PlasticDiagnosticCodes.StandoffThickSection, PlasticDiagnosticSeverity.Warning, $"Bounded material-accumulation proxy is {localRatio:G4} wall-thickness units; this is not sink prediction.", s.StandoffId);
            var radial = Math.Sqrt(s.Position.X * s.Position.X + s.Position.Y * s.Position.Y);
            var k = (x.Exterior.TopRadius - x.Exterior.BottomRadius) / x.Exterior.Height;
            var innerBottom = x.Exterior.BottomRadius + k * x.WallPolicy.NominalThickness - x.WallPolicy.NominalThickness * Math.Sqrt(1d + k * k);
            if (Math.Abs(s.Position.Z - x.WallPolicy.NominalThickness) > 1e-6 || radial + s.OuterDiameter / 2d >= innerBottom || s.Position.Z + s.Height >= x.Exterior.Height)
                yield return new(PlasticDiagnosticCodes.MaterializedFeatureOutsideAuthorizedRegion, PlasticDiagnosticSeverity.Error, "Standoff analytic envelope must remain on the core floor, inside the cavity, and below the parting plane.", s.StandoffId);
        }
    }

    private static PlasticThicknessEvidence VerifyThickness(PlasticShellIr x, ThinWalledBodyRealization realized)
    {
        var samples = realized.Construction.ThicknessWitnesses.Select((w, i) => new ThicknessSample(w.Role,
            i == 0 ? new Point3D(x.Exterior.BottomRadius, 0, 0) : new Point3D(0, 0, x.WallPolicy.NominalThickness), w.Distance, PlasticEvidenceStrength.ExactAnalytic)).ToArray();
        var min = samples.MinBy(s => s.Thickness)!; var max = samples.MaxBy(s => s.Thickness)!;
        var violations = samples.Where(s => s.Thickness < x.WallPolicy.MinimumThickness - x.WallPolicy.ThicknessTolerance || s.Thickness > x.WallPolicy.MaximumThickness + x.WallPolicy.ThicknessTolerance)
            .Select(s => $"{s.Region} measured {s.Thickness:G6} mm outside [{x.WallPolicy.MinimumThickness:G6},{x.WallPolicy.MaximumThickness:G6}] mm").ToArray();
        return new(x.WallPolicy.NominalThickness, min.Thickness, max.Thickness, samples.Average(s => s.Thickness), min.Location, max.Location, samples, violations,
            "Exact distance between parallel conical supports plus exact parallel-plane bottom supports; bounded to the admitted analytic frustum topology.", PlasticEvidenceStrength.ExactAnalytic);
    }

    private static IReadOnlyList<PlasticDraftRegionEvidence> AnalyzeDraft(PlasticShellIr x)
    {
        var angle = Math.Atan((x.Exterior.TopRadius - x.Exterior.BottomRadius) / x.Exterior.Height) * 180d / Math.PI;
        var pass = angle + 1e-10 >= x.MinimumDraftAngleDegrees;
        return
        [
            new("OuterConicalWall", MoldPullSide.CavitySide, angle, x.MinimumDraftAngleDegrees, pass, PlasticEvidenceStrength.ExactAnalytic, "Exact cone semi-angle relative to ToolingDirection."),
            new("InnerConicalWall", MoldPullSide.CoreSide, angle, x.MinimumDraftAngleDegrees, pass, PlasticEvidenceStrength.ExactAnalytic, "Parallel exact conical offset preserves semi-angle.")
        ];
    }

    private static PlasticPullabilityEvidence AnalyzePullability(PlasticShellIr x, IReadOnlyList<PlasticDraftRegionEvidence> draft)
    {
        var undercuts = draft.Where(d => d.DraftAngleDegrees <= 0d).Select(d => d.Region).ToArray();
        return new(["InnerConicalWall", "InnerBottom"], ["OuterConicalWall", "OuterBottom"], ["TopAnnularRim"], undercuts,
            "Exact monotone generatrix classification for the bounded coaxial frustum along ToolingDirection; no general arbitrary-BRep visibility claim.", PlasticEvidenceStrength.CertifiedBounded);
    }

    private static IReadOnlyList<PlasticGateEvidence> AnalyzeGates(PlasticShellIr x, ICollection<PlasticDiagnostic> diagnostics)
    {
        var result = new List<PlasticGateEvidence>();
        foreach (var gate in x.Gates)
        {
            var radial = Math.Sqrt(gate.Location.X * gate.Location.X + gate.Location.Y * gate.Location.Y);
            var onRim = Math.Abs(gate.Location.Z - x.Exterior.Height) <= 1e-4 && radial <= x.Exterior.TopRadius + 1e-4 && radial >= x.Exterior.TopRadius - x.WallPolicy.NominalThickness * 2d;
            if (!onRim) { diagnostics.Add(new(PlasticDiagnosticCodes.InvalidGate, PlasticDiagnosticSeverity.Error, "Gate must contact the admitted top annular rim in X0.", gate.GateId)); continue; }
            if (x.PreservedEntities.Contains(gate.TargetRegion, StringComparer.Ordinal)) { diagnostics.Add(new(PlasticDiagnosticCodes.GateInaccessible, PlasticDiagnosticSeverity.Error, "Gate targets a protected forbidden region.", gate.GateId)); continue; }
            var far = radial + x.Exterior.TopRadius - x.WallPolicy.NominalThickness;
            result.Add(new(gate.GateId, gate.Location, "TopAnnularRim", far, far * 0.62d, "Straight-line/geodesic-lower-bound geometric distance proxy; no rheology, pressure, or fill-time computation."));
        }
        return result;
    }

    private static IReadOnlyList<PlasticEjectorEvidence> AnalyzeEjectors(PlasticShellIr x, AutoRibJudgmentEvidence? ribJudgment, ICollection<PlasticDiagnostic> diagnostics)
    {
        var result = new List<PlasticEjectorEvidence>(); var inner = x.Exterior.BottomRadius - x.WallPolicy.NominalThickness;
        var selectedRibs = ribJudgment?.Candidates.SingleOrDefault(c => c.CandidateId == ribJudgment.SelectedCandidate)?.Edges ?? [];
        foreach (var ejector in x.Ejectors)
        {
            var radial = Math.Sqrt(ejector.Position.X * ejector.Position.X + ejector.Position.Y * ejector.Position.Y);
            var accessible = Math.Abs(ejector.Position.Z - x.WallPolicy.NominalThickness) <= 1e-4 && radial + ejector.Diameter / 2d < inner;
            var standoffCollision = x.Standoffs.Any(s => Distance2(ejector.Position, s.Position) < (ejector.Diameter + s.OuterDiameter) / 2d);
            var collidingRibs = selectedRibs.Where(edge =>
                {
                    var a = x.Standoffs.Single(s => s.StandoffId == edge.From).Position;
                    var b = x.Standoffs.Single(s => s.StandoffId == edge.To).Position;
                    var ribHalfWidth = x.WallPolicy.NominalThickness / 2d;
                    return DistanceToSegment(ejector.Position, a, b) < ejector.Diameter / 2d + ribHalfWidth;
                }).ToArray();
            var ribCollision = collidingRibs.Length > 0;
            var collision = standoffCollision || ribCollision;
            var cosmetic = x.PreservedEntities.Contains(ejector.TargetRegion, StringComparer.Ordinal);
            if (!accessible) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorNotCoreAccessible, PlasticDiagnosticSeverity.Error, "Ejector contact is not on the accessible inner core floor.", ejector.EjectorId));
            if (standoffCollision) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorCollidesFeature, PlasticDiagnosticSeverity.Error, "Ejector contact overlaps a standoff envelope.", ejector.EjectorId));
            if (ribCollision) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorRibCollision, PlasticDiagnosticSeverity.Error,
                $"Ejector contact overlaps selected molded rib(s) {string.Join(", ", collidingRibs.Select(e => $"{e.From}->{e.To}"))}; X0a does not relocate ejectors, and no alternate semantic candidate is selected after this post-selection conflict.", ejector.EjectorId));
            if (cosmetic) diagnostics.Add(new(PlasticDiagnosticCodes.EjectorCosmeticRegion, PlasticDiagnosticSeverity.Error, "Ejector contact targets a protected cosmetic region.", ejector.EjectorId));
            result.Add(new(ejector.EjectorId, accessible, !collision, !cosmetic, "Post-selection analytic radial containment and planar clearance against standoff plus selected-rib base envelopes."));
        }
        return result;
    }

    private static AutoRibJudgmentEvidence JudgeRibs(PlasticShellIr x, ICollection<PlasticDiagnostic> diagnostics)
    {
        var request = x.AutoRib!; var supports = request.Supports.Select(id => x.Standoffs.SingleOrDefault(s => s.StandoffId == id)).ToArray();
        if (!x.Gates.Any(g => g.GateId == request.GateId))
        {
            diagnostics.Add(new(PlasticDiagnosticCodes.AutoRibNoEligibleNetwork, PlasticDiagnosticSeverity.Error, $"AutoRib gate '{request.GateId}' is unresolved.", request.RequestId));
            return new(request.RequestId, [], null, ["Unresolved gate."], "No judgment performed.");
        }
        if (supports.Any(s => s is null) || supports.Length < 2)
        {
            diagnostics.Add(new(PlasticDiagnosticCodes.AutoRibNoEligibleNetwork, PlasticDiagnosticSeverity.Error, "AutoRib requires at least two resolved Standoff supports.", request.RequestId));
            return new(request.RequestId, [], null, ["Unresolved or insufficient supports."], "No judgment performed.");
        }
        var s = supports.Select(v => v!).OrderBy(v => Math.Atan2(v.Position.Y, v.Position.X)).ThenBy(v => v.StandoffId, StringComparer.Ordinal).ToArray();
        var gate = x.Gates.Single(g => g.GateId == request.GateId);
        var fanRoot = s.OrderBy(v => Distance2(v.Position, gate.Location)).ThenBy(v => v.StandoffId, StringComparer.Ordinal).First();
        var ring = Enumerable.Range(0, s.Length).Select(i => Edge(s[i], s[(i + 1) % s.Length], "perimeter")).ToArray();
        var chord = s.Where(v => v.StandoffId != fanRoot.StandoffId).Select(v => Edge(fanRoot, v, "fan")).ToArray();
        var candidates = new[] { Candidate("perimeter-network", ring), Candidate("gate-oriented-fan", chord) };
        var engineCandidates = candidates.Select((candidate, index) => new JudgmentCandidate<IReadOnlyDictionary<string, RibCandidateEvidence>>(
            candidate.CandidateId, context => context[candidate.CandidateId].Eligible, context => context[candidate.CandidateId].Metrics.Utility,
            context => string.Join("; ", context[candidate.CandidateId].RejectionReasons), index)).ToArray();
        var map = candidates.ToDictionary(c => c.CandidateId, StringComparer.Ordinal);
        var judged = new JudgmentEngine<IReadOnlyDictionary<string, RibCandidateEvidence>>().Evaluate(map, engineCandidates);
        if (!judged.IsSuccess) diagnostics.Add(new(PlasticDiagnosticCodes.AutoRibNoEligibleNetwork, PlasticDiagnosticSeverity.Error, "No AutoRib candidate passed manufacturing eligibility.", request.RequestId));
        var selectedName = judged.Selection?.Candidate.Name;
        return new(request.RequestId, candidates, selectedName, judged.Rejections.Select(r => $"{r.CandidateName}: {r.Reason}").ToArray(),
            "Eligibility precedes normalized utility; deterministic tie-break is priority, ordinal candidate id, then declaration order.", selectedName);

        RibEdge Edge(PlasticStandoff a, PlasticStandoff b, string kind) => new(a.StandoffId, b.StandoffId, Distance2(a.Position, b.Position), kind);
        RibCandidateEvidence Candidate(string id, IReadOnlyList<RibEdge> edges)
        {
            var rejects = new List<string>(); var p = request.Policy;
            if (Math.Abs(p.ThicknessRatio - 1d) > 1e-9) rejects.Add(PlasticDiagnosticCodes.RibThicknessViolation);
            if (p.MinimumHeight <= 0 || p.MaximumHeight < p.MinimumHeight || p.DraftAngleDegrees < x.MinimumDraftAngleDegrees) rejects.Add(PlasticDiagnosticCodes.RibToolingConflict);
            if (edges.Any(edge => edge.Length < p.MinimumSpacing)) rejects.Add($"minimum spacing {p.MinimumSpacing:R} mm is violated");
            if (request.KeepOuts.Contains(id, StringComparer.Ordinal)) rejects.Add("keepout intersects candidate network");
            var length = edges.Sum(e => e.Length); var complexity = Math.Min(1d, edges.Count / (double)(s.Length * 2)); var lengthScore = 1d - Math.Min(1d, length / (x.Exterior.TopRadius * 8d));
            var adjacency = s.ToDictionary(v => v.StandoffId, _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var edge in edges) { adjacency[edge.From].Add(edge.To); adjacency[edge.To].Add(edge.From); }
            var reached = new HashSet<string>(StringComparer.Ordinal) { s[0].StandoffId }; var pending = new Queue<string>(reached);
            while (pending.Count > 0) foreach (var next in adjacency[pending.Dequeue()]) if (reached.Add(next)) pending.Enqueue(next);
            var connectivity = reached.Count / (double)s.Length; var redundancy = Math.Min(1d, edges.Count / (double)s.Length); var support = Utility.Weighted((connectivity, .7), (redundancy, .3));
            var gateTravel = edges.Average(edge =>
            {
                var a = s.Single(v => v.StandoffId == edge.From).Position; var b = s.Single(v => v.StandoffId == edge.To).Position;
                return Distance2(new((a.X + b.X) / 2d, (a.Y + b.Y) / 2d, 0d), gate.Location);
            });
            var flow = 1d - Math.Min(1d, gateTravel / (2d * x.Exterior.TopRadius));
            var maximumConvergence = adjacency.Max(pair => pair.Value.Count) / (double)s.Length;
            var sink = Utility.Clamp01(1d - .35d * p.ThicknessRatio - .25d * maximumConvergence);
            var utility = Utility.Weighted((support, .30), (flow, .25), (sink, .15), (lengthScore, .20), (1d - complexity, .10));
            return new(id, rejects.Count == 0, new(support, flow, sink, length, complexity, utility), edges, rejects);
        }
    }

    private static double Distance2(Point3D a, Point3D b) { var dx = a.X - b.X; var dy = a.Y - b.Y; return Math.Sqrt(dx * dx + dy * dy); }
    private static double DistanceToSegment(Point3D p, Point3D a, Point3D b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var l2 = dx * dx + dy * dy;
        var t = l2 <= 1e-12 ? 0d : double.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0d, 1d);
        return Math.Sqrt(Math.Pow(p.X - (a.X + t * dx), 2d) + Math.Pow(p.Y - (a.Y + t * dy), 2d));
    }
    private static string Canonical(PlasticShellIr x, string? rib) => $"{x.PlasticShellId}|{x.Exterior.BottomRadius:R}|{x.Exterior.TopRadius:R}|{x.Exterior.Height:R}|{x.WallPolicy.NominalThickness:R}|{x.MinimumDraftAngleDegrees:R}|{rib}|{string.Join(',', x.Gates.Select(g => g.GateId))}|{string.Join(',', x.Standoffs.Select(s => s.StandoffId))}";
}

public sealed record PlasticStepSurfaceInventory(int Planes, int Cylinders, int Cones, int Spheres, int Tori, int NonRationalBSplines, int RationalProductSurfaces);
public sealed record PlasticStepExportResult(bool IsSuccess, string? Step, PlasticStepSurfaceInventory Inventory, IReadOnlyList<PlasticDiagnostic> Diagnostics);

public static class PlasticShellStepExporter
{
    public static PlasticStepExportResult Export(PlasticShellBodyState state, string productName)
    {
        var pmi = new List<Step242SemanticPmi>();
        var materialized = state.Evidence.Materialization?.Features.ToDictionary(f => f.FeatureId, StringComparer.Ordinal)
            ?? new Dictionary<string, MoldedFeatureEvidence>(StringComparer.Ordinal);
        pmi.Add(new Step242SemanticPmiNote($"plastic-shell:{state.Intent.PlasticShellId}", state.Intent.Exterior.StableId, $"First-class molded plastic product definition; wall={state.Intent.WallPolicy.NominalThickness:R} mm, tooling=({state.Intent.ToolingDirection.X:R},{state.Intent.ToolingDirection.Y:R},{state.Intent.ToolingDirection.Z:R}), parting={state.Intent.PartingPlane.StableId}, draft={state.Intent.MinimumDraftAngleDegrees:R} deg; gate, ejection, and reinforcement intent retained."));
        pmi.AddRange(state.Intent.Gates.Select(g => new Step242SemanticPmiNote($"gate:{g.GateId}", g.TargetRegion, $"{g.Kind} gate at ({g.Location.X:R},{g.Location.Y:R},{g.Location.Z:R}); geometric flow proxy only.")));
        pmi.AddRange(state.Intent.Standoffs.Select(s => (Step242SemanticPmi)new Step242SemanticPmiNote($"standoff:{s.StandoffId}", $"Standoff:{s.StandoffId}", $"{s.Intent} support at ({s.Position.X:R},{s.Position.Y:R},{s.Position.Z:R}); exact analytic annular feature with retained core hole.")
        {
            GeometricFaceIds = materialized.GetValueOrDefault($"Standoff:{s.StandoffId}")?.FaceIds ?? []
        }));
        pmi.AddRange(state.Intent.Ejectors.Select(e => new Step242SemanticPmiNote($"ejector-pin:{e.EjectorId}", e.TargetRegion, $"Tooling contact diameter {e.Diameter:R} mm; not a product hole.")));
        if (state.Evidence.RibNetwork?.SelectedCandidate is { } rib) pmi.Add(new Step242SemanticPmiNote($"autorib:{state.Intent.AutoRib!.RequestId}", $"AutoRib:{state.Intent.AutoRib.RequestId}", $"Selected network {rib}; physically grafted as constant-shell-thickness B-rep walls with flat tops and explicitly reported zero release draft; see judgment evidence sidecar.")
        {
            GeometricFaceIds = materialized.Where(p => p.Key.StartsWith("Rib:", StringComparison.Ordinal)).SelectMany(p => p.Value.FaceIds).Distinct().Order().ToArray()
        });
        var export = Step242Exporter.ExportBody(state.Body, pmi, new Step242ExportOptions { BrepExportPreflightMode = BrepExportPreflightMode.Enforce, ProductName = productName });
        if (!export.IsSuccess) return new(false, null, Empty(), export.Diagnostics.Select(d => new PlasticDiagnostic("plastic-shell-step-export-failed", PlasticDiagnosticSeverity.Error, d.Message)).ToArray());
        var step = export.Value; var inv = new PlasticStepSurfaceInventory(Count(step, "=PLANE("), Count(step, "=CYLINDRICAL_SURFACE("), Count(step, "=CONICAL_SURFACE("), Count(step, "=SPHERICAL_SURFACE("), Count(step, "=TOROIDAL_SURFACE("), Count(step, "=B_SPLINE_SURFACE_WITH_KNOTS("), Count(step, "RATIONAL_B_SPLINE_SURFACE"));
        if (inv.RationalProductSurfaces != 0) return new(false, null, inv, [new("plastic-shell-surface-export-normalization-failed", PlasticDiagnosticSeverity.Error, "Rational product surface emission is prohibited.")]);
        return new(true, step, inv, []);
    }
    private static int Count(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
    private static PlasticStepSurfaceInventory Empty() => new(0, 0, 0, 0, 0, 0, 0);
}
