using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Topology;

namespace Aetheris.Kernel.Core.Import;

[JsonConverter(typeof(JsonStringEnumConverter<ImportQualificationStatus>))]
public enum ImportQualificationStatus { Qualified, Inspectable, Degraded, Failed }

[JsonConverter(typeof(JsonStringEnumConverter<ImportDisplayStatus>))]
public enum ImportDisplayStatus { NotAssessed, Complete, RefinementLimited, Partial }

public sealed record ImportQualificationReason(string Code, string Message, int? FaceId = null, int? StepEntityId = null);
public sealed record ImportedShellQualification(int ShellId, int FaceCount, int ConnectedComponents, int BoundaryEdges, int NonManifoldEdges, bool Enclosed);
public sealed record BrepImportQualificationReport(
    ImportQualificationStatus Status,
    ImportQualificationStatus GeometryStatus,
    int ExpectedFaces,
    int BoundFaces,
    int? DisplayedFaces,
    int MissingPcurves,
    double? SourceDistanceAccuracyMillimetres,
    double PcurveToleranceMillimetres,
    BrepPcurveEvidence PcurveEvidence,
    IReadOnlyList<ImportedShellQualification> Shells,
    IReadOnlyList<ImportQualificationReason> Reasons)
{
    public ImportDisplayStatus DisplayStatus { get; init; } = ImportDisplayStatus.NotAssessed;
    public IReadOnlyList<ImportQualificationReason> DisplayReasons { get; init; } = [];
}

/// <summary>Import evidence is separate from parser success and from display preparation.</summary>
public static class BrepImportQualification
{
    internal static BrepImportQualificationReport Evaluate(BrepBody body, int expectedFaces,
        double? sourceAccuracy, double pcurveTolerance, BrepPcurveEvidence? pcurveEvidence = null)
    {
        var reasons = new List<ImportQualificationReason>();
        var topology = body.Topology;
        var faces = topology.Faces.ToArray();
        var faceByLoop = faces.SelectMany(f => f.LoopIds.Select(l => (Loop: l, Face: f.Id))).ToDictionary(p => p.Loop, p => p.Face);
        var uses = topology.Coedges.GroupBy(c => c.EdgeId).ToDictionary(g => g.Key, g => g.Select(c => faceByLoop[c.LoopId]).ToArray());
        var shells = new List<ImportedShellQualification>();
        foreach (var shell in topology.Shells.OrderBy(s => s.Id.Value))
        {
            var set = shell.FaceIds.ToHashSet();
            var adjacency = set.ToDictionary(f => f, _ => new HashSet<FaceId>());
            var boundary = 0;
            var nonManifold = 0;
            foreach (var incidence in uses.Values)
            {
                var local = incidence.Where(set.Contains).ToArray();
                if (local.Length == 0) continue;
                if (local.Length == 1) boundary++;
                if (local.Length > 2) nonManifold++;
                foreach (var a in local) foreach (var b in local) if (a != b) adjacency[a].Add(b);
            }
            var unseen = set.ToHashSet();
            var components = 0;
            while (unseen.Count > 0)
            {
                components++;
                var queue = new Queue<FaceId>();
                var first = unseen.First();
                unseen.Remove(first); queue.Enqueue(first);
                while (queue.TryDequeue(out var current))
                    foreach (var next in adjacency[current]) if (unseen.Remove(next)) queue.Enqueue(next);
            }
            var resolved = body.FaceOrientationReport?.Shells.SingleOrDefault(s => s.ShellId == shell.Id);
            var enclosed = set.Count > 0 && components == 1 && boundary == 0 && nonManifold == 0 && resolved?.IsClosedManifold == true;
            shells.Add(new(shell.Id.Value, set.Count, components, boundary, nonManifold, enclosed));
            if (!enclosed) reasons.Add(new("incomplete-shell", $"Shell {shell.Id.Value}: components={components}, boundaryEdges={boundary}, nonManifoldEdges={nonManifold}, orientationEnclosed={resolved?.IsClosedManifold}."));
            if (resolved?.Qualification != FaceOrientationQualification.DerivedQualified)
                reasons.Add(new("unqualified-orientation", $"Shell {shell.Id.Value}: {resolved?.Qualification.ToString() ?? "not-assessed"}."));
        }
        if (faces.Length != expectedFaces) reasons.Add(new("face-count-mismatch", $"Expected {expectedFaces} faces; bound {faces.Length}."));
        foreach (var face in faces)
            if (!body.TryGetFaceSurface(face.Id, out var surface) || surface is null)
                reasons.Add(new("unbound-face", $"Face {face.Id.Value} has no support geometry.", face.Id.Value));
        // Distinct coincident vertex IDs are legal interchange topology. Measure the
        // directed gap without mutating vertices or broadening the recovery budget.
        var closureTolerance = double.Max(1e-6, sourceAccuracy ?? 0d);
        foreach (var loop in topology.Loops.Where(l => l.Kind == LoopKind.Edge))
        {
            for (var i = 0; i < loop.CoedgeIds.Count; i++)
            {
                var c = topology.GetCoedge(loop.CoedgeIds[i]);
                var n = topology.GetCoedge(loop.CoedgeIds[(i + 1) % loop.CoedgeIds.Count]);
                var e = topology.GetEdge(c.EdgeId); var ne = topology.GetEdge(n.EdgeId);
                var end = DirectedEdgeUse.Resolve(e, c).EndVertexId;
                var start = DirectedEdgeUse.Resolve(ne, n).StartVertexId;
                if (end == start) continue;
                var gap = body.TryGetVertexPoint(end, out var p) && body.TryGetVertexPoint(start, out var q) ? (p - q).Length : double.PositiveInfinity;
                if (gap > closureTolerance) reasons.Add(new("trim-loop-not-closed", $"Loop {loop.Id.Value}, coedges {c.Id.Value}->{n.Id.Value}: gap={gap:R} mm, tolerance={closureTolerance:R} mm.", faceByLoop[loop.Id].Value));
            }
        }
        var evidence = pcurveEvidence ?? BrepPcurveValidator.Validate(body, pcurveTolerance);
        var missing = topology.Coedges.Count(c => !body.Bindings.TryGetPcurveBinding(c.Id, out _));
        foreach (var message in evidence.Diagnostics) reasons.Add(new("invalid-pcurve", message));
        foreach (var d in body.PcurveRecoveryReport?.Diagnostics ?? []) reasons.Add(new("pcurve-recovery-failed", d.Message));
        var degraded = reasons.Count > 0;
        if (missing > 0) reasons.Add(new("missing-pcurve", $"{missing} coedges lack qualified face-local trim bindings."));
        var geometryStatus = faces.Length == 0 ? ImportQualificationStatus.Failed : degraded ? ImportQualificationStatus.Degraded
            : missing > 0 ? ImportQualificationStatus.Inspectable : ImportQualificationStatus.Qualified;
        return new(geometryStatus,
            geometryStatus, expectedFaces, faces.Length, null, missing, sourceAccuracy, pcurveTolerance, evidence, shells, reasons);
    }

