using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.FirmamentV2;

public static partial class FirmamentV2Parser
{
    private static FirmamentV2SolidBinding? BindImportedStep(DeclarationSyntax declaration, List<string> diagnostics,
        string? directory, IReadOnlyDictionary<string, string>? resources)
    {
        var fields = new FirmamentSyntaxFields(declaration, diagnostics);
        var geometryValue = fields.Take("GeometryOnly");
        var geometryOnly = false;
        if (geometryValue is not null && !bool.TryParse(FirmamentSyntaxWriter.Expression(geometryValue), out geometryOnly)) fields.Invalid("GeometryOnly");
        var path = StringValue(fields.Take("Path"));
        var bytePinExpression = fields.Take("Sha256");
        var textPinExpression = fields.Take("TextSha256");
        if (bytePinExpression is not null && textPinExpression is not null)
            diagnostics.Add("firmament-inline-step-source-pin-conflict: Choose sha256 for exact bytes or textSha256 for UTF-8 text with LF line endings, not both.");
        var pinExpression = bytePinExpression ?? textPinExpression;
        var textPin = bytePinExpression is null && textPinExpression is not null;
        var pin = StringValue(pinExpression);
        if (pinExpression is not null && (pin is null || pin.Length != 64 || pin.Any(c => !char.IsAsciiHexDigit(c)))) fields.Invalid(textPin ? "TextSha256" : "Sha256");
        Modules.FirmamentLockedDependency? locked = null;
        if (path is not null)
        {
            try { locked = Modules.FirmamentDependencyLock.StepPin(directory, resources, path); }
            catch (ArgumentException error) { diagnostics.Add("firmament-lock-invalid: " + error.Message); }
        }
        if (pinExpression is null && locked is not null) { pin = locked.Sha256; textPin = false; }
        var toleranceExpression = fields.Take("ImportTolerance");
        double? tolerance = null;
        if (toleranceExpression is not null)
        {
            if (FirmamentSyntaxValues.TryMeasure(toleranceExpression, "mm", out var value) && value > 0 && value <= .1) tolerance = value;
            else fields.Invalid("ImportTolerance");
            if (pin is null) diagnostics.Add("firmament-inline-step-source-pin-required: Explicit import tolerance requires a pinned source.");
        }
        if (geometryOnly && pin is null) diagnostics.Add("firmament-inline-step-source-pin-required: geometryOnly requires a pinned source.");
        fields.Complete();
        if (path is null || declaration.Name is null) { diagnostics.Add(InlineStepPathMissing); return null; }
        var binding = LoadInlineStep(declaration.Name.Text, path, diagnostics, directory, resources, pin, tolerance, textPin);
        if (binding is null) return null;
        var imported = (FirmamentV2InlineStepRecord)binding.Primitive;
        if (locked is not null && !locked.Sha256.Equals(imported.SourceByteHash, StringComparison.OrdinalIgnoreCase))
        { diagnostics.Add("firmament-lock-dependency-mismatch: The STEP differs from its lock entry; reinspect before updating it."); return null; }
        return binding with { Primitive = imported with { GeometryOnly = geometryOnly }, SourceSpan = declaration.Span, AuthoredFields = fields.AuthoredFields };
    }

