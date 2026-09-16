using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal static class ConstrainedQualification
{
    public static int Run(string input, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        if (candidate.SourcePoseSurface.TopologyId != AntoniaSurfaceAdoption.CandidateTopologyId)
            throw new InvalidDataException("Expected the unpromoted Antonia X1 candidate.");
        Directory.CreateDirectory(output);
        var surface = candidate.SourcePoseSurface with { Vertices = candidate.SourcePoseSurface.Vertices
            .Select((v, i) => v with { Position = candidate.PreparedPositions[i] }).ToArray() };
        var skeleton = candidate.PreparedSkeleton;
        var cases = new List<object>();
        var timings = new List<object>();
        foreach (var (name, joints, policy) in Corpus())
        {
            var request = new RequestedHumanoidPose(name, skeleton.SkeletonId, skeleton.RestPoseId, 0, joints);
            var timer = Stopwatch.StartNew();
            var result = HumanoidKinematicSolver.Solve(skeleton, request, policy: policy);
            var solveMs = timer.Elapsed.TotalMilliseconds;
            if (result.Pose is not { } solved)
            {
                cases.Add(new { name, request, policy, result.IsSolved, result.Diagnostics, result.Projections });
                timings.Add(new { name, solveMs });
                continue;
            }
            timer.Restart();
            var evaluation = HumanoidConstrainedSurface.Evaluate(surface, skeleton, 0, solved);
            var surfaceAndValidationMs = timer.Elapsed.TotalMilliseconds;
            var replay = HumanoidKinematicSolver.Solve(skeleton, request, policy: policy).Pose!;
            var deterministic = solved.GlobalTransforms.SequenceEqual(replay.GlobalTransforms) && solved.Joints.SequenceEqual(replay.Joints);
            if (!deterministic) throw new InvalidOperationException("Solved state replay mismatch: " + name);
            // Failed surfaces have diagnostic filenames. They are not admitted render/export products.
            var obj = name + (evaluation.Evidence.IsAdmissible ? ".screened.obj" : ".failed-diagnostic.obj");
            Export(Path.Combine(output, obj), surface, evaluation.Positions);
            File.WriteAllText(Path.Combine(output, name + ".mechanism.json"), JsonSerializer.Serialize(new
            {
                joints = skeleton.Joints.Select((j, i) => new { j.Kind, j.ParentIndex, position = HumanoidKinematicSolverPosition(solved.GlobalTransforms[i]) }),
                solved.Residuals
            }, HumanoidArtifactIO.JsonOptions));
            cases.Add(new { name, request, policy, result.IsSolved, result.Diagnostics, result.Projections,
                solved.Joints, solved.Residuals, deterministic, evaluation.Evidence, diagnosticObj = obj });
            timings.Add(new { name, solveMs, surfaceAndValidationMs });
        }
        var baseline = AntoniaSurfaceAdoption.QualificationPoses().Select(p =>
        {
            var globals = HumanoidPosing.GlobalPose(skeleton, p);
            var positions = HumanoidPosing.EvaluateVertices(surface, skeleton, p);
            var evidence = HumanoidConstrainedSurface.Inspect(surface, skeleton, globals, positions);
            return new { p.PoseId, evidence };
        }).ToArray();
        File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(new
        {
            schema = "aetheris.humanoid.x2-constrained-progression.v1", verdict = "Meaningful progression",
            solverVersion = HumanoidKinematicSolver.Version,
            sourceCandidateSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(input))),
            surface.TopologyId, surface.ConnectivityHash,
            links = HumanoidKinematicSolver.Links(skeleton), cases, baseline,
            limitations = new[] { "Antonia remains an unpromoted candidate.", "Compound shoulder, collision, wrist/ankle/neck domains are not implemented.",
                "Legacy quaternion API remains available for X0/X1 research; it is not a constrained solve.",
                "Surface LBS and weights are unchanged to isolate kinematic and interpolation failures.",
                "No migrated Antonia landmarks, attachment frames, morphs or measurements; no human signoff.",
                "Orientation screen is a proxy; self-intersection count unavailable." }
        }, HumanoidArtifactIO.JsonOptions));
        File.WriteAllText(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(timings, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine($"Meaningful progression evidence: {Path.Combine(output, "evidence.json")}. Successful execution does not mean X2 acceptance.");
        return 0;
    }

    private static object HumanoidKinematicSolverPosition(Matrix4x4 m) => new { x = m.M41, y = m.M42, z = m.M43 };

    public static IEnumerable<(string Name, AnatomicalJointRequest[] Joints, AnatomicalSolvePolicy Policy)> Corpus()
    {
        yield return ("neutral", [], AnatomicalSolvePolicy.Reject);
        foreach (var degrees in new[] { 45d, 70, 90, 120 })
            yield return ($"hip-flexion-{degrees}", [new(HumanoidJointKind.LeftHip, degrees)], AnatomicalSolvePolicy.Reject);
        foreach (var degrees in new[] { 30d, 45, 70 })
            yield return ($"hip-abduction-{degrees}", [new(HumanoidJointKind.LeftHip, AbductionDegrees: degrees)], AnatomicalSolvePolicy.Project);
        foreach (var degrees in new[] { 45d, 90, 120 })
            yield return ($"elbow-flexion-{degrees}", [new(HumanoidJointKind.LeftElbow, degrees)], AnatomicalSolvePolicy.Reject);
        foreach (var degrees in new[] { 45d, 90, 140 })
            yield return ($"knee-flexion-{degrees}", [new(HumanoidJointKind.LeftKnee, degrees)], AnatomicalSolvePolicy.Reject);
        yield return ("hip-knee-90", [new(HumanoidJointKind.LeftHip, 90), new(HumanoidJointKind.LeftKnee, 90)], AnatomicalSolvePolicy.Reject);
        yield return ("backward-elbow", [new(HumanoidJointKind.LeftElbow, -45)], AnatomicalSolvePolicy.Reject);
        yield return ("knee-hyperextension", [new(HumanoidJointKind.LeftKnee, -30)], AnatomicalSolvePolicy.Project);
        yield return ("extreme-hip-abduction", [new(HumanoidJointKind.LeftHip, AbductionDegrees: 160)], AnatomicalSolvePolicy.Reject);
        yield return ("hip-corner", [new(HumanoidJointKind.LeftHip, 120, 45, 45)], AnatomicalSolvePolicy.Project);
        yield return ("shoulder-90", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90)], AnatomicalSolvePolicy.Reject);
    }

    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> positions)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("# NONCANONICAL X2 diagnostic; Antonia Polygon CC-BY-3.0 Olaf Delgado-Friedrichs; no endorsement");
        foreach (var p in positions) writer.WriteLine(FormattableString.Invariant($"v {p.X:R} {p.Y:R} {p.Z:R}"));
        foreach (var f in surface.Faces) writer.WriteLine($"f {f.A + 1} {f.B + 1} {f.C + 1}");
    }
}
