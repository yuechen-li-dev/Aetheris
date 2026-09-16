using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public sealed record ImportedRigJointFrame(string SourceJointId, string? ParentSourceJointId,
    Point3D HeadCanonicalMm, Point3D TailCanonicalMm, Matrix4x4 GlobalRestCanonical,
    bool SourceDeform, string SourceSemanticName);

public sealed record CanonicalRigJointFrame(HumanoidJointKind CanonicalJoint,
    HumanoidJointKind? CanonicalParent, string? SourceJointId, string MappingConfidence,
    string SemanticRole, IReadOnlyList<string> CollapsedSourceHelpers,
    Matrix4x4 GlobalRestCanonical, double SourceCenterDeltaMm,
    double SourceOrientationDeltaDegrees, double ParentRelativeDeltaMm);

public sealed record ReferenceRigArtifact(string Schema, string TopologyId, string SkeletonId,
    string RestPoseId, string SourceRigType, string SourceAssetSha256, string SourceMetarigSha256,
    string ExtractorSha256, string ExtractedFrameSha256, string CoordinateConversion,
    IReadOnlyList<ImportedRigJointFrame> SourceJoints,
    IReadOnlyList<CanonicalRigJointFrame> CanonicalJoints,
    IReadOnlyList<string> IgnoredSourceJoints,
    IReadOnlyList<string> Limitations);

public sealed record ReferenceRigAdoptionEvidence(int JointCount, double BindReconstructionMaximumMm,
    double MaximumSourceCenterDeltaMm, double MaximumSourceOrientationDeltaDegrees,
    double MaximumSymmetryCenterResidualMm, string ArtifactSha256);

public sealed record AntoniaReferenceRigAdoption(HumanoidSurface Surface, HumanoidSkeleton Skeleton,
    ReferenceRigArtifact Artifact, ReferenceRigAdoptionEvidence Evidence);

/// <summary>Source-name-independent validation and lowering for reviewed reference-rig artifacts.</summary>
public static class CanonicalReferenceRigAdapter
{
    public static readonly IReadOnlySet<HumanoidJointKind> JointDomain = Enum.GetValues<HumanoidJointKind>()
        .Where(kind => kind != HumanoidJointKind.Jaw).ToHashSet();

    public static ReferenceRigArtifact Load(string path, string expectedTopologyId)
    {
        var artifact = JsonSerializer.Deserialize<ReferenceRigArtifact>(File.ReadAllText(path), HumanoidArtifactIO.JsonOptions)
            ?? throw new InvalidDataException("Missing reference-rig artifact.");
        Validate(artifact, expectedTopologyId);
        return artifact;
    }

