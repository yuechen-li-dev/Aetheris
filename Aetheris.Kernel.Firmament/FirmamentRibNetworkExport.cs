using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static partial class FirmamentBuildAndExport
{
    private static KernelResult<FirmamentStepExportResult> ExportBoxPathNetwork(FirmamentV2Document document)
    {
        var stock = document.Solid; var size = stock.Box!.Size; var height = size[2];
        KernelResult<FirmamentStepExportResult> Fail(string text) => CombinedFailure("rib-network-rejected: " + text, sourceSpan: stock.SourceSpan);
        var holes = (document.ModifyBlocks ?? []).SelectMany(m => m.SemanticHoles).ToArray();
        var finishes = (document.ModifyBlocks ?? []).SelectMany(m => m.EdgeFinishes ?? []).ToArray();
        if (finishes.Length > 1 || finishes.Any(f => f.FaceAxis != "+Z" || f.Target != "Boundary" || f.Kind is not ("Fillet" or "Chamfer")))
            return Fail("Mixed ribs currently admit one +Z perimeter Fillet/Chamfer, not arbitrary-edge finishes.");
        if (holes.Any(h => h.EntryFace.Axis != "+Z" || h.EndCondition.Kind != FirmamentV2SemanticHoleEndKind.ThroughAll))
            return Fail("Mixed ribs currently admit top-entry ThroughAll shaft, counterbore and countersink holes.");
        var mixed = holes.Length > 0 || finishes.Length > 0;
        var plan = RibNetworkPlanner.Plan(size[0], size[1], height, document.PathFeatures);
        if (!plan.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(plan.Diagnostics.Select(d => d with { SourceSpan = stock.SourceSpan is { } s ? new(s.Start, s.Length) : null }).ToArray());
        var profiles = document.PathFeatures.ToDictionary(f => f.Feature.Name, f => RibNetworkPlanner.Profile(f.Feature, 0), StringComparer.Ordinal);
        var margin = finishes.SingleOrDefault()?.Distance ?? 0;
        foreach (var f in document.PathFeatures)
        {
            var bounds = RibNetworkPlanner.Bounds(profiles[f.Feature.Name]);
            if (bounds.MinX <= -size[0] / 2 + margin + 1e-7 || bounds.MaxX >= size[0] / 2 - margin - 1e-7 || bounds.MinY <= -size[1] / 2 + margin + 1e-7 || bounds.MaxY >= size[1] / 2 - margin - 1e-7)
                return Fail(f.Feature.Name + ": footprint enters the perimeter finish or leaves the support.");
            foreach (var hole in holes)
                if (!Disjoint(profiles[f.Feature.Name], HoleProfile(hole)))
                    return Fail(f.Feature.Name + ": intersects Hole '" + hole.Name + "'. A hole through a rib needs an explicit higher support; it is not silently drilled from the stock datum.");
        }
        foreach (var groove in document.PathFeatures.Where(f => !f.Feature.Add))
            foreach (var other in document.PathFeatures.Where(f => f.Feature.Name != groove.Feature.Name))
                if (!Disjoint(profiles[groove.Feature.Name], profiles[other.Feature.Name]))
                    return Fail(groove.Feature.Name + ": groove/rib or groove/groove intersections require a separate local removal plan.");
        var baseDocument = document with { PathFeatures = [], ModifyBlocks = mixed ? [new(stock.Name, [], holes, finishes)] : [] };
        KernelResult<FirmamentStepExportResult>? builtStock = null;
        if (holes.Length > 0 && finishes.Length > 0) builtStock = TryExportV2CombinedHoleEdgeFinishBody(baseDocument);
        else if (holes.Length > 0) builtStock = ExportV2BoxThroughHoleStock(baseDocument, allowUnfinished: true);
        else if (finishes.Length > 0) builtStock = TryExportV2AirChamferBody(baseDocument);
        if (builtStock is { IsSuccess: false }) return builtStock;
        BrepBody host; SemanticTopologyCorrespondence? stockCorrespondence = null;
        if (builtStock?.Value.RuntimeBody is { } existing) { host = existing; stockCorrespondence = builtStock.Value.RuntimeCorrespondence; }
        else if (mixed) return Fail("The existing stock feature owner did not produce an exact body for this combination.");
        else
        {
            var plain = BrepPrimitives.CreateBoxWithTopology(size[0], size[1], height, out var boxTopology);
            if (!plain.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(plain.Diagnostics);
            host = FirmamentPrimitiveExecutionTranslation.TranslateBody(plain.Value, new(0, 0, height / 2));
            stockCorrespondence = BoxConstructionCorrespondence.Create(stock.Name, boxTopology!, stock.SourceSpan);
        }
        var descendants = (stockCorrespondence?.Descendants ?? []).ToList();
        if (plan.Value.Isolated is { } isolated)
        {
            PathFeatureBody body;
            if (!mixed) body = isolated;
            else
            {
                var centered = FirmamentPrimitiveExecutionTranslation.TranslateBody(host, new(0, 0, -height / 2));
                var applied = BrepPlanarPathFeature.BuildOnBox(centered, size[0], size[1], height, document.PathFeatures.Select(f => f.Feature).ToArray());
                if (!applied.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(applied.Diagnostics);
                body = applied.Value;
            }
            host = FirmamentPrimitiveExecutionTranslation.TranslateBody(body.Body, new(0, 0, height / 2));
            foreach (var f in document.PathFeatures)
                descendants.AddRange(body.Faces[f.Feature.Name].Select((face, i) => Descendant(stock.Name, f.Feature.Name, face, i)));
        }
        else
        {
            var grooves = document.PathFeatures.Where(f => !f.Feature.Add).ToArray();
            if (grooves.Length > 0)
            {
                var centered = FirmamentPrimitiveExecutionTranslation.TranslateBody(host, new(0, 0, -height / 2));
                var applied = BrepPlanarPathFeature.BuildOnBox(centered, size[0], size[1], height, grooves.Select(f => f.Feature).ToArray());
                if (!applied.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(applied.Diagnostics);
                host = FirmamentPrimitiveExecutionTranslation.TranslateBody(applied.Value.Body, new(0, 0, height / 2));
                foreach (var f in grooves) descendants.AddRange(applied.Value.Faces[f.Feature.Name].Select((face, i) => Descendant(stock.Name, f.Feature.Name, face, i)));
            }
            var support = TopSupport(host, height);
            var addition = FirmamentPrimitiveExecutionTranslation.TranslateBody(plan.Value.Addition!, new(0, 0, height));
            var attached = BrepPlanarAdditiveAttachment.Attach(host, addition, height, support);
            if (!attached.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(attached.Diagnostics);
            host = attached.Value.Body;
            foreach (var f in document.PathFeatures.Where(f => f.Feature.Add))
            {
                var faces = plan.Value.FaceOwners.Where(pair => pair.Value == f.Feature.Name && attached.Value.AddedFaceMap.ContainsKey(pair.Key))
                    .Select(pair => attached.Value.AddedFaceMap[pair.Key]).Distinct().OrderBy(id => id.Value).ToArray();
                if (faces.Length == 0) return Fail(f.Feature.Name + ": rib is redundant or completely buried; no independent boundary survives. Remove it or adjust its guide/section.");
                descendants.AddRange(faces.Select((face, i) => Descendant(stock.Name, f.Feature.Name, face, i)));
            }
        }
        var supportPmi = ValidateV2PmiExportSupport(document); if (!supportPmi.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(supportPmi.Diagnostics);
        var spans = new Dictionary<string, FirmamentV2SourceSpan>(stockCorrespondence?.SourceSpans ?? new Dictionary<string, FirmamentV2SourceSpan>());
        foreach (var f in document.PathFeatures) spans[f.Host + "." + f.Feature.Name] = f.SourceSpan;
        var correspondence = new SemanticTopologyCorrespondence(stock.Name, descendants, ["UnifiedAst", "JudgmentEngine", plan.Value.Strategy, "ExactPlanarInterface"], spans);
        var pmi = BuildV2SemanticPmi(document, [], stock.Name, correspondence: correspondence, body: host);
        var exported = Step242Exporter.ExportBody(host, pmi, new Step242ExportOptions { BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        if (!exported.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(exported.Diagnostics);
        return KernelResult<FirmamentStepExportResult>.Success(new(exported.Value, document.ModelName, 0, "path-feature", "AnalyticPathFeature")
        {
            RuntimeBody = host,
            RuntimeBodySourceName = stock.Name,
            RuntimeCorrespondence = correspondence,
            RibPlanning = plan.Value.Report,
            MaterializedFeatures = (builtStock?.Value.MaterializedFeatures ?? []).Concat(document.PathFeatures.Select(f => new FirmamentFeatureReceipt(f.Host, f.Feature.Add ? "Rib" : "Groove", f.Feature.Name))).ToArray(),
            Features = builtStock?.Value.Features,
            EngineeringFeatures = document.PathFeatures.Select(f => new FirmamentEngineeringFeatureReport(f.Feature.Name, f.Feature.Add ? "Rib" : "Groove", f.Host + "." + f.Feature.Name, f.Host, f.Support, f.GuideReference,
                f.Feature.Extent, f.Feature.Add ? "Height" : "Depth", f.Feature.Add ? "Add" : "Remove", PolicySource: "ManualConceptGuide", MaterializationRoute: plan.Value.Strategy, ToolFamily: f.Feature.Section.ToString())).ToArray()
        }, exported.Diagnostics);
    }
    private static SemanticTopologyDescendant Descendant(string host, string name, FaceId face, int ordinal) => new(host + "." + name + ".Face" + ordinal, "Face", SemanticTopologyRole.PathFeatureFace, host + "." + name,
        Face: face, ParentStableId: host, Addressability: SemanticTopologyAddressability.DerivedStable);
    private static FaceId TopSupport(BrepBody body, double height) => body.Topology.Faces.Where(f => body.Geometry.GetSurface(body.Bindings.GetFaceBinding(f.Id).SurfaceGeometryId).Plane is { } p
        && p.Normal.Z > .999 && Math.Abs(p.Origin.Z - height) < 1e-7).OrderByDescending(f => f.LoopIds.SelectMany(l => body.Topology.GetLoop(l).CoedgeIds).Count()).First().Id;
    private static ResolvedProfile2D HoleProfile(FirmamentV2SemanticHoleDecl hole)
    {
        var radius = (hole.CounterboreDiameter ?? hole.CountersinkDiameter ?? hole.ShaftDiameter) / 2;
        return new(hole.Name, "XY", [new("Outer",true,Enumerable.Range(0,4).Select(i=>new ResolvedProfileSegment2D("q"+i,
            new LineArcCircularArc2D((hole.Center.U,hole.Center.V),radius,i*Math.PI/2,Math.PI/2),new("hole:"+hole.Name,hole.Name,"source","HoleClearance","XY"))).ToArray())]);
    }
    private static bool Disjoint(ResolvedProfile2D a, ResolvedProfile2D b)
    {
        foreach (var first in a.Loops.SelectMany(l => l.Segments)) foreach (var second in b.Loops.SelectMany(l => l.Segments))
        { var hits = ProfileArrangementBuilder.IntersectBounded(first.Geometry, second.Geometry); if (hits.Intersections.Count > 0 || hits.HasBoundedOverlap) return false; }
        static (double X, double Y) Point(LineArcProfileCurve2D c) => c switch { LineArcLineSegment2D l => l.Start, LineArcCircularArc2D arc => (arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians)), _ => throw new NotSupportedException() };
        return ProfileArrangementBuilder.PointInProfile(a, Point(b.Loops[0].Segments[0].Geometry)) == ArrangementPointLocation.Outside
            && ProfileArrangementBuilder.PointInProfile(b, Point(a.Loops[0].Segments[0].Geometry)) == ArrangementPointLocation.Outside;
    }
}
