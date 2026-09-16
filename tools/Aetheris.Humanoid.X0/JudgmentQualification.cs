using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal static class JudgmentQualification
{
    public static int Run(string input, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)!;
        if (candidate.SourcePoseSurface.TopologyId != AntoniaSurfaceAdoption.CandidateTopologyId) throw new InvalidDataException("Expected Antonia adoption candidate.");
        var surface = candidate.SourcePoseSurface with { Vertices = candidate.SourcePoseSurface.Vertices.Select((v,i) => v with { Position = candidate.PreparedPositions[i] }).ToArray() };
        var skeleton = candidate.PreparedSkeleton;
        Directory.CreateDirectory(output);
        var cases = new List<object>(); var timings = new List<object>();
        foreach (var (name, joint) in Poses())
        {
            var solve = HumanoidKinematicSolver.Solve(skeleton, new(name, skeleton.SkeletonId, skeleton.RestPoseId, 0, [joint]));
            if (solve.Pose is not { } solved) throw new InvalidOperationException("Qualification pose did not solve: " + name);
            var watch = Stopwatch.StartNew();
            _ = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, solved);
            var oldConstrainedPathMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var context = HumanoidDeformationContext.Create(surface, skeleton, 0, solved, joint.Joint);
            var contextMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var phases = new Dictionary<string, double>();
            var decision = HumanoidDeformationJudgment.Evaluate(context, DeformationPolicy.HipV1, recordTiming: (phase, ms) => phases[phase] = ms);
            var judgmentMs = watch.Elapsed.TotalMilliseconds;
            var trace = new
            {
                name, requested = joint, decision.PolicyVersion, decision.IsSuccess, decision.WinnerId, decision.RunnerUpId, decision.TieBroken,
                decision.Trace, metrics = decision.Candidates.Select(c => new { c.Candidate.Id, c.Candidate.Construction, c.Metrics }),
                decision.Diagnostics, decision.FinalSurfaceEvidence,
                patch = new { vertices = context.Vertices.Count, editableVertices = context.Vertices.Count(v => v.Attachment == SurfaceAttachmentClass.BlendedTransition),
                    boundaryVertices = context.Vertices.Count(v => v.Boundary), faces = context.Faces.Count, edges = context.Edges.Count },
                maximumCenterResidualMm = solved.Residuals.Max(r => r.CenterMm), maximumLengthResidualMm = solved.Residuals.Max(r => r.LinkLengthMm)
            };
            File.WriteAllText(Path.Combine(output, name + ".trace.json"), JsonSerializer.Serialize(trace, HumanoidArtifactIO.JsonOptions));
            foreach (var c in decision.Candidates)
                Export(Path.Combine(output, name + "." + c.Candidate.Id + ".diagnostic.obj"), surface, context.AssembleDiagnostic(c.Candidate));
            var winnerPath = Path.Combine(output, name + ".winner.obj");
            if (decision.Positions is { } positions) Export(winnerPath, surface, positions);
            else if (File.Exists(winnerPath)) File.Delete(winnerPath);
            cases.Add(trace); timings.Add(new { name, oldConstrainedPathMs, contextMs, judgmentMs, phases });
        }
        File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(new
        {
            schema = "aetheris.humanoid.x3.judgment.v1", sourceCandidateSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input))),
            surface.TopologyId, surface.ConnectivityHash, policy = DeformationPolicy.HipV1, candidateSpecs = HumanoidDeformationCandidates.Default,
            sourceBehavior = "Unavailable: original Poser CR2 audit is separate; X1 weights are Aetheris-generated, not source weights.", cases
        }, HumanoidArtifactIO.JsonOptions));
        File.WriteAllText(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(timings, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine($"Judgment qualification evidence: {Path.Combine(output, "evidence.json")}. Inspect winner and failure gates; command success is not milestone acceptance.");
        return 0;
    }
    private static IEnumerable<(string, AnatomicalJointRequest)> Poses()
    {
        foreach (var angle in new[] { 0d, 30, 45, 70, 90 }) yield return ($"hip-flexion-{angle}", new(HumanoidJointKind.LeftHip, angle));
        foreach (var angle in new[] { 0d, 15, 30, 45 }) yield return ($"hip-abduction-{angle}", new(HumanoidJointKind.LeftHip, AbductionDegrees: angle));
    }
    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> positions)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("# NONCANONICAL Antonia X3 research; CC-BY-3.0 Olaf Delgado-Friedrichs; no endorsement; diagnostic candidates may fail.");
        foreach (var p in positions) writer.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}"));
        foreach (var f in surface.Faces) writer.WriteLine($"f {f.A+1} {f.B+1} {f.C+1}");
    }
}
