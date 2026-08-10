using System.Net;
using System.Net.Http.Json;
using Aetheris.Server.Contracts;
using Aetheris.Server.Startup;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Aetheris.Server.Tests;

public sealed class CadmataStartupTests
{
    [Fact]
    public void Parse_NormalizesRelativeUnicodePathWithSpaces()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Cadmata startup ü " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var step = Path.Combine(directory, "nested part.stp");
            File.WriteAllText(step, "ISO-10303-21;");
            var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), step);

            var options = CadmataLaunchOptions.Parse([relative, "--no-browser"]);

            Assert.NotNull(options.Step);
            Assert.Equal(Path.GetFullPath(step), options.Step!.Path);
            Assert.Equal("nested part.stp", options.Step.FileName);
            Assert.Equal("ISO-10303-21;", options.Step.StepText);
            Assert.True(options.NoBrowser);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Parse_RejectsMissingAndInvalidStartupFilesClearly()
    {
        var missing = Assert.Throws<CadmataLaunchException>(() => CadmataLaunchOptions.Parse(["missing.step"]));
        Assert.Contains("not found", missing.Message, StringComparison.OrdinalIgnoreCase);

        var invalid = Path.Combine(Path.GetTempPath(), $"invalid-{Guid.NewGuid():N}.txt");
        File.WriteAllText(invalid, "not step");
        try
        {
            var exception = Assert.Throws<CadmataLaunchException>(() => CadmataLaunchOptions.Parse([invalid]));
            Assert.Contains(".step, .stp, .firmament, or .firmasm", exception.Message, StringComparison.Ordinal);
        }
        finally { File.Delete(invalid); }
    }

    [Fact]
    public void Parse_AcceptsFirmasmAsAssemblyStartupDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"assembly-{Guid.NewGuid():N}.firmasm");
        File.WriteAllText(path, "Assembly A { <Assembly A></Assembly> }");
        try
        {
            var options = CadmataLaunchOptions.Parse([path, "--no-browser"]);
            Assert.Equal("assembly", options.Step!.Kind);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ValidateProductionAssets_RequiresPackagedIndexForStartupOpen()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Cadmata assets " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var options = new CadmataLaunchOptions(
                new CadmataStartupStepContent("C:\\Models\\plate.step", "plate.step", "ISO-10303-21;"),
                NoBrowser: false,
                HasExplicitUrls: false);

            var missing = Assert.Throws<CadmataLaunchException>(() => options.ValidateProductionAssets(directory));
            Assert.Contains("Production frontend assets", missing.Message, StringComparison.Ordinal);

            Directory.CreateDirectory(Path.Combine(directory, "wwwroot"));
            File.WriteAllText(Path.Combine(directory, "wwwroot", "index.html"), "<!doctype html>");
            options.ValidateProductionAssets(directory);

            new CadmataLaunchOptions(null, false, false).ValidateProductionAssets(Path.Combine(directory, "missing"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task StartupEndpoint_ReturnsConfiguredStepExactlyOnce()
    {
        var content = new CadmataStartupStepContent("C:\\Models\\plate.step", "plate.step", "ISO-10303-21;");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<CadmataStartupStep>();
                services.AddSingleton(new CadmataStartupStep(content));
            }));
        using var client = factory.CreateClient();

        var first = await client.PostAsync("/api/v1/startup/step", null);
        first.EnsureSuccessStatusCode();
        var envelope = await first.Content.ReadFromJsonAsync<ApiResponseDto<CadmataStartupStepContent>>();
        Assert.Equal(content, envelope!.Data);

        var second = await client.PostAsync("/api/v1/startup/step", null);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }
}
