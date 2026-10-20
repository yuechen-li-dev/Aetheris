using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Geometry;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Server.Contracts;
using Aetheris.Server.Documents;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class MeasurementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    public MeasurementTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public void Document_revision_invalidates_measurements_after_placement_edit()
    {
        var document = new DocumentSession(Guid.NewGuid());
        var occurrence = document.AddBody(BrepPrimitives.CreateBox(10,20,6).Value);
        var revision = document.Revision;
        var request = new MeasurementRequest("planes", new(occurrence.OccurrenceId.ToString(),null,[0,0,30],[0,0,-1]),
            new(occurrence.OccurrenceId.ToString(),null,[0,0,-30],[0,0,1]));
        Assert.Equal(6, document.Measure(revision, request).DistanceMm);
        Assert.True(document.ApplyBodyTranslation(occurrence.OccurrenceId, new(1,0,0), out _));
        Assert.Equal("measurement-stale-revision", Assert.Throws<MeasurementException>(() => document.Measure(revision, request)).Code);
    }

    [Fact]
    public async Task Http_measurement_uses_current_BRep_and_rejects_stale_revision()
    {
        var created = await (await _client.PostAsJsonAsync("/api/v1/documents", new { name="measurement" })).Content.ReadFromJsonAsync<ApiResponseDto<DocumentCreateResponseDto>>();
        var url = $"/api/v1/documents/{created!.Data!.DocumentId}";
        var body = await (await _client.PostAsJsonAsync(url+"/bodies/primitives/box", new {width=10,height=20,depth=6})).Content.ReadFromJsonAsync<ApiResponseDto<BodyCreatedResponseDto>>();
        var summary = await _client.GetFromJsonAsync<ApiResponseDto<DocumentSummaryResponseDto>>(url);
        var request = new DocumentMeasurementRequest(summary!.Data!.Revision, new("planes", new(body!.Data!.BodyId.ToString(),null,[0,0,30],[0,0,-1]), new(body.Data.BodyId.ToString(),null,[0,0,-30],[0,0,1])));
        var response = await _client.PostAsJsonAsync(url+"/measure", request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(6, json.GetProperty("data").GetProperty("measurement").GetProperty("distanceMm").GetDouble());
        await _client.PostAsJsonAsync(url+$"/bodies/{body.Data.BodyId}/transform", new {translation=new {x=1,y=0,z=0}});
        var stale = await _client.PostAsJsonAsync(url+"/measure", request);
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        Assert.Contains("measurement-stale-revision", await stale.Content.ReadAsStringAsync());
    }
}
