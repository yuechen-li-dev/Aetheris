using Aetheris.Server.Api;
using Aetheris.Server.Configuration;
using Aetheris.Server.Documents;
using Aetheris.Server.Startup;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

// An embedded host (including WebApplicationFactory) owns its own presentation
// and lifetime. Only the real executable launches a browser window.
var ownsProcess = Assembly.GetEntryAssembly() == typeof(Program).Assembly;

CadmataLaunchOptions launchOptions;
try
{
    launchOptions = CadmataLaunchOptions.Parse(args);
    if (ownsProcess) launchOptions.ValidateProductionAssets(AppContext.BaseDirectory);
}
catch (CadmataLaunchException exception)
{
    Console.Error.WriteLine($"Cadmata could not start: {exception.Message}");
    return 2;
}

var browserSession = ownsProcess && !launchOptions.NoBrowser && !launchOptions.HasExplicitUrls;
var sessionToken = browserSession ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) : null;
var app = CadmataApplication.Create(args, launchOptions, sessionToken: sessionToken);
if (!ownsProcess)
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
if (launchOptions.Step is not null) Console.WriteLine($"Opening: {launchOptions.Step.Path}");
if (ownsProcess) Console.WriteLine("Close this server window or press Ctrl+C to end the Cadmata session.");

if (ownsProcess && !launchOptions.NoBrowser)
{
    try
    {
        var browserAddress = sessionToken is null ? address : address + "/__cadmata/session/" + sessionToken;
        Process.Start(new ProcessStartInfo(browserAddress) { UseShellExecute = true });
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
