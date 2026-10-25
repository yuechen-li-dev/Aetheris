using System.Net.Http.Json;
using Aetheris.Server.Api;
using Aetheris.Server.Contracts;
using Aetheris.Kernel.Core.Step242;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Aetheris.Server.Tests;

public sealed class ModelingDemoTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ComposedBlocks_UseSectionPlanningAndExportOneEnclosedSolid()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/demos/composed-block", new ComposedBlockRequest(80, 50, 8, 30, 20, 12));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var built = (await response.Content.ReadFromJsonAsync<ApiResponseDto<ComposedBlockResponse>>())!.Data!;
        Assert.Equal("PrismaticSectionStack/Add", built.MaterializationRoute);
        Assert.Equal("composed-block.step", built.ExportPath);
        var imported = Step242Importer.ImportBody(built.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Single(imported.Value.Topology.Bodies);
        Assert.True(Assert.Single(imported.Value.ImportQualification!.Shells).Enclosed);
        var points = imported.Value.Topology.Vertices.Select(vertex => {
            Assert.True(imported.Value.TryGetVertexPoint(vertex.Id, out var point)); return point;
        }).ToArray();
        Assert.Equal(-40, points.Min(p => p.X), 7); Assert.Equal(40, points.Max(p => p.X), 7);
        Assert.Equal(0, points.Min(p => p.Z), 7); Assert.Equal(20, points.Max(p => p.Z), 7);
    }

    [Fact]
    public async Task ComposedBlocks_RejectInvalidDimensions()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/demos/composed-block", new ComposedBlockRequest(10, 10, 8, 30, 20, 12));
        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains("fit within the base", await response.Content.ReadAsStringAsync());
    }
}
