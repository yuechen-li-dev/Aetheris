using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;

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
    IReadOnlyList<GeometricDeltaEntry> Correspondence);

public sealed record SculptValidationEvidence(
    string Check,
    bool Satisfied,
    LocalityEvidenceLevel Level,
    double? MaximumObservedDeviation,
    double Tolerance,
    string Detail);

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

public sealed record BodyState(
    BodyStateId StateId,
    BodyStateId? PredecessorStateId,
    string BodyStableId,
    string AuthoredName,
    BrepBody Body,
    HousingConstruction Construction,
    IReadOnlyDictionary<string, SculptSemanticEntity> SemanticInventory,
    GeometricDelta? Delta,
    IReadOnlyList<SculptValidationEvidence> ValidationEvidence)
{
    public IReadOnlyList<SurfacePatchMetadata> SurfacePatches => Construction.ReplacementPatch is { } patch
        ? [SurfacePatchMetadata.From(patch)] : [];
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
    string BoundaryContinuity = "G0")
{
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
    double G1AngularToleranceDegrees = 0.1)
{
    public string Canonical => string.Join('|', StableId, TargetRegion, ReplacementPatch.PatchId,
        ReplacementPatch.SurfaceClass, ReplacementPatch.DegreeU, ReplacementPatch.DegreeV,
        ReplacementPatch.ControlCountU, ReplacementPatch.ControlCountV,
        $"{ReplacementPatch.ParameterDomain.UMin:R},{ReplacementPatch.ParameterDomain.UMax:R},{ReplacementPatch.ParameterDomain.VMin:R},{ReplacementPatch.ParameterDomain.VMax:R}",
        string.Join(';', ReplacementPatch.BoundaryLoop.Boundaries.OrderBy(x => x.PatchSide).Select(x => $"{x.PatchSide}:{x.ExistingBoundary}:{x.Continuity}")),
        string.Join(',', MayModify.Order(StringComparer.Ordinal)),
        $"{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}",
        string.Join(',', Preserves.OrderBy(x => x.EntityId, StringComparer.Ordinal).Select(x => $"{x.EntityId}:{x.Mode}")),
        string.Join(',', Requirements.Order()), GeometricTolerance.ToString("R"), G1AngularToleranceDegrees.ToString("R"));
}

public sealed record SafeHoleOperation(
    string StableId,
    string TargetRegion,
    HousingHole Hole,
    SpatialInfluenceEnvelope InfluenceEnvelope,
    IReadOnlyList<PreservationContract> Preserves,
    IReadOnlyList<SculptRequirement> Requirements)
{
    public string Canonical => $"{StableId}|{TargetRegion}|{Hole.StableId}|{Hole.CenterX:R}|{Hole.CenterY:R}|{Hole.Diameter:R}|{InfluenceEnvelope.MinX:R},{InfluenceEnvelope.MinY:R},{InfluenceEnvelope.MinZ:R},{InfluenceEnvelope.MaxX:R},{InfluenceEnvelope.MaxY:R},{InfluenceEnvelope.MaxZ:R}";
}

public sealed record SculptDiagnostic(string Code, string Message, string? Entity = null);
public sealed record SculptResult(bool IsSuccess, BodyState? OutputState, GeometricDelta? Delta, IReadOnlyList<SculptValidationEvidence> Evidence, IReadOnlyList<SculptDiagnostic> Diagnostics)
{
    public static SculptResult Failure(IEnumerable<SculptDiagnostic> diagnostics, IEnumerable<SculptValidationEvidence>? evidence = null)
        => new(false, null, null, evidence?.ToArray() ?? [], diagnostics.ToArray());
}
