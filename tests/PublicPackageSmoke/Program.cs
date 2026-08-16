using System.Reflection;
using Aetheris.Forge.Host;
using Aetheris.Kernel.StandardLibrary.Materials;

string[] packageAssemblies =
[
    "Aetheris.Kernel.Core",
    "Aetheris.Kernel.Firmament",
    "Aetheris.Forge.Host",
    "Aetheris.Forge.KernelSDK"
];

foreach (var assemblyName in packageAssemblies)
{
    var assembly = Assembly.Load(assemblyName);
    var version = assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion;

    if (version is null || !version.StartsWith("2.0.0-preview.3", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{assemblyName} reported unexpected version '{version}'.");
    }

    Console.WriteLine($"{assemblyName} {version}");
}

var material = new MaterialResolver().Resolve("Standard.Materials.Aluminum.5052_H32");
if (!material.IsSuccess || material.Material?.Identity.StableId != "aluminum/5052-h32")
    throw new InvalidOperationException($"Packaged material resolution failed: {material.Message}");
Console.WriteLine($"Material {material.Material.Identity.FirmamentPath} resolved from the packaged SQLite catalog.");

var host = new ForgeProtocolHost();
if (host.GetHostInfo().ProtocolVersion != ForgeHostProtocol.Version || host.ListTemplates().Templates.Count == 0)
    throw new InvalidOperationException("Packaged Forge Host did not expose Protocol v1 templates.");
Console.WriteLine($"Forge Host Protocol v{ForgeHostProtocol.Version}: {host.ListTemplates().Templates.Count} templates.");
