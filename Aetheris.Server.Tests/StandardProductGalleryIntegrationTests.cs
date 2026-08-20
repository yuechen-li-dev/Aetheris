using System.Net.Http.Json;
using System.Text.Json;
using Aetheris.Forge.Host;
using Aetheris.Server.Api;
using Aetheris.Server.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class StandardProductGalleryIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public StandardProductGalleryIntegrationTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task GalleryDiscoversSchemasAndReturnsTheDownloadableGeneratedStep()
    {
        var catalog = await client.GetFromJsonAsync<ApiResponseDto<ForgeTemplateDescription[]>>("/api/v1/gallery/templates");
        Assert.True(catalog!.Success);
        Assert.Contains(catalog.Data!, item => item.Id == "Standard.Products.Mechanical.MountingPlate");
        Assert.Contains(catalog.Data!, item => item.Id == "Standard.SheetMetal.ElectronicsEnclosure");

        var values = new Dictionary<string, object?>
        {
            ["width"] = "120 mm", ["height"] = "80 mm", ["thickness"] = "10 mm",
            ["holeDiameter"] = "6.6 mm", ["holeSpacingX"] = "90 mm", ["holeSpacingY"] = "50 mm",
            ["counterboreDiameter"] = "11 mm", ["counterboreDepth"] = "4 mm",
            ["material"] = "Standard.Materials.Aluminum.6061_T6",
        };
        var request = new GalleryInvocationRequest(values.ToDictionary(pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal), [ForgeArtifactKind.StepAp242]);
        var response = await client.PostAsJsonAsync("/api/v1/gallery/templates/Standard.Products.Mechanical.MountingPlate", request);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<GalleryInvocationResponse>>();
        var artifact = Assert.Single(envelope!.Data!.Artifacts);
        Assert.Contains("ISO-10303-21", artifact.Content, StringComparison.Ordinal);
        Assert.Equal("part.step", artifact.Name);
    }

    [Fact]
    public async Task GalleryPreservesEngineeringConstraintDiagnostics()
    {
        var values = new Dictionary<string, object?>
        {
            ["width"] = "120 mm", ["height"] = "80 mm", ["thickness"] = "10 mm",
            ["holeDiameter"] = "6.6 mm", ["holeSpacingX"] = "121 mm", ["holeSpacingY"] = "50 mm",
            ["counterboreDiameter"] = "11 mm", ["counterboreDepth"] = "4 mm",
            ["material"] = "Standard.Materials.Aluminum.6061_T6",
        };
        var request = new GalleryInvocationRequest(values.ToDictionary(pair => pair.Key,
            pair => JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal), [ForgeArtifactKind.StepAp242]);
        var response = await client.PostAsJsonAsync("/api/v1/gallery/templates/Standard.Products.Mechanical.MountingPlate", request);
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponseDto<GalleryInvocationResponse>>();
        Assert.False(envelope!.Success);
        Assert.Contains(envelope.Diagnostics, item => item.Message.Contains("HoleSpacingXFitsPlate", StringComparison.Ordinal));
    }
}