    private static IReadOnlyList<FirmamentV2RecognizedRegion> BindImportedRegions(IEnumerable<DeclarationSyntax> declarations,
        Dictionary<string, FirmamentV2SolidBinding> solids, List<string> diagnostics, Action<DeclarationSyntax, int> capture)
    {
        var result = new List<FirmamentV2RecognizedRegion>();
        foreach (var declaration in declarations.Where(d => d.Type.Name.Text == "Recognize"))
        {
            var diagnosticStart = diagnostics.Count;
            if (declaration.Name is null || !solids.TryGetValue(declaration.Name.Text, out var solid) || solid.InlineStep is null)
            { diagnostics.Add(UnknownRecognitionBody); capture(declaration, diagnosticStart); continue; }
            var regions = declaration.Body?.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Region").ToArray() ?? [];
            new FirmamentSyntaxFields(declaration, diagnostics).Complete(regions.ToHashSet<StatementSyntax>());
            capture(declaration, diagnosticStart);
            foreach (var region in regions)
            {
                diagnosticStart = diagnostics.Count;
                var fields = new FirmamentSyntaxFields(region, diagnostics);
                var kind = NormalizeRecognitionKind(fields.Reference("Kind") ?? "");
                var confidence = fields.Reference("Confidence", "medium")!.ToLowerInvariant();
                if (kind is not ("holeShaft" or "datumPlane")) diagnostics.Add(InvalidRecognitionKind);
                if (confidence is not ("low" or "medium" or "high" or "certain")) diagnostics.Add(InvalidRecognitionConfidence);
                var raw = fields.Take("StepFaceEntities");
                var friendly = fields.Take("Faces");
                var candidateValue = fields.Take("Candidate");
                var refs = new List<string>();
                if (candidateValue is not null)
                {
                    if (raw is not null || friendly is not null) fields.Invalid("Candidate");
                    if (StringValue(candidateValue) is not { } candidateId || solid.InlineStep.SourceHashPin is null || solid.InlineStep.RuntimeBody is null)
                        diagnostics.Add("firmament-imported-hole-candidate-invalid: candidate requires an exact discovered ID and a pinned InlineStep source.");
                    else
                    {
                        var found = BrepThroughHoleRevision.Discover(solid.InlineStep.RuntimeBody, solid.InlineStep.ImportToleranceMillimetres ?? .001)
                            .Candidates.SingleOrDefault(c => c.Id == candidateId);
                        if (found is null || !found.CanPlan)
                            diagnostics.Add("firmament-imported-hole-candidate-invalid: Candidate is absent or not admitted in this pinned source. " + string.Join(" | ", found?.Diagnostics ?? []));
                        else refs.AddRange(found.StepFaceEntities.Select(id => "#" + id));
                    }
                }
                else if (raw is not null && friendly is not null) diagnostics.Add(UnknownRecognitionFace);
                else if ((raw ?? friendly) is SequenceSyntax { Kind: SequenceKind.List } list)
                {
                    foreach (var argument in list.Items)
                    {
                        string? entity = null;
                        if (raw is not null && StringValue(argument.Value) is { } id && solid.InlineStep.TopologyMap.TryResolveFaceEntity(id, out _)) entity = id;
                        else if (friendly is not null && FirmamentSyntaxValues.TryMeasure(argument.Value, "", out var number)
                            && number == (int)number && solid.InlineStep.TopologyMap.TryResolveSequentialFaceId((int)number, out var ordinal)) entity = ordinal;
                        if (entity is null || argument.Name is not null) diagnostics.Add(UnknownRecognitionFace); else refs.Add(entity);
                    }
                }
                if (refs.Count == 0 || refs.Distinct().Count() != refs.Count) diagnostics.Add(UnknownRecognitionFace);
                FirmamentV2RecognitionEvidence? evidence = null;
                if (fields.Take("Evidence") is { } value)
                {
                    if (value is not ObjectSyntax obj) fields.Invalid("Evidence");
                    else
                    {
                        var e = ImportedObject(obj, "Evidence", diagnostics);
                        var family = e.Reference("SurfaceFamily");
                        if (family is not null && family.ToLowerInvariant() is not ("cylindrical" or "planar")) diagnostics.Add(RecognitionEvidenceSurfaceFamilyUnknown);
                        var radiusValue = e.Take("Radius"); double? radius = null;
                        if (radiusValue is not null)
                        { if (FirmamentSyntaxValues.TryMeasure(radiusValue, "mm", out var r) && r > 0) radius = r; else diagnostics.Add(RecognitionEvidenceRadiusInvalid); }
                        var throughValue = e.Take("Through"); bool? through = null;
                        if (throughValue is not null)
                        { if (bool.TryParse(FirmamentSyntaxWriter.Expression(throughValue), out var b)) through = b; else e.Invalid("Through"); }
                        var axis = e.Reference("Axis");
                        var centerValue = e.Take("Center"); FirmamentV2FaceLocalPoint2D? center = null;
                        if (centerValue is not null)
                        { if (FirmamentSyntaxValues.Vector(centerValue, "mm", out var xy) && xy.Length == 2) center = new(xy[0], xy[1], "world-xy"); else e.Invalid("Center"); }
                        var notes = e.Take("Notes");
                        if (notes is not null && (notes is not SequenceSyntax { Kind: SequenceKind.List } notesList
                            || notesList.Items.Any(item => item.Name is not null || StringValue(item.Value) is null))) e.Invalid("Notes");
                        e.Complete();
                        evidence = new(family is null ? [] : [family.ToLowerInvariant()], radius, axis, center, through);
                    }
                }
                fields.Complete();
                if (region.Name is null) diagnostics.Add(CanonicalDeclarationUnrecognized);
                else if (result.Any(r => r.BodyName == solid.Name && r.RegionName == region.Name.Text)) diagnostics.Add(DuplicateRegion);
                else result.Add(new(solid.Name, region.Name.Text, kind, refs, confidence, evidence));
                capture(region, diagnosticStart);
            }
        }
        return result;
    }

