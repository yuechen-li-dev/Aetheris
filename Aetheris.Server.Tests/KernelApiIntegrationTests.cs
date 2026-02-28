using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task InvalidPrimitiveDimensions_ReturnStructuredBadRequestEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/bodies/primitives/box",
            new BoxCreateRequestDto(-1d, 2d, 3d));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Null(payload.Data);
        Assert.NotEmpty(payload.Diagnostics);
        Assert.Contains(payload.Diagnostics, d => d.Code == "InvalidArgument");
    }

    [Fact]
    public async Task Transform_UnknownBody_ReturnsNotFoundEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var missingBodyId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/bodies/{missingBodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(1d, 0d, 0d)));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Null(payload.Data);
        Assert.NotEmpty(payload.Diagnostics);
    }

    [Fact]
    public async Task Extrude_NullProfilePayload_ReturnsStructuredBadRequestEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var json = """
                   {
                     "profile": null,
                     "origin": {"x":0,"y":0,"z":0},
                     "normal": {"x":0,"y":0,"z":1},
                     "uAxis": {"x":1,"y":0,"z":0},
                     "depth": 1.0
                   }
                   """;

        var response = await _client.PostAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/operations/extrude",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.Null(payload.Data);
        Assert.Contains(payload.Diagnostics, d => d.Source == "operations.extrude");
    }


    [Fact]
    public async Task DocumentSummary_ReportsOccurrenceAndDefinitionIds()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var summaryResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSummaryResponseDto>>();

        Assert.NotNull(summary);
        Assert.True(summary!.Success);
        Assert.Single(summary.Data!.BodyIds);
        Assert.Single(summary.Data.Occurrences);
        Assert.Equal(1, summary.Data.DefinitionCount);
        Assert.Equal(box.Data!.BodyId, summary.Data.Occurrences[0].OccurrenceId);
        Assert.Equal(box.Data.DefinitionId, summary.Data.Occurrences[0].DefinitionId);
    }

    [Fact]
    public async Task CreateOccurrence_ReusesDefinitionAndSupportsIndependentPlacement()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/occurrences",
            new CreateOccurrenceRequestDto(box.Data!.BodyId, DefinitionId: null, Name: "copy"));
        duplicateResponse.EnsureSuccessStatusCode();

        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<ApiResponseDto<OccurrenceCreatedResponseDto>>();
        Assert.NotNull(duplicate);
        Assert.True(duplicate!.Success);
        Assert.Equal(box.Data.DefinitionId, duplicate.Data!.DefinitionId);

        var transformResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{duplicate.Data.BodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(10, 0, 0)));
        transformResponse.EnsureSuccessStatusCode();

        var originalTessellation = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data.BodyId}/tessellate",
            new TessellateRequestDto(null));
        var duplicateTessellation = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{duplicate.Data.BodyId}/tessellate",
            new TessellateRequestDto(null));

        var original = await originalTessellation.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();
        var copied = await duplicateTessellation.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();

        Assert.NotNull(original);
        Assert.NotNull(copied);
        var originalMaxX = original!.Data!.FacePatches.SelectMany(static patch => patch.Positions).Max(static p => p.X);
        var copiedMaxX = copied!.Data!.FacePatches.SelectMany(static patch => patch.Positions).Max(static p => p.X);
        Assert.True(copiedMaxX > originalMaxX + 8d);
    }


    [Fact]
    public async Task StepIo_ExportImportRoundTrip_TessellatesImportedOccurrence()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var exportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{box.Data!.DefinitionId}/export/step");
        exportResponse.EnsureSuccessStatusCode();
        var exported = await exportResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        Assert.NotNull(exported);
        Assert.True(exported!.Success);
        Assert.NotNull(exported.Data);
        Assert.Contains("ISO-10303-21", exported.Data!.StepText);
        Assert.Contains("MANIFOLD_SOLID_BREP", exported.Data.StepText);

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/import/step",
            new StepImportRequestDto(exported.Data.StepText, "Imported Box"));
        importResponse.EnsureSuccessStatusCode();

        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepImportResponseDto>>();
        Assert.NotNull(imported);
        Assert.True(imported!.Success);
        Assert.NotNull(imported.Data);
        Assert.NotEqual(box.Data.DefinitionId, imported.Data!.DefinitionId);
        Assert.NotEqual(box.Data.BodyId, imported.Data.OccurrenceId);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{imported.Data.OccurrenceId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();

        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();
        Assert.NotNull(tessellation);
        Assert.True(tessellation!.Success);
        Assert.NotEmpty(tessellation.Data!.FacePatches);
        Assert.NotEmpty(tessellation.Data.EdgePolylines);
    }

    [Fact]
    public async Task StepImport_EmptyText_ReturnsBadRequestEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/import/step",
            new StepImportRequestDto("", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotEmpty(payload.Diagnostics);
        Assert.Contains(payload.Diagnostics, d => d.Code == "InvalidArgument");
    }

    [Fact]
    public async Task StepExport_MissingDefinition_ReturnsNotFoundEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var response = await _client.GetAsync($"/api/v1/documents/{document.Data!.DocumentId}/definitions/{Guid.NewGuid()}/export/step");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotEmpty(payload.Diagnostics);
    }

    [Fact]
    public async Task StepImport_MalformedPayload_ReturnsDiagnostics()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/import/step",
            new StepImportRequestDto("NOT_A_STEP_FILE", null));

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity });
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotEmpty(payload.Diagnostics);
    }

    [Fact]
    public async Task Snapshot_ExportImportRoundTrip_PreservesDocumentAndSupportsTessellationAndPick()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var transformResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.BodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(4, -2, 1)));
        transformResponse.EnsureSuccessStatusCode();

        var exportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/snapshot");
        exportResponse.EnsureSuccessStatusCode();
        var exported = await exportResponse.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSnapshotDto>>();

        Assert.NotNull(exported);
        Assert.True(exported!.Success);
        Assert.NotNull(exported.Data);
        Assert.Single(exported.Data!.Definitions);
        Assert.Single(exported.Data.Occurrences);

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/snapshot",
            exported.Data);
        importResponse.EnsureSuccessStatusCode();

        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSnapshotImportResultDto>>();
        Assert.NotNull(imported);
        Assert.True(imported!.Success);
        Assert.Equal(1, imported.Data!.DefinitionCount);
        Assert.Equal(1, imported.Data.OccurrenceCount);

        var occurrenceId = exported.Data.Occurrences[0].OccurrenceId;
        Assert.NotNull(occurrenceId);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{occurrenceId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();
        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();

        Assert.NotNull(tessellation);
        Assert.True(tessellation!.Success);
        Assert.NotEmpty(tessellation.Data!.FacePatches);

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{occurrenceId}/pick",
            new PickRequestDto(
                new Point3Dto(4, -2, 6),
                new Vector3Dto(0, 0, -1),
                TessellationOptions: null,
                PickOptions: new PickOptionsDto(NearestOnly: true, IncludeBackfaces: null, EdgeTolerance: null, SortTieTolerance: null, MaxDistance: null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<ApiResponseDto<PickResponseDto>>();
        Assert.NotNull(pick);
        Assert.True(pick!.Success);
        Assert.NotEmpty(pick.Data!.Hits);
    }

    [Fact]
    public async Task Snapshot_ImportMalformedPayload_ReturnsBadRequestEnvelope()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/snapshot",
            new DocumentSnapshotDto(
                document.Data.DocumentId,
                [new DocumentSnapshotDefinitionDto(Guid.NewGuid(), null)],
                []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(payload);
        Assert.False(payload!.Success);
        Assert.NotEmpty(payload.Diagnostics);
    }

    [Fact]
    public async Task Snapshot_ExportIsDeterministicWithoutMutation()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var transformResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.BodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(2, 0, 0)));
        transformResponse.EnsureSuccessStatusCode();

        var responseA = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/snapshot");
        var responseB = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/snapshot");
        responseA.EnsureSuccessStatusCode();
        responseB.EnsureSuccessStatusCode();

        var payloadA = await responseA.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSnapshotDto>>();
        var payloadB = await responseB.Content.ReadFromJsonAsync<ApiResponseDto<DocumentSnapshotDto>>();

        Assert.NotNull(payloadA);
        Assert.NotNull(payloadB);

        var jsonA = JsonSerializer.Serialize(payloadA);
        var jsonB = JsonSerializer.Serialize(payloadB);
        Assert.Equal(jsonA, jsonB);
    }

    [Fact]
    public async Task PickResponse_ContainsOccurrenceIdentity()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.BodyId}/pick",
            new PickRequestDto(
                new Point3Dto(0, 0, 5),
                new Vector3Dto(0, 0, -1),
                TessellationOptions: null,
                PickOptions: new PickOptionsDto(NearestOnly: true, IncludeBackfaces: null, EdgeTolerance: null, SortTieTolerance: null, MaxDistance: null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<ApiResponseDto<PickResponseDto>>();
        Assert.NotNull(pick);
        Assert.True(pick!.Success);
        Assert.Equal(box.Data.BodyId, pick.Data!.Hits[0].OccurrenceId);
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
