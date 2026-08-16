namespace Aetheris.Server.Contracts;

public sealed record DiagnosticDto(string Code, string Severity, string Message, string? Source);

public sealed record ApiResponseDto<T>(bool Success, T? Data, IReadOnlyList<DiagnosticDto> Diagnostics);

public sealed record DocumentCreateRequestDto(string? Name);

public sealed record DocumentCreateResponseDto(Guid DocumentId, string? Name, bool Volatile);

public sealed record BodyOccurrenceSummaryDto(Guid OccurrenceId, Guid DefinitionId, string? Name, Vector3Dto Translation);

public sealed record DocumentSummaryResponseDto(
    Guid DocumentId,
    string? Name,
    int BodyCount,
    IReadOnlyList<Guid> BodyIds,
    int DefinitionCount,
    IReadOnlyList<BodyOccurrenceSummaryDto> Occurrences);

public sealed record BodyCreatedResponseDto(Guid DocumentId, Guid BodyId, Guid DefinitionId, int FaceCount, int EdgeCount, int VertexCount);

public sealed record Point3Dto(double X, double Y, double Z);

public sealed record Vector3Dto(double X, double Y, double Z);

public sealed record ProfilePoint2Dto(double X, double Y);

public sealed record BoxCreateRequestDto(double Width, double Height, double Depth);

public sealed record CylinderCreateRequestDto(double Radius, double Height);

public sealed record SphereCreateRequestDto(double Radius);

public sealed record TranslateBodyRequestDto(Vector3Dto Translation);

public sealed record BodyTransformedResponseDto(Guid DocumentId, Guid BodyId, Guid DefinitionId, Vector3Dto AppliedTranslation);

public sealed record CreateOccurrenceRequestDto(Guid? SourceOccurrenceId, Guid? DefinitionId, string? Name);

public sealed record OccurrenceCreatedResponseDto(Guid DocumentId, Guid BodyId, Guid DefinitionId, string? Name);

public sealed record StepExportResponseDto(Guid DocumentId, Guid DefinitionId, string StepText, string CanonicalHash, IReadOnlyList<DiagnosticDto> Diagnostics);

public sealed record StepImportRequestDto(string? StepText, string? Name);

public sealed record StepImportResponseDto(
    Guid DocumentId,
    Guid DefinitionId,
    Guid OccurrenceId,
    string? Name,
    IReadOnlyList<DiagnosticDto> Diagnostics,
    CadmataVisualizationArtifactDto? SemanticPresentation = null);

public sealed record ExtrudeRequestDto(
    IReadOnlyList<ProfilePoint2Dto> Profile,
    Point3Dto Origin,
    Vector3Dto Normal,
    Vector3Dto UAxis,
    double Depth);

public sealed record RevolveRequestDto(
    IReadOnlyList<ProfilePoint2Dto> Profile,
    Point3Dto Origin,
    Vector3Dto AxisDirection,
    Vector3Dto UAxis,
    double AngleRadians = 0d);

public sealed record BooleanRequestDto(Guid LeftBodyId, Guid RightBodyId, string Operation);

public sealed record TessellationOptionsDto(double? AngularToleranceRadians, double? ChordTolerance, int? MinimumSegments, int? MaximumSegments);

public sealed record TessellateRequestDto(TessellationOptionsDto? Options);

public sealed record TessellationResponseDto(IReadOnlyList<FacePatchDto> FacePatches, IReadOnlyList<EdgePolylineDto> EdgePolylines);

public sealed record FacePatchDto(
    int FaceId,
    IReadOnlyList<Point3Dto> Positions,
    IReadOnlyList<Vector3Dto> Normals,
    IReadOnlyList<int> TriangleIndices,
    string Source,
    string? ScaffoldRejectionReason);

public sealed record EdgePolylineDto(int EdgeId, IReadOnlyList<Point3Dto> Points, bool IsClosed);

public sealed record DisplayPrepareRequestDto(TessellationOptionsDto? TessellationOptions);

