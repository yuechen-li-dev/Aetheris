using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Server.Contracts;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Aetheris.Server.Api;

public static class ApiMappings
{
    public static IResult Ok<T>(T data)
        => Results.Ok(new ApiResponseDto<T>(true, data, []));

    public static IResult KernelFailure(IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        var mapped = diagnostics.Select(ToDiagnostic).ToArray();
        var statusCode = ResolveStatusCode(diagnostics);
        return Results.Json(new ApiResponseDto<object>(false, null, mapped), statusCode: statusCode);
    }

    public static DiagnosticDto ToDiagnostic(KernelDiagnostic diagnostic)
        => new(diagnostic.Code.ToString(), diagnostic.Severity.ToString(), diagnostic.Message, diagnostic.Source);

    public static int ResolveStatusCode(IReadOnlyList<KernelDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return StatusCodes.Status400BadRequest;
        }

        return diagnostics.Max(static d => d.Code switch
        {
            KernelDiagnosticCode.InternalError => StatusCodes.Status500InternalServerError,
            KernelDiagnosticCode.NotImplemented => StatusCodes.Status422UnprocessableEntity,
            KernelDiagnosticCode.ValidationFailed => StatusCodes.Status422UnprocessableEntity,
            KernelDiagnosticCode.InvalidArgument => StatusCodes.Status400BadRequest,
            KernelDiagnosticCode.ToleranceConflict => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        });
    }

    public static IResult NotFound(string message, string source)
    {
        var diagnostic = new KernelDiagnostic(KernelDiagnosticCode.InvalidArgument, KernelDiagnosticSeverity.Error, message, source);
        return Results.NotFound(new ApiResponseDto<object>(false, null, [ToDiagnostic(diagnostic)]));
    }

    public static Point3Dto ToPointDto(Point3D point) => new(point.X, point.Y, point.Z);

    public static Vector3Dto ToVectorDto(Vector3D vector) => new(vector.X, vector.Y, vector.Z);

    public static TessellationResponseDto ToTessellationResponse(DisplayTessellationResult result)
        => ToTessellationResponse(result, Transform3D.Identity);

    public static TessellationResponseDto ToTessellationResponse(DisplayTessellationResult result, Transform3D transform)
        => new(
            result.FacePatches.Select(p => new FacePatchDto(
                p.FaceId.Value,
                p.Positions.Select(position => ToPointDto(transform.Apply(position))).ToArray(),
                p.Normals.Select(normal => ToVectorDto(transform.Apply(normal))).ToArray(),
                p.TriangleIndices.ToArray(),
                p.Source.ToString(),
                p.ScaffoldRejectionReason)).ToArray(),
            result.EdgePolylines.Select(e => new EdgePolylineDto(
                e.EdgeId.Value,
                e.Points.Select(point => ToPointDto(transform.Apply(point))).ToArray(),
                e.IsClosed)).ToArray());

    public static AnalyticDisplayPacketDto ToAnalyticDisplayPacketResponse(AnalyticDisplayPacket packet)
        => new(
            packet.BodyId.Value,
            packet.AnalyticFaces.Select(face => new AnalyticDisplayFaceDto(
                face.FaceId.Value,
                face.ShellId.Value,
                face.ShellRole.ToString(),
                face.SurfaceGeometryId.Value,
                face.SurfaceKind.ToString(),
                face.LoopCount,
                face.DomainHint is { } hint ? new AnalyticDisplayFaceDomainHintDto(hint.MinV, hint.MaxV) : null,
                face.SurfaceGeometry.Plane is { } plane ? new AnalyticDisplayPlaneGeometryDto(
                    ToPointDto(plane.Origin),
                    ToVectorDto(plane.Normal.ToVector()),
                    ToVectorDto(plane.UAxis.ToVector()),
                    ToVectorDto(plane.VAxis.ToVector()),
                    face.PlanarOuterBoundary?.Select(ToPointDto).ToArray()) : null,
                face.SurfaceGeometry.Cylinder is { } cylinder ? new AnalyticDisplayCylinderGeometryDto(
                    ToPointDto(cylinder.Origin),
                    ToVectorDto(cylinder.Axis.ToVector()),
                    ToVectorDto(cylinder.XAxis.ToVector()),
                    ToVectorDto(cylinder.YAxis.ToVector()),
                    cylinder.Radius) : null,
                face.SurfaceGeometry.Cone is { } cone ? ToConeGeometryDto(cone) : null,
                face.SurfaceGeometry.Sphere is { } sphere ? new AnalyticDisplaySphereGeometryDto(
                    ToPointDto(sphere.Center),
                    ToVectorDto(sphere.Axis.ToVector()),
                    ToVectorDto(sphere.XAxis.ToVector()),
                    ToVectorDto(sphere.YAxis.ToVector()),
                    sphere.Radius) : null,
                face.SurfaceGeometry.Torus is { } torus ? new AnalyticDisplayTorusGeometryDto(
                    ToPointDto(torus.Center),
                    ToVectorDto(torus.Axis.ToVector()),
                    ToVectorDto(torus.XAxis.ToVector()),
                    ToVectorDto(torus.YAxis.ToVector()),
                    torus.MajorRadius,
                    torus.MinorRadius) : null)).ToArray(),
            packet.FallbackFaces.Select(face => new AnalyticDisplayFallbackFaceDto(
                face.FaceId.Value,
                face.ShellId.Value,
                face.ShellRole.ToString(),
                face.Reason.ToString(),
                face.SurfaceKind?.ToString(),
                face.Detail)).ToArray());

    public static PickResponseDto ToPickResponse(IReadOnlyList<PickHit> hits, Guid occurrenceId)
        => ToPickResponse(hits, Transform3D.Identity, occurrenceId);

    public static PickResponseDto ToPickResponse(IReadOnlyList<PickHit> hits, Transform3D transform, Guid occurrenceId)
        => new(hits.Select(h => new PickHitDto(
            occurrenceId,
            h.T,
            ToPointDto(transform.Apply(h.Point)),
            h.Normal is null ? null : ToVectorDto(transform.Apply(h.Normal.Value.ToVector())),
            h.EntityKind.ToString(),
            h.FaceId?.Value,
            h.EdgeId?.Value,
            h.BodyId?.Value,
            h.SourcePatchIndex,
            h.SourcePrimitiveIndex)).ToArray());

    public static BadRequest<ApiResponseDto<object>> BadRequestFromMessage(string message, string source)
        => TypedResults.BadRequest(new ApiResponseDto<object>(false, null, [new DiagnosticDto(
            KernelDiagnosticCode.InvalidArgument.ToString(),
            KernelDiagnosticSeverity.Error.ToString(),
            message,
            source)]));

    public static DisplayTessellationOptions? BuildTessellationOptions(TessellationOptionsDto? options)
    {
        if (options is null)
        {
            return null;
        }

        var baseOptions = DisplayTessellationOptions.Default;
        return new DisplayTessellationOptions(
            options.AngularToleranceRadians ?? baseOptions.AngularToleranceRadians,
            options.ChordTolerance ?? baseOptions.ChordTolerance,
            options.MinimumSegments ?? baseOptions.MinimumSegments,
            options.MaximumSegments ?? baseOptions.MaximumSegments);
    }

    public static PickQueryOptions? BuildPickOptions(PickOptionsDto? options)
    {
        if (options is null)
        {
            return null;
        }

        var defaults = PickQueryOptions.Default;
        return new PickQueryOptions(
            options.NearestOnly ?? defaults.NearestOnly,
            options.IncludeBackfaces ?? defaults.IncludeBackfaces,
            options.EdgeTolerance ?? defaults.EdgeTolerance,
            options.SortTieTolerance ?? defaults.SortTieTolerance,
            options.MaxDistance ?? defaults.MaxDistance);
    }

    private static AnalyticDisplayConeGeometryDto ToConeGeometryDto(ConeSurface cone)
    {
        var axis = cone.Axis.ToVector();
        var projectedX = cone.ReferenceAxis.ToVector() - (axis * cone.ReferenceAxis.ToVector().Dot(axis));
        var xAxis = projectedX.TryNormalize(out var normalizedX) ? normalizedX : cone.ReferenceAxis.ToVector();
        var yAxis = axis.Cross(xAxis);
        if (!yAxis.TryNormalize(out var normalizedY))
        {
            normalizedY = yAxis;
        }

        return new AnalyticDisplayConeGeometryDto(
            ToPointDto(cone.Apex),
            ToVectorDto(axis),
            ToVectorDto(xAxis),
            ToVectorDto(normalizedY),
            cone.SemiAngleRadians);
    }
}
