using System.Numerics;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Humanoid;

public static class CanonicalAdultStandardV1
{
    public const string TopologyId = "aetheris.humanoid.adult.standard.v1";
    public const string BindingTriangulationId = "aetheris.humanoid.adult.standard.v1.binding-triangles.v1";
    public const string SkeletonId = "aetheris.humanoid.adult.skeleton.v1";
    public const string RestPoseId = CanonicalHumanoidRestPose.Id;
    public const string MeasurementPoseId = "aetheris.humanoid.adult.measurement.v1";
    public const string MeasurementProtocolVersion = "aetheris.humanoid.measurements.v1";
    public const double NeutralHeightMm = 1750d;
    public const double WeightTolerance = 1e-5;
}

public enum AnatomicalSide { Center, Left, Right }
public enum HumanoidComponentKind { OuterSkin, LeftEye, RightEye, MouthInterior }
public enum HumanoidRegionKind
{
    Head, Face, LeftEar, RightEar, Neck, Chest, Abdomen, Pelvis,
    LeftShoulder, RightShoulder, LeftUpperArm, RightUpperArm, LeftElbow, RightElbow,
    LeftForearm, RightForearm, LeftWrist, RightWrist, LeftHand, RightHand,
    LeftThumb, RightThumb, LeftIndex, RightIndex, LeftMiddle, RightMiddle, LeftRing, RightRing, LeftLittle, RightLittle,
    LeftThigh, RightThigh, LeftKnee, RightKnee, LeftShin, RightShin, LeftAnkle, RightAnkle, LeftFoot, RightFoot, LeftToes, RightToes,
    LeftEye, RightEye, MouthInterior
}
public enum MaterialRegionKind { Skin, Eyes, MouthInterior }
public enum HumanoidJointKind
{
    Root, Pelvis, SpineLower, SpineMid, Chest, Neck, Head,
    LeftClavicle, RightClavicle, LeftShoulder, RightShoulder, LeftElbow, RightElbow, LeftWrist, RightWrist,
    LeftHip, RightHip, LeftKnee, RightKnee, LeftAnkle, RightAnkle, LeftToeBase, RightToeBase,
    LeftThumbMetacarpal, LeftThumbProximal, LeftThumbDistal, RightThumbMetacarpal, RightThumbProximal, RightThumbDistal,
    LeftIndexProximal, LeftIndexIntermediate, LeftIndexDistal, RightIndexProximal, RightIndexIntermediate, RightIndexDistal,
    LeftMiddleProximal, LeftMiddleIntermediate, LeftMiddleDistal, RightMiddleProximal, RightMiddleIntermediate, RightMiddleDistal,
    LeftRingProximal, LeftRingIntermediate, LeftRingDistal, RightRingProximal, RightRingIntermediate, RightRingDistal,
    LeftLittleProximal, LeftLittleIntermediate, LeftLittleDistal, RightLittleProximal, RightLittleIntermediate, RightLittleDistal,
    LeftEye, RightEye, Jaw
}
public enum HumanoidMorphKind { MeasuredDimension, ShapeControl, PoseCorrective, Expression }
public enum HumanoidMorphId { Height, ArmLength, LegLength, ShoulderWidth, HipWidth, TorsoLength }
public enum HumanoidMeasurementId { Height, ShoulderWidth, HipWidth, ArmLength, LegLength, FootLength, HandLength, ChestDepth, WaistCircumference, ChestCircumference }
public enum HumanoidDiagnosticCode
{
    HUM001_MissingRequiredLandmark, HUM002_ReferenceHashMismatch, HUM003_FitExceededSurfaceBudget,
    HUM004_InvertedFace, HUM005_SkinWeightsInvalid, HUM006_UnresolvedJointMapping, HUM007_NonFiniteCoordinate,
    HUM008_InvalidTopology, HUM009_InvalidSymmetry, HUM010_InvalidSkeleton, HUM011_AmbiguousFrame,
    HUM012_ReferenceNotAdmitted, HUM013_MorphOutOfBounds, HUM014_InvalidBinding, HUM015_ConnectivityChanged
}
public enum HumanoidDiagnosticSeverity { Info, Warning, Error }

