using System.Net;
using System.Net.Http.Json;
using Aetheris.Server.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class KernelApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public KernelApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateDocument_CreateBox_TessellateAndPick_Succeeds()
    {
        var document = await CreateDocumentAsync();

        var boxResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{document.DocumentId}/bodies/primitives/box",
            new BoxCreateRequestDto(2, 2, 2));
        boxResponse.EnsureSuccessStatusCode();

        var boxBody = await boxResponse.Content.ReadFromJsonAsync<BodyCreatedResponseDto>();
        Assert.NotNull(boxBody);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{document.DocumentId}/bodies/{boxBody!.BodyId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();

        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<TessellationResponseDto>();
        Assert.NotNull(tessellation);
        Assert.NotEmpty(tessellation!.FacePatches);

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{document.DocumentId}/bodies/{boxBody.BodyId}/pick",
            new PickRequestDto(
                new Point3Dto(0, 0, 5),
                new Vector3Dto(0, 0, -1),
                TessellationOptions: null,
                PickOptions: new PickOptionsDto(NearestOnly: true, IncludeBackfaces: null, EdgeTolerance: null, SortTieTolerance: null, MaxDistance: null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<PickResponseDto>();
        Assert.NotNull(pick);
        Assert.Single(pick!.Hits);
    }

    [Fact]
    public async Task UnsupportedBoolean_ReturnsStructuredError()
    {
        var document = await CreateDocumentAsync();
        var left = await CreateBoxAsync(document.DocumentId, 1, 1, 1);
        var right = await CreateBoxAsync(document.DocumentId, 1, 1, 1);

        var extrudeResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{document.DocumentId}/operations/extrude",
            new ExtrudeRequestDto(
                [new ProfilePoint2Dto(-0.5, -0.5), new ProfilePoint2Dto(0.5, -0.5), new ProfilePoint2Dto(0.5, 0.5), new ProfilePoint2Dto(-0.5, 0.5)],
                new Point3Dto(4, 0, 0),
                new Vector3Dto(0, 0, 1),
                new Vector3Dto(1, 0, 0),
                1));
        extrudeResponse.EnsureSuccessStatusCode();

        var moved = await extrudeResponse.Content.ReadFromJsonAsync<BodyCreatedResponseDto>();
        Assert.NotNull(moved);

        var booleanResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{document.DocumentId}/operations/boolean",
            new BooleanRequestDto(left.BodyId, moved!.BodyId, "union"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, booleanResponse.StatusCode);
        var error = await booleanResponse.Content.ReadFromJsonAsync<ErrorResponseDto>();
        Assert.NotNull(error);
        Assert.Contains(error!.Diagnostics, d => d.Code == "NotImplemented");
    }

    [Fact]
    public async Task MissingDocumentAndBody_ReturnNotFound()
    {
        var missingDocId = Guid.NewGuid();
        var missingBodyId = Guid.NewGuid();

        var docResponse = await _client.GetAsync($"/api/documents/{missingDocId}");
        Assert.Equal(HttpStatusCode.NotFound, docResponse.StatusCode);

        var created = await CreateDocumentAsync();
        var bodyResponse = await _client.PostAsJsonAsync(
            $"/api/documents/{created.DocumentId}/bodies/{missingBodyId}/tessellate",
            new TessellateRequestDto(null));
        Assert.Equal(HttpStatusCode.NotFound, bodyResponse.StatusCode);
    }

    private async Task<DocumentCreateResponseDto> CreateDocumentAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/documents", new DocumentCreateRequestDto("test"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<DocumentCreateResponseDto>();
        return payload!;
    }

    private async Task<BodyCreatedResponseDto> CreateBoxAsync(Guid documentId, double w, double h, double d)
    {
        var response = await _client.PostAsJsonAsync($"/api/documents/{documentId}/bodies/primitives/box", new BoxCreateRequestDto(w, h, d));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<BodyCreatedResponseDto>();
        return body!;
    }
}
