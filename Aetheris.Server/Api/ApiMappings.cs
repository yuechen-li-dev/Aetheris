using Aetheris.Kernel.Core.Brep.Picking;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Diagnostics;
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
        => new(
            result.FacePatches.Select(p => new FacePatchDto(
                p.FaceId.Value,
                p.Positions.Select(ToPointDto).ToArray(),
                p.Normals.Select(ToVectorDto).ToArray(),
                p.TriangleIndices.ToArray())).ToArray(),
            result.EdgePolylines.Select(e => new EdgePolylineDto(
                e.EdgeId.Value,
                e.Points.Select(ToPointDto).ToArray(),
                e.IsClosed)).ToArray());

    public static PickResponseDto ToPickResponse(IReadOnlyList<PickHit> hits)
        => new(hits.Select(h => new PickHitDto(
            h.T,
            ToPointDto(h.Point),
            h.Normal is null ? null : ToVectorDto(h.Normal.Value.ToVector()),
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
}
