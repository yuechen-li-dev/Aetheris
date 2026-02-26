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
    public async Task V1_CreateDocument_CreateBox_TessellateAndPick_ReturnsExpectedEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var boxResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/bodies/primitives/box",
            new BoxCreateRequestDto(2, 2, 2));
        boxResponse.EnsureSuccessStatusCode();

        var boxBody = await boxResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        Assert.NotNull(boxBody);
        Assert.True(boxBody!.Success);
        Assert.NotNull(boxBody.Data);
        Assert.Empty(boxBody.Diagnostics);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{boxBody.Data!.BodyId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();

        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();
        Assert.NotNull(tessellation);
        Assert.True(tessellation!.Success);
        Assert.NotNull(tessellation.Data);
        Assert.NotEmpty(tessellation.Data!.FacePatches);
        Assert.Empty(tessellation.Diagnostics);

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{boxBody.Data.BodyId}/pick",
            new PickRequestDto(
                new Point3Dto(0, 0, 5),
                new Vector3Dto(0, 0, -1),
                TessellationOptions: null,
                PickOptions: new PickOptionsDto(NearestOnly: true, IncludeBackfaces: null, EdgeTolerance: null, SortTieTolerance: null, MaxDistance: null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<ApiResponseDto<PickResponseDto>>();
        Assert.NotNull(pick);
        Assert.True(pick!.Success);
        Assert.NotNull(pick.Data);
        Assert.Single(pick.Data!.Hits);
        Assert.NotNull(pick.Data.Hits[0].Point);
    }

    [Fact]
    public async Task UnsupportedBoolean_OnV1_ReturnsUnprocessableEntityWithDiagnosticEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var left = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 1, 1, 1);

        var extrudeResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/operations/extrude",
            new ExtrudeRequestDto(
                [new ProfilePoint2Dto(-0.5, -0.5), new ProfilePoint2Dto(0.5, -0.5), new ProfilePoint2Dto(0.5, 0.5), new ProfilePoint2Dto(-0.5, 0.5)],
                new Point3Dto(4, 0, 0),
                new Vector3Dto(0, 0, 1),
                new Vector3Dto(1, 0, 0),
                1));
        extrudeResponse.EnsureSuccessStatusCode();

        var moved = await extrudeResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        Assert.NotNull(moved);
        Assert.NotNull(moved!.Data);

        var booleanResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/operations/boolean",
            new BooleanRequestDto(left.Data!.BodyId, moved.Data!.BodyId, "union"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, booleanResponse.StatusCode);
        var envelope = await booleanResponse.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Null(envelope.Data);
        Assert.Contains(envelope.Diagnostics, d => d.Code == "NotImplemented");
        Assert.All(envelope.Diagnostics, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Code));
            Assert.False(string.IsNullOrWhiteSpace(d.Severity));
            Assert.False(string.IsNullOrWhiteSpace(d.Message));
        });
    }

    [Fact]
    public async Task MissingDocumentAndBody_OnV1_ReturnNotFoundWithDiagnosticEnvelope()
    {
        var missingDocId = Guid.NewGuid();
        var missingBodyId = Guid.NewGuid();

        var docResponse = await _client.GetAsync($"/api/v1/documents/{missingDocId}");
        Assert.Equal(HttpStatusCode.NotFound, docResponse.StatusCode);
        var missingDoc = await docResponse.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(missingDoc);
        Assert.False(missingDoc!.Success);
        Assert.NotEmpty(missingDoc.Diagnostics);

        var created = await CreateDocumentAsync("/api/v1/documents");
        var bodyResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{created.Data!.DocumentId}/bodies/{missingBodyId}/tessellate",
            new TessellateRequestDto(null));
        Assert.Equal(HttpStatusCode.NotFound, bodyResponse.StatusCode);

        var missingBody = await bodyResponse.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(missingBody);
        Assert.False(missingBody!.Success);
        Assert.NotEmpty(missingBody.Diagnostics);
    }

    [Fact]
    public async Task UnversionedRoute_CompatibilityAlias_StillWorks()
    {
        var response = await _client.PostAsJsonAsync("/api/documents", new DocumentCreateRequestDto("compat"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<DocumentCreateResponseDto>>();
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);
        Assert.Equal("compat", envelope.Data!.Name);
    }

    private async Task<ApiResponseDto<DocumentCreateResponseDto>> CreateDocumentAsync(string routePrefix)
    {
        var response = await _client.PostAsJsonAsync(routePrefix, new DocumentCreateRequestDto("test"));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<DocumentCreateResponseDto>>();
        return payload!;
    }

    private async Task<ApiResponseDto<BodyCreatedResponseDto>> CreateBoxAsync(string routePrefix, Guid documentId, double w, double h, double d)
    {
        var response = await _client.PostAsJsonAsync($"{routePrefix}/{documentId}/bodies/primitives/box", new BoxCreateRequestDto(w, h, d));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        return body!;
    }
}
