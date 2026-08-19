using Aetheris.Server.Api;
using Aetheris.Server.Configuration;
using Aetheris.Server.Documents;
using Aetheris.Server.Startup;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using System.Diagnostics;

CadmataLaunchOptions launchOptions;
try
{
    launchOptions = CadmataLaunchOptions.Parse(args);
    launchOptions.ValidateProductionAssets(AppContext.BaseDirectory);
}
catch (CadmataLaunchException exception)
{
    Console.Error.WriteLine($"Cadmata could not start: {exception.Message}");
    return 2;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

if (launchOptions.Step is not null && !launchOptions.HasExplicitUrls)
{
    builder.WebHost.UseUrls("http://127.0.0.1:0");
}

if (launchOptions.Step is not null)
{
    // The CLI intentionally returns after process creation. Keep the detached
    // host from retaining noisy per-request console output in redirected pipes.
    builder.Logging.ClearProviders();
}

var stepUploadOptions = builder.Configuration
    .GetSection(StepUploadOptions.SectionName)
    .Get<StepUploadOptions>()
    ?? new StepUploadOptions();

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = stepUploadOptions.MaxUploadSizeBytes;
});

builder.Services.AddSingleton(stepUploadOptions);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<KernelDocumentStore>();
builder.Services.AddSingleton(new CadmataStartupStep(launchOptions.Step));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapKernelApi();
app.MapCadmataStartupApi();
app.MapPaperclipDemoApi();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

if (launchOptions.Step is null)
{
    app.Run();
    return 0;
}

await app.StartAsync();
var addresses = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses;
var address = addresses?.FirstOrDefault(static value => value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
    ?? app.Urls.FirstOrDefault()
    ?? throw new InvalidOperationException("Cadmata started without a browser address.");

Console.WriteLine($"Cadmata ready: {address}");
Console.WriteLine($"Opening: {launchOptions.Step.Path}");

if (!launchOptions.NoBrowser)
{
    try
    {
        Process.Start(new ProcessStartInfo(address) { UseShellExecute = true });
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Cadmata could not open the default browser: {exception.Message}");
        await app.StopAsync();
        return 3;
    }
}

await app.WaitForShutdownAsync();
return 0;

public partial class Program;
