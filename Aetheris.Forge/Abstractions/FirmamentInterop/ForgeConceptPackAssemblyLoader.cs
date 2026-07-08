using System.Reflection;
using System.Runtime.Loader;

namespace Aetheris.Forge.Abstractions.FirmamentInterop;

public sealed class ForgeConceptPackAssemblyLoader
{
    public IReadOnlyList<IForgeConceptPack> LoadFromAssemblyPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Forge concept pack path '{path}' must be a local file path, not a URI. Forge concept packs are trusted local code execution. Aetheris does not sandbox external packs. Do not load packs you do not trust.");
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            throw new InvalidOperationException(
                $"Forge concept pack path '{path}' must be a local file path, not a URI. Forge concept packs are trusted local code execution. Aetheris does not sandbox external packs. Do not load packs you do not trust.");
        }

        var fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"Forge concept pack path '{path}' resolves to a directory. Forge concept packs are trusted local code execution. Aetheris does not sandbox external packs. Do not load packs you do not trust.");
        }

        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                $"Forge concept pack assembly was not found: {path}. Forge concept packs are trusted local code execution. Aetheris does not sandbox external packs. Do not load packs you do not trust.");
        }

        if (!string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Forge concept pack path '{path}' must point to a local .dll assembly. Forge concept packs are trusted local code execution. Aetheris does not sandbox external packs. Do not load packs you do not trust.");
        }

        Assembly assembly;
        try
        {
            assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
        catch (BadImageFormatException ex)
        {
            throw new InvalidOperationException($"Forge concept pack assembly '{path}' is not a valid .NET assembly.", ex);
        }
        catch (FileLoadException ex)
        {
            throw new InvalidOperationException($"Forge concept pack assembly '{path}' could not be loaded.", ex);
        }

        var packTypes = assembly
            .GetExportedTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IForgeConceptPack).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        if (packTypes.Length == 0)
        {
            throw new InvalidOperationException($"Forge concept pack assembly '{path}' contains no public IForgeConceptPack implementations.");
        }

        var packs = new List<IForgeConceptPack>(packTypes.Length);
        foreach (var packType in packTypes)
        {
            var constructor = packType.GetConstructor(Type.EmptyTypes);
            if (constructor is null || !constructor.IsPublic)
            {
                throw new InvalidOperationException(
                    $"Forge concept pack type '{packType.FullName}' in '{path}' must have a public parameterless constructor.");
            }

            try
            {
                packs.Add((IForgeConceptPack)constructor.Invoke(null));
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException(
                    $"Forge concept pack type '{packType.FullName}' in '{path}' threw during activation: {ex.InnerException?.Message ?? ex.Message}",
                    ex.InnerException ?? ex);
            }
        }

        return packs;
    }
}
