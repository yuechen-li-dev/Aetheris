using System.Net.Http.Json;
using Aetheris.Server.Api;
using Aetheris.Server.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class PaperclipDemoIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PaperclipDemoIntegrationTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    [Fact]
    public async Task MaximumPaperclips_CompilesParametricTemplateThroughStepRoundTrip()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/demos/maximum-paperclips",
            new PaperclipDemoRequest(1, 40, 11, 6, 1.5, 1.2, "Standard.Materials.StainlessSteel.304_Annealed"));
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<PaperclipDemoResponse>>();
        Assert.NotNull(envelope?.Data);
        Assert.True(envelope!.Success);
        Assert.True(envelope.Data!.Manufacturable);
        Assert.True(envelope.Data.StepAp242);
        Assert.True(envelope.Data.Deterministic);
        Assert.Contains("ISO-10303-21", envelope.Data.StepText, StringComparison.Ordinal);
        Assert.Equal("Standard.Materials.StainlessSteel.304_Annealed", envelope.Data.Material);
        Assert.True(envelope.Data.CenterlineLength > 100);
        Assert.True(envelope.Data.MassGrams > 0);
        Assert.Equal(6, envelope.Data.Bounds.Count);
    }
}
