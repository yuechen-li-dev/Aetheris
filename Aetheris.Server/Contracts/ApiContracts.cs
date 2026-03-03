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

public sealed record StepImportResponseDto(Guid DocumentId, Guid DefinitionId, Guid OccurrenceId, string? Name, IReadOnlyList<DiagnosticDto> Diagnostics);

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

public sealed record FacePatchDto(int FaceId, IReadOnlyList<Point3Dto> Positions, IReadOnlyList<Vector3Dto> Normals, IReadOnlyList<int> TriangleIndices);

public sealed record EdgePolylineDto(int EdgeId, IReadOnlyList<Point3Dto> Points, bool IsClosed);

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
