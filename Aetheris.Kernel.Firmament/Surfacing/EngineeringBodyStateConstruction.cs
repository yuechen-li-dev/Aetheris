using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Surfacing;

/// <summary>Adopts an engineering product without reconstructing it as a housing.
/// The existing compiler owns admission and geometry; the AST projection only
/// separates state history from the engineering program it references.</summary>
internal static class EngineeringBodyStateConstruction
{
    internal static bool IsStateDeclaration(StatementSyntax statement) => statement is DeclarationSyntax d
        && d.Type.Name.Text is "BodyState" or "SculptState" or "MeshState" or "SurfacePatch" or "SectionChain";

    internal static SculptResult Realize(EngineeringSourceConstruction recipe)
    {
        var selected = FirmamentFrontendSchemas.Select(recipe.Source);
        if (!selected.IsSuccess) return SculptResult.Failure(selected.LocatedDiagnostics.Select(d => new SculptDiagnostic(d.Code, d.Message)));
        var syntax = FirmamentSyntaxParser.Parse(selected.Source, recipe.SourceFile);
        if (!syntax.IsSuccess) return SculptResult.Failure(syntax.Diagnostics.Select(d => new SculptDiagnostic(d.Code, d.Message)));
        var models = syntax.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Model").ToArray();
        if (models.Length != 1 || models[0].Body is not {} body)
            return SculptResult.Failure([new("bodystate-source-invalid", "Engineering construction requires one Model with a body.", recipe.BaseId)]);
        var model = models[0];
        // Keep admitted engineering spelling and source offsets verbatim. Some
        // existing lowerers still consume authored literal/call spelling; this
        // projection is not a reason to reserialize their complete program.
        var characters = selected.Source.ToCharArray();
        foreach (var statement in body.Statements.Where(s => IsStateDeclaration(s)
            || s is FieldSyntax { Name.Text: "output" or "Output" }))
            for (var index = statement.Span.Start; index < statement.Span.Start + statement.Span.Length; index++)
                if (characters[index] is not ('\r' or '\n')) characters[index] = ' ';
        var source = new string(characters);
        var directory = recipe.SourceFile == "memory" ? null : Path.GetDirectoryName(Path.GetFullPath(recipe.SourceFile));
        var built = Aetheris.Kernel.Firmament.FirmamentBuildAndExport.CompileSource(source, directory);
        if (!built.IsSuccess)
        {
            var origins = new FirmamentSyntaxOrigins(syntax.Document, FirmamentSyntaxParser.Parse(source).Document);
            return SculptResult.Failure(built.Diagnostics.Select(d =>
            {
                var presented = FirmamentBuildAdmissionValidation.Present(d, recipe.SourceFile, source);
                var span = presented.SourceSpan is {} location ? origins.Find(location) : null;
                return new SculptDiagnostic(presented.Code, presented.Message, recipe.InputName)
                { SourceFile = recipe.SourceFile, SourceSpan = span is null ? null : new(span.Start, span.Length) };
            }));
        }
        var admitted = built.Value.RuntimeAdmissionDocument;
        var productNames = (admitted?.Composes is { Count: > 0 } compositions
            ? new[] { compositions[^1].Name }
            : admitted?.Solids.Select(s => s.Name) ?? []).ToHashSet(StringComparer.Ordinal);
        if (built.Value.RuntimeBodySourceName is {} nativeProduct) productNames = new(StringComparer.Ordinal) { nativeProduct };
        if (admitted?.ConceptIr?.MaterializedStruct.Name is {} materialized) productNames.Add(materialized);
        var productSyntax = FirmamentSyntaxParser.Parse(source).Document;
        var containingStruct = FirmamentV2Parser.MechanicalDeclarations(productSyntax.Statements).Any(d => d.Name?.Text == recipe.InputName
            && d.Type.Name.Text == "Struct" && !d.Modifiers.Any(m => m.Name.Text == "Concept")
            && FirmamentV2Parser.MechanicalDeclarations(d.Body?.Statements ?? []).Any(child => child.Name is {} n && productNames.Contains(n.Text)));
        if (!productNames.Contains(recipe.InputName) && !containingStruct)
            return SculptResult.Failure([new("bodystate-input-unresolved",
                $"BodyState '{recipe.BaseId}' input '{recipe.InputName}' does not name an admitted engineering body. Reference the Model's body or final Compose.", recipe.BaseId)]);
        var export = built.Value;
        BrepBody realized;
        if (export.RuntimeBody is {} runtime) realized = runtime;
        else
        {
            var imported = Step242Importer.ImportBody(export.StepText);
            if (!imported.IsSuccess) return SculptResult.Failure(imported.Diagnostics.Select(d => new SculptDiagnostic("bodystate-body-realization-failed", d.Message, recipe.BaseId)));
            realized = imported.Value;
        }
        var preflight = BrepExportPreflight.Validate(realized);
        if (!preflight.IsValid) return SculptResult.Failure(preflight.Diagnostics
            .Where(d => d.Severity == BrepExportPreflightSeverity.Error)
            .Select(d => new SculptDiagnostic("bodystate-body-invalid", d.Message, recipe.BaseId)));
        var id = BodyStateId.Derive($"EngineeringSource|{recipe.InputName}|{export.StepText}");
        var bodyId = export.RuntimeCorrespondence?.BodyStableId ?? recipe.InputName;
        var inventory = new Dictionary<string, SculptSemanticEntity>(StringComparer.Ordinal)
        {
            [bodyId] = new(bodyId, SculptEntityKind.Body, id.Value, "Admitted engineering body; no prototype regions inferred.")
        };
        var associations = new List<PersistentGeometryAssociation>();
        foreach (var face in realized.Topology.Faces)
        {
            var handle = "Face" + face.Id.Value;
            if (inventory.TryAdd(handle, new(handle, SculptEntityKind.Region, id.Value + ":" + handle, "Current body face handle; inspect EdgeN boundary identities before grafting.")))
                associations.Add(new(handle, PersistentAssociationState.Preserved, [face.Id.Value], "Exact topology identity; no proximity lookup."));
        }
        foreach (var group in export.RuntimeCorrespondence?.Descendants.GroupBy(d => d.SourceStableId) ?? [])
        {
            var faces = group.Where(d => d.Face is not null).Select(d => d.Face!.Value.Value).Distinct().Order().ToArray();
            inventory.TryAdd(group.Key, new(group.Key, SculptEntityKind.Region,
                string.Join(";", group.Select(d => d.StableId).Order(StringComparer.Ordinal)), "Explicit engineering materialization correspondence."));
            if (faces.Length > 0) associations.Add(new(group.Key, PersistentAssociationState.Preserved, faces, "Retained authoritative materializer correspondence."));
        }
        var evidence = new SculptValidationEvidence[]
        {
            new("EngineeringAdmission", true, LocalityEvidenceLevel.ExactSemantic, 0d, 0d,
                "Normal engineering compiler admitted the source; original STEP, PMI and exact body retained."),
            new("BrepExportPreflight", true, LocalityEvidenceLevel.CertifiedBounded, null, 0d,
                "Existing BRep export preflight passed. No general self-intersection proof is claimed.")
        };
        var authority = new ConstructionState(ConstructionState.CurrentSchemaId, ConstructionState.CurrentSchemaVersion, recipe, []);
        return new(true, new(id, null, bodyId, recipe.BaseId, realized, null, inventory, null, evidence,
            associations, ConstructionAuthority: authority, RealizedStepText: export.StepText)
            { RealizedStepProductName = export.DocumentName ?? model.Name?.Text,
              HasImportedEngineeringSource = FirmamentV2Parser.MechanicalDeclarations(model.Body?.Statements ?? []).Any(d => d.Type.Name.Text == "InlineStep") }, null, evidence, []);
    }
}
