using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aetheris.Humanoid;

public sealed record HumanoidGameplayBodyArtifact(
    string Schema, string Id, HumanoidSurface Surface, HumanoidSkeleton Skeleton,
    IReadOnlyList<HumanoidPoseCorrective> Correctives, HumanoidProvenance Provenance);

/// <summary>
/// Explicitly loaded gameplay body, separate from CanonicalHumanoid admission.
/// A pose solve and surface screen remain independent; failed surfaces are diagnostic output.
/// </summary>
public sealed class HumanoidGameplayBody
{
    public const string Schema = "aetheris.humanoid.gameplay-body.v1";
    private readonly HumanoidSkinningBinding binding;

    public HumanoidGameplayBody(HumanoidGameplayBodyArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (artifact.Schema != Schema || string.IsNullOrWhiteSpace(artifact.Id) ||
            artifact.Surface is null || artifact.Skeleton is null || artifact.Correctives is null ||
            artifact.Provenance is null || artifact.Surface.Faces is null || artifact.Surface.Vertices is null ||
            artifact.Surface.SkinWeights is null || artifact.Skeleton.Joints is null ||
            artifact.Skeleton.Symmetry is null || artifact.Surface.Faces.Count == 0 ||
            artifact.Surface.Faces.Any(face => face.A < 0 || face.B < 0 || face.C < 0 ||
                face.A >= artifact.Surface.Vertices.Count || face.B >= artifact.Surface.Vertices.Count ||
                face.C >= artifact.Surface.Vertices.Count || face.A == face.B || face.A == face.C || face.B == face.C))
        {
            throw new InvalidDataException("HUM313: Incomplete or unsupported gameplay body artifact.");
        }

        Surface = artifact.Surface with
        {
            Vertices = Array.AsReadOnly(artifact.Surface.Vertices.ToArray()),
            Faces = Array.AsReadOnly(artifact.Surface.Faces.ToArray()),
            SkinWeights = Array.AsReadOnly(artifact.Surface.SkinWeights.Select(skin =>
                skin with { Weights = Array.AsReadOnly(skin.Weights.ToArray()) }).ToArray()),
        };
        Skeleton = artifact.Skeleton with
        {
            Joints = Array.AsReadOnly(artifact.Skeleton.Joints.ToArray()),
            Symmetry = new ReadOnlyDictionary<HumanoidJointKind, HumanoidJointKind>(
                artifact.Skeleton.Symmetry.ToDictionary(pair => pair.Key, pair => pair.Value)),
        };
        Id = artifact.Id;
        binding = new(Surface, Skeleton, correctives: artifact.Correctives);
        Correctives = Array.AsReadOnly(artifact.Correctives.Select(corrective => corrective with
        {
            Vertices = Array.AsReadOnly(corrective.Vertices.ToArray()),
        }).ToArray());
    }

    public string Id { get; }
    public HumanoidSurface Surface { get; }
    public HumanoidSkeleton Skeleton { get; }
    public IReadOnlyList<HumanoidPoseCorrective> Correctives { get; }

    public HumanoidSolveResult Solve(string poseId, IReadOnlyList<AnatomicalJointRequest> requests)
    {
        return HumanoidKinematicSolver.Solve(Skeleton,
            new(poseId, Skeleton.SkeletonId, Skeleton.RestPoseId, 0, requests));
    }

    public HumanoidSurfaceEvaluation Evaluate(SolvedHumanoidPose pose)
    {
        var positions = binding.Evaluate(pose);
        var evidence = HumanoidConstrainedSurface.Inspect(Surface, Skeleton, pose.GlobalTransforms, positions);
        return new(positions, evidence);
    }

    public IReadOnlyList<HumanoidSkinDualQuaternion> CreatePalette(SolvedHumanoidPose pose) =>
        binding.CreatePalette(pose);

    public IReadOnlyDictionary<string, double> ObserveCorrectives(SolvedHumanoidPose pose) =>
        binding.ObserveCorrectives(pose);

    public static HumanoidGameplayBody Load(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.Length > 128L * 1024 * 1024)
        {
            throw new InvalidDataException("HUM313: Gameplay body exceeds the 128 MiB artifact budget.");
        }
        var artifact = JsonSerializer.Deserialize(stream, HumanoidGameplayJson.Default.HumanoidGameplayBodyArtifact)
            ?? throw new InvalidDataException("HUM313: Gameplay body artifact is empty.");
        return new(artifact);
    }

    public static void Save(HumanoidGameplayBodyArtifact artifact, string path)
    {
        _ = new HumanoidGameplayBody(artifact);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, artifact, HumanoidGameplayJson.Default.HumanoidGameplayBodyArtifact);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    IncludeFields = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(HumanoidGameplayBodyArtifact))]
internal partial class HumanoidGameplayJson : JsonSerializerContext;
