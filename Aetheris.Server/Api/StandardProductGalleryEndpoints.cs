using System.Text.Json;
using Aetheris.Forge.Host;
using Aetheris.Server.Contracts;

namespace Aetheris.Server.Api;

public sealed record GalleryInvocationRequest(
    IReadOnlyDictionary<string, JsonElement>? Arguments,
    IReadOnlyList<ForgeArtifactKind>? Artifacts);

public sealed record GalleryArtifactContent(
    ForgeArtifactKind Kind,
    string Name,
    string ContentType,
    long Size,
    string Sha256,
    string Content);

public sealed record GalleryInvocationResponse(
    bool Success,
    ForgeInvocationIdentity Identity,
    IReadOnlyList<ForgeProtocolDiagnostic> Diagnostics,
    IReadOnlyList<GalleryArtifactContent> Artifacts,
    double ExecutionMilliseconds);

public static class StandardProductGalleryEndpoints
{
    public static void MapStandardProductGalleryApi(this WebApplication app)
    {
        app.MapGet("/api/v1/gallery/templates", () =>
        {
            var host = new ForgeProtocolHost();
            var descriptions = host.ListTemplates().Templates
                .Where(item => item.Id.StartsWith("Standard.Products.", StringComparison.Ordinal)
                    || item.Id == "Standard.SheetMetal.ElectronicsEnclosure")
                .Select(item => host.DescribeTemplate(item.Id)!)
                .ToArray();
            return ApiMappings.Ok(descriptions);
        });

        app.MapPost("/api/v1/gallery/templates/{**templateId}", (string templateId, GalleryInvocationRequest request) =>
        {
            var output = Path.Combine(Path.GetTempPath(), "aetheris-gallery-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = new ForgeProtocolHost().InvokeTemplate(templateId,
                    new ForgeTemplateInvocationRequest(ForgeHostProtocol.Version, request.Arguments, request.Artifacts), output);
                var artifacts = result.Artifacts.Select(artifact =>
                {
                    var path = Path.Combine(output, artifact.Path);
                    return new GalleryArtifactContent(artifact.Kind, artifact.Name, artifact.ContentType,
                        artifact.Size, artifact.Sha256, File.ReadAllText(path));
                }).ToArray();
                var response = new GalleryInvocationResponse(result.Success, result.Identity, result.Diagnostics,
                    artifacts, result.ExecutionMilliseconds);
                return result.Success
                    ? ApiMappings.Ok(response)
                    : Results.Json(new ApiResponseDto<GalleryInvocationResponse>(false, response,
                        result.Diagnostics.Select(item => new DiagnosticDto(item.Code, item.Severity.ToString(), item.Message,
                            item.Source ?? item.Target)).ToArray()),
                        statusCode: StatusCodes.Status422UnprocessableEntity);
            }
            finally
            {
                if (Directory.Exists(output)) Directory.Delete(output, true);
            }
        });
    }
}