public sealed record CadmataPointDto(double X, double Y, double Z);
public sealed record CadmataGeometryDto(string Type, IReadOnlyList<CadmataPointDto>? Points = null, CadmataPointDto? Center = null, double? Radius = null, CadmataPointDto? Origin = null, CadmataPointDto? U = null, CadmataPointDto? V = null, bool Closed = false);
public sealed record CadmataVisualizationDiagnosticDto(string Code, string Message, string Severity);
public sealed record CadmataTopologyDto(IReadOnlyList<int>? FaceIds = null, IReadOnlyList<int>? EdgeIds = null, IReadOnlyList<int>? LoopIds = null, IReadOnlyList<int>? VertexIds = null, IReadOnlyList<int>? DirectedEdgeIds = null);
public sealed record CadmataVisualizationEntityDto(string StableId, string Kind, string Label, string Layer, string? Role, CadmataGeometryDto? Geometry, string? SourceSpan, IReadOnlyList<string>? ParentIds, IReadOnlyList<string>? ChildIds, IReadOnlyList<string>? ConstructionDescendantIds, IReadOnlyList<string>? MaterializedDescendantIds, CadmataTopologyDto? Topology, IReadOnlyList<string>? SelectionIds, string? Consumer, IReadOnlyList<CadmataVisualizationDiagnosticDto>? Diagnostics, IReadOnlyDictionary<string, string>? Metadata);
public sealed record CadmataVisualizationSelectionDto(string StableId, string Label, string Kind, IReadOnlyList<string> EntityIds, IReadOnlyList<string>? OrderedEntityIds, bool Closed, IReadOnlyList<CadmataVisualizationDiagnosticDto>? Diagnostics);
public sealed record CadmataVisualizationArtifactDto(string SchemaVersion, string FixtureId, string SourcePath, IReadOnlyList<CadmataVisualizationEntityDto> Entities, IReadOnlyList<CadmataVisualizationSelectionDto> Selections, IReadOnlyList<CadmataVisualizationDiagnosticDto> Diagnostics, IReadOnlyDictionary<string, double> Metrics);
public sealed record CadmataFixtureLoadResponseDto(string DocumentId, string BodyId, string DefinitionId, string FixtureId, CadmataVisualizationArtifactDto Visualization);

public sealed record AnalyticDisplayFaceDomainHintDto(double? MinU, double? MaxU, double? MinV, double? MaxV);

public sealed record AnalyticDisplayPlaneGeometryDto(
    Point3Dto Origin,
    Vector3Dto Normal,
    Vector3Dto UAxis,
    Vector3Dto VAxis,
    IReadOnlyList<Point3Dto>? OuterBoundary);

public sealed record AnalyticDisplayCylinderGeometryDto(
    Point3Dto Origin,
    Vector3Dto Axis,
    Vector3Dto XAxis,
    Vector3Dto YAxis,
    double Radius);

public sealed record AnalyticDisplayConeGeometryDto(
    Point3Dto Apex,
    Vector3Dto Axis,
    Vector3Dto XAxis,
    Vector3Dto YAxis,
    double SemiAngleRadians);

public sealed record AnalyticDisplaySphereGeometryDto(
    Point3Dto Center,
    Vector3Dto Axis,
    Vector3Dto XAxis,
    Vector3Dto YAxis,
    double Radius);

public sealed record AnalyticDisplayTorusGeometryDto(
    Point3Dto Center,
    Vector3Dto Axis,
    Vector3Dto XAxis,
    Vector3Dto YAxis,
    double MajorRadius,
    double MinorRadius);

public sealed record AnalyticDisplayFaceDto(
    int FaceId,
    int ShellId,
    string ShellRole,
    int SurfaceGeometryId,
    string SurfaceKind,
    int LoopCount,
    AnalyticDisplayFaceDomainHintDto? DomainHint,
    AnalyticDisplayPlaneGeometryDto? PlaneGeometry,
    AnalyticDisplayCylinderGeometryDto? CylinderGeometry,
    AnalyticDisplayConeGeometryDto? ConeGeometry,
    AnalyticDisplaySphereGeometryDto? SphereGeometry,
    AnalyticDisplayTorusGeometryDto? TorusGeometry);

public sealed record AnalyticDisplayFallbackFaceDto(
    int FaceId,
    int ShellId,
    string ShellRole,
    string Reason,
    string? SurfaceKind,
    string? Detail);

public sealed record AnalyticDisplayPacketDto(
    int BodyId,
    IReadOnlyList<AnalyticDisplayFaceDto> AnalyticFaces,
    IReadOnlyList<AnalyticDisplayFallbackFaceDto> FallbackFaces);

public sealed record DisplayPreparationResponseDto(
    string Lane,
    AnalyticDisplayPacketDto AnalyticPacket,
    TessellationResponseDto? TessellationFallback,
    string Status = "Complete",
    string SourceAuthority = "BRep",
    string DisplayAuthority = "DisplayIR",
    IReadOnlyList<string>? Lanes = null,
    IReadOnlyList<DisplayFaceDto>? Faces = null,
    IReadOnlyList<DisplayDiagnosticDto>? Diagnostics = null,
    IReadOnlyList<DisplayLaneDto>? DisplayLanes = null);