public sealed record HumanoidDiagnostic(HumanoidDiagnosticCode Code, HumanoidDiagnosticSeverity Severity, string Message, string? EntityId = null);
public sealed record HumanoidMetadata(string Id, string Title, string AdultDomainId, string Description);
public sealed record CanonicalFrame(string Unit, string Handedness, Vector3D AnatomicalRight, Vector3D Forward, Vector3D Up, Point3D Origin, double NormalizedHeightMm);
public sealed record HumanoidVertex(string Id, Point3D Position, HumanoidRegionKind Region, int SymmetryPartnerIndex);
public sealed record HumanoidFace(string Id, int A, int B, int C, HumanoidRegionKind Region, int? SymmetryPartnerIndex);
public sealed record BindingTriangle(string Id, int FaceIndex, int A, int B, int C);
public sealed record HumanoidComponent(string Id, HumanoidComponentKind Kind, IReadOnlyList<int> FaceIndices, IReadOnlyList<string> DeclaredOpenings);
public sealed record HumanoidUv(double U, double V);
public sealed record JointWeight(int JointIndex, double Weight);
public sealed record VertexSkinWeights(int VertexIndex, IReadOnlyList<JointWeight> Weights);
public sealed record HumanoidSurface(
    string TopologyId,
    string BindingTriangulationId,
    string ConnectivityHash,
    IReadOnlyList<HumanoidVertex> Vertices,
    IReadOnlyList<HumanoidFace> Faces,
    IReadOnlyList<BindingTriangle> BindingTriangles,
    IReadOnlyList<HumanoidUv>? Uvs,
    IReadOnlyList<VertexSkinWeights> SkinWeights,
    IReadOnlyList<HumanoidComponent> Components,
    IReadOnlyDictionary<HumanoidRegionKind, HumanoidRegionKind> RegionSymmetry);

/// <summary>System.Numerics row-vector rigid transform. Local joint transforms map child-local coordinates into the parent frame.</summary>
public readonly record struct HumanoidTransform(Point3D Translation, Quaternion Rotation)
{
    public static HumanoidTransform Identity => new(Point3D.Origin, Quaternion.Identity);
    public Matrix4x4 Matrix => Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation((float)Translation.X, (float)Translation.Y, (float)Translation.Z);
    public static HumanoidTransform FromTranslation(Point3D point) => new(point, Quaternion.Identity);
}

public sealed record HumanoidJoint(
    string Id,
    HumanoidJointKind Kind,
    AnatomicalSide Side,
    int? ParentIndex,
    HumanoidTransform LocalRest,
    Matrix4x4 GlobalBind,
    Matrix4x4 InverseBind,
    bool IsAuxiliary = false);
public sealed record CanonicalJointSourceEvidence(HumanoidJointKind CanonicalJoint, string? SourceJointId,
    string? SourceParentJointId, string MappingConfidence, string SemanticRole, IReadOnlyList<string> CollapsedSourceHelpers);
public sealed record CanonicalSkeletonReference(string AdapterId, string SourceRigType, string SourceAssetSha256,
    string ExtractedFrameSha256, string MappedArtifactSha256, string CoordinateConversion,
    IReadOnlyList<CanonicalJointSourceEvidence> JointMappings);
public sealed record HumanoidSkeleton(string SkeletonId, string RestPoseId, string SkinningConvention, IReadOnlyList<HumanoidJoint> Joints, IReadOnlyDictionary<HumanoidJointKind, HumanoidJointKind> Symmetry)
{
    public CanonicalSkeletonReference? Reference { get; init; }
    public HumanoidJoint GetJoint(HumanoidJointKind kind) => Joints.Single(joint => joint.Kind == kind);
    public HumanoidTransform GetRestFrame(HumanoidJointKind kind) => GetJoint(kind).LocalRest;
}

public sealed record SurfaceBinding(string TopologyId, string FaceId, int TriangleWithinFace, double BarycentricA, double BarycentricB, double BarycentricC);
public sealed record JointBinding(string SkeletonId, HumanoidJointKind Joint, Vector3D LocalOffset);
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SurfaceLandmark), "surface")]
[JsonDerivedType(typeof(JointLandmark), "joint")]
[JsonDerivedType(typeof(MeasurementLandmark), "measurement")]
[JsonDerivedType(typeof(AttachmentLandmark), "attachment")]
public abstract record HumanoidLandmark(string Id, string PlacementProtocol, double Confidence, string? SymmetryPartnerId, string Provenance, bool Reviewed);
public sealed record SurfaceLandmark(string Id, SurfaceBinding Binding, string PlacementProtocol, double Confidence, string? SymmetryPartnerId, string Provenance, bool Reviewed)
    : HumanoidLandmark(Id, PlacementProtocol, Confidence, SymmetryPartnerId, Provenance, Reviewed);
