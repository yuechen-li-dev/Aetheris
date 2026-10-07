using Aetheris.Server.Api;
using Aetheris.Server.Configuration;
using Aetheris.Server.Documents;
using Aetheris.Server.Startup;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.StaticFiles;
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

var app = CadmataApplication.Create(args, launchOptions);

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