    private static IReadOnlyList<FirmamentV2ReplacementDecl> BindImportedReplacements(IEnumerable<DeclarationSyntax> declarations,
        Dictionary<string, FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2RecognizedRegion> regions,
        List<string> diagnostics, string source, Action<DeclarationSyntax, int> capture)
    {
        var result = new List<FirmamentV2ReplacementDecl>();
        foreach (var declaration in declarations.Where(d => d.Type.Name.Text == "Replace"))
        {
            var diagnosticStart = diagnostics.Count;
            try
            {
                var target = declaration.Name?.Text.Split('.') ?? [];
                if (target.Length != 2 || !solids.TryGetValue(target[0], out var solid) || solid.InlineStep is null) { diagnostics.Add(UnknownReplacementBody); continue; }
                var region = regions.SingleOrDefault(r => r.BodyName == target[0] && r.RegionName == target[1]);
                if (region is null) { diagnostics.Add(UnknownReplacementRegion); continue; }
                var kind = declaration.ResultType is null ? "" : NormalizeRecognitionKind(FirmamentSyntaxWriter.Expression(declaration.ResultType));
                if (kind != "holeShaft") diagnostics.Add(ReplacementUnsupportedKind);
                if (kind != region.Kind) diagnostics.Add(ReplacementKindMismatch);
                var name = (declaration.Initializer as ReferenceSyntax)?.Name.Text;
                if (name is null) { diagnostics.Add(CanonicalDeclarationUnrecognized); continue; }
                var measurements = new ImportedHoleMeasurements(solid, region, diagnostics);
                var fields = new FirmamentSyntaxFields(declaration with { ResultType = null, Initializer = null }, diagnostics, bind: measurements.Expression);
                var placement = fields.Take("On");
                string? faceEntity = null;
                if (placement is CallSyntax { Target: MemberSyntax { Target: ReferenceSyntax owner, Member.Text: "Face" or "face" } member, Arguments.Count: 1 } call && owner.Name.Text == solid.Name
                    && call.Arguments[0].Name is null && call.Arguments[0].Default is null)
                {
                    var argument = call.Arguments[0].Value;
                    if (FirmamentSyntaxValues.TryMeasure(argument, "", out var id) && id == (int)id && solid.InlineStep.TopologyMap.TryResolveSequentialFaceId((int)id, out var entity)) faceEntity = entity;
                    else if (StringValue(argument) is { } raw && solid.InlineStep.TopologyMap.TryResolveFaceEntity(raw, out _)) faceEntity = raw;
                }
                if (faceEntity is null) diagnostics.Add(ReplacementFaceUnresolved);
                var centerValue = fields.Take("Center");
                Point3D? world = null; FirmamentV2FaceLocalPoint2D center = new(0, 0, "");
                if (Point(centerValue) is { } p) { world = p; center = new(p.X, p.Y, "world-xy"); }
                else if (FirmamentSyntaxValues.Vector(centerValue, "mm", out var xy) && xy.Length == 2) center = new(xy[0], xy[1], "");
                else diagnostics.Add(HoleCenterMissing);
                var diameter = fields.Number("Diameter", "mm");
                if (!double.IsFinite(diameter) || diameter <= 0) diagnostics.Add(ReplacementRadiusInvalid);
                if (fields.Reference("End") is not ("ThroughAll" or "throughAll")) diagnostics.Add(ReplacementEndUnsupported);
                var hostValue = fields.Take("HostSize");
                var host = FirmamentSyntaxValues.Vector(hostValue, "mm", out var size) && size.Length == 3 && size.All(v => v > 0) ? size : [];
                ThroughHoleEditBounds? within = null;
                ThroughHoleEditBounds? destinationWithin = null;
                if (fields.Take("Within") is { } envelope)
                {
                    ThroughHoleEditBounds? ReadBounds(ExpressionSyntax value)
                    {
                        if (value is not ObjectSyntax obj) { fields.Invalid("Within"); return null; }
                        var bounds = ImportedObject(obj, "EditBounds", diagnostics);
                        var min = Point(bounds.Take("Min")); var max = Point(bounds.Take("Max")); bounds.Complete();
                        if (min is not null && max is not null) return new(min.Value, max.Value);
                        fields.Invalid("Within"); return null;
                    }
                    if (envelope is SequenceSyntax { Kind: SequenceKind.List, Items.Count: 2 } list
                        && list.Items.All(a => a.Name is null && a.Default is null))
                    {
                        within = ReadBounds(list.Items[0].Value);
                        destinationWithin = ReadBounds(list.Items[1].Value);
                    }
                    else within = ReadBounds(envelope);
                    if (world is null) fields.Invalid("Center");
                    if (solid.InlineStep.SourceHashPin is null) diagnostics.Add("firmament-inline-step-source-pin-required: A local revision requires sha256 or textSha256 on InlineStep.");
                    if (hostValue is not null) diagnostics.Add("firmament-imported-hole-host-size-conflict: within edits the retained BRep; HostSize is not admitted in this operation.");
                }
                else if (host.Length != 3) diagnostics.Add(ReplacementVerificationFailed);
                fields.Complete();
                result.Add(new(solid.Name, target[1], kind, name, $"{solid.Name}.face(\"{faceEntity}\")", center, diameter / 2, "throughAll", host,
                    source.Substring(declaration.Span.Start, declaration.Span.Length))
                { WorldCenter = world, Within = within, DestinationWithin = destinationWithin, SourceSpan = declaration.Span });
            }
            finally { capture(declaration, diagnosticStart); }
        }
        return result;
    }

