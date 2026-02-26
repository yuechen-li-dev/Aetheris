using System.Net;
using System.Net.Http.Json;
using Aetheris.Server.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class KernelApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public KernelApiIntegrationTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task DocumentSummary_ReturnsOccurrenceShape()
    {
        var document = await CreateDocumentAsync();
        var box = await CreateBoxAsync(document.Data!.DocumentId);

        var response = await _client.GetFromJsonAsync<ApiResponseDto<DocumentSummaryResponseDto>>($"/api/v1/documents/{document.Data.DocumentId}");

        Assert.NotNull(response);
        Assert.True(response!.Success);
        Assert.Equal(1, response.Data!.OccurrenceCount);
        Assert.Single(response.Data.Occurrences);
        Assert.Equal(box.Data!.OccurrenceId, response.Data.Occurrences[0].OccurrenceId);
        Assert.Equal(box.Data.DefinitionId, response.Data.Occurrences[0].DefinitionId);
    }

    [Fact]
    public async Task CreateOccurrence_UsesSameDefinition_AndIndependentTransform()
    {
        var document = await CreateDocumentAsync();
        var box = await CreateBoxAsync(document.Data!.DocumentId);

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.OccurrenceId}/occurrences",
            new { });
        duplicateResponse.EnsureSuccessStatusCode();

        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyOccurrenceCreatedResponseDto>>();
        Assert.NotNull(duplicate);
        Assert.Equal(box.Data.DefinitionId, duplicate!.Data!.DefinitionId);

        await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data.OccurrenceId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(4, 0, 0)));

        var originalTess = await Tessellate(document.Data.DocumentId, box.Data.OccurrenceId);
        var duplicateTess = await Tessellate(document.Data.DocumentId, duplicate.Data.OccurrenceId);

        Assert.Contains(originalTess.FacePatches.SelectMany(static p => p.Positions), p => p.X > 4);
        Assert.Contains(duplicateTess.FacePatches.SelectMany(static p => p.Positions), p => p.X < 1);
    }

    [Fact]
    public async Task Pick_ReturnsOccurrenceIdentity_AndWorldSpacePoint()
    {
        var document = await CreateDocumentAsync();
        var box = await CreateBoxAsync(document.Data!.DocumentId);

        var duplicateResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{box.Data!.OccurrenceId}/occurrences",
            new { });
        var duplicate = (await duplicateResponse.Content.ReadFromJsonAsync<ApiResponseDto<BodyOccurrenceCreatedResponseDto>>())!;

        await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{duplicate.Data!.OccurrenceId}/transform",
            new TranslateBodyRequestDto(new Vector3Dto(5, 0, 0)));

        var pickResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/bodies/{duplicate.Data.OccurrenceId}/pick",
            new PickRequestDto(new Point3Dto(5, 0, 5), new Vector3Dto(0, 0, -1), null, new PickOptionsDto(true, null, null, null, null)));
        pickResponse.EnsureSuccessStatusCode();

        var pick = await pickResponse.Content.ReadFromJsonAsync<ApiResponseDto<PickResponseDto>>();
        Assert.NotNull(pick);
        Assert.Single(pick!.Data!.Hits);
        Assert.Equal(duplicate.Data.OccurrenceId, pick.Data.Hits[0].OccurrenceId);
        Assert.Equal(duplicate.Data.DefinitionId, pick.Data.Hits[0].DefinitionId);
        Assert.InRange(pick.Data.Hits[0].Point.X, 4, 6);
    }

    [Fact]
    public async Task BooleanRegression_StillReturnsM17EnvelopeWhenNotImplemented()
    {
        var document = await CreateDocumentAsync();
        var left = await CreateBoxAsync(document.Data!.DocumentId);
        var right = await CreateBoxAsync(document.Data.DocumentId);

        var booleanResponse = await _client.PostAsJsonAsync(
            $"/api/v1/documents/{document.Data.DocumentId}/operations/boolean",
            new BooleanRequestDto(left.Data!.OccurrenceId, right.Data!.OccurrenceId, "union"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, booleanResponse.StatusCode);
        var envelope = await booleanResponse.Content.ReadFromJsonAsync<ApiResponseDto<object>>();
        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.NotEmpty(envelope.Diagnostics);
    }

    private async Task<ApiResponseDto<DocumentCreateResponseDto>> CreateDocumentAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/documents", new DocumentCreateRequestDto("test"));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponseDto<DocumentCreateResponseDto>>())!;
    }

    private async Task<ApiResponseDto<BodyCreatedResponseDto>> CreateBoxAsync(Guid documentId)
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/documents/{documentId}/bodies/primitives/box", new BoxCreateRequestDto(1, 1, 1));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>())!;
    }

    private async Task<TessellationResponseDto> Tessellate(Guid documentId, Guid occurrenceId)
    {
        var response = await _client.PostAsJsonAsync($"/api/v1/documents/{documentId}/bodies/{occurrenceId}/tessellate", new TessellateRequestDto(null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponseDto<TessellationResponseDto>>())!.Data!;
    }
}
