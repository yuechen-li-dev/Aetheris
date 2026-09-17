using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal static class RestX2Qualification
{
    public static int Run(string input, string rig, string sourceWeightsPath, string weightsPath, string blenderDirectory, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        var adoption = AntoniaReferenceRigAdapter.Adopt(candidate, rig);
        var sourceSurface = RestPoseQualification.ImportWeights(adoption.Surface, adoption.Skeleton, rig, sourceWeightsPath);
        var normalized = HumanoidRestPoseNormalizer.Normalize(sourceSurface, adoption.Skeleton);
        var surface = RestPoseQualification.ImportWeights(normalized.Surface, normalized.Skeleton, rig, weightsPath);
        Directory.CreateDirectory(output);
        var neutralPositions = HumanoidPosing.EvaluateVertices(surface, normalized.Skeleton,
            new("rest-x2-bind-check", [], HumanoidTransform.Identity));
        var bindMaximumMm = neutralPositions.Select((point, index) => (point-surface.Vertices[index].Position).Length).Max();
        var parity = new List<object>();
        var semantic = new List<object>();
        var transforms = new List<object>();
        var solveMs = 0d; var deformMs = 0d;
        foreach (var (name, requests) in Corpus())
        {
            var watch = Stopwatch.StartNew();
            var solve = HumanoidKinematicSolver.Solve(normalized.Skeleton,
                new(name, normalized.Skeleton.SkeletonId, normalized.Skeleton.RestPoseId, 0, requests));
            solveMs += watch.Elapsed.TotalMilliseconds;
            if (solve.Pose is not { } pose) throw new InvalidOperationException(name + ": " + string.Join("; ", solve.Diagnostics.Select(x => x.Message)));
            transforms.Add(new { pose = name,
                localRotations = pose.PoseState.LocalRotations.Select(rotation => new { joint = rotation.Joint.ToString(),
                    x = rotation.LocalRotation.X, y = rotation.LocalRotation.Y, z = rotation.LocalRotation.Z, w = rotation.LocalRotation.W }),
                joints = normalized.Skeleton.Joints.Select((joint, index) => new { joint = joint.Kind.ToString(), global = MatrixData(pose.GlobalTransforms[index]) }) });
            watch.Restart();
            var evaluated = HumanoidConstrainedSurface.Evaluate(surface, normalized.Skeleton, 0, pose);
            var portableLbs = HumanoidPosing.EvaluateVertices(surface, normalized.Skeleton, pose.PoseState);
            deformMs += watch.Elapsed.TotalMilliseconds;
            var blenderPath = Path.Combine(blenderDirectory, $"blender-selected--{name}.positions.json");
            if (File.Exists(blenderPath))
            {
                var blender = JsonSerializer.Deserialize<double[][]>(File.ReadAllText(blenderPath))
                    ?? throw new InvalidDataException("Missing Blender positions for " + name);
                if (blender.Length != portableLbs.Count) throw new InvalidDataException("Parity vertex count mismatch.");
                var distances = portableLbs.Select((point, i) => Distance(point, blender[i])).Order().ToArray();
                parity.Add(new { pose = name, vertexRmsMm = Math.Sqrt(distances.Select(x => x*x).Average()),
                    p95Mm = Quantile(distances, .95), maximumMm = distances[^1], vertices = distances.Length });
            }
            semantic.Add(new { pose = name, pose.SemanticResiduals,
                maximumSemanticResidualDegrees = pose.SemanticResiduals.Select(x => x.MaximumAbsoluteDegrees).DefaultIfEmpty().Max(), evaluated.Evidence.IsAdmissible });
            Export(Path.Combine(output, name + ".obj"), surface, portableLbs);
        }

        var judgment = new List<object>();
        foreach (var (name, request) in new[] { ("hip-flexion-70", new AnatomicalJointRequest(HumanoidJointKind.LeftHip, 70)),
            ("hip-abduction-45", new AnatomicalJointRequest(HumanoidJointKind.LeftHip, AbductionDegrees: 45)) })
        {
            var solve = HumanoidKinematicSolver.Solve(normalized.Skeleton,
                new(name, normalized.Skeleton.SkeletonId, normalized.Skeleton.RestPoseId, 0, [request]));
            if (solve.Pose is not { } pose) throw new InvalidOperationException("Judgment pose failed.");
            var context = HumanoidDeformationContext.Create(surface, normalized.Skeleton, 0, pose, request.Joint);
            var decision = HumanoidDeformationJudgment.Evaluate(context, DeformationPolicy.HipV1);
            judgment.Add(new { name, decision.IsSuccess, decision.WinnerId, decision.RunnerUpId, decision.TieBroken,
                candidates = decision.Candidates.Select(x => new { x.Candidate.Id, x.Metrics }) });
        }
        var maximumParity = parity.Select(x => (double)x.GetType().GetProperty("maximumMm")!.GetValue(x)!).Max();
        var maximumSemantic = semantic.Select(x => (double)x.GetType().GetProperty("maximumSemanticResidualDegrees")!.GetValue(x)!).Max();
        var evidence = new { schema = "aetheris.humanoid.rest-x2.aetheris-parity.v1", normalized.RestPoseId,
            surface.TopologyId, surface.ConnectivityHash, sourceWeightsSha256 = Sha(sourceWeightsPath), weightsSha256 = Sha(weightsPath), blenderDirectory,
            bindMaximumMm, parity, semantic, judgmentPolicy = DeformationPolicy.HipV1, judgment,
            performance = new { poseSolveTotalMilliseconds = solveMs, deformationTotalMilliseconds = deformMs },
            accepted = maximumParity <= .01 && maximumSemantic <= .05 };
        File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(evidence, HumanoidArtifactIO.JsonOptions));
        File.WriteAllText(Path.Combine(output, "pose-transforms.json"), JsonSerializer.Serialize(transforms, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine(JsonSerializer.Serialize(new { evidence.accepted, maximumParityMm = maximumParity,
            maximumSemanticResidualDegrees = maximumSemantic, output }, HumanoidArtifactIO.JsonOptions));
        return evidence.accepted ? 0 : 2;
    }

    private static IEnumerable<(string, AnatomicalJointRequest[])> Corpus()
    {
        yield return ("canonical-apose", []);
        yield return ("shoulder60", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 60)]);
        yield return ("shoulder90", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90)]);
        yield return ("shoulder120", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 120)]);
        yield return ("elbow45", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 35), new(HumanoidJointKind.LeftElbow, 45)]);
        yield return ("elbow90", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 35), new(HumanoidJointKind.LeftElbow, 90)]);
        yield return ("elbow120", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 35), new(HumanoidJointKind.LeftElbow, 120)]);
        yield return ("hip45", [new(HumanoidJointKind.LeftHip, 45)]);
        yield return ("hip70", [new(HumanoidJointKind.LeftHip, 70)]);
        yield return ("hip90", [new(HumanoidJointKind.LeftHip, 90)]);
        yield return ("abduction30", [new(HumanoidJointKind.LeftHip, AbductionDegrees: 30)]);
        yield return ("abduction45", [new(HumanoidJointKind.LeftHip, AbductionDegrees: 45)]);
        yield return ("knee45", [new(HumanoidJointKind.LeftKnee, 45)]);
        yield return ("knee90", [new(HumanoidJointKind.LeftKnee, 90)]);
        yield return ("walking", [new(HumanoidJointKind.LeftHip, 30), new(HumanoidJointKind.LeftKnee, 35), new(HumanoidJointKind.RightShoulder, AbductionDegrees: 20)]);
        yield return ("balance-kick", [new(HumanoidJointKind.LeftHip, 70), new(HumanoidJointKind.LeftKnee, 35), new(HumanoidJointKind.RightShoulder, AbductionDegrees: 55)]);
        yield return ("reach", [new(HumanoidJointKind.LeftShoulder, 35, 105), new(HumanoidJointKind.LeftElbow, 25), new(HumanoidJointKind.RightShoulder, AbductionDegrees: 35)]);
    }
    private static double Distance(Point3D point, double[] other) => Math.Sqrt(Math.Pow(point.X-other[0],2)+Math.Pow(point.Y-other[1],2)+Math.Pow(point.Z-other[2],2));
    private static double Quantile(double[] sorted, double q) => sorted[(int)Math.Clamp(Math.Ceiling(q*sorted.Length)-1,0,sorted.Length-1)];
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static object MatrixData(System.Numerics.Matrix4x4 m) => new { m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44 };
    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> points)
    {
        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine("# HUMANOID-REST-X2 Aetheris imported A-pose weights; local evidence");
        foreach (var point in points) writer.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
        foreach (var face in surface.Faces) writer.WriteLine($"f {face.A+1} {face.B+1} {face.C+1}");
    }
}
