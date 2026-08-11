using System.Reflection;

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

    if (version is null || !version.StartsWith("2.0.0-preview.2", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{assemblyName} reported unexpected version '{version}'.");
    }

    Console.WriteLine($"{assemblyName} {version}");
}
