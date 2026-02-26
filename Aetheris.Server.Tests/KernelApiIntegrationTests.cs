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

    [Theory]
    [InlineData("/api/v1/documents")]
    [InlineData("/api/documents")]
    public async Task CreateDocument_AndSummary_ReturnConsistentSuccessEnvelope(string routePrefix)
    {
        var createResponse = await _client.PostAsJsonAsync(routePrefix, new DocumentCreateRequestDto("test"));
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<ApiResponseDto<DocumentCreateResponseDto>>();
        Assert.NotNull(created);
        Assert.True(created!.Success);
        Assert.NotNull(created.Data);
        Assert.Empty(created.Diagnostics);

        var summaryResponse = await _client.GetAsync($"{routePrefix}/{created.Data!.DocumentId}");
        summaryResponse.EnsureSuccessStatusCode();

        var summary = await summaryResponse.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSummaryResponseDto>>();
        Assert.NotNull(summary);
        Assert.True(summary!.Success);
        Assert.NotNull(summary.Data);
        Assert.Empty(summary.Diagnostics);
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
        Assert.Empty(pick.Diagnostics);
    }


    [Fact]
    public async Task BodyTranslation_UpdatesTessellationAndPickCoordinates()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var transformResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.BodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(3, 0, 0)));
        transformResponse.EnsureSuccessStatusCode();

        var transformed = await transformResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyTransformedResponseDto>>();
        Assert.NotNull(transformed);
        Assert.True(transformed!.Success);
        Assert.Equal(3d, transformed.Data!.AppliedTranslation.X);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data.BodyId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();

        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();
        Assert.NotNull(tessellation);
        Assert.True(tessellation!.Success);
        Assert.Contains(tessellation.Data!.FacePatches.SelectMany(static p => p.Positions), p => p.X > 3.9);

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data.BodyId}/pick",
            new PickRequestDto(
                new Point3Dto(3, 0, 5),
                new Vector3Dto(0, 0, -1),
                TessellationOptions: null,
                PickOptions: new PickOptionsDto(NearestOnly: true, IncludeBackfaces: null, EdgeTolerance: null, SortTieTolerance: null, MaxDistance: null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<ApiResponseDto<PickResponseDto>>();
        Assert.NotNull(pick);
        Assert.True(pick!.Success);
        Assert.Single(pick.Data!.Hits);
        Assert.InRange(pick.Data.Hits[0].Point.X, 2d, 4d);
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

    [Theory]
    [InlineData("/api/v1/documents")]
    [InlineData("/api/documents")]
    public async Task InvalidPickDirection_ReturnsBadRequestWithDiagnosticEnvelope_OnV1AndAlias(string routePrefix)
    {
        var document = await CreateDocumentAsync(routePrefix);
        var box = await CreateBoxAsync(routePrefix, document.Data!.DocumentId, 1, 1, 1);

        var response = await _client.PostAsJsonAsync(
            $"{routePrefix}/{document.Data.DocumentId}/bodies/{box.Data!.BodyId}/pick",
            new PickRequestDto(
                new Point3Dto(0, 0, 5),
                new Vector3Dto(0, 0, 0),
                TessellationOptions: null,
                PickOptions: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.Null(envelope.Data);
        Assert.NotEmpty(envelope.Diagnostics);
        Assert.All(envelope.Diagnostics, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Code));
            Assert.False(string.IsNullOrWhiteSpace(d.Severity));
            Assert.False(string.IsNullOrWhiteSpace(d.Message));
            Assert.False(string.IsNullOrWhiteSpace(d.Source));
        });
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
