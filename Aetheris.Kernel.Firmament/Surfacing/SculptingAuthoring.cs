using System.Diagnostics;
using System.Globalization;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Block = Aetheris.Kernel.Firmament.FirmamentV2.Syntax.FirmamentSyntaxScope;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Surfacing;

public static class SculptingAuthoring
{
    public static bool IsSculptingSource(string source) => (source.Contains("BodyState", StringComparison.Ordinal) || source.Contains("SculptState", StringComparison.Ordinal))
        && FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Any(d => d.Type.Name.Text is "BodyState" or "SculptState");
    public static SculptingCompileResult CompileFile(string path) => Compile(File.ReadAllText(path), path);

    public static IReadOnlyList<Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic> KernelDiagnostics(SculptingCompileResult result) =>
        result.Diagnostics.Select(d=>new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
            Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            d.Code+": "+d.Message,"Firmament.Sculpting"){OriginalSourceSpan=d.SourceSpan}).ToArray();

    public static SculptingCompileResult Compile(string source, string sourceFile = "memory")
    {
        var total = Stopwatch.StartNew(); var diagnostics = new List<SculptDiagnostic>(); var states = new Dictionary<string, BodyState>(StringComparer.Ordinal);
        var patches = new Dictionary<string, BoundedSurfacePatch>(StringComparer.Ordinal);
        var sectionChains = new Dictionary<string, SectionChain>(StringComparer.Ordinal);
        var binding = new FirmamentDomainBinding(source, sourceFile, "BodyState", new HashSet<string>(StringComparer.Ordinal));
        diagnostics.AddRange(binding.Diagnostics.Select(d => Located(d)));
        if (diagnostics.Count != 0) return new(false, "<unknown>", null, states, diagnostics, new(0,0,0,0,0));
        var models = binding.Document.Statements.OfType<DeclarationSyntax>().Where(d => d.Type.Name.Text == "Model").ToArray();
        if (models.Length != 1) return Fail("<unknown>", "sculpt-source-malformed", "Sculpting source requires exactly one Model.");
        var model = new Block(models[0], binding.Source, sourceFile, binding.Nominal, binding.OriginalSpan);
        var bodyStates = Blocks(model, "BodyState");
        var engineeringInputs = bodyStates.Any(b => b.Declaration.Body?.Statements.OfType<FieldSyntax>()
            .Any(f => FirmamentSyntaxFields.SameField(f.Name.Text, "Input")) == true);
        foreach (var statement in binding.Document.Statements.Where(s => !ReferenceEquals(s, models[0])))
            if (!engineeringInputs && (statement is not DeclarationSyntax data || !binding.OwnsCommonDeclaration(data))) diagnostics.Add(new("sculpt-statement-unknown", "Unsupported statement outside the sculpting Model."));
        foreach (var data in models[0].Body?.Statements.OfType<DeclarationSyntax>().Where(binding.OwnsCommonDeclaration) ?? []) model.Consume(data);
        if (engineeringInputs)
            foreach (var statement in models[0].Body?.Statements ?? [])
                if (!EngineeringBodyStateConstruction.IsStateDeclaration(statement)
                    && statement is not FieldSyntax { Name.Text: "units" or "Units" or "output" or "Output" })
                    model.Consume(statement); // The normal engineering compiler must admit these statements.
        diagnostics.AddRange(binding.BindCommonRequirements(binding.Document.Statements.Concat(models[0].Body?.Statements ?? [])).Select(Located));
        if (model.Word("Units") != "mm") diagnostics.Add(new("sculpt-units-invalid", "Sculpting authoring requires units: mm."));
        foreach (var block in Blocks(model, "SurfacePatch"))
        {
            var degree = NumberVector(block.Body, "Degree", 2, diagnostics); var domain = NumberVector(block.Body, "Domain", 4, diagnostics);
            var knotsU = NumberVectorAny(block.Body, "KnotsU", diagnostics); var knotsV = NumberVectorAny(block.Body, "KnotsV", diagnostics);
            var rows = ControlRows(block.Body, diagnostics); var boundaries = ParseBoundaries(block.Body, diagnostics);
            if (degree is null || domain is null || knotsU is null || knotsV is null || rows is null || boundaries is null) continue;
            try
            {
                var du = checked((int)degree[0]); var dv = checked((int)degree[1]);
                if (degree[0] != du || degree[1] != dv) throw new ArgumentException("Patch degrees must be integers.");
                var (mu, ku) = CompressKnots(knotsU); var (mv, kv) = CompressKnots(knotsV);
                var spline = new BSplineSurfaceWithKnots(du, dv, rows, "UNSPECIFIED", false, false, false, mu, mv, ku, kv, "UNSPECIFIED");
                var patch = new BSplineSurfacePatch(block.Name, spline, new(domain[0], domain[1], domain[2], domain[3]), new(block.Name + ".OuterLoop", boundaries));
                var patchDiagnostics = patch.Validate(); diagnostics.AddRange(patchDiagnostics);
                if (patchDiagnostics.Count == 0 && !patches.TryAdd(block.Name, patch)) diagnostics.Add(new("surf-patch-duplicate", $"SurfacePatch '{block.Name}' is declared more than once.", block.Name));
            }
            catch (Exception exception) when (exception is ArgumentException or OverflowException)
            {
                diagnostics.Add(new("surf-patch-invalid", $"SurfacePatch '{block.Name}' is invalid: {exception.Message}", block.Name));
            }
        }
        foreach (var block in Blocks(model, "SectionChain"))
        {
            var chain = ParseSectionChain(block, diagnostics);
            if (chain is null) continue;
            var qualification = SectionChainMaterializer.Materialize(chain);
            if (!qualification.IsSuccess)
            {
                diagnostics.AddRange(qualification.Diagnostics.Select(item => new SculptDiagnostic(item.Code, item.Message, block.Name)));
                continue;
            }
            if (!sectionChains.TryAdd(block.Name, chain)) diagnostics.Add(new("section-chain-duplicate", $"SectionChain '{block.Name}' is declared more than once.", block.Name));
        }
        var baseClock = Stopwatch.StartNew();
        foreach (var block in bodyStates)
        {
            var errorsBefore = diagnostics.Count;
            var engineeringInput = ScalarText(block, "Input");
            if (engineeringInput is not null)
            {
                var realized = EngineeringBodyStateConstruction.Realize(new(block.Name, binding.Source, sourceFile, engineeringInput));
                if (!realized.IsSuccess || realized.OutputState is null) diagnostics.AddRange(realized.Diagnostics.Select(d =>
                {
                    var span = d.SourceSpan is {} location ? binding.OriginalSpan(new(location.Start, location.Length)) : null;
                    return d with { SourceSpan = span is null ? null : new(span.Start, span.Length) };
                }));
                else if (!states.TryAdd(block.Name, realized.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"BodyState '{block.Name}' is declared more than once.", block.Name));
                continue;
            }
            var size = Vector(block.Body, "Size", 3, diagnostics); var holes = ParseHoles(block.Body, diagnostics);
            if (size is null || diagnostics.Count != errorsBefore) continue;
            var created = SculptedHousingFactory.CreateBase(block.Name, size[0], size[1], size[2], holes);
            if (!created.IsSuccess || created.OutputState is null) diagnostics.AddRange(created.Diagnostics);
            else if (!states.TryAdd(block.Name, created.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"BodyState '{block.Name}' is declared more than once."));
        }
        baseClock.Stop(); var opConstruction = TimeSpan.Zero; var verification = TimeSpan.Zero; var brep = TimeSpan.Zero;
        foreach (var block in Blocks(model, "SculptState"))
        {
            var operationClock = Stopwatch.StartNew();
            var inputName = ScalarText(block.Body, "Input");
            if (inputName is null || !states.TryGetValue(inputName, out var input)) { diagnostics.Add(new("sculpt-predecessor-unresolved", $"SculptState '{block.Name}' must name one previously accepted Input state.")); continue; }
            var operationBlock = Single(block.Body, "OffsetRegion", diagnostics);
            var replaceBlock = Single(block.Body, "ReplaceRegion", diagnostics);
            var blendBlock = Single(block.Body, "BlendBoundary", diagnostics);
            var holeBlock = Single(block.Body, "HoleFeature", diagnostics);
            var addChainBlock = Single(block.Body, "AddSectionChain", diagnostics);
            var removeChainBlock = Single(block.Body, "RemoveSectionChain", diagnostics);
            if (new[] { operationBlock, replaceBlock, blendBlock, holeBlock, addChainBlock, removeChainBlock }.Count(x => x is not null) != 1) { diagnostics.Add(new("sculpt-operation-unsupported", $"SculptState '{block.Name}' requires exactly one OffsetRegion, ReplaceRegion, BlendBoundary, HoleFeature, AddSectionChain, or RemoveSectionChain operation.")); continue; }
            if (addChainBlock is not null)
            {
                var chainName = ScalarText(addChainBlock.Body, "Chain"); var support = ScalarText(addChainBlock.Body, "Support") ?? string.Empty;
                var terminal = ScalarText(addChainBlock.Body, "Terminal") ?? string.Empty; var chainEnvelope = Vector(addChainBlock.Body, "InfluenceEnvelope", 6, diagnostics);
                var addMayModify = List(block.Body, "MayModify"); var addPreserve = List(block.Body, "Preserve"); var addRequirements = ParseRequirements(List(block.Body, "Require"), diagnostics);
                operationClock.Stop(); opConstruction += operationClock.Elapsed;
                if (chainName is null || !sectionChains.TryGetValue(chainName, out var chain)) diagnostics.Add(new("section-chain-unresolved", $"AddSectionChain in '{block.Name}' must reference a declared SectionChain."));
                if (chainEnvelope is null || chainName is null || !sectionChains.TryGetValue(chainName, out chain)) continue;
                var correspondence = chain.Sections[0].Profile.Spans.Select(span => new SectionSpanCorrespondence(span.SpanId, span.SpanId)).ToArray();
                var addOperation = new AddSectionChainOperation(block.Name + ".AddSectionChain", chain,
                    new(support, terminal, SectionChainAttachmentPlacement.RelativeToSupport, correspondence), addMayModify,
                    new(chainEnvelope[0], chainEnvelope[1], chainEnvelope[2], chainEnvelope[3], chainEnvelope[4], chainEnvelope[5]), addPreserve.Select(Preservation).ToArray(), addRequirements);
                var addResult = AddSectionChainSculptor.Apply(input, block.Name, addOperation);
                if (!addResult.IsSuccess || addResult.OutputState is null) diagnostics.AddRange(addResult.Diagnostics);
                else if (!states.TryAdd(block.Name, addResult.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
                continue;
            }
            if (removeChainBlock is not null)
            {
                var chainName = ScalarText(removeChainBlock.Body, "Chain"); var supports = List(removeChainBlock.Body, "Supports");
                var chainEnvelope = Vector(removeChainBlock.Body, "InfluenceEnvelope", 6, diagnostics);
                var removeMayModify = List(block.Body, "MayModify"); var removePreserve = List(block.Body, "Preserve"); var removeRequirements = ParseRequirements(List(block.Body, "Require"), diagnostics);
                operationClock.Stop(); opConstruction += operationClock.Elapsed;
                if (chainName is null || !sectionChains.TryGetValue(chainName, out var chain)) diagnostics.Add(new("section-chain-unresolved", $"RemoveSectionChain in '{block.Name}' must reference a declared SectionChain."));
                if (chainEnvelope is null || chainName is null || !sectionChains.TryGetValue(chainName, out chain)) continue;
                var removeOperation = new RemoveSectionChainOperation(block.Name + ".RemoveSectionChain", chain, supports, removeMayModify,
                    new(chainEnvelope[0], chainEnvelope[1], chainEnvelope[2], chainEnvelope[3], chainEnvelope[4], chainEnvelope[5]), removePreserve.Select(Preservation).ToArray(), removeRequirements);
                var removeResult = RemoveSectionChainSculptor.Apply(input, block.Name, removeOperation);
                if (!removeResult.IsSuccess || removeResult.OutputState is null) diagnostics.AddRange(removeResult.Diagnostics);
                else if (!states.TryAdd(block.Name, removeResult.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
                continue;
            }
            if (holeBlock is not null)
            {
                var holeTarget = ScalarText(holeBlock.Body, "Target") ?? string.Empty; var holeId = ScalarText(holeBlock.Body, "Id") ?? string.Empty;
                var center = Vector(holeBlock.Body, "Center", 2, diagnostics); var diameter = Length(holeBlock.Body, "Diameter", diagnostics); var holeEnvelope = Vector(holeBlock.Body, "InfluenceEnvelope", 6, diagnostics);
                var preserveCurrent = List(block.Body, "Preserve"); var holeRequirements = ParseRequirements(List(block.Body, "Require"), diagnostics);
                operationClock.Stop(); opConstruction += operationClock.Elapsed;
                if (center is null || diameter is null || holeEnvelope is null) continue;
                var holeOperation = new SafeHoleOperation(block.Name + ".HoleFeature", holeTarget, new(holeId, center[0], center[1], diameter.Value),
                    new(holeEnvelope[0], holeEnvelope[1], holeEnvelope[2], holeEnvelope[3], holeEnvelope[4], holeEnvelope[5]), preserveCurrent.Select(Preservation).ToArray(), holeRequirements);
                var holeClock = Stopwatch.StartNew(); var holeResult = SafeHoleSculptor.Apply(input, block.Name, holeOperation); holeClock.Stop(); verification += holeClock.Elapsed;
                if (!holeResult.IsSuccess || holeResult.OutputState is null) diagnostics.AddRange(holeResult.Diagnostics);
                else if (!states.TryAdd(block.Name, holeResult.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
                continue;
            }
            if (replaceBlock is not null)
            {
                var replaceTarget = ScalarText(replaceBlock.Body, "Target") ?? string.Empty; var patchName = ScalarText(replaceBlock.Body, "Patch");
                var replaceEnvelope = Vector(replaceBlock.Body, "InfluenceEnvelope", 6, diagnostics);
                var replaceMayModify = List(block.Body, "MayModify"); var replacePreserve = List(block.Body, "Preserve"); var replaceRequirements = ParseRequirements(List(block.Body, "Require"), diagnostics);
                operationClock.Stop(); opConstruction += operationClock.Elapsed;
                if (replaceEnvelope is null || patchName is null || !patches.TryGetValue(patchName, out var patch))
                {
                    if (patchName is null || !patches.ContainsKey(patchName ?? string.Empty)) diagnostics.Add(new("surf-patch-unresolved", $"ReplaceRegion in '{block.Name}' must reference a declared SurfacePatch."));
                    continue;
                }
                var replaceContracts = replacePreserve.Select(Preservation).ToArray();
                var replaceOperation = new ReplaceRegionOperation(block.Name + ".ReplaceRegion", replaceTarget, patch, replaceMayModify,
                    new(replaceEnvelope[0], replaceEnvelope[1], replaceEnvelope[2], replaceEnvelope[3], replaceEnvelope[4], replaceEnvelope[5]), replaceContracts, replaceRequirements);
                var replaceApplyClock = Stopwatch.StartNew(); var replaceResult = ReplaceRegionSculptor.Apply(input, block.Name, replaceOperation); replaceApplyClock.Stop(); verification += replaceApplyClock.Elapsed;
                if (!replaceResult.IsSuccess || replaceResult.OutputState is null) diagnostics.AddRange(replaceResult.Diagnostics);
                else if (!states.TryAdd(block.Name, replaceResult.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
                continue;
            }
            if (blendBlock is not null)
            {
                var between = List(blendBlock.Body, "Between"); var regionName = ScalarText(blendBlock.Body, "Region") ?? string.Empty;
                var preferredInput = ScalarText(blendBlock.Body, "Preferred"); var legacyContinuity = ScalarText(blendBlock.Body, "Continuity");
                if (preferredInput is not null && legacyContinuity is not null) blendBlock.Invalid("Continuity", "Use preferred or the compatibility continuity field, not both.");
                var preferredText = preferredInput ?? legacyContinuity ?? "G2";
                var minimumText = ScalarText(blendBlock.Body, "Minimum") ?? preferredText;
                var size = Vector(blendBlock.Body, "RegionSize", 2, diagnostics); var height = Length(blendBlock.Body, "Height", diagnostics);
                var blendEnvelope = Vector(blendBlock.Body, "InfluenceEnvelope", 6, diagnostics);
                var blendMayModify = List(block.Body, "MayModify"); var blendPreserve = List(block.Body, "Preserve");
                var blendRequirements = ParseRequirements(List(block.Body, "Require"), diagnostics);
                var policyText = ScalarText(blendBlock.Body, "Policy") ?? "StandardBlendJudgment";
                var useCandidate = ScalarText(blendBlock.Body, "UseCandidate"); var maximumDegree = Integer(blendBlock.Body, "MaximumDegree") ?? 10;
                operationClock.Stop(); opConstruction += operationClock.Elapsed;
                if (between.Count != 2) diagnostics.Add(new("surf-blend-supports-invalid", $"BlendBoundary in '{block.Name}' requires exactly two Between supports."));
                if (!TryNamedEnum<BlendContinuity>(preferredText, true, out var preferred)) diagnostics.Add(new("surf-blend-continuity-invalid", $"Preferred continuity '{preferredText}' must be G0, G1, or G2."));
                if (!TryNamedEnum<BlendContinuity>(minimumText, true, out var minimum)) diagnostics.Add(new("surf-blend-continuity-invalid", $"Minimum continuity '{minimumText}' must be G0, G1, or G2."));
                if (!StringComparer.Ordinal.Equals(policyText, "StandardBlendJudgment")) diagnostics.Add(new("surf-blend-policy-unresolved", $"Blend policy '{policyText}' is not defined; use StandardBlendJudgment."));
                if (between.Count != 2 || size is null || height is null || blendEnvelope is null || !TryNamedEnum<BlendContinuity>(preferredText, true, out preferred)
                    || !TryNamedEnum<BlendContinuity>(minimumText, true, out minimum) || !StringComparer.Ordinal.Equals(policyText, "StandardBlendJudgment")) continue;
                var blendOperation = new BlendBoundaryOperation(block.Name + ".BlendBoundary", between[0], between[1], regionName, preferred, minimum,
                    size[0], size[1], height.Value, blendMayModify, new(blendEnvelope[0], blendEnvelope[1], blendEnvelope[2], blendEnvelope[3], blendEnvelope[4], blendEnvelope[5]),
                    blendPreserve.Select(Preservation).ToArray(), blendRequirements, BlendJudgmentPolicy.StandardBlendJudgment, useCandidate, maximumDegree);
                var blendClock = Stopwatch.StartNew(); var blendResult = BlendBoundarySculptor.Apply(input, block.Name, blendOperation); blendClock.Stop(); verification += blendClock.Elapsed;
                if (!blendResult.IsSuccess || blendResult.OutputState is null) diagnostics.AddRange(blendResult.Diagnostics);
                else if (!states.TryAdd(block.Name, blendResult.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
                continue;
            }
            var target = ScalarText(operationBlock!.Body, "Target") ?? string.Empty;
            var offset = Length(operationBlock.Body, "Offset", diagnostics);
            var region = Vector(operationBlock.Body, "Region", 2, diagnostics);
            var envelope = Vector(operationBlock.Body, "InfluenceEnvelope", 6, diagnostics);
            var mayModify = List(block.Body, "MayModify"); var preserve = List(block.Body, "Preserve"); var requirements = List(block.Body, "Require");
            var boundary = ScalarText(operationBlock.Body, "Boundary") ?? "G0";
            operationClock.Stop(); opConstruction += operationClock.Elapsed;
            if (offset is null || region is null || envelope is null) continue;
            var contracts = preserve.Select(Preservation).ToArray();
            var parsedRequirements = ParseRequirements(requirements, diagnostics);
            var operation = new OffsetRegionOperation(block.Name + ".OffsetRegion", target, offset.Value, region[0], region[1], mayModify,
                new(envelope[0], envelope[1], envelope[2], envelope[3], envelope[4], envelope[5]), contracts, parsedRequirements, boundary);
            var applyClock = Stopwatch.StartNew(); var result = OffsetRegionSculptor.Apply(input, block.Name, operation); applyClock.Stop(); verification += applyClock.Elapsed;
            if (!result.IsSuccess || result.OutputState is null) diagnostics.AddRange(result.Diagnostics);
            else if (!states.TryAdd(block.Name, result.OutputState)) diagnostics.Add(new("sculpt-state-duplicate", $"State '{block.Name}' is declared more than once."));
        }
        var outputName = ScalarText(model, "Output");
        BodyState? output = null;
        if (outputName is null || !states.TryGetValue(outputName, out output))
        {
            var failedDeclaration = models[0].Body?.Statements.OfType<DeclarationSyntax>()
                .Any(d => d.Type.Name.Text is "BodyState" or "SculptState" && d.Name?.Text == outputName) == true;
            if (outputName is null || !failedDeclaration || diagnostics.Count == 0)
                diagnostics.Add(new("sculpt-output-unresolved", "Output must name one accepted BodyState or SculptState."));
        }
        diagnostics.AddRange(model.CompleteTree().Select(Located));
        for (var i = 0; i < diagnostics.Count; i++)
        {
            if (diagnostics[i].SourceSpan is not null) continue;
            var enclosing = models[0].Body?.Statements.OfType<DeclarationSyntax>()
                .FirstOrDefault(d => d.Name?.Text == diagnostics[i].Entity) ?? models[0];
            var span = binding.OriginalSpan(enclosing.Span);
            diagnostics[i] = diagnostics[i] with { SourceFile = sourceFile,
                SourceSpan = span is null ? null : new(span.Start, span.Length)
                { LocationNote = "Location identifies the enclosing declaration; the domain operation supplied no finer original-source span." } };
        }
        total.Stop();
        return new(diagnostics.Count == 0 && output is not null, model.Name, output, states, diagnostics,
            new(baseClock.Elapsed.TotalMilliseconds, opConstruction.TotalMilliseconds, verification.TotalMilliseconds, brep.TotalMilliseconds, 0d));

        SculptingCompileResult Fail(string name, string code, string message) => new(false, name, null, states, [new(code, message)], new(0, 0, 0, 0, 0));
    }

    private static IReadOnlyList<HousingHole> ParseHoles(Block body, List<SculptDiagnostic> diagnostics)
    {
        var result = new List<HousingHole>();
        foreach (var hole in Blocks(body, "Hole"))
        {
            var center = Vector(hole.Body, "Center", 2, diagnostics); var diameter = Length(hole.Body, "Diameter", diagnostics);
            if (center is not null && diameter is not null) result.Add(new(hole.Name, center[0], center[1], diameter.Value));
        }
        return result;
    }

    private static SectionChain? ParseSectionChain(Block block, List<SculptDiagnostic> diagnostics)
    {
        var stations = new List<Section>(); var spanIds = new[] { "South", "East", "North", "West" };
        foreach (var station in Blocks(block.Body, "Station"))
        {
            var origin = Vector(station.Body, "Origin", 3, diagnostics); var size = Vector(station.Body, "Size", 2, diagnostics);
            if (origin is null || size is null) continue;
            if (size[0] <= 0d || size[1] <= 0d) { diagnostics.Add(new("section-chain-profile-size-invalid", $"Station '{station.Name}' Size must be positive.", station.Name)); continue; }
            var halfWidth = size[0] / 2d; var halfHeight = size[1] / 2d;
            var points = new[] { new SectionPoint2D(-halfWidth, -halfHeight), new(halfWidth, -halfHeight), new(halfWidth, halfHeight), new(-halfWidth, halfHeight) };
            var profile = new SectionProfile(station.Name + ".Profile", Enumerable.Range(0, 4).Select(index => new SectionProfileSpan(spanIds[index],
                new SectionProfileCurve.Line(points[index], points[(index + 1) % 4]) as SectionProfileCurve)).ToArray(), spanIds[0]);
            stations.Add(new(station.Name, SectionFrame.Create(new(origin[0], origin[1], origin[2]), new(0, 1, 0), new(0, 0, 1)), profile));
        }
        if (stations.Count < 2) { diagnostics.Add(new("section-chain-section-count-invalid", $"SectionChain '{block.Name}' requires at least two Station blocks.", block.Name)); return null; }
        var startText = ScalarText(block.Body, "StartTermination") ?? "Open"; var endText = ScalarText(block.Body, "EndTermination") ?? "Open";
        if (!TryNamedEnum<SectionTermination>(startText, true, out var start) || !TryNamedEnum<SectionTermination>(endText, true, out var end))
        { diagnostics.Add(new("section-chain-termination-invalid", $"SectionChain '{block.Name}' terminations must be Open or Cap.", block.Name)); return null; }
        var correspondence = stations.Zip(stations.Skip(1), (source, target) => new AdjacentSectionCorrespondence(source.SectionId, target.SectionId,
            spanIds.Select(span => new SectionSpanCorrespondence(span, span)).ToArray())).ToArray();
        // The compact X3b sculpt-operation grammar predates SURF-X4; absence remains G0 for replay compatibility.
        var continuityText = ScalarText(block.Body, "Continuity") ?? "G0";
        if (!TryNamedEnum<SectionChainContinuity>(continuityText, true, out var continuity))
        { diagnostics.Add(new("section-chain-continuity-invalid", $"SectionChain '{block.Name}' Continuity must be G0 or G1.", block.Name)); return null; }
        return new(block.Name, stations, correspondence,
            continuity == SectionChainContinuity.G1 ? SectionTransitionPolicy.SmoothPolynomial : SectionTransitionPolicy.Ruled,
            start, end, continuity);
    }

    private static bool TryNamedEnum<T>(string value, bool ignoreCase, out T result) where T : struct, Enum
    {
        result = default;
        return Enum.GetNames<T>().Any(name => string.Equals(name, value, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            && Enum.TryParse(value, ignoreCase, out result);
    }
    private static bool TryNamedEnum<T>(string value, out T result) where T : struct, Enum => TryNamedEnum(value, false, out result);

    private static IReadOnlyList<Block> Blocks(Block scope, string kind) => scope.Children(kind);
    private static Block? Single(Block scope, string kind, List<SculptDiagnostic> diagnostics)
    {
        var found = scope.Children(kind);
        if (found.Count > 1) diagnostics.Add(new("sculpt-operation-cardinality", $"Only one {kind} is allowed per state.", scope.Name));
        return found.Count == 1 ? found[0] : null;
    }
    private static SculptDiagnostic Located(FirmamentV2ValidationDiagnostic d) => new(d.Code, d.Message, d.Target)
        { SourceFile = d.SourceFile, SourceSpan = d.SourceSpan is {} span ? new(span.Start,span.Length) { LocationNote=d.LocationNote } : null };
    private static string? ScalarText(Block body, string field) => body.Word(field);
    private static int? Integer(Block body, string field)
    {
        var value=body.Take(field); if(value is null)return null;
        if(FirmamentSyntaxValues.TryMeasure(value,"",out var number) && double.IsFinite(number) && number>=0 && number<=int.MaxValue && number==Math.Truncate(number)) return (int)number;
        body.Invalid(field); return null;
    }
    private static double? Length(Block body, string field, List<SculptDiagnostic> diagnostics)
    {
        if(FirmamentSyntaxValues.TryMeasure(body.Take(field),"mm",out var value) && double.IsFinite(value))return value;
        body.Invalid(field); diagnostics.Add(new("sculpt-field-invalid", $"Field '{field}' requires a millimetre length."));return null;
    }
    private static double[]? Vector(Block body,string field,int count,List<SculptDiagnostic> diagnostics)
    {
        if(FirmamentSyntaxValues.Vector(body.Take(field),"mm",out var values) && values.Length==count && values.All(double.IsFinite))return values;
        body.Invalid(field);diagnostics.Add(new("sculpt-field-invalid",$"Field '{field}' requires {count} millimetre lengths."));return null;
    }
    private static IReadOnlyList<string> List(Block body,string field)
    {
        var value=body.Take(field); if(value is null)return [];
        if(value is SequenceSyntax { Kind:SequenceKind.List } list && list.Items.All(i=>i.Name is null && i.Default is null && FirmamentSyntaxValues.Reference(i.Value) is not null))
            return list.Items.Select(i=>FirmamentSyntaxValues.Reference(i.Value)!).ToArray();
        body.Invalid(field);return [];
    }
    private static PreservationContract Preservation(string entity) => new(entity, entity == SculptedHousingFactory.MountingHolePattern ? PreservationMode.PatternPlacementAndDiameter : PreservationMode.ExactGeometry);
    private static IReadOnlyList<SculptRequirement> ParseRequirements(IReadOnlyList<string> values, List<SculptDiagnostic> diagnostics)
    {
        var parsed = new List<SculptRequirement>();
        foreach (var value in values)
            if (TryNamedEnum<SculptRequirement>(value, out var requirement)) parsed.Add(requirement); else diagnostics.Add(new("sculpt-requirement-unsupported", $"Requirement '{value}' is not supported."));
        return parsed;
    }
    private static double[]? NumberVector(Block body, string field, int count, List<SculptDiagnostic> diagnostics)
    {
        var values = NumberVectorAny(body, field, diagnostics); if (values is not null && values.Length == count) return values;
        if (values is not null) diagnostics.Add(new("sculpt-field-invalid", $"Field '{field}' requires {count} dimensionless numbers.")); return null;
    }
    private static double[]? NumberVectorAny(Block body,string field,List<SculptDiagnostic> diagnostics)
    {
        if(FirmamentSyntaxValues.Vector(body.Take(field),"",out var values) && values.All(double.IsFinite))return values;
        body.Invalid(field);diagnostics.Add(new("sculpt-field-invalid",$"Field '{field}' requires a dimensionless number list."));return null;
    }
    private static IReadOnlyList<IReadOnlyList<Point3D>>? ControlRows(Block body,List<SculptDiagnostic> diagnostics)
    {
        var rows=new List<IReadOnlyList<Point3D>>();
        foreach(var field in body.TakeAll("ControlRow"))
        {
            if(field.Value is not SequenceSyntax {Kind:SequenceKind.List} row){body.Invalid("ControlRow");continue;}
            var points=new List<Point3D>();
            foreach(var item in row.Items)
                if(item.Name is null && item.Default is null && FirmamentSyntaxValues.Vector(item.Value,"mm",out var p) && p.Length==3 && p.All(double.IsFinite))points.Add(new(p[0],p[1],p[2]));
                else body.Invalid("ControlRow");
            if(points.Count==0)body.Invalid("ControlRow");
            rows.Add(points);
        }
        if(rows.Count==0){diagnostics.Add(new("surf-control-net-required","A non-rational SurfacePatch requires explicit controlRow entries."));return null;}
        return rows;
    }
    private static IReadOnlyList<PatchBoundaryCorrespondence>? ParseBoundaries(Block body, List<SculptDiagnostic> diagnostics)
    {
        var result = new List<PatchBoundaryCorrespondence>();
        foreach (var block in Blocks(body, "Boundary"))
        {
            if (!TryNamedEnum<PatchBoundarySide>(block.Name, true, out var side)) { diagnostics.Add(new("surf-boundary-side-invalid", $"Unknown patch boundary side '{block.Name}'.")); continue; }
            var existing = ScalarText(block.Body, "Existing") ?? string.Empty; var continuityText = ScalarText(block.Body, "Continuity") ?? "G0";
            if (!TryNamedEnum<PatchBoundaryContinuity>(continuityText, true, out var continuity) || continuity == PatchBoundaryContinuity.G2)
            {
                diagnostics.Add(new("surf-boundary-continuity-invalid", $"Direct SurfacePatch boundary '{block.Name}' continuity must be G0 or G1; use BlendBoundary for qualified G2 construction and evidence."));
                continue;
            }
            result.Add(new($"Boundary{block.Name}", side, existing, continuity));
        }
        return result;
    }
    private static (IReadOnlyList<int> Multiplicities, IReadOnlyList<double> Values) CompressKnots(IReadOnlyList<double> expanded)
    {
        var multiplicities = new List<int>(); var values = new List<double>();
        foreach (var knot in expanded)
        {
            if (values.Count > 0 && knot == values[^1]) multiplicities[^1]++;
            else { values.Add(knot); multiplicities.Add(1); }
        }
        return (multiplicities, values);
    }
}
