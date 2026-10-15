namespace Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

/// <summary>Finite lexical declaration ownership. Callable and construction names have
/// distinct namespaces (a Feature and its Profile may legitimately share a name).</summary>
internal sealed class FirmamentBindingScope(FirmamentBindingScope? parent, string identity)
{
    private readonly Dictionary<(string Kind, string Name), DeclarationSyntax> symbols = new();
    internal string Identity => identity;
    internal bool Declare(string kind, DeclarationSyntax declaration) => declaration.Name is { } name
        && symbols.TryAdd((kind, name.Text), declaration);
    internal DeclarationSyntax? Resolve(string kind, string name) => symbols.GetValueOrDefault((kind, name)) ?? parent?.Resolve(kind, name);
}
