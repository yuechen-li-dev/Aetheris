using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using Aetheris.Humanoid;

internal static class GameplayBodyQualification
{
    public static int Run(string input, string rig, string sourceWeights, string bundle, bool seed)
    {
        Directory.CreateDirectory(bundle);
        File.WriteAllText(Path.Combine(bundle, "runtime-evidence.json"),
            JsonSerializer.Serialize(new { accepted = false, stage = "preparing" }, HumanoidArtifactIO.JsonOptions));
        var candidate = JsonSerializer.Deserialize<AntoniaAdoptionCandidate>(
            File.ReadAllText(input), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing Antonia candidate.");
        var adoption = AntoniaReferenceRigAdapter.Adopt(candidate, rig);
        var source = RestPoseQualification.ImportWeights(adoption.Surface, adoption.Skeleton, rig, sourceWeights);
        var normalized = HumanoidRestPoseNormalizer.Normalize(source, adoption.Skeleton);
        using var weightMetadata = JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle, "weights.json")));
        if (weightMetadata.RootElement.GetProperty("skinning").GetString() != "dual-quaternion" ||
            weightMetadata.RootElement.GetProperty("restPoseId").GetString() != normalized.Skeleton.RestPoseId)
        {
            throw new InvalidDataException("Gameplay weights require the exact normalized rest and dual-quaternion convention.");
        }
        var surface = RestPoseQualification.ImportWeights(normalized.Surface, normalized.Skeleton,
            rig, Path.Combine(bundle, "weights.json"));
        var correctives = JsonSerializer.Deserialize<HumanoidPoseCorrective[]>(
            File.ReadAllText(Path.Combine(bundle, "correctives.json")), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing corrective bank.");
        var corpus = JsonSerializer.Deserialize<GameplayPoseCase[]>(
            File.ReadAllText(Path.Combine(bundle, "pose-corpus.json")), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing pose corpus.");
        var recipeIdentity = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",
            new[] { input, rig, sourceWeights, Path.Combine(bundle, "weights.json"),
                Path.Combine(bundle, "correctives.json") }.Select(path =>
                    Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))))))));
        var provenance = candidate.Provenance with
        {
            GeneratorVersion = "antonia.gameplay-body.v1",
            ConfigHash = recipeIdentity,
            OutputHash = recipeIdentity,
            AdmissionStatus = "Gameplay development candidate; separate pose qualification; no canonical promotion",
            Transformations = candidate.Provenance.Transformations.Concat(
            [
                "Canonical A-pose normalization, bilateral weights, smooth hinge transitions and central pelvis retention",
                "Retained dual-quaternion binding and two authored pre-skin hip flexion shapes",
            ]).ToArray(),
        };
        var artifact = new HumanoidGameplayBodyArtifact(HumanoidGameplayBody.Schema,
            "antonia.gameplay-candidate.v1", surface, normalized.Skeleton, correctives, provenance);
        var assetPath = Path.Combine(bundle, "antonia.gameplay-body.json");
        HumanoidGameplayBody.Save(artifact, assetPath);
        var body = HumanoidGameplayBody.Load(assetPath);
        var reports = new List<GameplayPoseReport>();
        var transforms = new List<object>();
        foreach (var item in corpus)
        {
            var solved = body.Solve(item.Name, item.Requested);
            if (!solved.IsSolved)
            {
                throw new InvalidDataException(item.Name + ": " + string.Join("; ", solved.Diagnostics));
            }
            var pose = solved.Pose!;
            var evaluated = body.Evaluate(pose);
            var parityPath = Path.Combine(bundle, "blender", item.Name + ".positions.f32");
            double? parityMm = null;
            if (!seed)
            {
                var data = File.ReadAllBytes(parityPath);
                if (data.Length != evaluated.Positions.Count * 12)
                {
                    throw new InvalidDataException("Blender parity vertex count mismatch: " + item.Name);
                }
                parityMm = 0;
                for (var index = 0; index < evaluated.Positions.Count; index++)
                {
                    var offset = index * 12;
                    var x = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));
                    var y = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 4, 4));
                    var z = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset + 8, 4));
                    var point = evaluated.Positions[index];
                    var distance = Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2) +
                        Math.Pow(point.Z - z, 2));
                    if (!double.IsFinite(distance))
                    {
                        throw new InvalidDataException("Nonfinite Blender parity: " + item.Name);
                    }
                    parityMm = Math.Max(parityMm.Value, distance);
                }
            }
            var report = new GameplayPoseReport(item.Name, item.Role, evaluated.Evidence.IsAdmissible,
                evaluated.Evidence.Regions.Max(region => region.Maximum),
                evaluated.Evidence.FlaggedFaceIds.Count, evaluated.Evidence.CollapsedTriangles,
                pose.SemanticResiduals.Select(residual => residual.MaximumAbsoluteDegrees).DefaultIfEmpty(0).Max(),
                parityMm, evaluated.Evidence.FlaggedFaceIds.ToArray());
            reports.Add(report);
            transforms.Add(new
            {
                name = item.Name,
                localRotations = pose.PoseState.LocalRotations.Select(rotation => new
                {
                    joint = rotation.Joint.ToString(), x = rotation.LocalRotation.X,
                    y = rotation.LocalRotation.Y, z = rotation.LocalRotation.Z, w = rotation.LocalRotation.W,
                }),
                correctives = body.ObserveCorrectives(pose),
            });
        }
        var qualified = reports.Where(report => report.Role != "stress").ToArray();
        var accepted = !seed && qualified.Length > 0 && qualified.All(report =>
            report.SurfaceAdmissible && report.SemanticMaximumDegrees <= .05 && report.ParityMaximumMm <= .015);
        var summary = new
        {
            schema = "aetheris.humanoid.gameplay-qualification.v1",
            stage = seed ? "seed-for-independent-blender-replay" : "qualification",
            accepted,
            assetSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assetPath))),
            bodyId = body.Id,
            canonicalPromotion = false,
            qualifiedCases = qualified.Length,
            stressCases = reports.Count - qualified.Length,
            qualifiedMaximumEdgeRatio = qualified.Max(report => report.MaximumEdgeRatio),
            qualifiedReversals = qualified.Sum(report => report.ReversalFlags),
            cases = reports,
        };
        File.WriteAllText(Path.Combine(bundle, "runtime-evidence.json"),
            JsonSerializer.Serialize(summary, HumanoidArtifactIO.JsonOptions));
        File.WriteAllText(Path.Combine(bundle, "pose-transforms.json"),
            JsonSerializer.Serialize(transforms, HumanoidArtifactIO.JsonOptions));
        Console.WriteLine(JsonSerializer.Serialize(summary, HumanoidArtifactIO.JsonOptions));
        return seed || accepted ? 0 : 2;
    }

    private sealed record GameplayPoseCase(string Name, AnatomicalJointRequest[] Requested, string Role);
    private sealed record GameplayPoseReport(string Pose, string Role, bool SurfaceAdmissible,
        double MaximumEdgeRatio, int ReversalFlags, int CollapsedTriangles,
        double SemanticMaximumDegrees, double? ParityMaximumMm, string[] FlaggedFaceIds);
}
