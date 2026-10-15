using System.Security.Cryptography;
using System.Text;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Modules;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Assembly;

internal static partial class AssemblyDefinitionMaterializer
{
    internal static MaterializedAssemblyDefinition? MaterializeModule(FirmamentModuleGraph graph,
        FirmamentModuleGraph.Reference reference, string identity, List<AssemblyDiagnostic> diagnostics,
        AssemblyDefinitionCache? cache = null)
    {
        var projection = reference.Module is null ? null : graph.ProjectSource(reference.Module, reference.Export, partContext: true);
        if (graph.Project is not null && reference.Module is { } module)
        {
            var inline = FirmamentSyntaxTraversal.Declarations(module.Syntax).FirstOrDefault(d => d.Type.Name.Text == "InlineStep");
            if (inline is not null)
            {
                diagnostics.Add(new("assembly-profile-external-resource-unsupported", "InlineStep requires a binary asset, which a source-only project snapshot cannot supply.")
                    { SourceSpan = new(module.Path,inline.Span.Start,inline.Span.Length) });
                return null;
            }
            if (reference.Export is not null)
            {
                var resource = FirmamentSyntaxTraversal.Expressions(module.Syntax).OfType<SpecializationSyntax>()
                    .FirstOrDefault(s => s.Target is ReferenceSyntax r && r.Name.Text is "LoftFile" or "SectionChainFile" or "ExternalStep");
                if (resource is not null)
                {
                    diagnostics.Add(new("firmament-module-resource-context-unsupported", "This concrete Part uses a file-resource adapter that has no source-snapshot context. Use a file-backed project or the existing project-aware Assembly resource definition.")
                        { SourceSpan = new(module.Path,resource.Span.Start,resource.Span.Length) });
                    return null;
                }
            }
        }
        var localIdentity = reference.Module is null ? "ExternalStep<\"" + reference.Path.Replace('\\', '/') + "\">" : graph.PartIdentity(reference, identity);
        MaterializedAssemblyDefinition? Build()
        {
            MaterializedAssemblyDefinition? result;
            if (reference.Module is null || reference.Export is null || reference.Export.Modifiers.Any(m => m.Name.Text == "Template"))
                result = TryMaterialize(localIdentity, projection?.Text, reference.Path, diagnostics, graph.Project, cache is null ? null : cache.ImportCanonical);
            else
            {
                var source = AssemblyPublishedPorts.Strip(projection!.Text, diagnostics);
                var compiled = FirmamentBuildAndExport.CompileSource(source, graph.Project is null ? Path.GetDirectoryName(reference.Path) : null);
                if (!compiled.IsSuccess || compiled.Value is null)
                {
                    foreach (var error in compiled.Diagnostics)
                        diagnostics.Add(new("assembly-definition-materialization-failed", error.Message)
                        { SourceSpan = reference.DeclarationOrigin,
                            LocationNote = "Enclosing exported Part; the engineering lowerer supplied no precise original expression location." });
                    return null;
                }
                var imported = cache is null ? Step242Importer.ImportBody(compiled.Value.StepText) : cache.ImportCanonical(compiled.Value.StepText);
                if (!imported.IsSuccess || imported.Value is null)
                {
                    diagnostics.AddRange(imported.Diagnostics.Select(d => new AssemblyDiagnostic("assembly-definition-reimport-failed", d.Message) { SourceSpan = reference.Origin }));
                    return null;
                }
                var provenance = new[] { new SemanticProvenance("Firmament-definition", reference.Path, reference.Definition,
                    reference.DeclarationOrigin) };
                result = new(identity, "module:" + reference.Definition, imported.Value,
                    graph.Ports(reference, identity, diagnostics), new("", identity, "module:" + reference.Definition,
                        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compiled.Value.StepText))), Metrics(imported.Value), provenance),
                    compiled.Value.StepText, compiled.Value.Cir);
            }
            if (result is null) return null;
            var stableId = "assembly-definition:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
            return result with { DefinitionIdentity = identity, DeclarationScope = null,
                Artifact = result.Artifact with { DefinitionIdentity = identity, StableId = stableId } };
        }
        return cache is null ? Build() : cache.Materialize(identity, projection?.Text, reference.Path, diagnostics, graph.Project, Build);
    }
}
