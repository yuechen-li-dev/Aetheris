using System.Reflection;
using System.Text.Json;
using Aetheris.Forge.Host;
using Aetheris.Kernel.Core.Step242;
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

var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
{
    ["width"] = JsonSerializer.SerializeToElement("120 mm"),
    ["height"] = JsonSerializer.SerializeToElement("40 mm"),
    ["depth"] = JsonSerializer.SerializeToElement("80 mm"),
    ["thickness"] = JsonSerializer.SerializeToElement("1.5 mm"),
    ["lidLipHeight"] = JsonSerializer.SerializeToElement("8 mm"),
    ["insideRadius"] = JsonSerializer.SerializeToElement("2 mm"),
    ["kFactor"] = JsonSerializer.SerializeToElement(0.42),
    ["reliefPolicy"] = JsonSerializer.SerializeToElement("Rectangular")
};
var forgeOutput = Path.Combine(Path.GetTempPath(), "aetheris-public-package-forge-" + Guid.NewGuid().ToString("N"));
try
{
    var invocation = host.InvokeTemplate(
        "Standard.SheetMetal.ElectronicsEnclosure",
        new ForgeTemplateInvocationRequest(ForgeHostProtocol.Version, arguments, [ForgeArtifactKind.StepAp242]),
        forgeOutput);
    if (!invocation.Success)
        throw new InvalidOperationException(string.Join("; ", invocation.Diagnostics.Select(item => item.Code + ": " + item.Message)));
    var step = Path.Combine(forgeOutput, AssertSingle(invocation.Artifacts).Path);
    if (!File.Exists(step) || !Step242Importer.ImportBody(File.ReadAllText(step)).IsSuccess)
        throw new InvalidOperationException("Direct packaged Forge invocation did not produce reimportable STEP AP242.");
    Console.WriteLine("Direct packaged Forge API invocation produced reimportable STEP AP242.");
}
finally
{
    if (Directory.Exists(forgeOutput)) Directory.Delete(forgeOutput, recursive: true);
}

static ForgeArtifact AssertSingle(IReadOnlyList<ForgeArtifact> artifacts) =>
    artifacts.Count == 1 ? artifacts[0] : throw new InvalidOperationException($"Expected one Forge artifact, found {artifacts.Count}.");