    public static void Validate(ReferenceRigArtifact artifact, string expectedTopologyId)
    {
        var mappedDomain = artifact.CanonicalJoints.Select(mapping => mapping.CanonicalJoint).ToHashSet();
        if (artifact.Schema != "aetheris.humanoid.reference-rig.v1" || artifact.TopologyId != expectedTopologyId ||
            artifact.CanonicalJoints.Count != JointDomain.Count || !mappedDomain.SetEquals(JointDomain))
            throw new InvalidDataException("HUM500: Reference-rig artifact schema, topology, or exact joint domain mismatch.");
        var sources = artifact.SourceJoints.ToDictionary(source => source.SourceJointId, StringComparer.Ordinal);
        if (artifact.SourceJoints.Any(source => source.ParentSourceJointId is { } parent && !sources.ContainsKey(parent)))
            throw new InvalidDataException("HUM500: Reference-rig source hierarchy contains a missing parent.");
        foreach (var source in artifact.SourceJoints)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            for (var current = source; current.ParentSourceJointId is { } parent; current = sources[parent])
                if (!visited.Add(current.SourceJointId)) throw new InvalidDataException("HUM500: Reference-rig source hierarchy contains a cycle.");
        }
        var mappedSources = artifact.CanonicalJoints.Where(mapping => mapping.SourceJointId is not null).Select(mapping => mapping.SourceJointId!).ToArray();
        if (mappedSources.Distinct(StringComparer.Ordinal).Count() != mappedSources.Length || mappedSources.Any(id => !sources.ContainsKey(id)) ||
            artifact.CanonicalJoints.Count(mapping => mapping.SourceJointId is null) != 1 ||
            artifact.CanonicalJoints.Single(mapping => mapping.SourceJointId is null).CanonicalJoint != HumanoidJointKind.Root ||
            artifact.CanonicalJoints.SelectMany(mapping => mapping.CollapsedSourceHelpers).Any(helper => !sources.ContainsKey(helper)))
            throw new InvalidDataException("HUM501: Reference-rig source mapping is missing, duplicated, or ambiguously synthetic.");
        foreach (var mapping in artifact.CanonicalJoints.Where(mapping => mapping.SourceJointId is not null))
            if (!HumanoidKinematicSolver.Near(mapping.GlobalRestCanonical, sources[mapping.SourceJointId!].GlobalRestCanonical))
                throw new InvalidDataException("HUM502: Canonical/source rest-frame evidence disagrees for " + mapping.CanonicalJoint);
    }

    public static HumanoidSkeleton BuildSkeleton(ReferenceRigArtifact artifact, string artifactSha256, string adapterId)
    {
        Validate(artifact, artifact.TopologyId);
        var mappings = artifact.CanonicalJoints.ToDictionary(mapping => mapping.CanonicalJoint);
        var joints = new List<HumanoidJoint>();
        foreach (var kind in artifact.CanonicalJoints.Select(mapping => mapping.CanonicalJoint))
        {
            var mapping = mappings[kind];
            int? parentIndex = mapping.CanonicalParent is { } parent ? joints.FindIndex(joint => joint.Kind == parent) : null;
            if (mapping.CanonicalParent is not null && parentIndex < 0)
                throw new InvalidDataException("HUM501: Canonical parent must precede child for " + kind);
            var global = mapping.GlobalRestCanonical;
            var local = parentIndex is int parentId ? global * joints[parentId].InverseBind : global;
            if (!Matrix4x4.Decompose(local, out var scale, out var rotation, out var translation) ||
                Vector3.Distance(scale, Vector3.One) > 1e-4f || !Finite(rotation) || !Finite(translation))
                throw new InvalidDataException("HUM502: Reference rest transform is not finite rigid data for " + kind);
            rotation = Quaternion.Normalize(rotation);
            if (!Matrix4x4.Invert(global, out var inverse))
                throw new InvalidDataException("HUM502: Singular reference bind for " + kind);
            var side = kind.ToString().StartsWith("Left") ? AnatomicalSide.Left : kind.ToString().StartsWith("Right") ? AnatomicalSide.Right : AnatomicalSide.Center;
            joints.Add(new("joint:" + kind, kind, side, parentIndex,
                new(new(translation.X, translation.Y, translation.Z), rotation), global, inverse,
                kind is HumanoidJointKind.LeftEye or HumanoidJointKind.RightEye));
        }
        return new HumanoidSkeleton(artifact.SkeletonId, artifact.RestPoseId,
            "linear-blend-skinning; System.Numerics row vectors; pWorld=pModel*InverseBind*GlobalPose; local rotation precedes child-local-to-parent rest transform",
            joints.AsReadOnly(), joints.Select(joint => joint.Kind).ToDictionary(kind => kind,
                kind => joints.Any(joint => joint.Kind == Mirror(kind)) ? Mirror(kind) : kind))
        {
            Reference = new(adapterId, artifact.SourceRigType, artifact.SourceAssetSha256,
                artifact.ExtractedFrameSha256, artifactSha256, artifact.CoordinateConversion,
                artifact.CanonicalJoints.Select(mapping => new CanonicalJointSourceEvidence(mapping.CanonicalJoint,
                    mapping.SourceJointId,
                    mapping.SourceJointId is null ? null : artifact.SourceJoints.Single(source => source.SourceJointId == mapping.SourceJointId).ParentSourceJointId,
                    mapping.MappingConfidence, mapping.SemanticRole, mapping.CollapsedSourceHelpers)).ToArray())
        };
    }

    private static HumanoidJointKind Mirror(HumanoidJointKind kind)
    {
        var name = kind.ToString();
        if (name.StartsWith("Left")) return Enum.Parse<HumanoidJointKind>("Right" + name[4..]);
        if (name.StartsWith("Right")) return Enum.Parse<HumanoidJointKind>("Left" + name[5..]);
        return kind;
    }
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Quaternion value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z) && float.IsFinite(value.W);
}

