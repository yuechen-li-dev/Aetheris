using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;

internal static class RestPoseQualification
{
    public static int Run(string input, string rig, string weightsPath, string output)
    {
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        var adoption = AntoniaReferenceRigAdapter.Adopt(candidate, rig);
        var sourceSurface = ImportWeights(adoption.Surface, adoption.Skeleton, rig, weightsPath);
        Directory.CreateDirectory(output);
        Export(Path.Combine(output, "source-native-rest.obj"), sourceSurface,
            sourceSurface.Vertices.Select(vertex => vertex.Position).ToArray(), "source-native rest; not canonical pose");

        var normalizeClock = Stopwatch.StartNew();
        var normalized = HumanoidRestPoseNormalizer.Normalize(sourceSurface, adoption.Skeleton);
        normalizeClock.Stop();
        Export(Path.Combine(output, "canonical-apose.obj"), normalized.Surface,
            normalized.Surface.Vertices.Select(vertex => vertex.Position).ToArray(), "canonical A-pose rest");

        var cases = new List<object>();
        var allSemantic = true;
        var solveMilliseconds = 0d;
        var deformMilliseconds = 0d;
        foreach (var (name, requests) in Corpus())
        {
            var solveClock = Stopwatch.StartNew();
            var solve = HumanoidKinematicSolver.Solve(normalized.Skeleton,
                new(name, normalized.Skeleton.SkeletonId, normalized.Skeleton.RestPoseId, 0, requests));
            solveClock.Stop(); solveMilliseconds += solveClock.Elapsed.TotalMilliseconds;
            if (solve.Pose is not { } posed) throw new InvalidOperationException(name + ": " + string.Join("; ", solve.Diagnostics.Select(d => d.Message)));
            var deformClock = Stopwatch.StartNew();
            var evaluated = HumanoidConstrainedSurface.Evaluate(normalized.Surface, normalized.Skeleton, 0, posed);
            deformClock.Stop(); deformMilliseconds += deformClock.Elapsed.TotalMilliseconds;
            var maximumSemanticResidual = posed.SemanticResiduals.Select(value => value.MaximumAbsoluteDegrees).DefaultIfEmpty().Max();
            allSemantic &= maximumSemanticResidual <= .05;
            var filename = name + (evaluated.Evidence.IsAdmissible ? ".screened.obj" : ".failed-diagnostic.obj");
            Export(Path.Combine(output, filename), normalized.Surface, evaluated.Positions, "absolute semantic pose");
            cases.Add(new { name, requested = requests, actual = requests.Select(request =>
                    HumanoidPoseSemantics.Measure(normalized.Skeleton, posed.GlobalTransforms, request.Joint)),
                posed.SemanticResiduals, maximumSemanticResidualDegrees = maximumSemanticResidual,
                maximumCenterResidualMm = posed.Residuals.Select(value => value.CenterMm).DefaultIfEmpty().Max(),
                maximumLinkResidualMm = posed.Residuals.Select(value => value.LinkLengthMm).DefaultIfEmpty().Max(),
                evaluated.Evidence, obj = filename });
        }

        var oldHash = HashPositions(sourceSurface.Vertices.Select(vertex => vertex.Position));
        var newHash = HashPositions(normalized.Surface.Vertices.Select(vertex => vertex.Position));
        var evidence = new
        {
            schema = "aetheris.humanoid.rest-x1.evidence.v1",
            verdict = allSemantic ? "Canonical A-pose and Antonia semantic adapter qualified" : "Semantic residual exceeds tolerance",
            identities = new { normalized.Surface.TopologyId, normalized.Surface.ConnectivityHash,
                skeletonId = normalized.Skeleton.SkeletonId, normalized.SourceRestPoseId, normalized.RestPoseId,
                CanonicalHumanoidRestPose.SemanticFrameId, CanonicalHumanoidRestPose.DeformationPolicyId },
            topology = new { verticesBefore = sourceSurface.Vertices.Count, verticesAfter = normalized.Surface.Vertices.Count,
                facesBefore = sourceSurface.Faces.Count, facesAfter = normalized.Surface.Faces.Count,
                connectivityUnchanged = sourceSurface.ConnectivityHash == normalized.Surface.ConnectivityHash,
                orderedConnectivityUnchanged = sourceSurface.Faces.Select(face => (face.A, face.B, face.C))
                    .SequenceEqual(normalized.Surface.Faces.Select(face => (face.A, face.B, face.C))) },
            canonicalNeutral = new { shoulderAbductionDegrees = CanonicalHumanoidRestPose.ShoulderAbductionDegrees,
                shoulderFlexionDegrees = 0, shoulderTwistDegrees = 0, elbowFlexionDegrees = 0,
                hipFlexionDegrees = 0, hipAbductionDegrees = 0, kneeFlexionDegrees = 0,
                CanonicalHumanoidRestPose.PalmOrientation, CanonicalHumanoidRestPose.FootOrientation },
            sourceNeutral = normalized.SourceNeutral, normalizationResiduals = normalized.Residuals,
            normalizedJoints = normalized.Skeleton.Joints.Select((joint, index) => new { index, joint.Kind, joint.ParentIndex, joint.GlobalBind }),
            normalized.MaximumBindReconstructionMm, sourceRestPositionSha256 = oldHash,
            canonicalRestPositionSha256 = newHash, sourceCandidateSha256 = Sha(input), referenceRigSha256 = Sha(rig),
            weightsSha256 = Sha(weightsPath), solverVersion = HumanoidKinematicSolver.Version,
            semanticToleranceDegrees = .05, allSemanticResidualsWithinTolerance = allSemantic,
            cases,
            boundaries = new[]
            {
                "Antonia is the only implemented source adapter in this evidence.",
                "The imported Blender-cleaned weight field was generated in the former source rest and re-bound; A-pose weight regeneration is not yet qualified.",
                "Mixamo and Genesis remain ignored local comparison assets and are not runtime dependencies.",
                "The Antonia research candidate has no admitted morph channels or attachment sites; generic canonical morph/attachment preservation is covered by tests."
            }
        };
        File.WriteAllText(Path.Combine(output, "evidence.json"), JsonSerializer.Serialize(evidence, HumanoidArtifactIO.JsonOptions));
        var performance = new { normalizationMilliseconds = normalizeClock.Elapsed.TotalMilliseconds,
            poseSolveTotalMilliseconds = solveMilliseconds, deformationTotalMilliseconds = deformMilliseconds };
        File.WriteAllText(Path.Combine(output, "timings.json"), JsonSerializer.Serialize(performance, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine(JsonSerializer.Serialize(new { success = allSemantic, output, normalized.RestPoseId,
            normalized.MaximumBindReconstructionMm, sourceRestPositionSha256 = oldHash, canonicalRestPositionSha256 = newHash,
            cases = cases.Count, performance }, HumanoidArtifactIO.JsonOptions));
        return allSemantic ? 0 : 2;
    }

    internal static HumanoidSurface ImportWeights(HumanoidSurface surface, HumanoidSkeleton skeleton, string rig, string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        Require(root.GetProperty("schema").GetString() == "aetheris.humanoid.experimental-weights.v1", "Unsupported weight schema.");
        Require(root.GetProperty("topologyId").GetString() == surface.TopologyId &&
                root.GetProperty("connectivityHash").GetString() == surface.ConnectivityHash, "Weight topology mismatch.");
        Require(root.GetProperty("referenceRigSha256").GetString() == Sha(rig), "Weight rig mismatch.");
        var indices = skeleton.Joints.Select((joint, index) => (joint.Kind, index))
            .ToDictionary(item => item.Kind.ToString(), item => item.index, StringComparer.Ordinal);
        var imported = new Dictionary<string, JointWeight[]>(StringComparer.Ordinal);
        foreach (var vertex in root.GetProperty("vertices").EnumerateArray())
        {
            var id = vertex.GetProperty("vertexId").GetString() ?? throw new InvalidDataException("Missing vertex ID.");
            var weights = vertex.GetProperty("weights").EnumerateArray().Select(weight =>
            {
                var bone = weight.GetProperty("boneId").GetString() ?? throw new InvalidDataException("Missing bone ID.");
                Require(indices.ContainsKey(bone), "Unknown bone " + bone);
                return new JointWeight(indices[bone], weight.GetProperty("weight").GetDouble());
            }).ToArray();
            Require(weights.Length > 0 && Math.Abs(weights.Sum(weight => weight.Weight) - 1) <= CanonicalAdultStandardV1.WeightTolerance,
                "Invalid weight sum for " + id);
            Require(imported.TryAdd(id, weights), "Duplicate vertex " + id);
        }
        Require(surface.Vertices.All(vertex => imported.ContainsKey(vertex.Id)), "Vertex identity mismatch.");
        return surface with { SkinWeights = surface.Vertices.Select((vertex, index) => new VertexSkinWeights(index, imported[vertex.Id])).ToArray() };
    }

    private static IEnumerable<(string Name, AnatomicalJointRequest[] Requests)> Corpus()
    {
        yield return ("canonical-neutral-apose", []);
        foreach (var angle in new[] { 60d, 90, 120 }) yield return ($"shoulder-abduction-{angle}", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: angle)]);
        yield return ("elbow-flexion-90", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 35), new(HumanoidJointKind.LeftElbow, 90)]);
        foreach (var angle in new[] { 70d, 90 }) yield return ($"hip-flexion-{angle}", [new(HumanoidJointKind.LeftHip, angle)]);
        yield return ("hip-abduction-45", [new(HumanoidJointKind.LeftHip, AbductionDegrees: 45)]);
        yield return ("knee-flexion-90", [new(HumanoidJointKind.LeftKnee, 90)]);
        yield return ("combined-pose", [new(HumanoidJointKind.LeftShoulder, AbductionDegrees: 90), new(HumanoidJointKind.LeftElbow, 90),
            new(HumanoidJointKind.LeftHip, 45, 15), new(HumanoidJointKind.LeftKnee, 45)]);
    }

    private static void Export(string path, HumanoidSurface surface, IReadOnlyList<Point3D> positions, string status)
    {
        using var writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false));
        writer.WriteLine("# Antonia-derived REST-X1 research evidence; CC-BY-3.0 Olaf Delgado-Friedrichs; no endorsement; " + status);
        foreach (var point in positions) writer.WriteLine(FormattableString.Invariant($"v {point.X:R} {point.Y:R} {point.Z:R}"));
        foreach (var face in surface.Faces) writer.WriteLine($"f {face.A + 1} {face.B + 1} {face.C + 1}");
    }
    private static string HashPositions(IEnumerable<Point3D> positions) => Convert.ToHexStringLower(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(string.Join("\n", positions.Select(point => FormattableString.Invariant($"{point.X:R},{point.Y:R},{point.Z:R}"))))));
    private static string Sha(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException("REST_X1: " + message); }
}