    private static IReadOnlyList<FirmamentV2RemovalDecl> BindImportedRemovals(IEnumerable<DeclarationSyntax> declarations,
        Dictionary<string,FirmamentV2SolidBinding> solids, IReadOnlyList<FirmamentV2RecognizedRegion> regions,
        List<string> diagnostics, Action<DeclarationSyntax,int> capture)
    {
        var result = new List<FirmamentV2RemovalDecl>();
        foreach (var declaration in declarations.Where(d => d.Type.Name.Text == "Remove"))
        {
            var start = diagnostics.Count;
            try
            {
                var target = declaration.Name?.Text.Split('.') ?? [];
                if (target.Length != 2 || !solids.TryGetValue(target[0],out var solid) || solid.InlineStep is null)
                { diagnostics.Add(UnknownReplacementBody); continue; }
                var region = regions.SingleOrDefault(r => r.BodyName == target[0] && r.RegionName == target[1]);
                if (region is null) { diagnostics.Add(UnknownReplacementRegion); continue; }
                if (region.Kind != "holeShaft") diagnostics.Add("firmament-imported-hole-removal-kind-unsupported: Remove currently requires a proved HoleShaft region.");
                if (solid.InlineStep.SourceHashPin is null) diagnostics.Add("firmament-inline-step-source-pin-required: Remove requires a pinned STEP source.");
                var fields = new FirmamentSyntaxFields(declaration,diagnostics,bind: new ImportedHoleMeasurements(solid,region,diagnostics).Expression);
                if (fields.Take("Within") is ObjectSyntax obj)
                {
                    var bounds = ImportedObject(obj,"EditBounds",diagnostics);
                    var min = Point(bounds.Take("Min")); var max = Point(bounds.Take("Max")); bounds.Complete();
                    if (min is not null && max is not null) result.Add(new(solid.Name,target[1],new(min.Value,max.Value),declaration.Span));
                    else fields.Invalid("Within");
                }
                else fields.Invalid("Within");
                fields.Complete();
            }
            finally { capture(declaration,start); }
        }
        return result;
    }

    private static FirmamentSyntaxFields ImportedObject(ObjectSyntax obj, string type, List<string> diagnostics)
    {
        if (obj.Type is not null && FirmamentSyntaxValues.Reference(obj.Type) != type || obj.Name is not null)
            diagnostics.Add($"{CanonicalDeclarationUnknown}:{type}:header");
        return FirmamentSyntaxFields.Object(obj.Type is null ? obj with { Type = new ReferenceSyntax(new(type, obj.Span), obj.Span) } : obj, null, diagnostics);
    }
    private static string? StringValue(ExpressionSyntax? value) => value is LiteralSyntax { Kind: LiteralKind.String } literal
        ? System.Text.Json.JsonSerializer.Deserialize(literal.Raw, FirmamentJsonContext.Default.String) : null;
    private static Point3D? Point(ExpressionSyntax? value) => value is CallSyntax { Target: ReferenceSyntax { Name.Text: "Point3" } }
        && FirmamentSyntaxValues.Vector(value, "mm", out var xyz) && xyz.Length == 3 ? new(xyz[0], xyz[1], xyz[2]) : null;
}