public sealed record DisplayFaceDto(
    int FaceId,
    int? ShellId,
    string? SurfaceKind,
    string Status,
    string PatchKind,
    FacePatchDto? MeshPatch,
    AnalyticDisplayFaceDto? AnalyticPatch,
    DisplayWirePatchDto? WirePatch,
    string? MaterializationLane,
    IReadOnlyList<DisplayDiagnosticDto> Diagnostics);

public sealed record DisplayWirePatchDto(
    string Kind,
    string Source,
    string Quality,
    IReadOnlyList<DisplayLoopDto> Loops);

public sealed record DisplayLoopDto(
    int LoopId,
    string Role,
    IReadOnlyList<DisplayEdgeDto> Edges);

public sealed record DisplayEdgeDto(
    int EdgeId,
    IReadOnlyList<Point3Dto> Points,
    string SourceCurveKind,
    int SampleCount,
    IReadOnlyList<DisplayDiagnosticDto> Diagnostics);

public sealed record DisplayLaneDto(
    string Kind,
    string Status,
    string Source,
    string DisplayAuthority,
    string? Implementation,
    string? Quality,
    int? TimeoutMs,
    int FaceCount,
    int DiagnosticCount);

public sealed record DisplayDiagnosticDto(
    string Code,
    string Message,
    int? FaceId,
    string? SurfaceKind,
    string? Phase,
    string? SuggestedNextAction);

public sealed record AssemblyDisplayRequestDto(string Path);
public sealed record AssemblyDisplayDefinitionDto(string StableId, string DefinitionIdentity, IReadOnlyList<FacePatchDto> FacePatches);
public sealed record AssemblyDisplayPublicSemanticDto(string Name, string Type, IReadOnlyList<string> Capabilities, IReadOnlyList<string> BindingKinds, string? InternalImplementationPath);
public sealed record AssemblyDisplayModuleDefinitionDto(string StableId, string DefinitionIdentity, string TemplateName, string SpecializationIdentity, IReadOnlyList<string> Provenance, IReadOnlyList<AssemblyDisplayPublicSemanticDto> PublicSemantics, double LocalSolveMilliseconds);
public sealed record AssemblyDisplayOccurrenceDto(string StableId, string Name, string InstancePath, string? ParentStableId, string? DefinitionStableId, string Kind, IReadOnlyList<double> WorldTransform, string PlacementAuthority, IReadOnlyList<string>? SelectionMembers = null);
public sealed record AssemblyDisplayMateDto(string StableId, string Name, string InterfaceStableId, IReadOnlyList<string> Participants, IReadOnlyList<string> ConstraintIds, string ValidationStatus);
public sealed record AssemblyDisplayToleranceDto(string Name, bool Passed, double Nominal, double Minimum, double Maximum, string Unit, IReadOnlyList<string> Contributors, IReadOnlyList<string>? ExpandedContributors = null);
public sealed record AssemblyDisplayBoundsDto(IReadOnlyList<double> Minimum, IReadOnlyList<double> Maximum);
public sealed record AssemblyDisplayPacketDto(string Schema, string Name, string RootOccurrenceStableId, IReadOnlyList<AssemblyDisplayDefinitionDto> Definitions, IReadOnlyList<AssemblyDisplayOccurrenceDto> Occurrences, IReadOnlyList<AssemblyDisplayMateDto> Mates, IReadOnlyList<AssemblyDisplayToleranceDto> ToleranceStackups, AssemblyDisplayBoundsDto Bounds, IReadOnlyList<DisplayDiagnosticDto> Diagnostics, IReadOnlyDictionary<string, double> Performance, IReadOnlyList<AssemblyDisplayModuleDefinitionDto>? ModuleDefinitions = null);

public sealed record PickOptionsDto(bool? NearestOnly, bool? IncludeBackfaces, double? EdgeTolerance, double? SortTieTolerance, double? MaxDistance);

public sealed record PickRequestDto(Point3Dto Origin, Vector3Dto Direction, TessellationOptionsDto? TessellationOptions, PickOptionsDto? PickOptions);

public sealed record PickResponseDto(IReadOnlyList<PickHitDto> Hits);

public sealed record PickHitDto(
    Guid OccurrenceId,
    double T,
    Point3Dto Point,
    Vector3Dto? Normal,
    string EntityKind,
    int? FaceId,
    int? EdgeId,
    int? BodyId,
    int? SourcePatchIndex,
    int? SourcePrimitiveIndex);