/// <summary>Maps a reviewed, hash-pinned source-frame artifact into the existing 55 semantic joints.</summary>
public static class AntoniaReferenceRigAdapter
{
    public const string AdapterId = "aetheris.humanoid.antonia-charmorph-metarig.v1";

    public static ReferenceRigArtifact Load(string path) =>
        CanonicalReferenceRigAdapter.Load(path, AntoniaSurfaceAdoption.CandidateTopologyId);

    public static AntoniaReferenceRigAdoption Adopt(AntoniaAdoptionCandidate candidate, string artifactPath)
    {
        var artifact = Load(artifactPath);
        if (candidate.SourcePoseSurface.TopologyId != artifact.TopologyId)
            throw new InvalidDataException("HUM500: Reference rig belongs to another topology.");
        var artifactHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(artifactPath)));
        var skeleton = BuildSkeleton(artifact, artifactHash);
        var joints = skeleton.Joints;
        var neutral = new HumanoidPoseState(artifact.RestPoseId, [], HumanoidTransform.Identity);
        var reconstructed = HumanoidPosing.EvaluateVertices(candidate.SourcePoseSurface, skeleton, neutral);
        var bindError = reconstructed.Select((position, index) => (position - candidate.SourcePoseSurface.Vertices[index].Position).Length).Max();
        var symmetry = joints.Where(joint => joint.Side == AnatomicalSide.Left).Select(joint =>
        {
            var mirror = joints.Single(other => other.Kind == Mirror(joint.Kind));
            var left = Position(joint.GlobalBind); var right = Position(mirror.GlobalBind);
            return Vector3.Distance(new(-left.X, left.Y, left.Z), right);
        }).DefaultIfEmpty().Max();
        var evidence = new ReferenceRigAdoptionEvidence(joints.Count, bindError,
            artifact.CanonicalJoints.Max(mapping => mapping.SourceCenterDeltaMm),
            artifact.CanonicalJoints.Max(mapping => mapping.SourceOrientationDeltaDegrees), symmetry, artifactHash);
        if (bindError > .001 || symmetry > .001)
            throw new InvalidDataException("HUM503: Reference rig failed neutral bind or symmetry qualification.");
        return new(candidate.SourcePoseSurface, skeleton, artifact, evidence);
    }

    /// <summary>Builds the runtime skeleton from an already admitted artifact without any Blender dependency.</summary>
    public static HumanoidSkeleton BuildSkeleton(ReferenceRigArtifact artifact, string artifactSha256)
    {
        return CanonicalReferenceRigAdapter.BuildSkeleton(artifact, artifactSha256, AdapterId);
    }

    private static HumanoidJointKind Mirror(HumanoidJointKind kind)
    {
        var name = kind.ToString();
        if (name.StartsWith("Left")) return Enum.Parse<HumanoidJointKind>("Right" + name[4..]);
        if (name.StartsWith("Right")) return Enum.Parse<HumanoidJointKind>("Left" + name[5..]);
        return kind;
    }
    private static Vector3 Position(Matrix4x4 matrix) => new(matrix.M41, matrix.M42, matrix.M43);
}