public sealed record JointLandmark(string Id, JointBinding Binding, string PlacementProtocol, double Confidence, string? SymmetryPartnerId, string Provenance, bool Reviewed)
    : HumanoidLandmark(Id, PlacementProtocol, Confidence, SymmetryPartnerId, Provenance, Reviewed);
public sealed record MeasurementLandmark(string Id, string OperandId, string ProtocolId, string PlacementProtocol, double Confidence, string? SymmetryPartnerId, string Provenance, bool Reviewed)
    : HumanoidLandmark(Id, PlacementProtocol, Confidence, SymmetryPartnerId, Provenance, Reviewed);
public sealed record AttachmentLandmark(string Id, string SiteId, string PlacementProtocol, double Confidence, string? SymmetryPartnerId, string Provenance, bool Reviewed)
    : HumanoidLandmark(Id, PlacementProtocol, Confidence, SymmetryPartnerId, Provenance, Reviewed);

public enum MeasurementEvaluatorKind { VerticalHeight, StraightDistance, JointChainLength, ProjectedExtent, ClosedSectionCircumference }
public sealed record MeasurementProtocol(string ProtocolId, HumanoidMeasurementId Measurement, string PoseId, string Unit, MeasurementEvaluatorKind Evaluator, IReadOnlyList<string> Operands, string Convention);
public sealed record HumanoidMeasurement(HumanoidMeasurementId Measurement, double? ValueMm, string ProtocolId, string PoseId, bool IsValid, string Validity);
public sealed record HumanoidMorphChannel(HumanoidMorphId Id, HumanoidMorphKind Kind, double Minimum, double Default, double Maximum, string Unit, IReadOnlyList<HumanoidRegionKind> AffectedRegions, string Implementation, string Validation);

public sealed record AttachmentFrame(Point3D Position, Vector3D Tangent, Vector3D Normal, Vector3D Binormal);
public sealed record HumanoidAttachmentSite(string Id, SurfaceBinding PositionBinding, AttachmentFrame NeutralFrame, HumanoidJointKind FollowJoint, string OffsetPolicy);
public sealed record JointPose(HumanoidJointKind Joint, Quaternion LocalRotation);
public sealed record HumanoidPoseState(string PoseId, IReadOnlyList<JointPose> LocalRotations, HumanoidTransform RootTransform);
public sealed record HumanoidProvenanceItem(string Id, string Role, string Source, string Revision, string Sha256, string License, string Use, bool Admitted, string Notes);
public sealed record HumanoidProvenance(string Schema, IReadOnlyList<HumanoidProvenanceItem> Inputs, IReadOnlyList<string> Transformations, string GeneratorVersion, string ConfigHash, string OutputHash, string AdmissionStatus, IReadOnlyList<string> ManualReview);

public sealed record ImportedHumanoid(
    string Id,
    string SourceTopologyId,
    CanonicalFrame SourceFrame,
    string SourcePoseId,
    IReadOnlyList<Point3D> Positions,
    IReadOnlyList<HumanoidFace> Faces,
    IReadOnlyList<HumanoidJoint> SourceRig,
    HumanoidProvenance Provenance,
    IReadOnlyList<HumanoidDiagnostic> Diagnostics);

public sealed record CanonicalHumanoid(
    string SchemaVersion,
    HumanoidMetadata Metadata,
    CanonicalFrame Frame,
    HumanoidSurface Surface,
    HumanoidSkeleton Skeleton,
    IReadOnlyList<HumanoidLandmark> Landmarks,
    IReadOnlyList<MeasurementProtocol> MeasurementProtocols,
    IReadOnlyList<HumanoidMorphChannel> MorphChannels,
    IReadOnlyDictionary<HumanoidRegionKind, MaterialRegionKind> MaterialRegions,
    IReadOnlyList<HumanoidAttachmentSite> Attachments,
    HumanoidPoseState PoseState,
    HumanoidProvenance Provenance,
    long ShapeRevision = 0);

public sealed record HumanoidValidationResult(bool IsValid, IReadOnlyList<HumanoidDiagnostic> Diagnostics);
