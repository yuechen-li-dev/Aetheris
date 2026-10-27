using Aetheris.Kernel.Core.Diagnostics;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

/// <summary>A receipt from a completed geometry route, before authored display reports are attached.
/// Names are scoped to the material host; expanded Pattern keys remain part of the name.</summary>
public sealed record FirmamentFeatureReceipt(string Host, string Kind, string Name);

internal static class FirmamentFeatureAccounting
{
    internal static IReadOnlyList<FirmamentFeatureReceipt> Receipts(string host, string kind, params string[] names) =>
        names.Select(name => new FirmamentFeatureReceipt(host, kind, name)).Distinct().ToArray();

    internal static IReadOnlyList<FirmamentFeatureReceipt> Composition(
        PrismaticSectionStackConstruction stack, SemanticTopologyCorrespondence? correspondence)
    {
        var feature = stack.Feature;
        var active = stack.Slabs.SelectMany(s => s.ActiveOperations).ToHashSet(StringComparer.Ordinal);
        var realized = (correspondence?.Descendants ?? []).Where(d => d.Face.HasValue || d.Edge.HasValue)
            .Select(d => d.SourceStableId).ToHashSet(StringComparer.Ordinal);
        // Slabs identify operations consumed by the material arrangement. Semantic
        // cavities additionally need their actual topology descendants, including
        // the blind-drill bridge which is applied after ordinary stack emission.
        var receipts = feature.Operations.Where(o => active.Contains(o.Name) && o.SemanticFeatureId is null)
            .Select(o => new FirmamentFeatureReceipt(feature.Name, o.SemanticFeatureKind ?? o.Intent.ToString(), o.Name));
        receipts = receipts.Concat((feature.Bosses ?? []).Where(h => active.Contains(h.Name)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Boss", h.Name)))
            .Concat((feature.Pockets ?? []).Where(h => active.Contains(h.Name)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Pocket", h.Name)))
            .Concat((feature.MaterialOffsets ?? []).Where(h => active.Contains(h.Name)).Select(h => new FirmamentFeatureReceipt(feature.Name, h.OperationKind + "Offset", h.Name)))
            .Concat(feature.AllSlotProfiles.Where(h => realized.Contains(h.StableId)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Slot", h.Name)))
            .Concat((feature.ConstructionPlaneBlindDrills ?? []).Where(h => realized.Contains(h.StableId)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Hole", h.Name)))
            .Concat((feature.ShaftHoles ?? []).Where(h => realized.Contains(h.StableId)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Hole", h.Name)))
            .Concat((feature.CounterboreHoles ?? []).Where(h => realized.Contains(h.StableId)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Hole", h.Name)))
            .Concat((feature.CountersinkHoles ?? []).Where(h => realized.Contains(h.StableId)).Select(h => new FirmamentFeatureReceipt(feature.Name, "Hole", h.Name)));
        return receipts.Distinct().ToArray();
    }

    internal static IReadOnlyList<FirmamentFeatureReceipt> ProfileFinish(string source, string name, string materialHost)
    {
        var owners = FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements)
            .Where(d => d.Type.Name.Text == "Modify" && d.Body?.Statements.OfType<DeclarationSyntax>()
                .Any(f => f.Type.Name.Text == "EdgeFinish" && f.Name?.Text == name) == true).ToArray();
        if (owners.Length == 1) return Receipts(owners[0].Name!.Text, "EdgeFinish", name);
        if (owners.Length > 1) return [];
        var roots = FirmamentV2Parser.MechanicalDeclarations(FirmamentSyntaxParser.Parse(source).Document.Statements)
            .Count(d => d.Type.Name.Text == "EdgeFinish" && d.Name?.Text == name);
        return roots == 1 ? Receipts(materialHost, "EdgeFinish", name) : [];
    }

    internal static KernelResult<FirmamentStepExportResult> Verify(
        FirmamentV2ParseResult parsed, string source, FirmamentStepExportResult result)
    {
        if (!parsed.IsSuccess || parsed.Document is null) return KernelResult<FirmamentStepExportResult>.Success(result);
        var original = FirmamentSyntaxParser.Parse(FirmamentSourceSpelling.Normalize(source)).Document;
        // This is the admitted expansion, before materializer-specific source transport.
        // Checking only Document.ModifyBlocks misses the profile/composition binder's operations.
        var expanded = FirmamentSyntaxParser.Parse(parsed.DiagnosticSource ?? parsed.BoundSource ?? source).Document;
        var origins = new FirmamentSyntaxOrigins(original, expanded);
        // The admitted compatibility grammar retains lowercase construct tokens.
        // Construct kind casing is not an operation identity; authored names are.
        static (string Host, string Kind, string Name) Identity(FirmamentFeatureReceipt receipt) =>
            (receipt.Host, receipt.Kind.ToUpperInvariant(), receipt.Name);
        var actual = result.MaterializedFeatures.Select(Identity).ToHashSet();
        var errors = new List<KernelDiagnostic>();
        var missing = new HashSet<(string Host, string Kind, string Name)>();
        void Missing(string host, string kind, string name, FirmamentV2SourceSpan span)
        {
            if (actual.Contains(Identity(new(host, kind, name))) || !missing.Add(Identity(new(host, kind, name)))) return;
            var authored = origins.Find(span);
            var enclosing = authored is null ? FirmamentSyntaxTraversal.Declarations(original)
                .Where(d => d.Type.Name.Text is "Modify" or "Compose" && d.Name?.Text == host).ToArray() : [];
            var location = authored ?? (enclosing.Length == 1 ? enclosing[0].Name!.Span : null);
            errors.Add(new(KernelDiagnosticCode.ValidationFailed, KernelDiagnosticSeverity.Error,
                $"firmament-feature-not-materialized:{kind}:{name}: {kind} {name} was accepted but not built on '{host}'.",
                "Firmament.FeatureAccounting")
            {
                OriginalSourceSpan = location is null ? null : new(location.Start, location.Length)
                { LocationNote = authored is null ? "Enclosing material operation; the expanded instance has no exact original declaration span." : null },
                SourceSpan = new(span.Start, span.Length)
                { LocationNote = authored is null ? "Expanded feature; original declaration mapping is unavailable." : null }
            });
        }
        // Semantic point Patterns expand into bound instances rather than source text.
        // Demand every bound instance, then independently check the declared expansion count.
        foreach (var modify in parsed.Document.ModifyBlocks ?? [])
            foreach (var hole in modify.SemanticHoles)
                Missing(modify.TargetSolid, "Hole", hole.Name, hole.SourceSpan ?? new(0, 0));
        foreach (var pattern in parsed.Document.ConceptIr?.PatternExpansions ?? [])
        {
            var generated = pattern.GeneratedDeclarations.Select(p => p.Split("::")[^1]).ToArray();
            if (pattern.Count == 0 && generated.Length == 0) continue;
            var host = (parsed.Document.ModifyBlocks ?? []).SingleOrDefault(m => m.SemanticHoles.Any(h => generated.Contains(h.Name)))?.TargetSolid;
            if (host is not null && generated.Length == pattern.Count && generated.Distinct().Count() == pattern.Count
                && generated.All(name => actual.Contains(Identity(new(host, "Hole", name))))) continue;
            var name = pattern.Pattern.Split("::")[^1];
            Missing(host ?? pattern.Pattern.Split("::")[0], "Pattern", name, pattern.SourceSpan);
        }
        void Scope(IEnumerable<StatementSyntax> statements)
        {
            foreach (var declaration in statements.OfType<DeclarationSyntax>())
            {
                // Definitions are not executions. Their instantiated bodies are in the admitted expansion.
                if (declaration.Type.Name.Text is "Template" or "Feature" or "Record" or "Interface"
                    || declaration.Modifiers.Any(m => m.Name.Text is "Concept" or "Template")) continue;
                if (declaration.Type.Name.Text is "Modify" or "Compose")
                {
                    var host = declaration.Name?.Text ?? "";
                    foreach (var feature in declaration.Body?.Statements.OfType<DeclarationSyntax>() ?? [])
                    {
                        // Guides are compile-time input, owned and checked by the Profile binder.
                        if (ProfileAuthoringParser.OwnsDeclaration(feature)) continue;
                        if (feature.Type.Name.Text == "Pattern" && (parsed.Document.ConceptIr?.PatternExpansions ?? [])
                            .Any(p => p.Pattern.EndsWith("::" + feature.Name?.Text, StringComparison.Ordinal))) continue;
                        Missing(host, feature.Type.Name.Text, feature.Name?.Text ?? "", feature.Name?.Span ?? feature.Span);
                    }
                }
                else if (declaration.Type.Name.Text == "EdgeFinish")
                    Missing(parsed.BoundProfile?.Name ?? parsed.BoundComposition?.Feature?.Name
                        ?? parsed.BoundCoaxialComposition?.Name ?? parsed.Document.Solids.SingleOrDefault()?.Name ?? "",
                        "EdgeFinish", declaration.Name?.Text ?? "", declaration.Name?.Span ?? declaration.Span);
                else if (declaration.Body is not null) Scope(declaration.Body.Statements);
            }
        }
        Scope(expanded.Statements);
        return errors.Count > 0 ? KernelResult<FirmamentStepExportResult>.Failure(errors)
            : KernelResult<FirmamentStepExportResult>.Success(result);
    }
}