    /// <summary>Projects actual display evidence; it never repairs or adds geometry.</summary>
    public static BrepImportQualificationReport WithDisplay(BrepBody body, DisplayTessellationResult mesh)
    {
        var report = body.ImportQualification ?? Evaluate(body, body.Topology.Faces.Count(), null, 1e-3);
        var displayed = mesh.FacePatches.Where(p => p.TriangleIndices.Count > 0).Select(p => p.FaceId).ToHashSet();
        var reasons = new List<ImportQualificationReason>();
        foreach (var face in body.Topology.Faces.Where(f => !displayed.Contains(f.Id)))
        {
            body.Bindings.TryGetFaceBinding(face.Id, out var binding);
            body.TryGetFaceSurface(face.Id, out var support);
            reasons.Add(new("missing-display-face", $"Face {face.Id.Value} ({support?.Kind}) produced no display triangles.", face.Id.Value, binding.SourceStepEntityId));
        }
        foreach (var d in mesh.FaceDiagnostics ?? [])
            reasons.Add(new(d.Code, d.Message, d.FaceId?.Value,
                d.FaceId is { } faceId && body.Bindings.TryGetFaceBinding(faceId, out var binding) ? binding.SourceStepEntityId : null));
        var displayStatus = displayed.Count < report.BoundFaces ? ImportDisplayStatus.Partial
            : reasons.Count > 0 ? ImportDisplayStatus.RefinementLimited : ImportDisplayStatus.Complete;
        return report with { Status = report.GeometryStatus, DisplayedFaces = displayed.Count,
            DisplayStatus = displayStatus, DisplayReasons = reasons };
    }
}
