using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>A planar maker mark on the existing threaded HexBolt head cap.</summary>
public static class ThreadedHexBoltMakerMark
{
    public sealed record Result(BrepBody Body, string StepText, PrismaticSectionStackConstruction Section,
        PlanarTextProfileResult Text, double Depth, FaceId OriginalTopFace);

    public static Result Build(string threadedHexBoltSource, string content, double heightMm, double depthMm)
    {
        if (!double.IsFinite(depthMm) || depthMm <= 0d || depthMm > .3d)
            throw new ArgumentOutOfRangeException(nameof(depthMm));
        var compiled = FirmamentBuildAndExport.CompileSource(threadedHexBoltSource);
        if (!compiled.IsSuccess || compiled.Value.RuntimeBody is null || compiled.Value.Thread is null ||
            compiled.Value.StandardPart?.Family != "HexBolt")
            throw new InvalidOperationException("Maker mark requires a compiled threaded HexBolt: " +
                string.Join(" | ", compiled.Diagnostics.Select(x => x.Message)));
        var host = compiled.Value.RuntimeBody;
        var topId = compiled.Value.StandardPart.SemanticDescendants.Single(x =>
            x.StableId.EndsWith(".Head.TopFlat", StringComparison.Ordinal)).FaceId;
        if (topId is null) throw new InvalidOperationException("Threaded HexBolt top face is missing.");
        var topFace = new FaceId(topId.Value);
        var (disk, topX) = DiskFromTopRim(host, topFace);
        var text = PlanarTextProfiles.Build(content, heightMm, PlanarTextAlignment.Center);
        if (!text.Succeeded) throw new InvalidOperationException(string.Join(" | ", text.Diagnostics));
        var profiles = text.Regions.Select(region => region with { PlaneFrame = "YZ" }).Append(disk)
            .ToDictionary(x => x.Name, StringComparer.Ordinal);
        var operations = new List<PrismaticProfileOperation>
        {
            new("HeadCap", PrismaticProfileIntent.Base, disk.Name, -2d * depthMm, 0d, "Stock", "maker-mark")
        };
        operations.AddRange(text.Regions.Select((region, index) => new PrismaticProfileOperation(
            $"Mark{index}", PrismaticProfileIntent.Remove, region.Name, -depthMm, 0d,
            "Pocket", "maker-mark")));
        var feature = new PrismaticProfileCompositionFeature("MakerMark", "YZ", "-X",
            new PrismaticProfilePlacement("HeadTop", topX, 0d, 0d, "YZ", "-X", "+Y", true),
            operations, [-2d * depthMm, -depthMm, 0d], "maker-mark");
        var section = PrismaticSectionStackCompiler.Normalize(new(feature, profiles, []), out var diagnostics);
        if (section is null) throw new InvalidOperationException("Maker mark section: " + string.Join(" | ", diagnostics));
        var planned = PrismaticSectionStackEmitter.Emit(section);
        if (planned.Body is null || planned.Plan?.TopologyPlan is null)
            throw new InvalidOperationException("Maker mark BRep: " + string.Join(" | ", planned.Diagnostics));
        var patchFaces = planned.Plan.TopologyPlan.FaceMappings.Where(mapping =>
            (mapping.Kind == "TransitionCap" && mapping.ConstructionStableId != $"transition:{-2d * depthMm:R}") ||
            (mapping.Kind == "PrismaticSide" && mapping.SourceStableId.Contains("Text.Glyph[", StringComparison.Ordinal)))
            .Select(mapping => mapping.FaceId).ToArray();
        var body = HexBoltThreadStitch.ImprintTop(host, topFace, planned.Body, patchFaces);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = "ThreadedHexBoltMakerMark",
            ApplicationName = "Aetheris.Firmament.MakerMark.X2",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce
        });
        if (!step.IsSuccess) throw new InvalidOperationException("Maker mark STEP: " +
            string.Join(" | ", step.Diagnostics.Select(x => x.Message)));
        return new(body, step.Value, section, text, depthMm, topFace);
    }

    private static (ResolvedProfile2D Profile, double TopX) DiskFromTopRim(BrepBody body, FaceId topFace)
    {
        var face = body.Topology.GetFace(topFace);
        if (face.LoopIds.Count != 1) throw new InvalidOperationException("Maker mark head cap requires one circular outer loop.");
        var coedges = body.Topology.GetLoop(face.LoopIds[0]).CoedgeIds;
        var segments = new List<ResolvedProfileSegment2D>();
        double? topX = null;
        foreach (var (coedgeId, index) in coedges.Reverse().Select((id, i) => (id, i)))
        {
            var coedge = body.Topology.GetCoedge(coedgeId);
            var edge = body.Topology.GetEdge(coedge.EdgeId);
            var startVertex = coedge.IsReversed ? edge.StartVertexId : edge.EndVertexId;
            var endVertex = coedge.IsReversed ? edge.EndVertexId : edge.StartVertexId;
            body.TryGetVertexPoint(startVertex, out var start);
            body.TryGetVertexPoint(endVertex, out var end);
            topX ??= start.X;
            if (Math.Abs(start.X - topX.Value) > 1e-7d || Math.Abs(end.X - topX.Value) > 1e-7d)
                throw new InvalidOperationException("Maker mark head cap is not planar on X.");
            var radius = Math.Sqrt(start.Y * start.Y + start.Z * start.Z);
            var angle = Math.Atan2(-start.Z, start.Y);
            var finish = Math.Atan2(-end.Z, end.Y);
            var sweep = finish - angle;
            while (sweep <= 0d) sweep += 2d * Math.PI;
            if (sweep >= Math.PI) throw new InvalidOperationException("Maker mark rim arc is not bounded below 180 degrees.");
            segments.Add(new($"Rim{index}", new LineArcCircularArc2D((0d, 0d), radius, angle, sweep),
                new ProfileSegmentProvenance($"profile:HeadTop.Rim{index}", "HeadTop", "head-cap", "bolt-top-rim", "YZ")));
        }
        return (new ResolvedProfile2D("HeadTop", "YZ", [new ResolvedProfileLoop2D("Outer", true, segments)]), topX!.Value);
    }
}
