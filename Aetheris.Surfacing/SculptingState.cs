using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Surfacing;

public readonly record struct BodyStateId(string Value)
{
    public override string ToString() => Value;
    public static BodyStateId Derive(string canonical)
        => new("state-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant()[..20]);
}

public enum SculptEntityKind { Body, Region, Interface, Pattern, Datum, Surface }
public enum PreservationMode { ExactGeometry, SemanticIdentity, PatternPlacementAndDiameter }
public enum SculptRequirement { ClosedManifold, OrientationConsistency, NoSelfIntersection }
public enum GeometricChangeKind { Preserved, Replaced, Introduced, Removed }
public enum LocalityEvidenceLevel { ExactSemantic, ExactAnalytic, CertifiedBounded, SampledConservative }

public sealed record SculptSemanticEntity(string StableId, SculptEntityKind Kind, string GeometryFingerprint, string Description);
public sealed record PreservationContract(string EntityId, PreservationMode Mode);
public sealed record SpatialInfluenceEnvelope(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    public bool Contains(SpatialInfluenceEnvelope other, double tolerance) =>
        MinX <= other.MinX + tolerance && MinY <= other.MinY + tolerance && MinZ <= other.MinZ + tolerance
        && MaxX >= other.MaxX - tolerance && MaxY >= other.MaxY - tolerance && MaxZ >= other.MaxZ - tolerance;
}

public sealed record GeometricDeltaEntry(string InputEntity, GeometricChangeKind Change, IReadOnlyList<string> OutputEntities, string Evidence);
public sealed record GeometricDelta(
    BodyStateId InputState,
    BodyStateId OutputState,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> Preserves,
    IReadOnlyList<string> Replaces,
    IReadOnlyList<string> Removes,
    IReadOnlyList<string> Introduces,
    IReadOnlyList<string> AuthorizedRegion,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<GeometricDeltaEntry> Correspondence,
    BlendJudgmentProvenance? BlendJudgment = null);

public sealed record SculptValidationEvidence(
    string Check,
    bool Satisfied,
    LocalityEvidenceLevel Level,
    double? MaximumObservedDeviation,
    double Tolerance,
    string Detail);

public enum PersistentAssociationState { Preserved, ReplacedBy, Removed }
public sealed record PersistentGeometryAssociation(string SemanticTarget, PersistentAssociationState State, IReadOnlyList<int> FaceIds, string Evidence);
public sealed record PersistentAssociationRemapResult(bool IsSuccess, IReadOnlyList<PersistentGeometryAssociation> Associations, IReadOnlyList<SculptDiagnostic> Diagnostics);
public sealed record SculptAssemblyInterface(string StableId, string SemanticTarget, IReadOnlyList<int> FaceIds, string Description);

public sealed record HousingHole(string StableId, double CenterX, double CenterY, double Diameter);
public sealed record HousingConstruction(
    double Width,
    double Depth,
    double BaseHeight,
    double CrownWidth,
    double CrownDepth,
    double CrownOffset,
    IReadOnlyList<HousingHole> Holes,
    BoundedSurfacePatch? ReplacementPatch = null)
{
    public double FinalHeight => BaseHeight + CrownOffset;
    public bool HasCrown => CrownOffset > 0d && (CrownWidth < Width || CrownDepth < Depth);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$baseKind")]
[JsonDerivedType(typeof(HousingBaseConstruction), "Housing")]
public interface IBaseConstruction
{
    string BaseId { get; }
    int SchemaVersion { get; }
    string BaseKind { get; }
}

/// <summary>The existing housing recipe as one admitted generalized construction base.</summary>
public sealed record HousingBaseConstruction(string BaseId, HousingConstruction Housing, int SchemaVersion = 1) : IBaseConstruction
{
    public string BaseKind => "Housing";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$operationKind")]
[JsonDerivedType(typeof(OffsetRegionOperation), "OffsetRegion")]
[JsonDerivedType(typeof(ReplaceRegionOperation), "ReplaceRegion")]
[JsonDerivedType(typeof(SafeHoleOperation), "HoleFeature")]
[JsonDerivedType(typeof(BlendBoundaryOperation), "BlendBoundary")]
[JsonDerivedType(typeof(AddSectionChainOperation), "AddSectionChain")]
[JsonDerivedType(typeof(RemoveSectionChainOperation), "RemoveSectionChain")]
public interface IConstructionOperation
{
    string StableId { get; }
    string OperationKind { get; }
    int SchemaVersion { get; }
    IReadOnlyList<string> Reads { get; }
    IReadOnlyList<string> MayModifySet { get; }
    SpatialInfluenceEnvelope AuthorizedEnvelope { get; }
    IReadOnlyList<PreservationContract> PreservationContracts { get; }
}

public enum ConstructionReplayStatus { AuthoredAndValidated, ReplayedAndValidated }

/// <summary>
/// Durable typed operation payload plus its geometric-SSA relationship and accepted evidence.
/// The payload, rather than a Boolean opcode or final faces, is the replay authority.
/// </summary>
public sealed record ConstructionOperationState(
    string OperationId,
    string OperationKind,
    int PayloadVersion,
    BodyStateId PredecessorStateId,
    BodyStateId OutputStateId,
    string OutputAuthoredName,
    IConstructionOperation Payload,
    IReadOnlyList<string> Reads,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope AuthorizedRegion,
    IReadOnlyList<PreservationContract> Preserves,
    GeometricDelta Delta,
    IReadOnlyList<SculptValidationEvidence> ValidationEvidence,
    ConstructionReplayStatus ReplayStatus = ConstructionReplayStatus.AuthoredAndValidated)
{
    public static ConstructionOperationState Accepted(BodyState input, string outputName, IConstructionOperation payload, BodyStateId outputStateId,
        GeometricDelta delta, IReadOnlyList<SculptValidationEvidence> evidence, ConstructionReplayStatus status = ConstructionReplayStatus.AuthoredAndValidated)
        => new(payload.StableId, payload.OperationKind, payload.SchemaVersion, input.StateId, outputStateId, outputName, payload,
            payload.Reads, payload.MayModifySet, payload.AuthorizedEnvelope, payload.PreservationContracts, delta, evidence, status);
}

public sealed record ConstructionState(
    string SchemaId,
    int SchemaVersion,
    IBaseConstruction Base,
    IReadOnlyList<ConstructionOperationState> Operations)
{
    public const string CurrentSchemaId = "aetheris.surfacing.construction-state";
    public const int CurrentSchemaVersion = 1;

    public static ConstructionState FromHousing(HousingConstruction housing, string baseId = "housing-base")
        => new(CurrentSchemaId, CurrentSchemaVersion, new HousingBaseConstruction(baseId, housing), []);

    public ConstructionState Append(ConstructionOperationState operation) => this with { Operations = [.. Operations, operation] };
}

public sealed record BodyState(
    BodyStateId StateId,
    BodyStateId? PredecessorStateId,
    string BodyStableId,
    string AuthoredName,
    BrepBody Body,
    HousingConstruction Construction,
    IReadOnlyDictionary<string, SculptSemanticEntity> SemanticInventory,
    GeometricDelta? Delta,
    IReadOnlyList<SculptValidationEvidence> ValidationEvidence,
    IReadOnlyList<PersistentGeometryAssociation>? GeometryAssociations = null,
    IReadOnlyList<Step242SemanticPmi>? SemanticPmi = null,
    IReadOnlyList<SculptAssemblyInterface>? AssemblyInterfaces = null,
    BlendJudgmentTrace? BlendJudgment = null,
    ConstructionState? ConstructionAuthority = null)
{
    public IReadOnlyList<SurfacePatchMetadata> SurfacePatches => Construction.ReplacementPatch is { } patch
        ? [SurfacePatchMetadata.From(patch, Body.Topology.Faces.FirstOrDefault(face =>
            Body.TryGetFaceSurfaceGeometry(face.Id, out var support) && Equals(support, patch.Support))?.LoopIds.Count ?? 1)] : [];
}

public sealed record OffsetRegionOperation(
    string StableId,
    string TargetRegion,
    double Offset,
    double RegionWidth,
    double RegionDepth,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements,
    string BoundaryContinuity = "G0") : IConstructionOperation
{
    public string OperationKind => "OffsetRegion";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [TargetRegion, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => MayModify;
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public string Canonical => string.Join('|', StableId, TargetRegion, Offset.ToString("R"), RegionWidth.ToString("R"), RegionDepth.ToString("R"),
        string.Join(',', MayModify.Order(StringComparer.Ordinal)),
        $"{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}",
        string.Join(',', Preserves.OrderBy(x => x.EntityId, StringComparer.Ordinal).Select(x => $"{x.EntityId}:{x.Mode}")),
        string.Join(',', Requirements.Order()), BoundaryContinuity);
}

public sealed record ReplaceRegionOperation(
    string StableId,
    string TargetRegion,
    BoundedSurfacePatch ReplacementPatch,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements,
    double GeometricTolerance = 1e-6,
    double G1AngularToleranceDegrees = 0.1,
    double G2CurvatureTolerance = 1e-6) : IConstructionOperation
{
    public string OperationKind => "ReplaceRegion";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [TargetRegion, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => MayModify;
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public string Canonical => string.Join('|', StableId, TargetRegion, ReplacementPatch.PatchId,
        ReplacementPatch.SurfaceClass, ReplacementPatch.DegreeU, ReplacementPatch.DegreeV,
        ReplacementPatch.ControlCountU, ReplacementPatch.ControlCountV,
        $"{ReplacementPatch.ParameterDomain.UMin:R},{ReplacementPatch.ParameterDomain.UMax:R},{ReplacementPatch.ParameterDomain.VMin:R},{ReplacementPatch.ParameterDomain.VMax:R}",
        string.Join(';', ReplacementPatch.BoundaryLoop.Boundaries.OrderBy(x => x.PatchSide).Select(x => $"{x.PatchSide}:{x.ExistingBoundary}:{x.Continuity}")),
        string.Join(',', MayModify.Order(StringComparer.Ordinal)),
        $"{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}",
        string.Join(',', Preserves.OrderBy(x => x.EntityId, StringComparer.Ordinal).Select(x => $"{x.EntityId}:{x.Mode}")),
        string.Join(',', Requirements.Order()), GeometricTolerance.ToString("R"), G1AngularToleranceDegrees.ToString("R"), G2CurvatureTolerance.ToString("R"));
}

public sealed record SafeHoleOperation(
    string StableId,
    string TargetRegion,
    HousingHole Hole,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements) : IConstructionOperation
{
    public string OperationKind => "HoleFeature";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [TargetRegion, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => [TargetRegion];
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public string Canonical => $"{StableId}|{TargetRegion}|{Hole.StableId}|{Hole.CenterX:R}|{Hole.CenterY:R}|{Hole.Diameter:R}|{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}";
}

public enum SectionChainAttachmentPlacement { RelativeToSupport }

public sealed record SectionChainAttachment(
    string SupportRegion,
    string TerminalSectionId,
    SectionChainAttachmentPlacement Placement,
    IReadOnlyList<SectionSpanCorrespondence> BoundaryCorrespondence);

public sealed record AddSectionChainOperation(
    string StableId,
    SectionChain Chain,
    SectionChainAttachment Attachment,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements) : IConstructionOperation
{
    public string OperationKind => "AddSectionChain";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [Attachment.SupportRegion, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => MayModify;
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public string Canonical => $"{StableId}|{Chain.StableId}|{SectionChainCanonical.Fingerprint(Chain)}|{Attachment.SupportRegion}|{Attachment.TerminalSectionId}|{Attachment.Placement}|{CanonicalContract()}";
    private string CanonicalContract() => $"{string.Join(',', MayModify.Order(StringComparer.Ordinal))}|{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}|{string.Join(',', Preserves.OrderBy(item => item.EntityId, StringComparer.Ordinal).Select(item => $"{item.EntityId}:{item.Mode}"))}|{string.Join(',', Requirements.Order())}";
}

public sealed record RemoveSectionChainOperation(
    string StableId,
    SectionChain Chain,
    IReadOnlyList<string> SupportRegions,
    IReadOnlyList<string> MayModify,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements) : IConstructionOperation
{
    public string OperationKind => "RemoveSectionChain";
    public int SchemaVersion => 1;
    public IReadOnlyList<string> Reads => [.. SupportRegions, .. Preserves.Select(item => item.EntityId)];
    public IReadOnlyList<string> MayModifySet => MayModify;
    public SpatialInfluenceEnvelope AuthorizedEnvelope => InfluenceEnvelope;
    public IReadOnlyList<PreservationContract> PreservationContracts => Preserves;
    public string Canonical => $"{StableId}|{Chain.StableId}|{SectionChainCanonical.Fingerprint(Chain)}|{string.Join(',', SupportRegions)}|{string.Join(',', MayModify.Order(StringComparer.Ordinal))}|{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}|{string.Join(',', Preserves.OrderBy(item => item.EntityId, StringComparer.Ordinal).Select(item => $"{item.EntityId}:{item.Mode}"))}|{string.Join(',', Requirements.Order())}";
}

public sealed record SculptDiagnostic(string Code, string Message, string? Entity = null);
public sealed record SculptResult(bool IsSuccess, BodyState? OutputState, GeometricDelta? Delta, IReadOnlyList<SculptValidationEvidence> Evidence, IReadOnlyList<SculptDiagnostic> Diagnostics)
{
    public static SculptResult Failure(IEnumerable<SculptDiagnostic> diagnostics, IEnumerable<SculptValidationEvidence>? evidence = null)
        => new(false, null, null, evidence?.ToArray() ?? [], diagnostics.ToArray());
}
