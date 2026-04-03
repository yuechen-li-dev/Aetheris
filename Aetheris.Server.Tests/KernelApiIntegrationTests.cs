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
    public async Task DisplayPrepare_BoxCylinderThroughHole_UsesAnalyticLaneWithoutTessellationFallback()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 40, 30, 12);

        var cylinderResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/primitives/cylinder",
            new CylinderCreateRequestDto(4, 20));
        cylinderResponse.EnsureSuccessStatusCode();
        var cylinder = await cylinderResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        Assert.NotNull(cylinder);
        Assert.True(cylinder!.Success);

        var translateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{cylinder.Data!.BodyId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(3, -2, 6)));
        translateResponse.EnsureSuccessStatusCode();

        var booleanResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/operations/boolean",
            new BooleanRequestDto(box.Data!.BodyId, cylinder.Data.BodyId, "subtract"));
        booleanResponse.EnsureSuccessStatusCode();
        var resultBody = await booleanResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        Assert.NotNull(resultBody);
        Assert.True(resultBody!.Success);

        var prepared = await PrepareDisplayAsync(document.Data.DocumentId, resultBody.Data!.BodyId);

        Assert.Equal("analytic-only", prepared.Data!.Lane);
        Assert.NotEmpty(prepared.Data.AnalyticPacket.AnalyticFaces);
        Assert.Contains(prepared.Data.AnalyticPacket.AnalyticFaces, face => face.SurfaceKind == "Cylinder");
        Assert.Empty(prepared.Data.AnalyticPacket.FallbackFaces);
        Assert.Null(prepared.Data.TessellationFallback);
    }

    [Fact]
    public async Task DisplayPrepare_SpherePrimitive_UsesAnalyticLaneWithSphereGeometry()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/bodies/primitives/sphere",
            new SphereCreateRequestDto(5));
        response.EnsureSuccessStatusCode();
        var sphere = await response.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        Assert.NotNull(sphere);
        Assert.True(sphere!.Success);

        var prepared = await PrepareDisplayAsync(document.Data.DocumentId, sphere.Data!.BodyId);

        Assert.Equal("analytic-only", prepared.Data!.Lane);
        Assert.NotEmpty(prepared.Data.AnalyticPacket.AnalyticFaces);
        Assert.Contains(prepared.Data.AnalyticPacket.AnalyticFaces, face =>
            face.SurfaceKind == "Sphere"
            && face.SphereGeometry is not null
            && face.PlaneGeometry is null
            && face.CylinderGeometry is null
            && face.ConeGeometry is null
            && face.TorusGeometry is null);
        Assert.Empty(prepared.Data.AnalyticPacket.FallbackFaces);
        Assert.Null(prepared.Data.TessellationFallback);
    }

    [Fact]
    public async Task DisplayPrepare_ImportedUnsupportedBody_PreservesFallbackRouting()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var unsupportedStepText = await File.ReadAllTextAsync(GetRepositoryPath("testdata/step242/handcrafted/edge-trimming/block-full-round.step"));

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/import/step",
            new StepImportRequestDto(unsupportedStepText, "Unsupported"));
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepImportResponseDto>>();
        Assert.NotNull(imported);
        Assert.True(imported!.Success);

        var prepared = await PrepareDisplayAsync(document.Data.DocumentId, imported.Data!.OccurrenceId);

        Assert.NotEqual("analytic-only", prepared.Data!.Lane);
        Assert.NotEmpty(prepared.Data.AnalyticPacket.FallbackFaces);
        Assert.Contains(prepared.Data.AnalyticPacket.FallbackFaces, face => face.Reason is "UnsupportedSurfaceKind" or "UnsupportedTrim");
        Assert.NotNull(prepared.Data.TessellationFallback);
        Assert.NotEmpty(prepared.Data.TessellationFallback!.FacePatches);
    }

    [Fact]
    public async Task DisplayPrepare_BoundedBsplineFallback_UsesSoftScaffoldSource()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/import/step",
            new StepImportRequestDto(RepresentativeSingleFaceBsplineStep, "BoundedBspline"));
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepImportResponseDto>>();
        Assert.NotNull(imported);
        Assert.True(imported!.Success);

        var prepared = await PrepareDisplayAsync(document.Data.DocumentId, imported.Data!.OccurrenceId);

        Assert.Equal("fallback-only", prepared.Data!.Lane);
        Assert.NotNull(prepared.Data.TessellationFallback);
        var patch = Assert.Single(prepared.Data.TessellationFallback!.FacePatches);
        Assert.Equal("BsplineUvScaffold", patch.Source);
        Assert.Null(patch.ScaffoldRejectionReason);
    }

    [Fact]
    public async Task DisplayPrepare_SameBodyTwice_IsDeterministic()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var first = await PrepareDisplayAsync(document.Data.DocumentId, box.Data!.BodyId);
        var second = await PrepareDisplayAsync(document.Data.DocumentId, box.Data.BodyId);

        Assert.NotNull(first.Data);
        Assert.NotNull(second.Data);
        var firstSignature = JsonSerializer.Serialize(first.Data);
        var secondSignature = JsonSerializer.Serialize(second.Data);
        Assert.Equal(firstSignature, secondSignature);
    }

    [Fact]
    public async Task Tessellate_UnsupportedImportedBody_StillWorksAfterDisplayPrepareIntegration()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var unsupportedStepText = await File.ReadAllTextAsync(GetRepositoryPath("testdata/step242/handcrafted/edge-trimming/block-full-round.step"));
        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data!.DocumentId}/import/step",
            new StepImportRequestDto(unsupportedStepText, "Unsupported"));
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepImportResponseDto>>();

        var displayPrepared = await PrepareDisplayAsync(document.Data.DocumentId, imported!.Data!.OccurrenceId);
        Assert.NotNull(displayPrepared.Data!.TessellationFallback);

        var tessellationResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{imported.Data.OccurrenceId}/tessellate",
            new TessellateRequestDto(null));
        tessellationResponse.EnsureSuccessStatusCode();
        var tessellation = await tessellationResponse.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>();
        Assert.NotNull(tessellation);
        Assert.True(tessellation!.Success);
        Assert.NotEmpty(tessellation.Data!.FacePatches);
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
        Assert.False(string.IsNullOrWhiteSpace(exported.Data.CanonicalHash));
        Assert.Equal(64, exported.Data.CanonicalHash.Length);

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
    public async Task StepExport_Twice_ReturnsDeterministicTextAndHash()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var firstResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{box.Data!.DefinitionId}/export/step");
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        var secondResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{box.Data.DefinitionId}/export/step");
        secondResponse.EnsureSuccessStatusCode();
        var second = await secondResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Data!.StepText, second!.Data!.StepText);
        Assert.Equal(first.Data.CanonicalHash, second.Data.CanonicalHash);
    }

    [Fact]
    public async Task StepExport_ImportThenExport_ProducesStableCanonicalHash()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var box = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);

        var exportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{box.Data!.DefinitionId}/export/step");
        exportResponse.EnsureSuccessStatusCode();
        var exported = await exportResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        var importResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/import/step",
            new StepImportRequestDto(exported!.Data!.StepText, "Imported Hash Test"));
        importResponse.EnsureSuccessStatusCode();
        var imported = await importResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepImportResponseDto>>();

        var importedExportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{imported!.Data!.DefinitionId}/export/step");
        importedExportResponse.EnsureSuccessStatusCode();
        var importedExport = await importedExportResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        Assert.NotNull(importedExport);
        Assert.Equal(exported.Data.CanonicalHash, importedExport!.Data!.CanonicalHash);
    }

    [Fact]
    public async Task StepExport_HashChangesForDifferentGeometry()
    {
        var document = await CreateDocumentAsync("/api/v1/documents");
        var firstBox = await CreateBoxAsync("/api/v1/documents", document.Data!.DocumentId, 2, 2, 2);
        var secondBox = await CreateBoxAsync("/api/v1/documents", document.Data.DocumentId, 3, 2, 2);

        var firstExportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{firstBox.Data!.DefinitionId}/export/step");
        firstExportResponse.EnsureSuccessStatusCode();
        var firstExport = await firstExportResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        var secondExportResponse = await _client.GetAsync($"/api/v1/documents/{document.Data.DocumentId}/definitions/{secondBox.Data!.DefinitionId}/export/step");
        secondExportResponse.EnsureSuccessStatusCode();
        var secondExport = await secondExportResponse.Content.ReadFromJsonAsync<ApiResponseDto<StepExportResponseDto>>();

        Assert.NotNull(firstExport);
        Assert.NotNull(secondExport);
        Assert.NotEqual(firstExport!.Data!.CanonicalHash, secondExport!.Data!.CanonicalHash);
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

    private async Task<ApiResponseDto<DisplayPreparationResponseDto>> PrepareDisplayAsync(Guid documentId, Guid bodyId)
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{documentId}/bodies/{bodyId}/display/prepare",
            new DisplayPrepareRequestDto(null));
        response.EnsureSuccessStatusCode();
        var prepared = await response.Content.ReadFromJsonAsync<ApiResponseDto<DisplayPreparationResponseDto>>();
        Assert.NotNull(prepared);
        Assert.True(prepared!.Success);
        return prepared;
    }

    private static string GetRepositoryPath(string relativePath)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", relativePath));

    private const string RepresentativeSingleFaceBsplineStep = "ISO-10303-21;\nHEADER;\nENDSEC;\nDATA;\n#1=MANIFOLD_SOLID_BREP('s',#2);\n#2=CLOSED_SHELL($,(#3));\n#3=ADVANCED_FACE((#4),#5,.T.);\n#4=FACE_OUTER_BOUND($,#6,.T.);\n#5=B_SPLINE_SURFACE_WITH_KNOTS('',3,3,((#100,#101,#102,#103),(#104,#105,#106,#107),(#108,#109,#110,#111),(#112,#113,#114,#115)),.UNSPECIFIED.,.F.,.F.,.F.,(4,4),(4,4),(0.,1.),(0.,1.),.UNSPECIFIED.);\n#6=EDGE_LOOP($,(#10,#11,#12,#13));\n#10=ORIENTED_EDGE($,$,$,#20,.T.);\n#11=ORIENTED_EDGE($,$,$,#21,.T.);\n#12=ORIENTED_EDGE($,$,$,#22,.T.);\n#13=ORIENTED_EDGE($,$,$,#23,.T.);\n#20=EDGE_CURVE($,#30,#31,#40,.T.);\n#21=EDGE_CURVE($,#31,#32,#41,.T.);\n#22=EDGE_CURVE($,#32,#33,#42,.T.);\n#23=EDGE_CURVE($,#33,#30,#43,.T.);\n#30=VERTEX_POINT($,#50);\n#31=VERTEX_POINT($,#51);\n#32=VERTEX_POINT($,#52);\n#33=VERTEX_POINT($,#53);\n#40=LINE($,#50,#80);\n#41=LINE($,#51,#81);\n#42=LINE($,#52,#82);\n#43=LINE($,#53,#83);\n#50=CARTESIAN_POINT($,(0,0,0));\n#51=CARTESIAN_POINT($,(1,0,0));\n#52=CARTESIAN_POINT($,(1,1,0));\n#53=CARTESIAN_POINT($,(0,1,0));\n#80=VECTOR($,#84,1.0);\n#81=VECTOR($,#85,1.0);\n#82=VECTOR($,#86,1.0);\n#83=VECTOR($,#87,1.0);\n#84=DIRECTION($,(1,0,0));\n#85=DIRECTION($,(0,1,0));\n#86=DIRECTION($,(-1,0,0));\n#87=DIRECTION($,(0,-1,0));\n#100=CARTESIAN_POINT($,(0,0,0));\n#101=CARTESIAN_POINT($,(0,0.33,0));\n#102=CARTESIAN_POINT($,(0,0.66,0));\n#103=CARTESIAN_POINT($,(0,1,0));\n#104=CARTESIAN_POINT($,(0.33,0,0));\n#105=CARTESIAN_POINT($,(0.33,0.33,0));\n#106=CARTESIAN_POINT($,(0.33,0.66,0));\n#107=CARTESIAN_POINT($,(0.33,1,0));\n#108=CARTESIAN_POINT($,(0.66,0,0));\n#109=CARTESIAN_POINT($,(0.66,0.33,0));\n#110=CARTESIAN_POINT($,(0.66,0.66,0));\n#111=CARTESIAN_POINT($,(0.66,1,0));\n#112=CARTESIAN_POINT($,(1,0,0));\n#113=CARTESIAN_POINT($,(1,0.33,0));\n#114=CARTESIAN_POINT($,(1,0.66,0));\n#115=CARTESIAN_POINT($,(1,1,0));\nENDSEC;\nEND-ISO-10303-21;";
}
