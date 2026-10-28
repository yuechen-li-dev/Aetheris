using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static partial class FirmamentV2Parser
{
    internal static IEnumerable<DeclarationSyntax> MechanicalDeclarations(IEnumerable<StatementSyntax> statements)
    {
        foreach (var declaration in statements.OfType<DeclarationSyntax>())
        {
            if (declaration.Modifiers.Any(m => m.Name.Text == "Template") || declaration.Type.Name.Text == "Template") continue;
            yield return declaration;
            if (declaration.Type.Name.Text is "Model" or "Struct" && !declaration.Modifiers.Any(m => m.Name.Text == "Concept"))
                foreach (var child in MechanicalDeclarations(declaration.Body?.Statements ?? [])) yield return child;
        }
    }

    internal static bool IsPrimitiveDeclaration(DeclarationSyntax d) => d.Type.Name.Text is
        "Box" or "Cylinder" or "Cone" or "Sphere" or "Ellipsoid" or "Polyhedron" or "Torus" or "RoundedBox" or "Frustum" or "StandardPart" or "ExactCoaxialPart";

    private static FirmamentV2ParseResult? RejectMultipleMaterialBodies(IReadOnlyList<DeclarationSyntax> bodies,
        string model, List<string> diagnostics)
    {
        if (bodies.Count <= 1) return null;
        const string code = "firmament-v2-multiple-material-bodies-unsupported";
        diagnostics.Add(code);
        return CanonicalFailure(diagnostics, CanonicalDocumentMalformed) with
        {
            SyntaxDiagnostics = [new(code, "fatal",
                $"Model '{model}' declares {bodies.Count} material bodies ({string.Join(", ", bodies.Select(d => d.Name?.Text))}). The current Model exporter admits one body. Use separate Parts in an Assembly for separate objects, or Compose for admitted material operations on one body.")
                { SourceSpan = bodies[1].Span, Stage = "Capability" }]
        };
    }

    /// <summary>Mechanical syntax has one owner. Selection of a material lowerer
    /// follows declared material bodies; unused construction data cannot select a grammar.</summary>
    private static FirmamentV2ParseResult BindMechanicalModel(DocumentSyntax syntax, DeclarationSyntax model,
        string source, List<string> diagnostics, string? sourceDirectory,
        IReadOnlyList<ConceptIrTemplateInstantiation>? templateInstantiations, FirmamentCompileTimeValues? compileTime = null,
        IReadOnlyDictionary<string, string>? resources = null, IReadOnlyList<FirmamentV2ClosedBoundary2Decl>? boundaries = null,
        DocumentSyntax? authoredSyntax = null)
    {
        if (model.Body is null || model.Name is null) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        if (model.Clauses.Count > 0 || model.Type.Arguments.Count > 0)
            return CanonicalFailure(diagnostics, "firmament-v2-mechanical-root-header-unsupported: Use the admitted Model/Struct header and declare placement in its owning construct.");
        var materialBodies = MechanicalDeclarations(model.Body.Statements).Where(d => IsPrimitiveDeclaration(d) || d.Type.Name.Text == "InlineStep").ToArray();
        if (RejectMultipleMaterialBodies(materialBodies, model.Name.Text, diagnostics) is { } rejectedBodies) return rejectedBodies;
        if (model.Annotation is not null)
        {
            if (syntax.Statements.Concat(model.Body.Statements).OfType<DeclarationSyntax>().Any(d => d.Type.Name.Text == "Static"))
                return CanonicalFailure(diagnostics, "firmament-v2-static-scalar-consumer-unsupported: Concept-valued material construction requires its existing typed Template inputs.");
            return ParseConceptModelingDocument(source, diagnostics, templateInstantiations, compileTime);
        }
        var outerStatements = syntax.Statements.Where(s => !ReferenceEquals(s, model)).ToArray();
        var staticValues = BindStaticValues(syntax, model, source, diagnostics, compileTime?.MaterialInputs(source), compileTime);
        model = staticValues.Model;
        BindMechanicalScope(outerStatements.Where(s => s is not DeclarationSyntax { Type.Name.Text: "Static" }).ToArray(), diagnostics);
        BindMechanicalScope(model.Body!.Statements.Where(s => s is not DeclarationSyntax { Type.Name.Text: "Static" }).ToArray(), diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed)
            with { SyntaxDiagnostics = staticValues.Diagnostics };
        var declarations = MechanicalDeclarations(model.Body.Statements).ToArray();
        // Non-materialized guides are still authored intent. Validate them through
        // their ordinary owner even when no Profile happens to consume them.
        diagnostics.AddRange(ProfileAuthoringParser.ValidateConstructionGuides(source,
            compileTime is null ? null : compileTime.NominalExpression));
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        var primitives = declarations.Where(IsPrimitiveDeclaration).ToArray();
        var constructions = declarations.Where(d => d.Type.Name.Text is "Compose" or "Extrude").ToArray();
        var materialConstruction = constructions.Length > 0;
        // Concept-valued stock retains its existing semantic resolver. Its actual
        // declarations do not change the grammar of neighbouring nodes.
        var semanticStock = declarations.Any(d => d.Type.Name.Text == "Pattern" || d.Type.Name.Text == "Modify" && d.Body?.Statements.OfType<DeclarationSyntax>().Any(f => f.Type.Name.Text == "Pattern") == true)
            || declarations.Any(d => d.Type.Name.Text == "Struct" && d.Annotation is not null)
            || declarations.Where(d => d.Type.Name.Text == "Modify").SelectMany(d => d.Body?.Statements.OfType<DeclarationSyntax>() ?? [])
                .Any(d => d.Type.Name.Text == "Hole" && d.Body?.Statements.OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "From")
                    || FirmamentSyntaxFields.SameField(f.Name.Text, "Center") && f.Value is IndexSyntax) == true);
        var constructionConsumer = materialConstruction || semanticStock || primitives.Any(d => d.Body?.Statements.OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Bounds")) == true);
        var wholeStockFinish = declarations.Where(d => d.Type.Name.Text == "Modify")
            .SelectMany(d => d.Body?.Statements.OfType<DeclarationSyntax>() ?? [])
            .FirstOrDefault(d => d.Type.Name.Text == "EdgeFinish" && d.Body?.Statements.OfType<FieldSyntax>()
                .Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Target") && FirmamentSyntaxValues.Reference(f.Value) == "AllEdges") == true);
        if (constructionConsumer && wholeStockFinish is not null)
        {
            diagnostics.Add(UniformStockFilletAdmission.Code);
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed) with
            { SyntaxDiagnostics = [new(UniformStockFilletAdmission.Code, "fatal",
                "target: AllEdges currently requires direct size-based Box or regular Polyhedron stock. Profile, Pattern and semantic Bounds construction adapters do not yet admit whole-stock rounding; arbitrary-body rounding is unsupported.")
                { SourceSpan = wholeStockFinish.Span, Stage = "Capability" }] };
        }
        if (constructions.Length > 1 && constructions.All(d => d.Type.Name.Text == "Extrude") && primitives.Length == 0)
            return BindMechanicalExtrusionStack(model, constructions, source, diagnostics);
        if (constructions.Length > 1 || materialConstruction && primitives.Length > 0)
            return CanonicalFailure(diagnostics, "firmament-v2-mechanical-material-owners-unsupported: Use one material body per mechanical document; use Assembly for separate parts.");
        if (constructionConsumer)
        {
            // The construction adapter consumes serialized bound AST fields. An
            // unused Static is data, never a reason to reject a material owner.
            var construction = constructions.Length == 1 && IsCoaxialComposition(constructions[0])
                ? BindCoaxialComposition(model, constructions[0], source, diagnostics, staticValues.Values)
                : BindMechanicalConstruction(source, model, diagnostics, templateInstantiations, compileTime);
            return construction.Document is null ? construction : construction with
            {
                Document = construction.Document with { Lets = staticValues.Declarations, BoundLets = staticValues.Values },
                SyntaxDiagnostics = construction.SyntaxDiagnostics.Concat(staticValues.Diagnostics).ToArray()
            };
        }

        var units = model.Body.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray();
        if (units.Length == 0 && model.Type.Name.Text != "Struct" || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm")) return CanonicalFailure(diagnostics, CanonicalUnitsInvalid);
        foreach (var profile in declarations.Where(d => d.Type.Name.Text == "Profile" && d.Name is not null))
        {
            _ = ProfileAuthoringParser.ResolveNamedProfile(source, profile.Name!.Text, out var profileDiagnostics);
            diagnostics.AddRange(profileDiagnostics);
        }
        foreach (var path in declarations.Where(d => d.Type.Name.Text == "Path" && d.Name is not null))
        {
            _ = ProfileAuthoringParser.ResolveConceptPath(source, path.Name!.Text, out var pathDiagnostics);
            diagnostics.AddRange(pathDiagnostics);
        }
        var solids = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal);
        var fieldDiagnostics = new List<FirmamentV2ValidationDiagnostic>();
        void CaptureFieldDiagnostics(DeclarationSyntax declaration, int start)
        {
            for (var index = start; index < diagnostics.Count; index++)
            {
                var diagnostic = diagnostics[index];
                var parts = diagnostic.Split(": ", 2, StringSplitOptions.None);
                var adapted = FirmamentInterop.FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode(diagnostic);
                var fieldName = parts[0].Split(':')[^1];
                var field = declaration.Body?.Statements.OfType<FieldSyntax>().FirstOrDefault(f => FirmamentSyntaxFields.SameField(f.Name.Text, fieldName));
                // Keep the stable code separate from its explanatory text. Both
                // presentation and preflight consume this same owner-located error.
                diagnostics[index] = parts[0];
                fieldDiagnostics.Add(new(parts[0], adapted.Severity.ToString().ToLowerInvariant(), parts.Length == 2 ? parts[1] : adapted.Message,
                    FieldName: field?.Name.Text, Target: declaration.Name?.Text)
                { SourceSpan = field?.Name.Span ?? declaration.Span, Stage = "Binding" });
            }
        }
        foreach (var declaration in primitives)
        {
            var fields = new FirmamentSyntaxFields(declaration, diagnostics);
            var diagnosticStart = diagnostics.Count;
            var primitive = BindMechanicalPrimitive(fields, diagnostics);
            FirmamentV2HollowIntent? hollow = null;
            if (declaration.Type.Arguments.Count > 0 && declaration.Type.Name.Text != "Polyhedron")
                hollow = BindMechanicalHollow(fields, primitive, diagnostics);
            fields.Complete();
            CaptureFieldDiagnostics(declaration, diagnosticStart);
            if (primitive is null) { diagnostics.Add(CanonicalPrimitiveMalformed); continue; }
            if (declaration.Name is null || !solids.TryAdd(declaration.Name.Text, new(declaration.Name.Text,
                declaration.Type.Name.Text, primitive, ConstructionPolicy: hollow is null ? FirmamentV2ConstructionPolicy.Solid : FirmamentV2ConstructionPolicy.Hollow, Hollow: hollow, SourceSpan: declaration.Span, AuthoredFields: fields.AuthoredFields))) diagnostics.Add(DuplicateName);
        }
        foreach (var declaration in declarations.Where(d => d.Type.Name.Text == "InlineStep"))
        {
            var body = declaration.Body;
            if (body is null || declaration.Name is null) { diagnostics.Add(CanonicalPrimitiveMalformed); continue; }
            var diagnosticStart = diagnostics.Count;
            var imported = BindImportedStep(declaration, diagnostics, sourceDirectory, resources);
            CaptureFieldDiagnostics(declaration, diagnosticStart);
            if (imported is not null && !solids.TryAdd(imported.Name, imported)) diagnostics.Add(DuplicateName);
        }
        if (solids.Count == 0) diagnostics.Add(MissingSolid);
        var modifies = new List<FirmamentV2ModifyBlock>();
        var pathFeatures = new List<FirmamentV2PathFeature>();
        foreach (var modify in declarations.Where(d => d.Type.Name.Text == "Modify"))
        {
            if (modify.Clauses.Count > 0 || modify.Type.Arguments.Count > 0) diagnostics.Add(CanonicalModifyMalformed);
            if (modify.Name is null || !solids.TryGetValue(modify.Name.Text, out var solid)) { diagnostics.Add(ModifyTargetUnresolved); continue; }
            // An authored empty body is an admitted no-op after target binding.
            // Keep it out of material feature lowering, which requires real work.
            if (modify.Body is { Statements.Count: 0 }) continue;
            var holes = new List<FirmamentV2SemanticHoleDecl>(); var finishes = new List<FirmamentV2EdgeFinishDecl>(); var perforations = new List<FirmamentV2PerforationDecl>();
            foreach (var statement in modify.Body?.Statements ?? [])
            {
                if (statement is not DeclarationSyntax feature) { diagnostics.Add(CanonicalModifyMalformed); continue; }
                var fields = new FirmamentSyntaxFields(feature, diagnostics);
                var diagnosticStart = diagnostics.Count;
                switch (feature.Type.Name.Text)
                {
                    case "Hole":
                        if (BindMechanicalHole(fields, diagnostics, ownerName: solid.Name) is { } hole) holes.Add(hole);
                        break;
                    case "EdgeFinish":
                        if (BindMechanicalFinish(fields, diagnostics) is { } finish) finishes.Add(finish);
                        break;
                    case "Perforation":
                        if (BindMechanicalPerforation(fields, diagnostics) is { } perforation) perforations.Add(perforation);
                        break;
                    case "Rib" or "Groove":
                        if (BindPathFeature(fields, solid.Name, source, diagnostics, compileTime, boundaries ?? [], authoredSyntax ?? syntax) is { } pathFeature) pathFeatures.Add(pathFeature);
                        break;
                    default: diagnostics.Add($"{CanonicalDeclarationUnknown}:Modify:{feature.Type.Name.Text}"); break;
                }
                fields.Complete();
                CaptureFieldDiagnostics(feature, diagnosticStart);
            }
            if (holes.Count > 0 && solid.Box is null) diagnostics.Add(ModifyTargetNotSolid);
            if (solid.ConstructionPolicy == FirmamentV2ConstructionPolicy.Hollow && !pathFeatures.Any(f => f.Host == solid.Name) &&
                (solid.Cylinder is null || holes.Count > 0 || finishes.Count > 0 || perforations.Count != 1 ||
                 perforations.Any(p => p.SupportFace != "OuterWall" || p.Layout is not ("CylindricalGrid" or "CylindricalStaggered"))))
                diagnostics.Add("firmament-v2-hollow-modify-unsupported");
            modifies.Add(new(solid.Name, [], holes, finishes, perforations));
        }
        if (pathFeatures.Count > 0)
        {
            var stock = solids.Values.SingleOrDefault();
            if (stock is null || stock.Box is null && stock.Cylinder is null
                || modifies.Any(m => m.SemanticHoles.Count > 0 || m.EdgeFinishes?.Count > 0 || m.Perforations?.Count > 0)
                || declarations.Any(d => d.Type.Name.Text == "Thread"))
                diagnostics.Add("firmament-path-feature-host: Use direct Box or Cylinder stock without Hole, EdgeFinish, Perforation or Thread operations.");
            else if (stock.Box is not null && pathFeatures.Any(f => f.Support != "+Z") || stock.Cylinder is not null
                && pathFeatures.Any(f => f.Feature.Add || f.Support is not ("OuterWall" or "InnerWall")
                    || f.Feature.Guide.Kind != Aetheris.Kernel.Core.Brep.Features.PlanarGuideKind.Circle
                    || double.Abs(f.Feature.Guide.Center.X) + double.Abs(f.Feature.Guide.Center.Y) > 1e-9
                    || double.Abs(f.Feature.Guide.Radius - (stock.Cylinder.Radius - (f.Support == "InnerWall" ? stock.Hollow?.WallThickness ?? 0 : 0))) > 1e-8))
                diagnostics.Add("firmament-path-feature-support: Box supports +Z; cylindrical grooves require a coaxial Circle2 on OuterWall or InnerWall with the support radius and an explicit station.");
            if (stock?.Hollow is { } shell && (shell.BottomBlendRadius != 0 || shell.Openings.Count != 1 || shell.Openings[0] != "Top"))
                diagnostics.Add("firmament-path-feature-hollow: Cylindrical grooves currently support only top-open hollow stock without a bottom blend.");
        }
        if (UniformStockFilletAdmission.Error(solids.Values.ToArray(), modifies,
            declarations.Any(d => d.Type.Name.Text is "Thread" or "Replace")) is { } stockError)
        {
            var operation = modifies.SelectMany(m => m.EdgeFinishes ?? []).First(f => f.Target == "AllEdges");
            diagnostics.Add(UniformStockFilletAdmission.Code);
            return CanonicalFailure(diagnostics, CanonicalDocumentMalformed) with
            { SyntaxDiagnostics = [new(UniformStockFilletAdmission.Code, "fatal", stockError) { SourceSpan = operation.SourceSpan, Stage = "Capability" }] };
        }
        var bodyText = source.Substring(model.Body.Span.Start + 1, model.Body.Span.Length - 2);
        var recognized = BindImportedRegions(declarations, solids, diagnostics, CaptureFieldDiagnostics);
        var replacements = BindImportedReplacements(declarations, solids, recognized, diagnostics, source, CaptureFieldDiagnostics);
        var removals = BindImportedRemovals(declarations, solids, recognized, diagnostics, CaptureFieldDiagnostics);
        var selections = ParseCanonicalSelections(bodyText, model.Body.Span.Start + 1, diagnostics);
        ValidateCanonicalSelectionSources(bodyText, selections, [], diagnostics);
        var (pmi, pmiBlock, boundPmi) = ParsePmi(source, solids, modifies, recognized, staticValues.Values, [], diagnostics);
        var pmiDiagnostics = ValidateStaticPmi(pmiBlock, boundPmi, modifies, staticValues.Values, diagnostics);
        var locatedDiagnostics = (compileTime?.Diagnostics ?? []).Concat(staticValues.Diagnostics).Concat(pmiDiagnostics).Concat(fieldDiagnostics).ToArray();
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed)
            with { SyntaxDiagnostics = locatedDiagnostics };
        return FirmamentV2ParseResult.Success(new(model.Name!.Text, "mm", solids.Values.ToArray(), modifies,
            Pmi: pmi, RecognizedRegions: recognized, Replacements: replacements, PmiBlock: pmiBlock, BoundPmi: boundPmi, Selections: selections,
            Lets: staticValues.Declarations, BoundLets: staticValues.Values,
            TemplateInstantiations: templateInstantiations is { Count: > 0 } ? templateInstantiations : null) { Removals = removals, PathFeatures = pathFeatures },
            diagnostics.Append("firmament-v2-unified-canonical-parsed").Append("firmament-v2-parse-succeeded").Distinct(StringComparer.Ordinal).Order().ToArray())
            with { SyntaxDiagnostics = locatedDiagnostics };
    }

    // Multiple explicit extrusions are material requests, not alternative parser
    // hints. Bind every one into the existing section-stack engineering IR.
    private static FirmamentV2ParseResult BindMechanicalExtrusionStack(DeclarationSyntax model, IReadOnlyList<DeclarationSyntax> extrusions,
        string source, List<string> diagnostics)
    {
        var units = model.Body!.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray();
        if (units.Length == 0 && model.Type.Name.Text != "Struct" || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm")) return CanonicalFailure(diagnostics, CanonicalUnitsInvalid);
        if (MechanicalDeclarations(model.Body.Statements).Any(d => d.Type.Name.Text == "Modify"))
            return CanonicalFailure(diagnostics, "firmament-v2-extrusion-stack-modify-unsupported: Use Compose for features on a multi-extrusion body.");
        var profiles = new Dictionary<string, ResolvedProfile2D>(StringComparer.Ordinal);
        var operations = new List<PrismaticProfileOperation>();
        foreach (var d in extrusions)
        {
            var fields = new FirmamentSyntaxFields(d, diagnostics);
            var profileName = fields.Word("Profile"); var from = fields.Number("From", "mm"); var to = fields.Number("To", "mm");
            var role = fields.Word("Role") ?? "Stock";
            fields.Complete();
            if (profileName is null || d.Name is null || !double.IsFinite(from) || !double.IsFinite(to) || from >= to)
            { diagnostics.Add("profile-extrude-missing-or-mismatched"); continue; }
            if (!profiles.TryGetValue(profileName, out var profile))
            {
                profile = ProfileAuthoringParser.ResolveNamedProfile(source, profileName, out var reported);
                diagnostics.AddRange(reported);
                if (profile is null) continue;
                profiles.Add(profileName, profile);
            }
            if (profile.EffectiveConstructionPlane != ConstructionPlane.WorldXY)
            { diagnostics.Add("firmament-v2-extrusion-stack-frame-unsupported: Multiple extrusions currently require the shared world XY frame."); continue; }
            if (operations.Any(operation => operation.Name == d.Name.Text)) { diagnostics.Add(DuplicateName); continue; }
            operations.Add(new(d.Name.Text, operations.Count == 0 ? PrismaticProfileIntent.Base : PrismaticProfileIntent.Add,
                profileName, from, to, role, $"offset:{d.Span.Start}"));
        }
        if (diagnostics.Any(IsFatalDiagnosticCode) || operations.Count != extrusions.Count) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        var name = model.Name!.Text;
        var placement = new PrismaticProfilePlacement("LegacyImplicitWorldXY", 0, 0, 0, "XY", "+Z", "+X", false);
        var feature = new PrismaticProfileCompositionFeature(name, "XY", "+Z", placement, operations,
            operations.SelectMany(o => new[] { o.From, o.To }).Distinct().Order().ToArray(), "ast-extrusion-stack");
        var composition = new PrismaticProfileCompositionParseResult(feature, profiles, []);
        var solids = new Dictionary<string, FirmamentV2SolidBinding>(StringComparer.Ordinal)
            { [name] = new(name, "Compose", new FirmamentV2AdvancedMaterialRecord("Compose")) };
        var selections = ParseCanonicalSelections(source, 0, diagnostics);
        ValidateCanonicalSelectionSources(source, selections, profiles.Keys, diagnostics);
        var (pmi, pmiBlock, boundPmi) = ParsePmi(source, solids, [], [], [], [], diagnostics);
        if (diagnostics.Any(IsFatalDiagnosticCode)) return CanonicalFailure(diagnostics, CanonicalDocumentMalformed);
        var document = new FirmamentV2Document(name, "mm", solids.Values.ToArray(),
            Pmi: pmi, PmiBlock: pmiBlock, BoundPmi: boundPmi, Selections: selections,
            Profiles: profiles.Values.Select(p => new FirmamentV2ProfileDecl(p.Name, p.EffectiveConstructionPlane.StableId, 0, 0, model.Span)).ToArray(),
            Composes: [new(name, operations.Select(o => o.Name).ToArray(), model.Span)]);
        return FirmamentV2ParseResult.Success(document, diagnostics.Append("firmament-v2-parse-succeeded").Distinct(StringComparer.Ordinal).Order().ToArray())
            with { BoundComposition = composition, BoundSource = source };
    }

    private static FirmamentV2HollowIntent? BindMechanicalHollow(FirmamentSyntaxFields f, FirmamentV2PrimitiveRecord? primitive, List<string> diagnostics)
    {
        var d = f.Declaration;
        if (d.Type.Arguments.Count != 1 || FirmamentSyntaxValues.Reference(d.Type.Arguments[0].Value) != "Hollow")
        { diagnostics.Add(UnknownConstructionPolicy); return null; }
        if (d.Type.Name.Text is not ("RoundedBox" or "Cylinder" or "Frustum"))
        { diagnostics.Add(PrimitiveDoesNotSatisfyHollowConstructible); return null; }
        var wallExpression = f.Take("WallThickness");
        if (wallExpression is null) diagnostics.Add(WallThicknessRequired);
        var wall = FirmamentSyntaxValues.TryMeasure(wallExpression, "mm", out var w) ? w : double.NaN;
        if (!(wall > 0)) diagnostics.Add(WallThicknessMustBePositive);
        var openings = f.Take("Openings") as SequenceSyntax;
        var items = openings?.Items.Select(a => FirmamentSyntaxValues.Reference(a.Value)).ToArray() ?? [];
        if (items.Length != 1 || items[0] != "Top") diagnostics.Add(items.Length > 1 ? MultipleOpeningsNotSupported : UnsupportedOpening);
        var blendExpression = f.Take("BottomBlendRadius");
        var blend = blendExpression is null ? 0 : FirmamentSyntaxValues.TryMeasure(blendExpression, "mm", out var b) ? b : double.NaN;
        if (blendExpression is not null && (primitive is not FirmamentV2CylinderRecord cylinder || !(blend > wall && blend < cylinder.Radius && blend < cylinder.Height)))
            diagnostics.Add("firmament-v2-hollow-bottom-blend-invalid");
        return new(wall, items.OfType<string>().ToArray(), d.Span, blend);
    }

    private static FirmamentV2PrimitiveRecord? BindMechanicalPrimitive(FirmamentSyntaxFields f, List<string> diagnostics)
    {
        var type = f.Declaration.Type.Name.Text;
        bool Representable(double value, string field)
        {
            if (!double.IsFinite(value)) return false;
            if (Math.BitIncrement(Math.Abs(value)) - Math.Abs(value) <= Aetheris.Kernel.Core.Numerics.ToleranceContext.Default.Linear) return true;
            diagnostics.Add($"firmament-dimension-precision-unsupported:{type}:{field}: The dimension is too large for the kernel's linear tolerance. Use a smaller model extent or a local coordinate frame.");
            return false;
        }
        double Length(string name)
        {
            var value = f.Number(name, "mm");
            return Representable(value, name) ? value : double.NaN;
        }
        double[]? Size()
        {
            if (!f.Vector("Size", "mm", out var values)) return null;
            if (values.Length != 3) { diagnostics.Add(BoxSizeArity); return null; }
            if (values.Any(v => v <= 0)) { diagnostics.Add(DegenerateDimension); return null; }
            if (values.Any(v => !Representable(v, "Size"))) return null;
            return values;
        }
        switch (type)
        {
            case "Box": return Size() is { } box ? new FirmamentV2BoxRecord(box, []) : null;
            case "Cylinder":
                var radius = Length("Radius"); var height = Length("Height");
                return radius > 0 && height > 0 ? new FirmamentV2CylinderRecord(radius, height) : null;
            case "Cone":
                var bottom = Length("BottomRadius"); var top = Length("TopRadius"); var coneHeight = Length("Height");
                return bottom > 0 && top >= 0 && coneHeight > 0 ? new FirmamentV2ConeRecord(bottom, top, coneHeight) : null;
            case "Sphere": var sphere = Length("Radius"); return sphere > 0 ? new FirmamentV2SphereRecord(sphere) : null;
            case "Ellipsoid":
                if (!f.Vector("Radii", "mm", out var radii) || radii.Length != 3 || radii.Any(r => !double.IsFinite(r) || r <= 0))
                { diagnostics.Add("firmament-v2-ellipsoid-radii-invalid: radii requires three finite positive X/Y/Z semiaxes in length units."); return null; }
                return new FirmamentV2EllipsoidRecord(radii);
            case "Polyhedron":
                var arguments = f.Declaration.Type.Arguments;
                var edgeLength = Length("EdgeLength");
                if (arguments.Count != 1 || arguments[0].Name is not null || arguments[0].Default is not null
                    || !FirmamentSyntaxValues.TryMeasure(arguments[0].Value, "", out var faceCount)
                    || faceCount != double.Truncate(faceCount) || faceCount < 0 || faceCount > int.MaxValue
                    || !Aetheris.Kernel.Core.Brep.ConvexStockPrimitives.IsRegularFaceCount((int)faceCount))
                { diagnostics.Add("firmament-v2-polyhedron-face-count-unsupported: Polyhedron<N> uses a regular convex face count: 4, 6, 8, 12 or 20. Other dice families require a separate construction."); return null; }
                if (!double.IsFinite(edgeLength) || edgeLength <= 0)
                { diagnostics.Add("firmament-v2-polyhedron-edge-length-invalid: edgeLength must be a finite positive length."); return null; }
                return new FirmamentV2PolyhedronRecord((int)faceCount, edgeLength);
            case "Torus":
                var major = Length("MajorRadius"); var minor = Length("MinorRadius");
                return major > minor && minor > 0 ? new FirmamentV2TorusRecord(major, minor) : null;
            case "RoundedBox":
                var size = Size(); var corner = Length("CornerRadius");
                return size is not null && corner > 0 && corner < Math.Min(size[0], size[1]) / 2 ? new FirmamentV2RoundedBoxRecord(size, corner) : null;
            case "Frustum":
                var low = Length("BottomRadius"); var high = Length("TopRadius"); var h = Length("Height");
                return low > 0 && high > 0 && h > 0 && Math.Abs(low - high) > 1e-12 ? new FirmamentV2FrustumRecord(low, high, h) : null;
            // These recipe binders own family-specific field schemas. Their input
            // is admitted through their existing typed recipe authority below.
            case "StandardPart":
                var family = f.Word("Family");
                if (family is null) return null;
                return new FirmamentV2StandardPartRecord(family, RecipeFields(f));
            case "ExactCoaxialPart": return new FirmamentV2ExactCoaxialPartRecord(RecipeFields(f));
            default: return null;
        }
    }

    private static Dictionary<string, string> RecipeFields(FirmamentSyntaxFields f)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in f.Declaration.Body?.Statements.OfType<FieldSyntax>() ?? [])
        {
            var value = f.Take(field.Name.Text);
            if (field.Name.Text == "Family") continue;
            result[field.Name.Text] = value switch
            {
                LiteralSyntax literal => literal.Raw,
                QuantitySyntax { Value: LiteralSyntax literal } q => literal.Raw + q.Unit.Text,
                _ => FirmamentSyntaxValues.Reference(value) ?? ""
            };
        }
        return result;
    }

    private static FirmamentV2SemanticHoleDecl? BindMechanicalHole(FirmamentSyntaxFields f, List<string> diagnostics, FirmamentV2SolidBinding? compatibilityStock = null, string? ownerName = null)
    {
        var d = f.Declaration;
        if (d.Modifiers.Count > 0 || d.Type.Arguments.Any(a => a.Name is not null || a.Default is not null))
        { diagnostics.Add(HoleVariantUnknown); return null; }
        var variantName = d.Type.Arguments.Count == 1 ? FirmamentSyntaxValues.Reference(d.Type.Arguments[0].Value) : null;
        if (!Enum.TryParse<FirmamentV2SemanticHoleVariant>(variantName, compatibilityStock is not null, out var variant)) { diagnostics.Add(HoleVariantUnknown); return null; }
        var on = f.Take("On"); var axis = MechanicalFaceAxis(on, compatibilityStock?.Name ?? ownerName);
        FirmamentV2FaceTarget? face = null;
        if (compatibilityStock is not null)
        {
            face = BindCompatibilityFace(on, compatibilityStock, HoleEntryFaceMissing, diagnostics);
            axis = face?.Axis;
        }
        var point = f.Take("Center");
        double Diameter(string canonical, string? legacyStem = null)
        {
            if (compatibilityStock is null) return f.Number(canonical, "mm");
            var names = legacyStem is null ? new[] { canonical } : new[] { canonical, legacyStem + "Diameter", legacyStem + "Radius" };
            if (canonical == "Diameter") names = ["Diameter", "ShaftDiameter", "Radius", "ShaftRadius"];
            var authored = names.Distinct().Select(name => (Name: name, Value: f.Take(name))).Where(pair => pair.Value is not null).ToArray();
            if (authored.Length != 1 || !FirmamentSyntaxValues.TryMeasure(authored.FirstOrDefault().Value, "mm", out var value, "mm"))
            { f.Invalid(canonical); return double.NaN; }
            return authored[0].Name.EndsWith("Radius", StringComparison.Ordinal) ? value * 2 : value;
        }
        var diameter = Diameter("Diameter");
        var endExpression = f.Take("End"); var end = FirmamentSyntaxValues.Reference(endExpression);
        f.Take("PatternIdentity");
        FirmamentV2SemanticHoleEnd? termination = (end == "ThroughAll" || compatibilityStock is not null && end == "throughAll") ? new(FirmamentV2SemanticHoleEndKind.ThroughAll) : null;
        if (endExpression is CallSyntax { Target: ReferenceSyntax { Name.Text: "Blind" or "depth" }, Arguments.Count: 1 } blind
            && (compatibilityStock is not null || blind.Target is ReferenceSyntax { Name.Text: "Blind" })
            && FirmamentSyntaxValues.TryMeasure(blind.Arguments[0].Value, "mm", out var depth, compatibilityStock is null ? null : "mm") && depth > 0) termination = new(FirmamentV2SemanticHoleEndKind.Depth, depth);
        if (compatibilityStock is not null && termination is null)
            diagnostics.Add(endExpression is CallSyntax { Target: ReferenceSyntax { Name.Text: "depth" } } ? HoleDepthInvalid : HoleEndMissing);
        double? cbDiameter = null, cbDepth = null, csDiameter = null, csAngle = null;
        if (variant == FirmamentV2SemanticHoleVariant.Counterbore) { cbDiameter = Diameter("CounterboreDiameter", "Counterbore"); cbDepth = f.Number("CounterboreDepth", "mm"); }
        if (variant == FirmamentV2SemanticHoleVariant.Countersink) { csDiameter = Diameter("CountersinkDiameter", "Countersink");
            var angle = f.Take("CountersinkAngle");
            csAngle = FirmamentSyntaxValues.TryMeasure(angle, "deg", out var degrees, compatibilityStock is null ? null : "deg") ? degrees : double.NaN; }
        if (compatibilityStock is null && point is not (SequenceSyntax { Kind: SequenceKind.List } or CallSyntax { Target: ReferenceSyntax { Name.Text: "Point2" } })
            || !FirmamentSyntaxValues.Vector(point, "mm", out var center, compatibilityStock is null ? null : "mm") || center.Length != 2) { diagnostics.Add(CanonicalPoint2Invalid); return null; }
        if (axis is not ("+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z") || termination is null || d.Name is null) { diagnostics.Add(CanonicalModifyMalformed); return null; }
        if (!(diameter > 0)) { diagnostics.Add(HoleDiameterInvalid); return null; }
        if (cbDiameter is not null && (!(cbDiameter > diameter) || !(cbDepth > 0))) { diagnostics.Add(HoleCounterboreInvalid); return null; }
        if (csDiameter is not null && (!(csDiameter > diameter) || !(csAngle > 0 && csAngle < 180))) { diagnostics.Add(HoleCountersinkInvalid); return null; }
        face ??= FirmamentV2FaceTarget.Direct(axis);
        return new(d.Name.Text, variant, face, new(center[0], center[1], FirmamentV2FaceLocalPoint2D.ConventionFor(axis)), diameter, termination,
            cbDiameter, cbDepth, csDiameter, csAngle, SourceSpan: d.Span, AuthoredFields: f.AuthoredFields);
    }

    private static string? MechanicalFinishKind(FirmamentSyntaxFields fields, List<string> diagnostics)
    {
        var arguments = fields.Declaration.Type.Arguments;
        var generic = arguments.Count == 1 && arguments[0].Name is null && arguments[0].Default is null
            ? FirmamentSyntaxValues.Reference(arguments[0].Value) : null;
        var aliasExpression = fields.Take("Kind");
        var alias = FirmamentSyntaxValues.Reference(aliasExpression);
        if (aliasExpression is not null && alias is null) { fields.Invalid("Kind"); return null; }
        if (arguments.Count > 0 && generic is not ("Chamfer" or "Fillet") || generic is not null && alias is not null && generic != alias)
        { diagnostics.Add("firmament-v2-edgefinish-variant-conflict: Use EdgeFinish<Chamfer> or EdgeFinish<Fillet>; Kind, if supplied, must agree."); return null; }
        return generic ?? alias;
    }

    // Transport only bound value/header edits to the existing domain adapters.
    // Reprinting an entire document would unnecessarily change legacy productions
    // such as ProfileDelta anchors and Concept descriptor fields.
    private static string MechanicalConstructionSource(string source, DeclarationSyntax bound, List<string> diagnostics)
    {
        var original = FirmamentSyntaxParser.Parse(source).Document;
        var originalFields = FirmamentSyntaxTraversal.Statements(original).OfType<FieldSyntax>()
            .ToDictionary(f => f.Span);
        var changes = new List<(FirmamentV2SourceSpan Span, string Text)>();
        void Project(DeclarationSyntax declaration)
        {
            if (declaration.Type.Name.Text == "Static")
            { changes.Add((declaration.Span, "")); return; }
            if (declaration.Type.Name.Text is "Pmi" or "Concept" or "Feature" or "Template" || declaration.Modifiers.Count > 0) return;
            if (declaration.Type.Name.Text == "EdgeFinish" && declaration.Type.Arguments.Count > 0)
            {
                var fields = new FirmamentSyntaxFields(declaration, diagnostics);
                var kind = MechanicalFinishKind(fields, diagnostics);
                if (kind is not null && declaration.Body is not null)
                {
                    changes.Add((declaration.Type.Span, "EdgeFinish"));
                    if (!declaration.Body.Statements.OfType<FieldSyntax>().Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Kind")))
                        changes.Add((new(declaration.Body.Span.Start + 1, 0), " Kind: " + kind + "; "));
                }
            }
            foreach (var statement in declaration.Body?.Statements ?? [])
            {
                if (statement is DeclarationSyntax child) Project(child);
                if (statement is FieldSyntax field && originalFields.TryGetValue(field.Span, out var authored)
                    && FirmamentSyntaxWriter.Expression(authored.Value) != FirmamentSyntaxWriter.Expression(field.Value))
                    changes.Add((authored.Value.Span, FirmamentSyntaxWriter.Expression(field.Value)));
            }
        }
        Project(bound);
        var patterns = bound.Body!.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Pattern").ToArray();
        if (patterns.Length > 0)
        {
            var bodies = bound.Body.Statements.OfType<DeclarationSyntax>().Where(IsPrimitiveDeclaration).ToArray();
            if (bodies.Length != 1) diagnostics.Add("firmament-pattern-target-ambiguous: Model Pattern requires one material body; use Modify target.");
            else foreach (var pattern in patterns)
            {
                changes.Add((new(pattern.Span.Start, 0), "Modify " + bodies[0].Name!.Text + " {\n"));
                changes.Add((new(pattern.Span.Start + pattern.Span.Length, 0), "\n}"));
            }
        }
        foreach (var (span, text) in changes.OrderByDescending(c => c.Span.Start))
            source = source.Remove(span.Start, span.Length).Insert(span.Start, text);
        return source;
    }

    internal static string? MechanicalFaceAxis(ExpressionSyntax? expression, string? ownerName)
    {
        if (expression is CallSyntax { Target: ReferenceSyntax { Name.Text: "face" or "Face" }, Arguments.Count: 1 } call
            && call.Arguments[0].Name is null && call.Arguments[0].Default is null)
            expression = call.Arguments[0].Value;
        var reference = FirmamentSyntaxValues.Reference(expression);
        if (reference is "+X" or "-X" or "+Y" or "-Y" or "+Z" or "-Z") return reference;
        if (reference == "Top" || ownerName is not null && reference == ownerName + ".Top") return "+Z";
        return null;
    }

    private static FirmamentV2EdgeFinishDecl? BindMechanicalFinish(FirmamentSyntaxFields f, List<string> diagnostics)
    {
        var kind = MechanicalFinishKind(f, diagnostics);
        var face = f.Take("Face"); var axis = FirmamentSyntaxValues.Reference(face); var target = f.Word("Target");
        var distance = f.Take("Distance"); var radius = f.Take("Radius");
        if (face is not null && (axis is null || target == "AllEdges")) { f.Invalid("Face"); return null; }
        if (target == "AllEdges" && kind == "Fillet" && radius is null) { f.Invalid("Radius"); return null; }
        if ((distance is not null && radius is not null) || f.Declaration.Name is null || axis is null && target != "AllEdges" || target is null || kind is null
            || !FirmamentSyntaxValues.TryMeasure(distance ?? radius, "mm", out var value))
        { diagnostics.Add(CanonicalModifyMalformed); return null; }
        // The selected feature admission owner diagnoses nonpositive distances and
        // radii with its existing capability-specific explanation during preflight.
        return new(f.Declaration.Name.Text, axis ?? "", target, kind, value, f.Declaration.Span);
    }

    private static FirmamentV2PerforationDecl? BindMechanicalPerforation(FirmamentSyntaxFields f, List<string> diagnostics)
    {
        var on = f.Word("On"); var layout = f.Word("Layout");
        var diameter = f.Number("Diameter", "mm"); var pitch = f.Number("Pitch", "mm");
        var margin = f.Number("Margin", "mm", 0); var ligament = f.Number("MinimumLigament", "mm", 0);
        var offsetX = f.Number("OffsetX", "mm", 0); var offsetY = f.Number("OffsetY", "mm", 0);
        var circumferential = f.Number("CircumferentialPitch", "mm");
        var top = f.Number("MarginTop", "mm"); var bottom = f.Number("MarginBottom", "mm"); var angle = f.Number("StartAngle", "deg", 0);
        if (on is null || layout is null || !(diameter > 0 && pitch > 0 && margin >= 0 && ligament >= 0) || f.Declaration.Name is null)
        { diagnostics.Add("perforation-field-invalid"); return null; }
        return new(f.Declaration.Name.Text, on, layout, diameter, pitch, margin, ligament, offsetX, offsetY, f.Declaration.Span,
            double.IsNaN(circumferential) ? null : circumferential, double.IsNaN(top) ? null : top, double.IsNaN(bottom) ? null : bottom, angle);
    }

    /// <summary>Dedicated material binders reuse construction-scope recognition and
    /// reject material operations that their engineering lowerer cannot consume.</summary>
    internal static void BindMaterialDomainScope(DocumentSyntax syntax, string materialKind, List<string> diagnostics)
    {
        void Bind(IReadOnlyList<StatementSyntax> statements)
        {
            foreach (var wrapper in statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text is "Model" or "Struct"
                && !d.Modifiers.Any(m => m.Name.Text is "Concept" or "Template") && d.Annotation is null))
            {
                if (wrapper.Type.Arguments.Count > 0 || wrapper.Clauses.Count > 0 || wrapper.Parameters.Count > 0 || wrapper.Initializer is not null)
                    diagnostics.Add($"firmament-v2-domain-root-header-unsupported:{materialKind}:{wrapper.Name?.Text}");
                var units = wrapper.Body?.Statements.OfType<FieldSyntax>().Where(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Units")).ToArray() ?? [];
                if (wrapper.Type.Name.Text == "Model" && units.Length == 0 || units.Any(f => FirmamentSyntaxValues.Reference(f.Value) != "mm")) diagnostics.Add(CanonicalUnitsInvalid);
                Bind(wrapper.Body?.Statements ?? []);
            }
            var owned = statements.Where(s => s is not DeclarationSyntax d || !(d.Type.Name.Text == materialKind
                || d.Type.Name.Text is "Model" or "Struct" && !d.Modifiers.Any(m => m.Name.Text is "Concept" or "Template") && d.Annotation is null)).ToArray();
            BindMechanicalScope(owned, diagnostics);
            foreach (var declaration in owned.OfType<DeclarationSyntax>().Where(d => IsPrimitiveDeclaration(d)
                || d.Type.Name.Text is "Compose" or "Extrude" or "Revolve" or "WireForm" or "WireRoute" or "Sweep" or "SectionChain" or "Loft" or "Modify" or "Thread" or "EdgeFinish"))
                diagnostics.Add($"firmament-v2-domain-material-combination-unsupported:{materialKind}:{declaration.Type.Name.Text}: This material owner cannot consume the additional operation; use Assembly for separate parts.");
        }
        Bind(syntax.Statements);
    }

    internal static void BindMechanicalScope(IReadOnlyList<StatementSyntax> statements, List<string> diagnostics)
    {
        foreach (var statement in statements)
        {
            if (statement is DirectiveSyntax directive)
            {
                diagnostics.Add($"firmament-v2-directive-unconsumed:{directive.Kind}: Every directive must be consumed before mechanical binding.");
                continue;
            }
            if (statement is DeclarationSyntax definition && definition.Modifiers.Any(m => m.Name.Text == "Template")) continue;
            if (statement is FieldSyntax field)
            {
                if (!FirmamentSyntaxFields.SameField(field.Name.Text, "Units") && !FirmamentSyntaxFields.SameField(field.Name.Text, "Material"))
                    diagnostics.Add($"{CanonicalFieldUnknown}:Model:{field.Name.Text}");
                continue;
            }
            if (statement is not DeclarationSyntax declaration)
            {
                diagnostics.Add(CanonicalDeclarationUnrecognized); continue;
            }
            if (declaration.Modifiers.Any(m => m.Name.Text == "Assert")) continue;
            switch (declaration.Type.Name.Text)
            {
                case "Static":
                    diagnostics.Add("firmament-v2-static-scalar-consumer-unsupported: This scope does not bind scalar Static values; use the owning domain's typed Template inputs."); break;
                case "Struct" when declaration.Annotation is not null: break;
                case "Struct" when !declaration.Modifiers.Any(m => m.Name.Text == "Concept"):
                    BindMechanicalScope(declaration.Body?.Statements ?? [], diagnostics); break;
                case "Extrude":
                    ProfileAuthoringParser.ReadExtrudeFields(declaration, diagnostics); break;
                case "Profile" when declaration.Body is not null:
                    ProfileAuthoringParser.ValidateProfileBody(declaration, diagnostics);
                    break;
                case "Modify":
                    foreach (var item in declaration.Body?.Statements ?? [])
                        if (item is FieldSyntax unknown) diagnostics.Add($"{CanonicalFieldUnknown}:Modify:{unknown.Name.Text}");
                        else if (item is not DeclarationSyntax) diagnostics.Add(CanonicalModifyMalformed);
                    break;
                case "Expose":
                    var publications = new List<Assembly.AssemblyDiagnostic>();
                    Assembly.AssemblyPublishedPorts.ValidatePublicationSyntax(declaration, "inline", publications);
                    diagnostics.AddRange(publications.Select(d => d.Code + ": " + d.Message));
                    break;
                case "Box" or "Cylinder" or "Cone" or "Sphere" or "Ellipsoid" or "Polyhedron" or "Torus" or "RoundedBox" or "Frustum" or "StandardPart" or "ExactCoaxialPart":
                case "Plane" or "Axis" or "Axis2" or "Point2" or "Line2" or "Arc2" or "CubicBezier2" or "Rect2" or "Square2" or "Triangle2" or "Polygon2" or "RegularPolygon2" or "Circle2" or "Ellipse2" or "Ellipse2Guide" or "Slot2" or "RoundedRect2" or "SmoothRoundedRect2" or "Path":
                case "Plateau" or "Concept" or "Struct" or "Profile" or "Span" or "Compose" or "Boss" or "Pocket" or "AddOffset" or "RemoveOffset" or "EdgeFinish":
                case "Enum" or "Record" or "Template" or "template" or "Feature" or "Pattern" or "ProfileDelta" or "Selection" or "InlineStep" or "Recognize" or "Replace" or "Remove" or "Pmi" or "Match" or "Require" or "Assert" or "Thread":
                    break;
                case "Solid": diagnostics.Add(CanonicalConstructNotYetSupported + ":Solid"); break;
                default: diagnostics.Add(CanonicalDeclarationUnrecognized); diagnostics.Add($"{CanonicalDeclarationUnknown}:{declaration.Type.Name.Text}"); break;
            }
        }
    }
}
