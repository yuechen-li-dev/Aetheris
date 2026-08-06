using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Verification;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Numerics;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Execution;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

public static class FirmamentBuildAndExport
{
    public const string EdgeFinishProfileComposeBoundaryUnsupported = "EdgeFinishProfileComposeBoundaryUnsupported";
    private enum ExportRouteDisposition { Declined, Failed, Succeeded }
    private sealed record ExportRouteResult(string Route, ExportRouteDisposition Disposition, KernelResult<FirmamentStepExportResult>? Artifact = null)
    {
        public static ExportRouteResult Decline(string route) => new(route, ExportRouteDisposition.Declined);
        public static ExportRouteResult Complete(string route, KernelResult<FirmamentStepExportResult> artifact) => new(route, artifact.IsSuccess ? ExportRouteDisposition.Succeeded : ExportRouteDisposition.Failed, artifact);
    }

    public static KernelResult<FirmamentBuildAndExportResult> Run(string sourcePath, string? outputPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullSourcePath = Path.GetFullPath(sourcePath);
        var sourceText = NormalizeLf(File.ReadAllText(fullSourcePath, Encoding.UTF8));
        var exportResult = ExportSource(sourceText, Path.GetDirectoryName(fullSourcePath));
        if (!exportResult.IsSuccess)
        {
            return KernelResult<FirmamentBuildAndExportResult>.Failure(exportResult.Diagnostics);
        }

        var resolvedOutputPath = string.IsNullOrWhiteSpace(outputPath)
            ? ResolveDefaultOutputPath(fullSourcePath)
            : Path.GetFullPath(outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(resolvedOutputPath)!);
        File.WriteAllText(resolvedOutputPath, exportResult.Value.StepText, new UTF8Encoding(false));

        return KernelResult<FirmamentBuildAndExportResult>.Success(
            new FirmamentBuildAndExportResult(
                fullSourcePath,
                resolvedOutputPath,
                exportResult.Value));
    }


    private static KernelResult<FirmamentStepExportResult> ExportSource(string sourceText, string? sourceDirectory = null)
    {
        // The V2 parser owns canonical-root admission.  Profile/composition
        // materializers consume only the extracted normalized declaration body,
        // so their historical top-level spelling is no longer author-visible.
        // Static authoring is intentionally erased before this boundary, too:
        // parser admission alone is insufficient because the profile/composition
        // materializers read source declarations directly.
        var staticDiagnostics = new List<string>();
        var staticExpansion = CanonicalStaticAuthoring.Expand(sourceText, staticDiagnostics);
        if (staticExpansion is null)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(staticDiagnostics
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select(diagnostic => new Kernel.Core.Diagnostics.KernelDiagnostic(
                    Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                    Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                    diagnostic,
                    "FirmamentV2.StaticAuthoring"))
                .ToArray());
        }
        var materializerInput = staticExpansion.Source;
        var canonicalAdvanced = FirmamentV2Parser.TryGetCanonicalAdvancedBody(materializerInput, out var canonicalBody);
        var materializerSource = canonicalAdvanced ? canonicalBody : materializerInput;
        if (PrismaticProfileCompositionParser.IsCompositionSource(materializerSource))
        {
            var parsed = PrismaticProfileCompositionParser.Parse(materializerSource);
            var stack = PrismaticSectionStackCompiler.Normalize(parsed, out var diagnostics);
            if (stack is null)
                return KernelResult<FirmamentStepExportResult>.Failure(diagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.ProfileComposition")).ToArray());
            var emitted = PrismaticSectionStackEmitter.Emit(stack);
            if (emitted.Body is null)
                return KernelResult<FirmamentStepExportResult>.Failure(emitted.Diagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.ProfileComposition")).ToArray());
            if ((stack.Feature.ConstructionPlaneBlindDrills?.Count ?? 0) > 0)
            {
                var finalPlan = SectionStackBlindDrillComposeBridge.TryApply(stack, emitted.Plan!, out var bridgeDiagnostics, out _);
                if (finalPlan?.TopologyPlan is null)
                    return KernelResult<FirmamentStepExportResult>.Failure(bridgeDiagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.ProfileComposition.BlindDrill")).ToArray());
                var materialized = PrismaticSectionStackBrepMaterializer.TryMaterialize(finalPlan.TopologyPlan);
                if (materialized.Body is null)
                    return KernelResult<FirmamentStepExportResult>.Failure(materialized.Diagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.ProfileComposition.BlindDrill")).ToArray());
                emitted = new PrismaticSectionStackEmissionResult(materialized.Body, finalPlan, emitted.Diagnostics.Concat(bridgeDiagnostics).ToArray(), finalPlan.Correspondence);
            }
            if (materializerSource.Contains("EdgeFinish", StringComparison.Ordinal))
                return ExportComposedSemanticTopBoundaryChamfer(materializerSource, parsed, stack, emitted);
            // The composition route either retained the checked body above or
            // replaced it with a checked blind-drill materialization.
            var completedBody = emitted.Body;
            if (completedBody is null)
                return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, "profile-composition-materialization-missing-body", "FirmamentV2.ProfileComposition")]);
            var step = Step242Exporter.ExportBody(completedBody);
            if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
            return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, stack.Feature.Name, 0, "prismatic-section-stack", "line-arc-profile-composition"));
        }
        if (ProfileAuthoringParser.IsProfileSource(materializerSource))
        {
            var parsed = ProfileAuthoringParser.Parse(materializerSource);
            if (parsed.Profile is null) return KernelResult<FirmamentStepExportResult>.Failure(parsed.Diagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.Profile")).ToArray());
            var emitted = ResolvedProfile2DValidator.Extrude(parsed.Profile, parsed.Height);
            if (emitted.Status != LineArcProfileExtrudeStatus.Succeeded || emitted.Body is null) return KernelResult<FirmamentStepExportResult>.Failure(emitted.Diagnostics.Select(x => new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, x, "FirmamentV2.Profile")).ToArray());
            if (materializerSource.Contains("EdgeFinish", StringComparison.Ordinal))
                return ExportProfileSemanticTopBoundaryChamfer(materializerSource, parsed.Profile, parsed.Height, emitted);
            var step = Step242Exporter.ExportBody(emitted.Body); if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
            return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, parsed.Profile.Name, 0, "profile-extrude", "line-arc-profile"));
        }
        var v2Parse = FirmamentV2Parser.Parse(sourceText, sourceDirectory);
        if (v2Parse.IsSuccess && v2Parse.Document is not null)
        {
            var dfm = FirmamentV2DfmEnforcement.Validate(v2Parse.Document);
            if (!dfm.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(dfm.Diagnostics);
            }

            if (TryExportV2StandaloneCubicLattice(v2Parse.Document) is { } latticeExport)
            {
                return latticeExport;
            }

            if (TryExportV2HollowBody(v2Parse.Document) is { } hollowExport)
            {
                return hollowExport;
            }

            if (TryExportV2RoundedBoxBody(v2Parse.Document) is { } roundedBoxExport)
            {
                return roundedBoxExport;
            }

            // Do not let the existing semantic-hole route silently export the unmodified host.
            // M9 has an explicit AIR/graph contract, but the authoritative single-body merge
            // (retained box-with-void plus analytic struts/nodes/bonds) is not yet materialized.
            if (v2Parse.Document.LatticeFills is { Count: > 0 })
            {
                return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(
                    Kernel.Core.Diagnostics.KernelDiagnosticCode.NotImplemented,
                    Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                    "lattice-fill-brep-plan-not-materialized: M9 parsed and DFM-validated the explicit FillRegion, but the current BRep backend cannot emit the required single authoritative body combining an internal box cavity, a preserved through-hole, and bonded analytic lattice members. No STEP artifact was emitted.",
                    "FirmamentV2.LatticeFill")]);
            }

            var routed = DispatchV2Routes(v2Parse.Document,
                ("CombinedHoleEdgeFinish", TryExportV2CombinedHoleEdgeFinishBody),
                ("AirChamfer", TryExportV2AirChamferBody),
                ("SemanticHole", TryExportV2SemanticHoleBody),
                ("ControlledSideHole", TryExportV2ControlledSideHoleBody),
                ("InlineStepReplacement", TryExportV2InlineStepReplacementBody),
                ("InlineStep", TryExportV2InlineStepBody));
            if (routed is not null)
            {
                return routed;
            }

            var lowering = FirmamentV2BuildLowering.LowerPrimitiveBridge(v2Parse.Document);
            if (!lowering.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(lowering.Diagnostics);
            }

            var execution = FirmamentPrimitiveExecutor.Execute(lowering.Value);
            if (!execution.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(execution.Diagnostics);
            }

            var executedPrimitive = execution.Value.ExecutedPrimitives.LastOrDefault();
            if (executedPrimitive is null)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(execution.Diagnostics);
            }

            var semanticPmi = BuildV2SemanticPmi(v2Parse.Document, [], executedPrimitive.FeatureId);
            var step = Step242Exporter.ExportBody(executedPrimitive.Body, semanticPmi);
            if (!step.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
            }

            return KernelResult<FirmamentStepExportResult>.Success(
                new FirmamentStepExportResult(
                    step.Value,
                    executedPrimitive.FeatureId,
                    executedPrimitive.OpIndex,
                    "primitive",
                    v2Parse.Document.Solid.RecordType.ToLowerInvariant(),
                    DatumInspection: v2Parse.Document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.DatumPlane).Select(p => new FirmamentPmiInspectionDatum(p.Name, "planar", p.Target)).ToArray() ?? [],
                    DimensionInspection: []));
        }

        if (v2Parse.Diagnostics.Contains(FirmamentV2Parser.InlineStepRequiresCanonical, StringComparer.Ordinal))
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(
                Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                "firmament-inline-step-requires-aetheris-canonical-step: Inline STEP requires an Aetheris-canonical AP242 file. Run `aetheris canon <input.step> --out <canonical.step>` first.",
                "FirmamentV2.InlineStep")]);
        }

        if (v2Parse.Diagnostics.Any(d => d.StartsWith("firmament-v2-inline-step-", StringComparison.Ordinal)))
        {
            return KernelResult<FirmamentStepExportResult>.Failure(v2Parse.Diagnostics.Select(d => new Kernel.Core.Diagnostics.KernelDiagnostic(
                Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                d,
                "FirmamentV2.InlineStep")).ToArray());
        }

        var fatalV2Diagnostics = v2Parse.Diagnostics.Where(FirmamentV2Parser.IsFatalDiagnosticCode).Distinct(StringComparer.Ordinal).ToArray();
        if (v2Parse.Disposition == FirmamentV2ParseDisposition.RecognizedInvalid && fatalV2Diagnostics.Length > 0)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(fatalV2Diagnostics.Select(code => new Kernel.Core.Diagnostics.KernelDiagnostic(
                Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                code,
                "FirmamentV2.Parse")).ToArray());
        }

        return FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
    }

    private static KernelResult<FirmamentStepExportResult> ExportComposedSemanticTopBoundaryChamfer(
        string source, PrismaticProfileCompositionParseResult parsed, PrismaticSectionStackConstruction stack, PrismaticSectionStackEmissionResult emitted)
    {
        KernelResult<FirmamentStepExportResult> Fail(string code) => KernelResult<FirmamentStepExportResult>.Failure([
            new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, code, "FirmamentV2.ComposeSemanticSelection")]);
        if (emitted.Body is null || emitted.Correspondence is null) return Fail("MissingCorrespondenceEvidence");
        var composedCurves = stack.Slabs.MaxBy(s => s.To)!.Region.Outer.Loops.Single().Segments.Select(segment => segment.Geometry).ToArray();
        if (!IsAdmittedPrimitiveBoundary(composedCurves))
            return Fail($"{EdgeFinishProfileComposeBoundaryUnsupported}:host={stack.Feature.Name}:construction=Compose:face=+Z:target=Boundary:supported=primitive-box-or-explicit-semantic-chain:Profile/Compose polygon-boundary materialization is not implemented.");
        var selections = SemanticSelectionSourceParser.Parse(source, parsed.Profiles.Values.First(), stack.Feature.Name, out var diagnostics);
        if (diagnostics.Count != 0 || selections.Count != 1) return Fail(diagnostics.FirstOrDefault() ?? "SemanticSourceNotFound");
        var selection = SemanticTopologySelectionResolver.Resolve(emitted.Body, emitted.Correspondence, selections[0]);
        if (!selection.Succeeded || !selection.IsClosed || selection.Request.Require != SemanticSelectionRequirement.ClosedLoop) return Fail(selection.Failure == SemanticSelectionFailure.None ? "SelectionConsumerMismatch" : selection.Failure.ToString());
        var finish = Regex.Match(source, @"\bEdgeFinish\s+(?<name>\w+)\s*\{\s*Target\s*:\s*(?<target>\w+)\s*;?\s*Kind\s*:\s*(?<kind>Chamfer)\s*;?\s*Distance\s*:\s*(?<amount>[-+.\d]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!finish.Success || finish.Groups["target"].Value != selection.Request.Label) return Fail("SelectionConsumerMismatch");
        var topSlab = stack.Slabs.MaxBy(s => s.To)!;
        var curves = topSlab.Region.Outer.Loops.Single().Segments.Select(x => x.Geometry).ToArray();
        if (curves.Length < 4 || curves.Any(x => x is not LineArcLineSegment2D)) return Fail("UnsupportedTopologyChange");
        var points = curves.Cast<LineArcLineSegment2D>().SelectMany(x => new[] { x.Start, x.End }).ToArray();
        var minX = points.Min(x => x.X); var maxX = points.Max(x => x.X); var minY = points.Min(x => x.Y); var maxY = points.Max(x => x.Y);
        if (points.Any(p => p.X != minX && p.X != maxX && p.Y != minY && p.Y != maxY)) return Fail("UnsupportedTopologyChange");
        var compiled = AirTopFaceBoundaryChamferCompiler.Compile(new(stack.Feature.Name, $"{stack.Feature.Name}.{finish.Groups["name"].Value}", finish.Groups["name"].Value, maxX - minX, maxY - minY, stack.Feature.CriticalLevels.Max() - stack.Feature.CriticalLevels.Min(), "+Z", "Boundary", "Chamfer", double.Parse(finish.Groups["amount"].Value, System.Globalization.CultureInfo.InvariantCulture), new AirSourceSpan(finish.Index, finish.Length, stack.Feature.Name)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.BRepPlan?.RealizationPlan is null) return Fail(compiled.Diagnostics.FirstOrDefault() ?? "UnsupportedTopologyChange");
        var step = Step242Exporter.ExportBody(compiled.Body, new Step242ExportOptions { ProductName = compiled.Feature.FeatureName, ApplicationName = AirTopFaceBoundaryChamferCompileResult.ProductionRoute, BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value)) return Fail("semantic-compose-finish-step-reimport-failed");
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, compiled.Feature.FeatureId, 0, "semantic-compose-edge-finish", "source-grounded-composed-top-boundary-chamfer"));
    }

    private static KernelResult<FirmamentStepExportResult> ExportProfileSemanticTopBoundaryChamfer(
        string source,
        ResolvedProfile2D profile,
        double height,
        LineArcProfileExtrudeResult emitted)
    {
        KernelResult<FirmamentStepExportResult> Fail(string code) => KernelResult<FirmamentStepExportResult>.Failure([
            new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, code, "FirmamentV2.SemanticSelection")]);
        if (emitted.Body is null || emitted.Correspondence is null) return Fail("NoMaterializedDescendants");
        if (!IsAdmittedPrimitiveBoundary(profile.Loops.Single().Segments.Select(segment => segment.Geometry).ToArray()))
            return Fail($"{EdgeFinishProfileComposeBoundaryUnsupported}:host={profile.Name}:construction=Profile:face=+Z:target=Boundary:supported=primitive-box-or-explicit-semantic-chain:Profile/Compose polygon-boundary materialization is not implemented.");
        var selections = SemanticSelectionSourceParser.Parse(source, profile, profile.Name, out var selectionDiagnostics);
        if (selectionDiagnostics.Count > 0 || selections.Count != 1) return Fail(selectionDiagnostics.FirstOrDefault() ?? "SemanticSourceNotFound");
        var selection = SemanticTopologySelectionResolver.Resolve(emitted.Body, emitted.Correspondence, selections[0]);
        if (!selection.Succeeded) return Fail(selection.Failure.ToString());
        var finish = Regex.Match(source, @"\bEdgeFinish\s+(?<name>\w+)\s*\{\s*Target\s*:\s*(?<target>\w+)\s*;?\s*Kind\s*:\s*(?<kind>Chamfer|Fillet)\s*;?\s*(?:Distance|Radius)\s*:\s*(?<amount>[-+.\d]+)mm", RegexOptions.Singleline | RegexOptions.CultureInvariant);
        if (!finish.Success || !string.Equals(finish.Groups["target"].Value, selection.Request.Label, StringComparison.Ordinal)) return Fail("SelectionConsumerMismatch");
        if (!string.Equals(finish.Groups["kind"].Value, "Chamfer", StringComparison.Ordinal) || selection.Request.Require != SemanticSelectionRequirement.ClosedLoop || !selection.IsClosed) return Fail("SelectionConsumerMismatch");
        var lines = profile.Loops.Single().Segments.Select(x => x.Geometry).OfType<LineArcLineSegment2D>().ToArray();
        if (lines.Length != 4 || profile.Loops.Single().Segments.Count != 4) return Fail("UnsupportedTopologyChange");
        var points = lines.SelectMany(x => new[] { x.Start, x.End }).ToArray();
        var minX = points.Min(x => x.X); var maxX = points.Max(x => x.X); var minY = points.Min(x => x.Y); var maxY = points.Max(x => x.Y);
        if (lines.Any(x => Math.Abs(x.Start.X - x.End.X) > 1e-9 && Math.Abs(x.Start.Y - x.End.Y) > 1e-9)) return Fail("UnsupportedTopologyChange");
        var compiled = AirTopFaceBoundaryChamferCompiler.Compile(new(
            profile.Name, $"{profile.Name}.{finish.Groups["name"].Value}", finish.Groups["name"].Value,
            maxX - minX, maxY - minY, height, "+Z", "Boundary", "Chamfer",
            double.Parse(finish.Groups["amount"].Value, System.Globalization.CultureInfo.InvariantCulture),
            new AirSourceSpan(finish.Index, finish.Length, profile.Name)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.BRepPlan?.RealizationPlan is null) return Fail(compiled.Diagnostics.FirstOrDefault() ?? "UnsupportedTopologyChange");
        if (!FirmamentManifoldChecker.IsManifold(compiled.Body)) return Fail("semantic-selection-finish-nonmanifold");
        var step = Step242Exporter.ExportBody(compiled.Body, new Step242ExportOptions { ProductName = compiled.Feature.FeatureName, ApplicationName = AirTopFaceBoundaryChamferCompileResult.ProductionRoute, BrepExportPreflightMode = BrepExportPreflightMode.Enforce, BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value)) return Fail("semantic-selection-finish-step-reimport-failed");
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, compiled.Feature.FeatureId, 0, "semantic-profile-edge-finish", "source-grounded-top-boundary-chamfer"));
    }

    private static bool IsAdmittedPrimitiveBoundary(IReadOnlyList<LineArcProfileCurve2D> curves) =>
        curves.Count == 4
        && curves.All(curve => curve is LineArcLineSegment2D)
        && curves.Cast<LineArcLineSegment2D>().All(line => Math.Abs(line.Start.X - line.End.X) < 1e-9 || Math.Abs(line.Start.Y - line.End.Y) < 1e-9);

    private static KernelResult<FirmamentStepExportResult>? TryExportV2StandaloneCubicLattice(FirmamentV2Document document)
    {
        var fills = document.StandaloneLatticeFills ?? [];
        if (fills.Count == 0) return null;
        if (fills.Count != 1)
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, "standalone-fill-multiple-unsupported", "FirmamentV2.CubicLattice")]);
        }

        var fill = fills[0];
        var center = new Point3D(fill.Region.Center[0], fill.Region.Center[1], fill.Region.Center[2]);
        var realization = CubicLatticeBRepPlanner.Create(fill.CellsX, fill.CellsY, fill.CellsZ, fill.CellSize, fill.StrutRadius, fill.NodeRadius, center);
        if (!realization.IsSuccess || realization.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(realization.Diagnostics);
        var body = realization.Value.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid || !FirmamentManifoldChecker.IsManifold(body))
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, "lattice-topology-verification-failed", "FirmamentV2.CubicLattice")]);
        }

        var mass = BrepMassProperties.Evaluate(body);
        if (mass.Status == BrepMassPropertiesStatus.Unavailable || mass.AbsoluteVolume <= 0d || mass.Centroid is null)
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, "lattice-topology-verification-failed: BRep mass properties unavailable", "FirmamentV2.CubicLattice")]);
        }

        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = fill.Name,
            ApplicationName = "Aetheris.Firmament.CubicTruss.M9R",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
            EmitFullCircleTrimmedCurves = true,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
        {
            return KernelResult<FirmamentStepExportResult>.Failure(reimport.IsSuccess
                ? [new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, "lattice-topology-verification-failed: STEP reimport is not manifold", "FirmamentV2.CubicLattice")]
                : reimport.Diagnostics);
        }

        var template = (document.Templates ?? []).Single(template => string.Equals(template.Process, "Additive", StringComparison.OrdinalIgnoreCase));
        var graph = realization.Value.Plan.Construction;
        var centroid = mass.Centroid.Value;
        var report = new FirmamentStandaloneLatticeReport(
            template.Name, fill.Pattern, [fill.CellsX, fill.CellsY, fill.CellsZ], fill.CellSize, fill.StrutRadius, fill.NodeRadius, fill.Region.Size, fill.Placement,
            graph.Nodes.Count, graph.Members.Count, realization.Value.Plan.SeamCount,
            graph.Nodes.Count(node => node.Valence == 3), graph.Nodes.Count(node => node.Valence == 4), graph.Nodes.Count(node => node.Valence == 5), graph.Nodes.Count(node => node.Valence == 6),
            realization.Value.Plan.IsAuthoritative, realization.Value.Plan.Signature,
            body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(),
            body.Geometry.Surfaces.Count(pair => pair.Value.Kind == SurfaceGeometryKind.Sphere), body.Geometry.Surfaces.Count(pair => pair.Value.Kind == SurfaceGeometryKind.Cylinder),
            realization.Value.AnalyticVolume, mass.AbsoluteVolume, double.Abs(realization.Value.AnalyticVolume - mass.AbsoluteVolume), mass.SurfaceArea,
            [centroid.X, centroid.Y, centroid.Z], Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))).ToLowerInvariant(), true, true);
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, fill.Name, 0, "standalone-lattice", "cubic-truss", Lattice: report));
    }

    /// <summary>
    /// Converts the historical nullable TryExport routes into one explicit dispatcher
    /// contract.  A null is only a Declined route; a non-null failed result has claimed
    /// the document and is terminal.  This keeps route order from deciding feature
    /// composition policy.
    /// </summary>
    private static KernelResult<FirmamentStepExportResult>? DispatchV2Routes(
        FirmamentV2Document document,
        params (string Name, Func<FirmamentV2Document, KernelResult<FirmamentStepExportResult>?> Execute)[] routes)
    {
        foreach (var route in routes)
        {
            var raw = route.Execute(document);
            var result = raw is null ? ExportRouteResult.Decline(route.Name) : ExportRouteResult.Complete(route.Name, raw);
            if (result.Disposition == ExportRouteDisposition.Declined)
            {
                continue;
            }

            return result.Artifact!;
        }

        return null;
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2CombinedHoleEdgeFinishBody(FirmamentV2Document document)
    {
        var modifies = document.ModifyBlocks ?? [];
        var holesDeclared = modifies.SelectMany(m => m.SemanticHoles).ToArray();
        var finishes = modifies.SelectMany(m => (m.EdgeFinishes ?? [])).ToArray();
        if (holesDeclared.Length == 0 || finishes.Length == 0)
        {
            return null;
        }

        if (document.Solids.Count != 1 || modifies.Count != 1 || modifies[0].Regions.Count != 0 || modifies[0].TargetSolid != document.Solids[0].Name)
        {
            return CombinedFailure("CombinedFeatureHostUnsupported: CombinedHoleEdgeFinish requires one Box host and one empty-of-Regions Modify context.");
        }

        var modify = modifies[0];
        var solid = document.Solids[0];
        if (solid.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
        {
            return CombinedFailure($"CombinedFeatureHostUnsupported: host '{solid.Name}' is not an admitted Box.");
        }

        if (finishes.Length != 1)
        {
            return CombinedFailure($"CombinedFeatureThirdFamilyUnsupported: CombinedHoleEdgeFinish X1 admits exactly one EdgeFinish (actual={finishes.Length}).");
        }

        var finish = finishes[0];
        if (!string.Equals(finish.FaceAxis, "+Z", StringComparison.Ordinal) || !string.Equals(finish.Target, "Boundary", StringComparison.Ordinal) || !string.Equals(finish.Kind, "Chamfer", StringComparison.Ordinal))
        {
            return CombinedFailure($"CombinedFeatureSelectionLost: EdgeFinish '{finish.Name}' must select the +Z outer Boundary with Kind Chamfer.");
        }

        var holes = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document);
        if (holes.Count != holesDeclared.Length || holes.Count == 0)
        {
            return CombinedFailure("CombinedFeaturePlanChainInvalid: semantic Hole lowering did not preserve the admitted source hole set.");
        }

        // Hole is intentionally the first construction stage.  Existing materialization
        // is used as the semantic/host proof; the bounded final plan below then consumes
        // its same source intent alongside the admitted outer-boundary finish.
        var host = document.ConceptIr is null
            ? new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d)
            : new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], 0d, box.Size[2]);
        var holeStages = holes.Select(h => AirHoleSimpleShaftMaterializer.Execute(h, host)).ToArray();
        if (holeStages.Any(stage => !stage.Succeeded || stage.Plan is null || stage.Correspondence is null))
        {
            return CombinedFailure("CombinedFeatureMaterializerDiverged: admitted semantic Hole stage did not materialize its authoritative plan.", holeStages.SelectMany(stage => stage.Diagnostics));
        }

        var combinedHoles = new List<CombinedTopBoundaryChamferThroughHole>(holes.Count);
        foreach (var (hole, stage) in holes.Zip(holeStages))
        {
            if (hole.EndCondition is not AirHoleEndCondition.ThroughAll || hole.Stack.Kind != AirHoleStackKind.SimpleShaft || hole.Placement is not AirFaceLocalHolePlacement placement || hole.Axis.Direction.Z < 1d - 1e-9)
            {
                return CombinedFailure($"CombinedFeaturePlanChainInvalid: Hole '{hole.FeatureId}' is outside X1; only top/+Z simple-shaft ThroughAll holes are admitted.");
            }
            combinedHoles.Add(new CombinedTopBoundaryChamferThroughHole(hole.FeatureId, placement.U, placement.V, hole.Shaft.Radius));
        }

        // Resolve and admit the exact EdgeFinish family after the Hole stage.  X1's
        // interaction classification is geometric-plan evidence, not a raw BRep ID or
        // coordinate search: the selected descendant is the known outer boundary role.
        var compiledFinish = AirTopFaceBoundaryChamferCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{finish.Name}", finish.Name,
            box.Size[0], box.Size[1], box.Size[2], finish.FaceAxis, finish.Target, finish.Kind, finish.Distance,
            new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName)));
        if (!compiledFinish.Succeeded || compiledFinish.BRepPlan?.RealizationPlan is null)
        {
            return CombinedFailure("CombinedFeatureSelectionLost: post-Hole outer boundary selection did not admit the exact chamfer planner.", compiledFinish.Diagnostics);
        }

        var parentHolePlan = string.Join(",", holeStages.Select(s => s.Plan!.HoleBRepPlan?.StableId ?? s.Plan!.SemanticFeatureId));
        var finalPlan = new CombinedTopBoundaryChamferThroughHolePlan(
            $"brep-plan:combined-hole-edgefinish:{solid.Name}:{finish.Name}",
            $"brep-plan:host:{solid.Name}->[{parentHolePlan}]",
            ["HostBRepPlan", "HoleChangedBRepPlan", "EdgeFinishChangedBRepPlan"],
            box.Size[0], box.Size[1], host.ZMin, host.ZMax, finish.Distance, combinedHoles);
        var materialized = CombinedTopBoundaryChamferThroughHoleBuilder.Build(finalPlan);
        if (!materialized.IsSuccess || materialized.Value is null)
        {
            return CombinedFailure("CombinedFeatureInteractionUnsupported: Hole descendants and EdgeFinish target are not Disjoint in X1.", materialized.Diagnostics.Select(d => d.Message));
        }

        var body = materialized.Value;
        if (!FirmamentManifoldChecker.IsManifold(body))
        {
            return CombinedFailure("CombinedFeaturePlanChainInvalid: final combined plan did not produce one enclosed manifold body.");
        }
        var step = Step242Exporter.ExportBody(body);
        if (!step.IsSuccess || step.Value is null)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        }
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
        {
            return CombinedFailure("CombinedFeatureMaterializerDiverged: final STEP reimport did not preserve one enclosed manifold body.", reimport.Diagnostics.Select(d => d.Message));
        }

        var descendants = new List<SemanticTopologyDescendant>();
        var finalTop = body.Topology.Faces.Single(f => f.Id.Value == 2);
        var finalBottom = body.Topology.Faces.Single(f => f.Id.Value == 1);
        for (var i = 0; i < holes.Count; i++)
        {
            var source = $"hole:{holes[i].FeatureId}";
            descendants.Add(new($"combined:{source}:mouth-loop", "Loop", SemanticTopologyRole.HoleEntryLoop, source, Loop: finalTop.LoopIds[i + 1], ParentStableId: holes[i].FeatureId));
            descendants.Add(new($"combined:{source}:exit-loop", "Loop", SemanticTopologyRole.HoleExitLoop, source, Loop: finalBottom.LoopIds[i + 1], ParentStableId: holes[i].FeatureId));
            descendants.Add(new($"combined:{source}:wall", "Face", SemanticTopologyRole.HoleWallFace, source, Face: new FaceId(11 + i), ParentStableId: holes[i].FeatureId));
        }
        descendants.Add(new($"combined:edgefinish:{finish.Name}:replacement", "Face", SemanticTopologyRole.EdgeFinishReplacementFace, $"edgefinish:{finish.Name}", Face: new FaceId(7), ParentStableId: finish.Name));
        var correspondence = new SemanticTopologyCorrespondence(solid.Name, descendants, ["HostBRepPlan", "HoleChangedBRepPlan", "EdgeFinishChangedBRepPlan", "AuthoritativeBRepPlan"]);
        if (correspondence.Descendants.Count(d => d.Role == SemanticTopologyRole.HoleEntryLoop) != holes.Count || correspondence.Descendants.Count(d => d.Role == SemanticTopologyRole.HoleExitLoop) != holes.Count)
        {
            return CombinedFailure("CombinedFeaturePlanChainInvalid: final plan lost Hole mouth or exit correspondence.");
        }

        var holeRemoved = combinedHoles.Sum(h => System.Math.PI * h.Radius * h.Radius * (host.ZMax - host.ZMin));
        var report = new FirmamentCombinedFeaturePlanReport(
            "CombinedHoleEdgeFinish", "Succeeded", finalPlan.ParentHostPlanId!, finalPlan.AppliedFeatureIds, finalPlan.PlanId, "Disjoint",
            descendants.Count(d => d.SourceStableId.StartsWith("hole:", StringComparison.Ordinal)), 1,
            box.Size[0] * box.Size[1] * (host.ZMax - host.ZMin), holeRemoved, finalPlan.AnalyticVolume,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(),
            body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane), body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder), true, true);
        var featureReports = holes.Select(h => new FirmamentHoleFeatureReport(h.Name, "Hole", h.FeatureId, h.Shaft.Diameter, h.Placement.U, h.Placement.V, null, null, null, null, "top", null, nameof(AirHoleSimpleShaftMaterializer), "HoleChangedBRepPlan", h.Stack.Kind.ToString(), "CombinedHoleEdgeFinish", 1, 0, 0, report.StepSha256, true)).ToArray();
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, finish.Name, 0, "combined-hole-edgefinish", "CombinedHoleEdgeFinish", ConceptIr: document.ConceptIr, Features: featureReports, Combined: report));
    }

    private static KernelResult<FirmamentStepExportResult> CombinedFailure(string message, IEnumerable<string>? details = null) =>
        KernelResult<FirmamentStepExportResult>.Failure((new[] { message }).Concat(details ?? []).Distinct().Select(detail => new Kernel.Core.Diagnostics.KernelDiagnostic(
            Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            detail,
            "FirmamentV2.CombinedHoleEdgeFinish")).ToArray());

    private static KernelResult<FirmamentStepExportResult>? TryExportV2AirChamferBody(FirmamentV2Document document)
    {
        var finishes = (document.ModifyBlocks ?? []).SelectMany(m => (m.EdgeFinishes ?? []).Select(f => (Modify: m, Finish: f))).ToArray();
        if (finishes.Length == 0) return null;
        // A narrow finish-only route never owns a document that also has semantic holes.
        // CombinedHoleEdgeFinish is the only X1 route permitted to claim that shape.
        if ((document.ModifyBlocks ?? []).Any(m => m.SemanticHoles.Count > 0)) return null;
        if (finishes.Length == 2 && finishes.All(x => string.Equals(x.Finish.Kind, "Chamfer", StringComparison.Ordinal)))
        {
            if (document.Solids.Count != 1 || document.ModifyBlocks!.Count != 1 || document.ModifyBlocks[0].Regions.Count != 0 || document.ModifyBlocks[0].SemanticHoles.Count != 0)
                return AirChamferFailure("localized-junction-production-route-requires-one-box-and-one-modify-block");
            var junctionSolid = document.Solids.SingleOrDefault(s => s.Name == finishes[0].Modify.TargetSolid);
            if (junctionSolid?.Primitive is not FirmamentV2BoxRecord junctionBox || junctionBox.Size.Count != 3)
                return AirChamferFailure("localized-junction-unsupported-history:expected-history-known-box");
            return ExportV2LocalizedEdgeJunctionChamfer(document, junctionSolid, finishes[0].Finish, finishes[1].Finish, junctionBox);
        }
        if (finishes.Length == 2 && finishes.All(x => string.Equals(x.Finish.Kind, "Fillet", StringComparison.Ordinal)))
        {
            if (document.Solids.Count != 1 || document.ModifyBlocks!.Count != 1 || document.ModifyBlocks[0].Regions.Count != 0 || document.ModifyBlocks[0].SemanticHoles.Count != 0)
                return AirChamferFailure("localized-fillet-junction-production-route-requires-one-box-and-one-modify-block");
            var junctionSolid = document.Solids.SingleOrDefault(s => s.Name == finishes[0].Modify.TargetSolid);
            if (junctionSolid?.Primitive is not FirmamentV2BoxRecord junctionBox || junctionBox.Size.Count != 3)
                return AirChamferFailure("localized-fillet-junction-unsupported-history:expected-history-known-box");
            return ExportV2LocalizedEdgeJunctionFillet(document, junctionSolid, finishes[0].Finish, finishes[1].Finish, junctionBox);
        }
        if (finishes.Length == 3 && finishes.All(x => string.Equals(x.Finish.Kind, "Fillet", StringComparison.Ordinal)))
        {
            if (document.Solids.Count != 1 || document.ModifyBlocks!.Count != 1 || document.ModifyBlocks[0].Regions.Count != 0 || document.ModifyBlocks[0].SemanticHoles.Count != 0)
                return AirChamferFailure("localized-trihedral-fillet-production-route-requires-one-box-and-one-modify-block");
            var junctionSolid = document.Solids.SingleOrDefault(s => s.Name == finishes[0].Modify.TargetSolid);
            if (junctionSolid?.Primitive is not FirmamentV2BoxRecord junctionBox || junctionBox.Size.Count != 3)
                return AirChamferFailure("localized-trihedral-fillet-unsupported-history:expected-history-known-box");
            return ExportV2LocalizedTrihedralFillet(document, junctionSolid, finishes.Select(x => x.Finish).ToArray(), junctionBox);
        }
        if (finishes.Length == 3)
            return AirChamferFailure("localized-trihedral-fillet-unsupported-finish-combination:mixed-families");
        if (finishes.Length == 2)
            return AirChamferFailure("localized-junction-unsupported-finish-combination:mixed-families");
        if (finishes.Length > 2)
            return AirChamferFailure("localized-trihedral-fillet-unsupported-valence:three-selected-edges-maximum");
        if (finishes.Length != 1 || document.Solids.Count != 1 || document.ModifyBlocks!.Count != 1
            || document.ModifyBlocks[0].Regions.Count != 0 || document.ModifyBlocks[0].SemanticHoles.Count != 0)
            return AirChamferFailure("air-chamfer-production-route-requires-one-box-and-one-edge-finish");

        var (modify, finish) = finishes[0];
        var solid = document.Solids.SingleOrDefault(s => s.Name == modify.TargetSolid);
        if (solid?.Primitive is FirmamentV2CylinderRecord cylinder)
            return ExportV2CircularRimChamfer(document, solid, finish, cylinder);
        if (solid?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
            return AirChamferFailure("chamfer-unsupported-history:expected-history-known-box-or-right-circular-cylinder");

        // AIR-FILLET-LOCALIZED-M1: semantic support-face selection only; emitted BRep IDs never enter Firmament.
        if (string.Equals(finish.Kind, "Fillet", StringComparison.Ordinal))
            return ExportV2LocalizedTangentBlendSingleEdgeFillet(document, solid, finish, box);

        // AIR-CHAMFER-LOCALIZED-PLAN-A1: a semantic face-pair selector, never an emitted edge id.
        if (string.Equals(finish.FaceAxis, "+X", StringComparison.Ordinal)
            && string.Equals(finish.Target, "SharedEdgePlusZ", StringComparison.Ordinal))
            return ExportV2LocalizedPlanarSingleEdgeChamfer(document, solid, finish, box);

        var compiled = AirTopFaceBoundaryChamferCompiler.Compile(new(
            solid.Name,
            $"{solid.Name}.{finish.Name}",
            finish.Name,
            box.Size[0], box.Size[1], box.Size[2],
            finish.FaceAxis, finish.Target, finish.Kind, finish.Distance,
            new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.RealizationPlan is null || compiled.Topology is null)
            return AirChamferFailure(compiled.Feature.AdmissionReason, compiled.Diagnostics);

        var manifold = FirmamentManifoldChecker.IsManifold(compiled.Body);
        if (!manifold) return AirChamferFailure("air-chamfer-emitted-body-is-not-manifold", compiled.Diagnostics);

        var step = Step242Exporter.ExportBody(compiled.Body, new Step242ExportOptions
        {
            ProductName = compiled.Feature.FeatureName,
            ApplicationName = AirTopFaceBoundaryChamferCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);

        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(reimport.Diagnostics);
        var reimported = reimport.Value;
        var reimportedManifold = FirmamentManifoldChecker.IsManifold(reimported);
        if (!reimportedManifold) return AirChamferFailure("air-chamfer-step-reimport-is-not-manifold");

        var plan = compiled.BRepPlan.RealizationPlan;
        var top = plan.Vertices.Where(v => v.SectionIndex == 2).Select(v => v.Point).ToArray();
        var sourceBounds = Bounds(compiled.Body);
        var reimportedBounds = Bounds(reimported);
        var featureProvenance = new Dictionary<string, string>(StringComparer.Ordinal);
        if (solid.Provenance?.TryGetValue("Bounds", out var boundsProvenance) == true) featureProvenance["Bounds"] = boundsProvenance;
        if (finish.Provenance?.TryGetValue("Face", out var faceProvenance) == true) featureProvenance["Selection"] = faceProvenance;
        if (finish.Provenance?.TryGetValue("Distance", out var distanceProvenance) == true) featureProvenance["Distance"] = distanceProvenance;
        var report = new FirmamentAirChamferReport(
            new("Chamfer", compiled.Feature.BodyId, compiled.Feature.FeatureId, compiled.Feature.FeatureName, $"FaceBoundary({compiled.Feature.Selection.FaceAxis})", compiled.Feature.Rule.Distance, compiled.Feature.Rule.Unit, $"{compiled.Feature.SourceSpan.Start}:{compiled.Feature.SourceSpan.Length}", compiled.Feature.Admission.ToString(), compiled.Feature.AdmissionReason, featureProvenance),
            new("SectionTransition", compiled.Construction.Profiles.Count, compiled.Construction.Profiles.Select(p => p.Z).ToArray(), compiled.Construction.Transition.Correspondence, compiled.Construction.Transition.SplitPolicy),
            new(compiled.BRepPlan.IsAuthoritative, plan.Vertices.Count, plan.Edges.Count, plan.Faces.Count, plan.ExpectedLoopCount, plan.ExpectedCoedgeCount, compiled.BRepPlan.Summary.ChamferFaceCount, plan.SplitPolicy, plan.DeterministicSignature),
            new(AirTopFaceBoundaryChamferCompileResult.ProductionRoute, false, manifold, compiled.Topology.VertexCount, compiled.Topology.EdgeCount, compiled.Topology.FaceCount, sourceBounds,
                top.Min(p => p.X) - (-box.Size[0] / 2d), top.Min(p => p.Y) - (-box.Size[1] / 2d)),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true,
                reimported.Topology.Vertices.Count(), reimported.Topology.Edges.Count(), reimported.Topology.Faces.Count(), reimportedBounds, reimportedManifold));

        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(
            step.Value, compiled.Feature.FeatureId, 0, "air-chamfer", "top-face-boundary-chamfer", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2LocalizedPlanarSingleEdgeChamfer(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        FirmamentV2EdgeFinishDecl finish,
        FirmamentV2BoxRecord box)
    {
        var compiled = AirLocalizedPlanarReplacementChamferCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{finish.Name}", finish.Name, box.Size[0], box.Size[1], box.Size[2],
            finish.FaceAxis, "+Z", finish.Kind, finish.Distance,
            new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.LocalizedEdgeReplacementRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? compiled.Feature.AdmissionReason, compiled.Diagnostics);

        var body = compiled.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return AirChamferFailure("localized-chamfer-preflight-verification-failed", preflight.Diagnostics.Select(d => d.Code));
        if (!FirmamentManifoldChecker.IsManifold(body)) return AirChamferFailure("localized-chamfer-emitted-body-is-not-manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = compiled.Feature.FeatureName,
            ApplicationName = AirLocalizedPlanarReplacementChamferCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
            return AirChamferFailure("localized-chamfer-step-reimport-verification-failed");

        var plan = compiled.Construction.TopologyPlan;
        var witness = compiled.Construction.Witness;
        var planes = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane);
        var report = new FirmamentAirChamferReport(
            new("Chamfer", compiled.Feature.BodyId, compiled.Feature.FeatureId, compiled.Feature.FeatureName, "SharedEdge(+X,+Z)", finish.Distance, "mm",
                $"{compiled.Feature.SourceSpan.Start}:{compiled.Feature.SourceSpan.Length}", compiled.Feature.Admission.ToString(), compiled.Feature.AdmissionReason,
                new Dictionary<string, string> { ["Selection"] = "semantic Face(+X),Face(+Z)", ["MaterialSide"] = witness.MaterialSide }),
            new("LocalizedPlanarReplacement", 0, [], "ordered-explicit-loops", "ExplicitOwnedEndpoints",
                $"retainedFaces=2;replacementFaces=1;endpointPolicy={witness.EndpointPolicy}", true),
            new(true, plan.ExpectedVertexCount, plan.ExpectedEdgeCount, plan.ExpectedFaceCount, plan.ExpectedLoopCount, plan.ExpectedCoedgeCount, 1,
                "ExplicitOwnedEndpoints", plan.DeterministicSignature, "LocalizedPlanarReplacement"),
            new(AirLocalizedPlanarReplacementChamferCompileResult.ProductionRoute, false, true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), Bounds(body), finish.Distance, finish.Distance, 0, 0, planes),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), Bounds(reimport.Value), true),
            new("SharedEdge(+X,+Z)", "LocalizedPlanarReplacement", "Direct", 2, 1, "ExplicitOwnedEndpoints", new(true, plan.DeterministicSignature), "valid", false),
            LocalizedEdgeFinish: new("Chamfer", "SharedEdge(+X,+Z)", "EqualDistance", finish.Distance, "LocalizedEdgeReplacement", "PlanarChamfer", "Direct", 2, 1, "ExplicitOwnedEndpoints", new(true, plan.DeterministicSignature), "valid", false));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, compiled.Feature.FeatureId, 0, "air-chamfer", "localized-planar-single-edge-chamfer", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2LocalizedEdgeJunctionChamfer(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        FirmamentV2EdgeFinishDecl first,
        FirmamentV2EdgeFinishDecl second,
        FirmamentV2BoxRecord box)
    {
        var compiled = AirLocalizedEdgeJunctionChamferCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{first.Name}.{second.Name}", $"{first.Name}+{second.Name}", box.Size[0], box.Size[1], box.Size[2],
            first.FaceAxis, first.Target, second.FaceAxis, second.Target, first.Distance, second.Distance,
            new AirSourceSpan(first.SourceSpan.Start, first.SourceSpan.Length + second.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.LocalizedEdgeJunctionRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? "localized-junction-construction-witness-required", compiled.Diagnostics);

        var body = compiled.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return AirChamferFailure("localized-junction-chamfer-preflight-verification-failed", preflight.Diagnostics.Select(d => d.Code));
        if (!FirmamentManifoldChecker.IsManifold(body)) return AirChamferFailure("localized-junction-chamfer-emitted-body-is-not-manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = compiled.Construction.ConstructionId,
            ApplicationName = AirLocalizedEdgeJunctionChamferCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
            return AirChamferFailure("localized-junction-chamfer-step-reimport-verification-failed");

        var topology = compiled.Construction.TopologyPlan;
        var planes = body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Plane);
        var report = new FirmamentAirChamferReport(
            new("Chamfer", solid.Name, $"{solid.Name}.{first.Name}.{second.Name}", $"{first.Name}+{second.Name}", "SharedEdge(+X,+Z),SharedEdge(+Y,+Z)", first.Distance, "mm",
                $"{first.SourceSpan.Start}:{first.SourceSpan.Length}|{second.SourceSpan.Start}:{second.SourceSpan.Length}", "Admitted", "localized-junction-direct-single-miter-candidate",
                new Dictionary<string, string> { ["Selection"] = "semantic Face(+X),Face(+Z); Face(+Y),Face(+Z)", ["MaterialSide"] = compiled.Construction.MaterialSide }),
            new("LocalizedEdgeJunction", 0, [], "ordered-explicit-loops", "SharedMiterEdge",
                $"replacementFaces=2;junctionFaces=0;cornerPatch={compiled.Construction.CornerPatch.Kind};boundaryOwnership={compiled.Construction.BoundaryOwnership}", true),
            new(true, topology.ExpectedVertexCount, topology.ExpectedEdgeCount, topology.ExpectedFaceCount, topology.ExpectedLoopCount, topology.ExpectedCoedgeCount, 2,
                "SharedMiterEdge", topology.DeterministicSignature, "LocalizedEdgeJunction"),
            new(AirLocalizedEdgeJunctionChamferCompileResult.ProductionRoute, false, true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), Bounds(body), first.Distance, first.Distance, 0, 0, planes),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), Bounds(reimport.Value), true),
            LocalizedEdgeJunction: new(["SharedEdge(+X,+Z)", "SharedEdge(+Y,+Z)"], "Chamfer", "EqualDistance", first.Distance, "Direct", "LocalizedEdgeJunction", compiled.Construction.CornerPatch.Kind, 2, 0,
                new(true, topology.DeterministicSignature), "valid", false, 1, 1));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, $"{solid.Name}.{first.Name}.{second.Name}", 0, "air-chamfer", "localized-edge-junction-chamfer", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2LocalizedEdgeJunctionFillet(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        FirmamentV2EdgeFinishDecl first,
        FirmamentV2EdgeFinishDecl second,
        FirmamentV2BoxRecord box)
    {
        var compiled = AirLocalizedEdgeJunctionFilletCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{first.Name}.{second.Name}", $"{first.Name}+{second.Name}", box.Size[0], box.Size[1], box.Size[2],
            first.FaceAxis, first.Target, second.FaceAxis, second.Target, first.Distance, second.Distance,
            new AirSourceSpan(first.SourceSpan.Start, first.SourceSpan.Length + second.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.LocalizedEdgeJunctionRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? "localized-fillet-junction-direct-intersection-required", compiled.Diagnostics);

        var body = compiled.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return AirChamferFailure("localized-fillet-junction-preflight-verification-failed", preflight.Diagnostics.Select(d => d.Code));
        if (!FirmamentManifoldChecker.IsManifold(body)) return AirChamferFailure("localized-fillet-junction-emitted-body-is-not-manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = compiled.Construction.ConstructionId,
            ApplicationName = AirLocalizedEdgeJunctionFilletCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
            return AirChamferFailure("localized-fillet-junction-step-reimport-verification-failed");

        var topology = compiled.Construction.TopologyPlan;
        var cylinders = body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Cylinder);
        var planes = body.Geometry.Surfaces.Count(surface => surface.Value.Kind == SurfaceGeometryKind.Plane);
        var report = new FirmamentAirChamferReport(
            new("Fillet", solid.Name, $"{solid.Name}.{first.Name}.{second.Name}", $"{first.Name}+{second.Name}", "SharedEdge(+X,+Z),SharedEdge(+Y,+Z)", first.Distance, "mm",
                $"{first.SourceSpan.Start}:{first.SourceSpan.Length}|{second.SourceSpan.Start}:{second.SourceSpan.Length}", "Admitted", "localized-fillet-junction-direct-intersection-candidate",
                new Dictionary<string, string> { ["Selection"] = "semantic Face(+X),Face(+Z); Face(+Y),Face(+Z)", ["MaterialSide"] = compiled.Construction.MaterialSide, ["Branch"] = compiled.Construction.Closure.Branch }),
            new("LocalizedEdgeJunction", 0, [], "ordered-explicit-loops", "DirectIntersectionEllipse",
                $"replacementFaces=2;junctionFaces=0;sharedEdges=1;curve=Ellipse;branch={compiled.Construction.Closure.Branch}", true),
            new(true, topology.ExpectedVertexCount, topology.ExpectedEdgeCount, topology.ExpectedFaceCount, topology.ExpectedLoopCount, topology.ExpectedCoedgeCount, 0,
                "DirectIntersectionEllipse", topology.DeterministicSignature, "LocalizedEdgeJunction"),
            new(AirLocalizedEdgeJunctionFilletCompileResult.ProductionRoute, false, true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), Bounds(body), first.Distance, first.Distance, cylinders, 0, planes),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), Bounds(reimport.Value), true),
            LocalizedEdgeJunction: new(["SharedEdge(+X,+Z)", "SharedEdge(+Y,+Z)"], "Fillet", "ConstantRadius", first.Distance, "Direct", "LocalizedEdgeJunction", "None(DirectIntersection)", 2, 0,
                new(true, topology.DeterministicSignature), "valid", false, 1, 1,
                new("DirectIntersection", "Cylinder", "Cylinder", "Ellipse", true, 1, compiled.Construction.Closure.Branch)));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, $"{solid.Name}.{first.Name}.{second.Name}", 0, "air-fillet", "localized-edge-junction-fillet", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2HollowBody(FirmamentV2Document document)
    {
        if (document.Solids.Count != 1 || document.Solid.ConstructionPolicy != FirmamentV2ConstructionPolicy.Hollow)
            return null;
        var solid = document.Solid;
        if (solid.Hollow is null || solid.Hollow.Openings.Count != 1 || solid.Hollow.Openings[0] != "Top")
            return HollowFailure("UnsupportedOpening");

        KernelResult<ThinWalledBodyRealization> realization = solid.Primitive switch
        {
            FirmamentV2RoundedBoxRecord rounded when rounded.Size.Count == 3 => ThinWalledBodyBRepPlanner.CreateRoundedBox(rounded.Size[0], rounded.Size[1], rounded.Size[2], rounded.CornerRadius, solid.Hollow.WallThickness),
            FirmamentV2FrustumRecord frustum => ThinWalledBodyBRepPlanner.CreateFrustum(frustum.BottomRadius, frustum.TopRadius, frustum.Height, solid.Hollow.WallThickness),
            _ => throw new InvalidOperationException("Parser admitted a Hollow policy without a HollowConstructible witness.")
        };
        if (!realization.IsSuccess || realization.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(realization.Diagnostics);
        var body = realization.Value.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return HollowFailure("VerificationFailure");
        if (!FirmamentManifoldChecker.IsManifold(body)) return HollowFailure("VerificationFailure: vessel boundary is not a closed manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = solid.Name,
            ApplicationName = "Aetheris.Firmament.Primitive<Hollow>",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(reimport.Diagnostics);
        var reimportedManifold = FirmamentManifoldChecker.IsManifold(reimport.Value);
        if (!reimportedManifold) return HollowFailure("VerificationFailure: STEP reimport vessel boundary is not manifold");
        var surfaces = body.Geometry.Surfaces.Select(x => x.Value.Kind).ToArray();
        var r = realization.Value;
        var volume = r.Feature.PrimitiveKind == "RoundedBox"
            ? RoundedHollowVolume((FirmamentV2RoundedBoxRecord)solid.Primitive, r.Feature.WallThickness)
            : FrustumHollowVolume((FirmamentV2FrustumRecord)solid.Primitive, r.Feature.WallThickness);
        var report = new FirmamentHollowBodyReport(r.Feature.PrimitiveKind, "Hollow", r.Feature.WallThickness, r.Feature.Openings, r.Feature.Witness.Kind, r.Feature.Witness.Exact,
            r.Feature.ThicknessPolicy, r.Construction.ThicknessWitnesses.All(w => w.Exact && double.Abs(w.Distance - r.Feature.WallThickness) <= 1e-9), r.Plan.Kind, r.Plan.IsAuthoritative, r.Plan.DeterministicSignature,
            body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), surfaces.Count(x => x == SurfaceGeometryKind.Plane), surfaces.Count(x => x == SurfaceGeometryKind.Cylinder), surfaces.Count(x => x == SurfaceGeometryKind.Cone), r.Plan.RimFaces.Count,
            volume, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimportedManifold);
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, solid.Name, 0, "air-hollow", "thin-walled-body", Hollow: report));
    }

    private static string RoundedHollowVolume(FirmamentV2RoundedBoxRecord rounded, double t)
    {
        var outerArea = RoundedRectangleArea(rounded.Size[0], rounded.Size[1], rounded.CornerRadius);
        var innerArea = RoundedRectangleArea(rounded.Size[0] - 2d * t, rounded.Size[1] - 2d * t, rounded.CornerRadius - t);
        var analytic = outerArea * rounded.Size[2] - innerArea * (rounded.Size[2] - t);
        var numerical = RoundedRectangleAreaNumerical(rounded.Size[0], rounded.Size[1], rounded.CornerRadius) * rounded.Size[2]
            - RoundedRectangleAreaNumerical(rounded.Size[0] - 2d * t, rounded.Size[1] - 2d * t, rounded.CornerRadius - t) * (rounded.Size[2] - t);
        return FormattableString.Invariant($"analytic={analytic:R};numericalSimpson={numerical:R};delta={double.Abs(analytic - numerical):R}");
    }

    private static string FrustumHollowVolume(FirmamentV2FrustumRecord frustum, double t)
    {
        var k = (frustum.TopRadius - frustum.BottomRadius) / frustum.Height;
        var q = t * double.Sqrt(1d + k * k);
        var ib = frustum.BottomRadius + k * t - q; var it = frustum.TopRadius - q;
        var outer = double.Pi * frustum.Height * (frustum.BottomRadius * frustum.BottomRadius + frustum.BottomRadius * frustum.TopRadius + frustum.TopRadius * frustum.TopRadius) / 3d;
        var innerHeight = frustum.Height - t;
        var inner = double.Pi * innerHeight * (ib * ib + ib * it + it * it) / 3d;
        var analytic = outer - inner;
        var numerical = Simpson(0d, frustum.Height, z => double.Pi * double.Pow(frustum.BottomRadius + k * z, 2d))
            - Simpson(t, frustum.Height, z => double.Pi * double.Pow(frustum.BottomRadius + k * z - q, 2d));
        return FormattableString.Invariant($"analytic={analytic:R};numericalSimpson={numerical:R};delta={double.Abs(analytic - numerical):R}");
    }

    private static double RoundedRectangleArea(double width, double depth, double radius) => width * depth - (4d - double.Pi) * radius * radius;

    private static double RoundedRectangleAreaNumerical(double width, double depth, double radius)
    {
        // Parameterizing each quarter-circle by theta avoids the square-root endpoint singularity
        // of direct Cartesian sampling, while still providing an independent numerical integral.
        var straight = (width - 2d * radius) * depth;
        var cornerBands = 4d * radius * (depth / 2d - radius);
        var quarterCircleIntegral = Simpson(0d, double.Pi / 2d, theta => double.Cos(theta) * double.Cos(theta));
        return straight + cornerBands + 4d * radius * radius * quarterCircleIntegral;
    }

    private static double Simpson(double start, double end, Func<double, double> f)
    {
        const int segments = 16384; // even; deterministic numerical evidence, independent of closed-form volume.
        var step = (end - start) / segments; var sum = f(start) + f(end);
        for (var i = 1; i < segments; i++) sum += (i % 2 == 0 ? 2d : 4d) * f(start + i * step);
        return sum * step / 3d;
    }

    private static KernelResult<FirmamentStepExportResult> HollowFailure(string message) =>
        KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "FirmamentV2.Hollow")]);

    private static KernelResult<FirmamentStepExportResult>? TryExportV2RoundedBoxBody(FirmamentV2Document document)
    {
        if (document.Solids.Count != 1 || document.Solids[0].Primitive is not FirmamentV2RoundedBoxRecord rounded || rounded.Size.Count != 3)
            return null;
        var solid = document.Solids[0];
        var finishes = (document.ModifyBlocks ?? []).SelectMany(m => m.EdgeFinishes ?? []).ToArray();
        if (finishes.Length > 1) return RoundedBoxFailure("unsupported top/whole-body or mixed edge-finish request");
        double? fillet = null;
        if (finishes.Length == 1)
        {
            var finish = finishes[0];
            if (!string.Equals(finish.FaceAxis, "+Z", StringComparison.Ordinal) || !string.Equals(finish.Target, "Boundary", StringComparison.Ordinal) || !string.Equals(finish.Kind, "Fillet", StringComparison.Ordinal))
                return RoundedBoxFailure("UnsupportedSupportPair");
            fillet = finish.Distance;
        }
        var realization = RoundedBoxBRepPlanner.Create(rounded.Size[0], rounded.Size[1], rounded.Size[2], rounded.CornerRadius, fillet);
        if (!realization.IsSuccess || realization.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(realization.Diagnostics);
        var body = realization.Value.Body; var plan = realization.Value.Plan;
        if (!FirmamentManifoldChecker.IsManifold(body)) return RoundedBoxFailure("VerificationFailure: rounded-box body is not manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = solid.Name,
            ApplicationName = "AIR-ROUNDED-BOX-M6",
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var imported = Step242Importer.ImportBody(step.Value);
        if (!imported.IsSuccess || imported.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(imported.Diagnostics);
        if (!FirmamentManifoldChecker.IsManifold(imported.Value)) return RoundedBoxFailure("VerificationFailure: STEP reimport is not manifold");
        var surfaces = body.Geometry.Surfaces.Select(x => x.Value.Kind).ToArray();
        var preflight = BrepExportPreflight.Validate(body);
        var report = new FirmamentRoundedBoxReport(
            new("RoundedBoxFeature", "RoundedRectangleProfile -> LinearSweep", rounded.Size[0], rounded.Size[1], rounded.Size[2], rounded.CornerRadius,
                4, surfaces.Count(x => x == SurfaceGeometryKind.Cylinder) - (fillet is null ? 0 : 4), false),
            fillet is null ? null : new("TopBoundary", "Fillet", fillet.Value, 4, 4, rounded.CornerRadius - fillet.Value, fillet.Value,
                $"torus axis=+Z; center is each corner cylinder axis at z=top-{fillet.Value:R}; major=Rc-Rf={rounded.CornerRadius - fillet.Value:R}; minor=Rf={fillet.Value:R}; v=pi/2 is the top-plane trim and v=0 is the retained corner-cylinder trim."),
            new(plan.IsAuthoritative, plan.DeterministicSignature, plan.ExpectedVertexCount, plan.ExpectedEdgeCount, plan.ExpectedFaceCount, plan.ExpectedLoopCount, plan.ExpectedCoedgeCount, plan.FaceRoles),
            new(Bounds(body), true, surfaces.Count(x => x == SurfaceGeometryKind.Plane), surfaces.Count(x => x == SurfaceGeometryKind.Cylinder), surfaces.Count(x => x == SurfaceGeometryKind.Torus), RoundedBoxAnalyticVolume(rounded.Size[0], rounded.Size[1], rounded.Size[2], rounded.CornerRadius, fillet), preflight.IsValid ? "valid" : "invalid"),
            new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, true, imported.Value.Topology.Faces.Count(), Bounds(imported.Value)));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, solid.Name, 0, "air-rounded-box", fillet is null ? "rounded-box-primitive" : "rounded-box-top-boundary-fillet", RoundedBox: report));
    }

    private static KernelResult<FirmamentStepExportResult> RoundedBoxFailure(string message) =>
        KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, message, "FirmamentV2.RoundedBox")]);

    // Exact volume of the admitted family.  Each straight finish removes a
    // square-minus-quarter-circle section; each corner is that section swept
    // through a quadrant about the corner-cylinder axis.
    private static double RoundedBoxAnalyticVolume(double width, double depth, double height, double cornerRadius, double? topFilletRadius)
    {
        var primitive = (width * depth - ((4d - double.Pi) * cornerRadius * cornerRadius)) * height;
        if (topFilletRadius is not double r) return primitive;
        var section = r * r * (1d - double.Pi / 4d);
        var straightRemoval = section * (2d * ((width - 2d * cornerRadius) + (depth - 2d * cornerRadius)));
        var cornerFirstMoment = r * r * r * (5d / 6d - double.Pi / 4d);
        var cornerRemoval = 2d * double.Pi * ((cornerRadius * section) - cornerFirstMoment);
        return primitive - straightRemoval - cornerRemoval;
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2LocalizedTrihedralFillet(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        IReadOnlyList<FirmamentV2EdgeFinishDecl> finishes,
        FirmamentV2BoxRecord box)
    {
        // The compiler owns canonical classification; source order carries no topology meaning.
        var xz = finishes.SingleOrDefault(f => string.Equals(f.FaceAxis, "+X", StringComparison.Ordinal) && string.Equals(f.Target, "SharedEdgePlusZ", StringComparison.Ordinal));
        var yz = finishes.SingleOrDefault(f => string.Equals(f.FaceAxis, "+Y", StringComparison.Ordinal) && string.Equals(f.Target, "SharedEdgePlusZ", StringComparison.Ordinal));
        var xy = finishes.SingleOrDefault(f => string.Equals(f.FaceAxis, "+X", StringComparison.Ordinal) && string.Equals(f.Target, "SharedEdgePlusY", StringComparison.Ordinal));
        if (xz is null || yz is null || xy is null)
            return AirChamferFailure("localized-trihedral-fillet-edges-do-not-share-canonical-vertex");
        var compiled = AirLocalizedTrihedralFilletCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{xz.Name}.{yz.Name}.{xy.Name}", $"{xz.Name}+{yz.Name}+{xy.Name}", box.Size[0], box.Size[1], box.Size[2],
            xz.FaceAxis, xz.Target, yz.FaceAxis, yz.Target, xy.FaceAxis, xy.Target, xz.Distance, yz.Distance, xy.Distance,
            new AirSourceSpan(xz.SourceSpan.Start, xz.SourceSpan.Length + yz.SourceSpan.Length + xy.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.LocalizedEdgeJunctionRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? "localized-trihedral-fillet-spherical-octant-required", compiled.Diagnostics);

        var body = compiled.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return AirChamferFailure("localized-trihedral-fillet-preflight-verification-failed", preflight.Diagnostics.Select(d => d.Code));
        if (!FirmamentManifoldChecker.IsManifold(body)) return AirChamferFailure("localized-trihedral-fillet-emitted-body-is-not-manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = compiled.Construction.ConstructionId,
            ApplicationName = AirLocalizedTrihedralFilletCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
            return AirChamferFailure("localized-trihedral-fillet-step-reimport-verification-failed", reimport.Diagnostics.Select(d => d.Message));

        var topology = compiled.Construction.TopologyPlan;
        var cylinders = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        var spheres = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Sphere);
        var planes = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane);
        var report = new FirmamentAirChamferReport(
            new("Fillet", solid.Name, $"{solid.Name}.{xz.Name}.{yz.Name}.{xy.Name}", $"{xz.Name}+{yz.Name}+{xy.Name}", "SharedEdge(+X,+Z),SharedEdge(+Y,+Z),SharedEdge(+X,+Y)", xz.Distance, "mm",
                $"{xz.SourceSpan.Start}:{xz.SourceSpan.Length}|{yz.SourceSpan.Start}:{yz.SourceSpan.Length}|{xy.SourceSpan.Start}:{xy.SourceSpan.Length}", "Admitted", "localized-trihedral-fillet-spherical-octant-candidate",
                new Dictionary<string, string> { ["Selection"] = "semantic Face(+X),Face(+Z); Face(+Y),Face(+Z); Face(+X),Face(+Y)", ["MaterialSide"] = compiled.Construction.MaterialSide, ["SphereCenter"] = $"({compiled.Construction.SphericalCornerPatch.Center.X:R},{compiled.Construction.SphericalCornerPatch.Center.Y:R},{compiled.Construction.SphericalCornerPatch.Center.Z:R})", ["Continuity"] = "G0=Exact;G1=normal-deviation:0" }),
            new("LocalizedTrihedralFillet", 0, [], "ordered-explicit-loops", "SphereCylinderSharedSeams",
                "replacementFaces=3;junctionFaces=1;sharedEdges=3;patch=SphericalOctant;G0=Exact;G1=WithinTolerance", true),
            new(true, topology.ExpectedVertexCount, topology.ExpectedEdgeCount, topology.ExpectedFaceCount, topology.ExpectedLoopCount, topology.ExpectedCoedgeCount, 0,
                "SphereCylinderSharedSeams", topology.DeterministicSignature, "LocalizedEdgeJunction"),
            new(AirLocalizedTrihedralFilletCompileResult.ProductionRoute, false, true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), Bounds(body), xz.Distance, xz.Distance, cylinders, 0, planes, spheres),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), Bounds(reimport.Value), true),
            LocalizedEdgeJunction: new(["SharedEdge(+X,+Z)", "SharedEdge(+Y,+Z)", "SharedEdge(+X,+Y)"], "Fillet", "EqualConstantRadius", xz.Distance, "Direct", "LocalizedTrihedralFillet", "SphericalOctant", 3, 1,
                new(true, topology.DeterministicSignature), "valid", false, 1, 1,
                new("SphericalOctant", "Sphere", "Cylinder", "Circle", true, 3, "+X,+Y,+Z octant")));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, $"{solid.Name}.{xz.Name}.{yz.Name}.{xy.Name}", 0, "air-fillet", "localized-trihedral-fillet", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2LocalizedTangentBlendSingleEdgeFillet(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        FirmamentV2EdgeFinishDecl finish,
        FirmamentV2BoxRecord box)
    {
        var compiled = AirLocalizedTangentBlendFilletCompiler.Compile(new(
            solid.Name, $"{solid.Name}.{finish.Name}", finish.Name, box.Size[0], box.Size[1], box.Size[2],
            finish.FaceAxis, finish.Target switch { "SharedEdgePlusZ" => "+Z", "SharedEdgePlusY" => "+Y", _ => finish.Target }, finish.Kind, finish.Distance,
            new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.LocalizedEdgeReplacementRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? compiled.Feature.AdmissionReason, compiled.Diagnostics);

        var body = compiled.Body;
        var preflight = BrepExportPreflight.Validate(body);
        if (!preflight.IsValid) return AirChamferFailure("localized-fillet-preflight-verification-failed", preflight.Diagnostics.Select(d => d.Code));
        if (!FirmamentManifoldChecker.IsManifold(body)) return AirChamferFailure("localized-fillet-emitted-body-is-not-manifold");
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions
        {
            ProductName = compiled.Feature.FeatureName,
            ApplicationName = AirLocalizedTangentBlendFilletCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null || !FirmamentManifoldChecker.IsManifold(reimport.Value))
            return AirChamferFailure("localized-fillet-step-reimport-verification-failed");

        var plan = compiled.Construction.TopologyPlan;
        var witness = compiled.Construction.Witness;
        var cylinders = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder);
        var planes = body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Plane);
        var report = new FirmamentAirChamferReport(
            new("Fillet", compiled.Feature.BodyId, compiled.Feature.FeatureId, compiled.Feature.FeatureName, "SharedEdge(+X,+Z)", finish.Distance, "mm",
                $"{compiled.Feature.SourceSpan.Start}:{compiled.Feature.SourceSpan.Length}", compiled.Feature.Admission.ToString(), compiled.Feature.AdmissionReason,
                new Dictionary<string, string> { ["Selection"] = "semantic Face(+X),Face(+Z)", ["MaterialSide"] = witness.MaterialSide, ["History"] = witness.Provenance }),
            new("LocalizedTangentBlend", 0, [], "ordered-explicit-loops", "ExplicitOwnedEndpoints",
                $"profile=QuarterCircle;sweep=Linear;radius={witness.Radius:R};axis=+Y;retainedFaces=2;replacementFaces=1", true),
            new(true, plan.ExpectedVertexCount, plan.ExpectedEdgeCount, plan.ExpectedFaceCount, plan.ExpectedLoopCount, plan.ExpectedCoedgeCount, 0,
                "ExplicitOwnedEndpoints", plan.DeterministicSignature, "LocalizedTangentBlend"),
            new(AirLocalizedTangentBlendFilletCompileResult.ProductionRoute, false, true, body.Topology.Vertices.Count(), body.Topology.Edges.Count(), body.Topology.Faces.Count(), Bounds(body), finish.Distance, finish.Distance, cylinders, 0, planes),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true, reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), Bounds(reimport.Value), true),
            LocalizedFillet: new("SharedEdge(+X,+Z)", "ConstantRadius", finish.Distance, "LocalizedTangentBlend", "QuarterCircle", "Linear", "Direct", 2, 1, "ExplicitOwnedEndpoints", new(true, plan.DeterministicSignature), "valid", false),
            LocalizedEdgeFinish: new("Fillet", "SharedEdge(+X,+Z)", "ConstantRadius", finish.Distance, "LocalizedEdgeReplacement", "CylindricalFillet", "Direct", 2, 1, "ExplicitOwnedEndpoints", new(true, plan.DeterministicSignature), "valid", false));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, compiled.Feature.FeatureId, 0, "air-fillet", "localized-tangent-blend-single-edge-fillet", Air: report, ConceptIr: document.ConceptIr));
    }

    private static KernelResult<FirmamentStepExportResult> ExportV2CircularRimChamfer(
        FirmamentV2Document document,
        FirmamentV2SolidBinding solid,
        FirmamentV2EdgeFinishDecl finish,
        FirmamentV2CylinderRecord cylinder)
    {
        var compiled = AirCylinderTopRimChamferCompiler.Compile(new(
            solid.Name,
            $"{solid.Name}.{finish.Name}",
            finish.Name,
            cylinder.Radius,
            cylinder.Height,
            finish.FaceAxis,
            finish.Target,
            finish.Kind,
            finish.Distance,
            new AirSourceSpan(finish.SourceSpan.Start, finish.SourceSpan.Length, document.ModelName)));
        if (!compiled.Succeeded || compiled.Body is null || compiled.Construction is null || compiled.BRepPlan?.RevolvedRealizationPlan is null)
            return AirChamferFailure(compiled.Error?.Code ?? compiled.Feature.AdmissionReason, compiled.Diagnostics);

        var manifold = FirmamentManifoldChecker.IsManifold(compiled.Body);
        if (!manifold) return AirChamferFailure("chamfer-backend-circular-rim-body-is-not-manifold", compiled.Diagnostics);
        var step = Step242Exporter.ExportBody(compiled.Body, new Step242ExportOptions
        {
            ProductName = compiled.Feature.FeatureName,
            ApplicationName = AirCylinderTopRimChamferCompileResult.ProductionRoute,
            BrepExportPreflightMode = BrepExportPreflightMode.Enforce,
            BrepExportPreflightPolicy = BrepExportPreflightPolicy.TrustedProductionRoute,
        });
        if (!step.IsSuccess || step.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null) return KernelResult<FirmamentStepExportResult>.Failure(reimport.Diagnostics);
        var reimportedManifold = FirmamentManifoldChecker.IsManifold(reimport.Value);
        if (!reimportedManifold) return AirChamferFailure("chamfer-verification-circular-rim-step-reimport-is-not-manifold");

        var topology = compiled.Construction.TopologyPlan;
        var surfaces = compiled.Body.Geometry.Surfaces.Select(s => s.Value.Kind).ToArray();
        var sharp = compiled.Construction.Witness.SharpProfile.Select(p => (IReadOnlyList<double>)new[] { p.X, p.Y }).ToArray();
        var replacement = compiled.Construction.Witness.ReplacementProfile.Select(p => (IReadOnlyList<double>)new[] { p.X, p.Y }).ToArray();
        var featureProvenance = new Dictionary<string, string>(StringComparer.Ordinal);
        if (solid.Provenance?.TryGetValue("Bounds", out var boundsProvenance) == true) featureProvenance["Bounds"] = boundsProvenance;
        if (finish.Provenance?.TryGetValue("Face", out var faceProvenance) == true) featureProvenance["Selection"] = faceProvenance;
        if (finish.Provenance?.TryGetValue("Distance", out var distanceProvenance) == true) featureProvenance["Distance"] = distanceProvenance;
        var report = new FirmamentAirChamferReport(
            new("Chamfer", compiled.Feature.BodyId, compiled.Feature.FeatureId, compiled.Feature.FeatureName, "FaceBoundary(+Z,circular,outer,complete)", compiled.Feature.Rule.Distance, compiled.Feature.Rule.Unit,
                $"{compiled.Feature.SourceSpan.Start}:{compiled.Feature.SourceSpan.Length}", compiled.Feature.Admission.ToString(), compiled.Feature.AdmissionReason, featureProvenance),
            new("RevolutionProfileRewrite", 0, [], "ordered-radial-profile", "preserve-profile-corners",
                $"axis={compiled.Construction.Witness.Axis};materialSide={compiled.Construction.Witness.MaterialSide}", compiled.Construction.Witness.CompilerGenerated, sharp, replacement),
            new(true, topology.ExpectedVertexCount, topology.ExpectedEdgeCount, topology.ExpectedFaceCount, topology.ExpectedLoopCount, topology.ExpectedCoedgeCount, 1,
                "preserve-profile-corners", topology.DeterministicSignature, "RevolvedProfile"),
            new(AirCylinderTopRimChamferCompileResult.ProductionRoute, false, manifold,
                compiled.Body.Topology.Vertices.Count(), compiled.Body.Topology.Edges.Count(), compiled.Body.Topology.Faces.Count(), CircularBounds(cylinder.Radius, cylinder.Height), finish.Distance, finish.Distance,
                surfaces.Count(k => k == SurfaceGeometryKind.Cylinder), surfaces.Count(k => k == SurfaceGeometryKind.Cone), surfaces.Count(k => k == SurfaceGeometryKind.Plane)),
            new("AP242", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value))), true,
                reimport.Value.Topology.Vertices.Count(), reimport.Value.Topology.Edges.Count(), reimport.Value.Topology.Faces.Count(), CircularBounds(cylinder.Radius, cylinder.Height), reimportedManifold));
        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(
            step.Value, compiled.Feature.FeatureId, 0, "air-chamfer", "circular-top-rim-chamfer", Air: report, ConceptIr: document.ConceptIr));
    }

    private static string CircularBounds(double radius, double height) =>
        FormattableString.Invariant($"[{-radius:0.###},{-radius:0.###},{-height / 2d:0.###}]..[{radius:0.###},{radius:0.###},{height / 2d:0.###}]");

    private static string Bounds(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var point) ? point : throw new InvalidOperationException($"Missing point for vertex {v.Id}.")).ToArray();
        return FormattableString.Invariant($"[{points.Min(p => p.X):0.###},{points.Min(p => p.Y):0.###},{points.Min(p => p.Z):0.###}]..[{points.Max(p => p.X):0.###},{points.Max(p => p.Y):0.###},{points.Max(p => p.Z):0.###}]");
    }

    private static KernelResult<FirmamentStepExportResult> AirChamferFailure(string primary, IEnumerable<string>? details = null) =>
        KernelResult<FirmamentStepExportResult>.Failure((new[] { primary }).Concat(details ?? []).Distinct().Select(message => new Kernel.Core.Diagnostics.KernelDiagnostic(
            Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            message,
            "FirmamentV2.AirChamfer")).ToArray());

    private static KernelResult<FirmamentStepExportResult>? TryExportV2InlineStepReplacementBody(FirmamentV2Document document)
    {
        if (document.Replacements is not { Count: 1 } replacements || document.Solids.Count != 1)
        {
            return null;
        }

        var replacement = replacements[0];
        var solid = document.Solids.SingleOrDefault(s => string.Equals(s.Name, replacement.ImportedBodyName, StringComparison.Ordinal));
        if (solid?.InlineStep is null || replacement.ReplacementKind != "holeShaft" || replacement.EndCondition != "throughAll")
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, FirmamentV2Parser.ReplacementVerificationFailed, "FirmamentV2.InlineStepReplacement")]);
        }

        var stepText = File.ReadAllText(solid.InlineStep.NormalizedPath, Encoding.UTF8);
        var import = Step242Importer.ImportBody(stepText);
        if (!import.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(import.Diagnostics);

        var cylFaceCount = import.Value.Geometry.Surfaces.Count(entry => entry.Value.Kind == SurfaceGeometryKind.Cylinder && Math.Abs((entry.Value.Cylinder?.Radius ?? 0d) - replacement.Radius) <= 1e-6);
        if (cylFaceCount != 1)
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, FirmamentV2Parser.ReplacementVerificationFailed, "FirmamentV2.InlineStepReplacement")]);
        }

        var size = replacement.HostSize;
        var syntheticDocument = new FirmamentV2Document(
            document.ModelName,
            document.Units,
            [new FirmamentV2SolidBinding("replacementHost", "Box", new FirmamentV2BoxRecord(size, []))],
            [new FirmamentV2ModifyBlock("replacementHost", [], [new FirmamentV2SemanticHoleDecl(replacement.ReplacementFeatureName, FirmamentV2SemanticHoleVariant.Shaft, FirmamentV2FaceTarget.Direct("+Z"), replacement.Center with { Convention = FirmamentV2FaceLocalPoint2D.PlusZConvention }, replacement.Radius * 2d, new FirmamentV2SemanticHoleEnd(FirmamentV2SemanticHoleEndKind.ThroughAll))])],
            [],
            document.Pmi,
            document.RecognizedRegions,
            document.Replacements);

        var semanticHoles = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(syntheticDocument);
        var feature = semanticHoles[0];
        var host = new AirHoleSimpleShaftHost(size[0], size[1], -size[2] / 2d, size[2] / 2d);
        var materialized = AirHoleSimpleShaftMaterializer.Execute(feature, host);
        if (!materialized.Succeeded || materialized.Body is null) return SemanticHoleFailure(materialized.Diagnostics);

        var expectedVolume = size[0] * size[1] * size[2] - Math.PI * replacement.Radius * replacement.Radius * size[2];
        var rebuiltVolume = EstimateBoundingBoxVolume(materialized.Body) - Math.PI * replacement.Radius * replacement.Radius * size[2];
        if (Math.Abs(expectedVolume - rebuiltVolume) > 1e-6)
        {
            return KernelResult<FirmamentStepExportResult>.Failure([new Kernel.Core.Diagnostics.KernelDiagnostic(Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed, Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error, FirmamentV2Parser.ReplacementVerificationFailed, "FirmamentV2.InlineStepReplacement")]);
        }

        var step = Step242Exporter.ExportBody(materialized.Body, new Step242ExportOptions { ProductName = replacement.ReplacementFeatureName, ApplicationName = "Aetheris.Firmament.InlineStepReplacement" });
        if (!step.IsSuccess) return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);

        return KernelResult<FirmamentStepExportResult>.Success(new FirmamentStepExportResult(step.Value, replacement.ReplacementFeatureName, 0, "inline-step-replacement", "holeShaft-bounded-rebuild", DatumInspection: [], DimensionInspection: [], InlineStepMigration: InlineStepMigrationReportBuilder.Build(document, solid, replacementsVerified: true, replacementsEmitted: true, emissionStrategy: "holeShaft-bounded-rebuild", residualSurgery: false),
                InlineStepReplacementAssist: InlineStepReplacementAssistReportBuilder.Build(document)));
    }

    private static double EstimateBoundingBoxVolume(BrepBody body)
    {
        var points = body.Topology.Vertices.Select(v => body.TryGetVertexPoint(v.Id, out var p) ? p : new Point3D(0, 0, 0)).ToArray();
        return (points.Max(p => p.X) - points.Min(p => p.X)) * (points.Max(p => p.Y) - points.Min(p => p.Y)) * (points.Max(p => p.Z) - points.Min(p => p.Z));
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2InlineStepBody(FirmamentV2Document document)
    {
        if (document.Solids.Count != 1)
        {
            return null;
        }

        var solid = document.Solids[0];
        if (solid.Primitive is not FirmamentV2InlineStepRecord inlineStep)
        {
            return null;
        }

        var stepText = File.ReadAllText(inlineStep.NormalizedPath, Encoding.UTF8);
        var import = Step242Importer.ImportBody(stepText);
        if (!import.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(import.Diagnostics);
        }

        var semanticPmiValidation = ValidateV2PmiExportSupport(document);
        if (!semanticPmiValidation.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(semanticPmiValidation.Diagnostics);
        }

        var semanticPmi = BuildV2SemanticPmi(document, [], solid.Name);
        var export = Step242Exporter.ExportBody(import.Value, semanticPmi, new Step242ExportOptions
        {
            ProductName = solid.Name,
            ApplicationName = "Aetheris.Firmament.InlineStep"
        });
        if (!export.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(export.Diagnostics);
        }

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                export.Value,
                solid.Name,
                0,
                "inline-step",
                "aetheris-canonical-ap242",
                DatumInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.DatumPlane).Select(p => new FirmamentPmiInspectionDatum(p.Name, "planar", p.Target)).ToArray() ?? [],
                DimensionInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.HoleDiameter).Select(p => new FirmamentPmiInspectionDimension("Diameter", p.Target, null, p.Value ?? 0d, "explicit-v2-record-pmi", p.Name)).ToArray() ?? [],
                InlineStepMigration: InlineStepMigrationReportBuilder.Build(document, solid, emissionStrategy: "canonical-reexport", residualSurgery: false),
                InlineStepReplacementAssist: InlineStepReplacementAssistReportBuilder.Build(document)));
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2ControlledSideHoleBody(FirmamentV2Document document)
    {
        if ((document.ModifyBlocks ?? []).Any(m => (m.EdgeFinishes?.Count ?? 0) > 0)) return null;
        var intent = document.SideHoleIntent;
        if (intent is null)
        {
            return null;
        }

        if (document.ModifyBlocks is not { Count: 1 }
            || document.ModifyBlocks[0].Regions.Count != 1
            || document.ModifyBlocks[0].SemanticHoles.Count != 0)
        {
            return null;
        }

        if (!string.Equals(intent.Tool, "Cylinder", StringComparison.Ordinal)
            || !string.Equals(intent.AttachFace, "+X", StringComparison.Ordinal)
            || !string.Equals(intent.ThroughFace, "-X", StringComparison.Ordinal))
        {
            return null;
        }

        var targetSolid = document.Solids.SingleOrDefault(s => string.Equals(s.Name, intent.TargetSolid, StringComparison.Ordinal));
        if (targetSolid?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
        {
            return null;
        }

        var sizeX = box.Size[0];
        var sizeY = box.Size[1];
        var sizeZ = box.Size[2];
        var zAxis = Direction3D.Create(new Vector3D(0, 0, 1));
        var xAxis = Direction3D.Create(new Vector3D(1, 0, 0));
        var extents = new AxisAlignedBoxExtents(-sizeY / 2d, sizeY / 2d, -sizeZ / 2d, sizeZ / 2d, -sizeX / 2d, sizeX / 2d);
        var cylinder = new RecognizedCylinder(new Point3D(intent.CenterU, intent.CenterV, 0), zAxis, intent.Radius, -sizeX / 2d, sizeX / 2d);
        var hole = new SupportedBooleanHole(
            intent.RegionName,
            new AnalyticSurface(AnalyticSurfaceKind.Cylinder, Cylinder: cylinder),
            intent.CenterU,
            intent.CenterV,
            new Point3D(intent.CenterU, intent.CenterV, -sizeX / 2d),
            new Point3D(intent.CenterU, intent.CenterV, sizeX / 2d),
            zAxis,
            xAxis,
            intent.Radius,
            intent.Radius,
            SupportedBooleanHoleSpanKind.Through,
            -sizeX / 2d,
            sizeX / 2d);
        var composition = new SafeBooleanComposition(extents, [hole], SafeBooleanRootDescriptor.FromBox(extents));
        var built = BrepBooleanBoxCylinderHoleBuilder.BuildComposition(composition, ToleranceContext.Default);
        if (!built.IsSuccess || built.Value is null)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(built.Diagnostics);
        }

        var body = ReorientCanonicalSideHoleBodyFromZToX(built.Value);
        var step = Step242Exporter.ExportBody(body, new Step242ExportOptions { ProductName = "firmament-v2-controlled-side-hole" });
        if (!step.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        }

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                step.Value,
                intent.RegionName,
                0,
                "boolean",
                "side-hole-controlled-x",
                DatumInspection: [],
                DimensionInspection: []));
    }

    private static BrepBody ReorientCanonicalSideHoleBodyFromZToX(BrepBody body)
    {
        static Point3D P(Point3D p) => new(p.Z, p.X, p.Y);
        static Direction3D D(Direction3D d)
        {
            var v = d.ToVector();
            return Direction3D.Create(new Vector3D(v.Z, v.X, v.Y));
        }

        var geometry = new BrepGeometryStore();
        foreach (var entry in body.Geometry.Curves)
        {
            geometry.AddCurve(entry.Key, entry.Value.Kind switch
            {
                CurveGeometryKind.Line3 => CurveGeometry.FromLine(new Line3Curve(P(entry.Value.Line3!.Value.Origin), D(entry.Value.Line3.Value.Direction))),
                CurveGeometryKind.Circle3 => CurveGeometry.FromCircle(new Circle3Curve(P(entry.Value.Circle3!.Value.Center), D(entry.Value.Circle3.Value.Normal), entry.Value.Circle3.Value.Radius, D(entry.Value.Circle3.Value.XAxis))),
                CurveGeometryKind.Ellipse3 => CurveGeometry.FromEllipse(new Ellipse3Curve(P(entry.Value.Ellipse3!.Value.Center), D(entry.Value.Ellipse3.Value.Normal), entry.Value.Ellipse3.Value.MajorRadius, entry.Value.Ellipse3.Value.MinorRadius, D(entry.Value.Ellipse3.Value.XAxis))),
                _ => entry.Value
            });
        }

        foreach (var entry in body.Geometry.Surfaces)
        {
            geometry.AddSurface(entry.Key, entry.Value.Kind switch
            {
                SurfaceGeometryKind.Plane => SurfaceGeometry.FromPlane(new PlaneSurface(P(entry.Value.Plane!.Value.Origin), D(entry.Value.Plane.Value.Normal), D(entry.Value.Plane.Value.UAxis))),
                SurfaceGeometryKind.Cylinder => SurfaceGeometry.FromCylinder(new CylinderSurface(P(entry.Value.Cylinder!.Value.Origin), D(entry.Value.Cylinder.Value.Axis), entry.Value.Cylinder.Value.Radius, D(entry.Value.Cylinder.Value.XAxis))),
                _ => entry.Value
            });
        }

        var vertexPoints = new Dictionary<VertexId, Point3D>();
        foreach (var vertex in body.Topology.Vertices)
        {
            if (body.TryGetVertexPoint(vertex.Id, out var point))
            {
                vertexPoints[vertex.Id] = P(point);
            }
        }

        return new BrepBody(body.Topology, geometry, body.Bindings, vertexPoints, safeBooleanComposition: null, body.ShellRepresentation);
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2SemanticHoleBody(FirmamentV2Document document)
    {
        if ((document.ModifyBlocks ?? []).Any(m => (m.EdgeFinishes?.Count ?? 0) > 0)) return null;
        if (document.ModifyBlocks is not { Count: > 0 })
        {
            return null;
        }

        var modifyTargets = document.ModifyBlocks.Select(m => m.TargetSolid).Distinct(StringComparer.Ordinal).ToArray();
        if (modifyTargets.Length != 1)
        {
            return null;
        }

        var targetSolid = document.Solids.SingleOrDefault(s => string.Equals(s.Name, modifyTargets[0], StringComparison.Ordinal));
        if (targetSolid?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
        {
            return null;
        }

        var semanticHoles = FirmamentV2SemanticHoleLowering.LowerSemanticHoles(document);
        if (semanticHoles.Count == 0)
        {
            return null;
        }

        var feature = semanticHoles[0];
        // Concept Box3 uses an XY-centered frame with Z in [0, height]. Preserve that frame for
        // Concept-driven holes so the materialized face and resolved Point3 share coordinates.
        var host = document.ConceptIr is null
            ? new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d)
            : new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], 0d, box.Size[2]);
        Aetheris.Kernel.Core.Brep.BrepBody? body;
        IReadOnlyList<string> diagnostics;
        if (semanticHoles.Count == 1)
        {
            var materialized = AirHoleSimpleShaftMaterializer.Execute(feature, host);
            body = materialized.Body;
            diagnostics = materialized.Diagnostics;
            if (!materialized.Succeeded || body is null)
            {
                return SemanticHoleFailure(diagnostics);
            }
        }
        else
        {
            var materialized = AirHoleCompositeMaterializer.Execute(semanticHoles, host);
            body = materialized.Body;
            diagnostics = materialized.Diagnostics;
            if (!materialized.Succeeded || body is null)
            {
                return SemanticHoleFailure(diagnostics);
            }
        }

        var semanticPmi = BuildV2SemanticPmi(document, semanticHoles, modifyTargets[0]);
        // Semantic-hole/profile-stack and bounded-Boolean routes remain Audit until their
        // historical coincident/seam topology is remediated producer-by-producer.
        var step = Step242Exporter.ExportBody(body, semanticPmi);
        if (!step.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        }

        var reimport = Step242Importer.ImportBody(step.Value);
        if (!reimport.IsSuccess || reimport.Value is null)
            return KernelResult<FirmamentStepExportResult>.Failure(reimport.Diagnostics);
        var stepHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(step.Value)));
        var surfaceKinds = body.Geometry.Surfaces.Select(s => s.Value.Kind).ToArray();

        var featureReports = semanticHoles.Select(h =>
        {
            var legacyPlacement = h.Placement as AirFaceLocalHolePlacement;
            var constructionPlacement = h.ConstructionPlanePlacement;
            return new FirmamentHoleFeatureReport(
            h.Name,
            "Hole",
            h.FeatureId,
            h.Shaft.Diameter,
            h.Placement.U,
            h.Placement.V,
            legacyPlacement?.ResolvedPoint3 is { } p ? new[] { p.X, p.Y, p.Z } : constructionPlacement is { } cp ? new[] { cp.WorldMouthCenter.X, cp.WorldMouthCenter.Y, cp.WorldMouthCenter.Z } : null,
            legacyPlacement?.ResolvedPoint3 is { } sourcePoint ? sourcePoint.Ordinal is { } ordinal ? $"{sourcePoint.SourceMember}[{ordinal}]" : sourcePoint.SourceMember : constructionPlacement?.SourceConceptPlaneId,
            legacyPlacement?.ResolvedPoint3?.StableId ?? constructionPlacement?.SourceConceptPlaneId,
            legacyPlacement?.ResolvedPoint3?.Ordinal,
            legacyPlacement?.ResolvedPoint3?.PlacementFace ?? constructionPlacement?.ConstructionPlaneId ?? legacyPlacement?.EntryFaceName ?? "unplaced",
            legacyPlacement?.ResolvedPoint3?.SourceSpan ?? constructionPlacement?.SourceSpan,
            semanticHoles.Count == 1 ? nameof(AirHoleSimpleShaftMaterializer) : nameof(AirHoleCompositeMaterializer),
            "HoleProfileStack",
            h.Stack.Kind.ToString(),
            string.Join(" -> ", h.Stack.Components.Select(component => component switch
            {
                AirHoleCountersinkComponent countersink => $"conical-entry(entryRadius={countersink.EntryRadius:R},angle={countersink.AngleDegrees:R})",
                AirHoleCounterboreComponent counterbore => $"counterbore(radius={counterbore.Radius:R},depth={counterbore.Depth:R})",
                AirHoleShaftComponent shaft => $"cylindrical-shaft(radius={shaft.Radius:R},{shaft.EndCondition.Kind})",
                _ => component.Kind.ToString(),
            })),
            surfaceKinds.Count(k => k == SurfaceGeometryKind.Cylinder),
            surfaceKinds.Count(k => k == SurfaceGeometryKind.Cone),
            surfaceKinds.Count(k => k == SurfaceGeometryKind.Plane),
            stepHash,
            true);
        }).ToArray();
        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                step.Value,
                feature.FeatureId,
                0,
                semanticHoles.Count == 1 ? nameof(AirHoleSimpleShaftMaterializer) : nameof(AirHoleCompositeMaterializer),
                semanticHoles.Count == 1 ? feature.Stack.Kind.ToString() : "CompositeSimpleShaft",
                DatumInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.DatumPlane).Select(p => new FirmamentPmiInspectionDatum(p.Name, "planar", p.Target)).ToArray() ?? [],
                DimensionInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.HoleDiameter).Select(p => new FirmamentPmiInspectionDimension("Diameter", p.Target, null, p.Value ?? 0d, "explicit-v2-semantic-pmi", null)).ToArray() ?? [],
                ConceptIr: document.ConceptIr,
                Features: featureReports));
    }

    private static KernelResult<bool> ValidateV2PmiExportSupport(FirmamentV2Document document)
    {
        var unsupported = document.PmiBlock?.Records.Where(r => r.Kind is not (FirmamentV2PmiKind.DatumPlane or FirmamentV2PmiKind.HoleDiameter)).ToArray() ?? [];
        if (unsupported.Length == 0)
        {
            return KernelResult<bool>.Success(true);
        }

        return KernelResult<bool>.Failure(unsupported.Select(r => new Kernel.Core.Diagnostics.KernelDiagnostic(
            Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            $"firmament-v2-pmi-export-deferred: AP242 export for PMI record '{r.Name}' ({r.Kind.ToString().ToLowerInvariant()}) is deferred in V2 Phase 1 P2; supported export records are datum and diameter.",
            "FirmamentV2.PmiExport")).ToArray());
    }

    private static IReadOnlyList<Step242SemanticPmi> BuildV2SemanticPmi(FirmamentV2Document document, IReadOnlyList<Core.Air.AirHoleFeature> semanticHoles, string targetSolid)
    {
        if (document.Pmi is null || document.Pmi.Count == 0)
        {
            return [];
        }

        var holeByName = semanticHoles.SelectMany(h => new[] { new KeyValuePair<string, Core.Air.AirHoleFeature>(h.FeatureId, h), new KeyValuePair<string, Core.Air.AirHoleFeature>(h.Name, h) }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        var boundPmiByName = (document.BoundPmi?.Datums ?? []).Concat(document.BoundPmi?.Dimensions ?? []).Concat(document.BoundPmi?.Controls ?? []).ToDictionary(p => p.Name, StringComparer.Ordinal);
        var targetBinding = document.Solids.Single(s => string.Equals(s.Name, targetSolid, StringComparison.Ordinal));
        var result = new List<Step242SemanticPmi>();
        foreach (var pmi in document.Pmi)
        {
            if (pmi.Kind == FirmamentV2PmiKind.HoleDiameter && pmi.Value.HasValue)
            {
                boundPmiByName.TryGetValue(pmi.Name, out var boundPmi);
                var tolerancePlus = boundPmi?.DimensionTolerance?.Plus;
                var toleranceMinus = boundPmi?.DimensionTolerance?.Minus;
                if (holeByName.TryGetValue(pmi.Target, out var pmiHole))
                {
                    result.Add(new Step242SemanticPmiHole(pmiHole.FeatureId, pmi.Value.Value, null, "explicit_v2_semantic_hole_diameter", tolerancePlus, toleranceMinus));
                }
                else if (TryResolveV2RecognizedRegionTarget(document, targetBinding, pmi.Target, out var importedTarget) || TryResolveV2ImportedFaceTarget(targetBinding, pmi.Target, out importedTarget))
                {
                    result.Add(new Step242SemanticPmiHole($"{targetSolid}.{pmi.Name}", pmi.Value.Value, null, $"imported_canonical_face:{importedTarget}", tolerancePlus, toleranceMinus));
                }
            }
            else if (pmi.Kind == FirmamentV2PmiKind.DatumPlane)
            {
                var selector = TryResolveV2RecognizedRegionTarget(document, targetBinding, pmi.Target, out var recognizedDatumTarget) ? recognizedDatumTarget : ResolveV2DatumTarget(targetBinding, pmi.Target);
                result.Add(new Step242SemanticPmiDatum(targetSolid, "plane", pmi.Name, selector));
            }
        }

        return result;
    }


    private static bool TryResolveV2RecognizedRegionTarget(FirmamentV2Document document, FirmamentV2SolidBinding solid, string target, out string resolved)
    {
        resolved = string.Empty;
        const string marker = ".region(\"";
        if (solid.InlineStep is null || !target.StartsWith(solid.Name + marker, StringComparison.Ordinal) || !target.EndsWith("\")", StringComparison.Ordinal))
        {
            return false;
        }

        var regionName = target[(solid.Name.Length + marker.Length)..^2];
        var region = document.RecognizedRegions?.FirstOrDefault(r => string.Equals(r.BodyName, solid.Name, StringComparison.Ordinal) && string.Equals(r.RegionName, regionName, StringComparison.Ordinal));
        if (region is null || region.FaceRefs.Count == 0 || !solid.InlineStep.TopologyMap.TryResolveFaceEntity(region.FaceRefs[0], out var faceId))
        {
            return false;
        }

        resolved = $"{solid.Name}.{region.RegionName}.{faceId}:{region.FaceRefs[0]}";
        return true;
    }

    private static bool TryResolveV2ImportedFaceTarget(FirmamentV2SolidBinding solid, string target, out string resolved)
    {
        resolved = string.Empty;
        const string marker = ".face(\"";
        if (solid.InlineStep is null || !target.StartsWith(solid.Name + marker, StringComparison.Ordinal) || !target.EndsWith("\")", StringComparison.Ordinal))
        {
            return false;
        }

        var entity = target[(solid.Name.Length + marker.Length)..^2];
        if (!solid.InlineStep.TopologyMap.TryResolveFaceEntity(entity, out var faceId))
        {
            return false;
        }

        resolved = $"{solid.Name}.{faceId}:{entity}";
        return true;
    }

    private static string ResolveV2DatumTarget(FirmamentV2SolidBinding solid, string target)
    {
        if (TryResolveV2ImportedFaceTarget(solid, target, out var importedTarget))
        {
            return importedTarget;
        }

        if (target.StartsWith("face(", StringComparison.Ordinal))
        {
            return $"{solid.Name}.{FaceAxisToPort(target)}";
        }

        var exposure = solid.Box?.Exposures.FirstOrDefault(e => string.Equals(e.Alias, target, StringComparison.Ordinal));
        return exposure is null ? $"{solid.Name}.{target}" : $"{solid.Name}.{FaceAxisToPort(exposure.Selector)}";
    }

    private static string FaceAxisToPort(string selector) => selector switch
    {
        "face(+Z)" => "top_face",
        "face(-Z)" => "bottom_face",
        "face(+X)" => "plus_x_face",
        "face(-X)" => "minus_x_face",
        "face(+Y)" => "plus_y_face",
        "face(-Y)" => "minus_y_face",
        _ => selector
    };

    private static KernelResult<FirmamentStepExportResult> SemanticHoleFailure(IEnumerable<string> diagnostics) =>
        KernelResult<FirmamentStepExportResult>.Failure(diagnostics.Select(d => new Kernel.Core.Diagnostics.KernelDiagnostic(
            Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            d,
            "FirmamentV2.SemanticHoleBuild")).ToArray());

    private static string ResolveDefaultOutputPath(string fullSourcePath)
    {
        var root = FindRepositoryRoot(Path.GetDirectoryName(fullSourcePath)!);
        var sourceFileName = Path.GetFileNameWithoutExtension(fullSourcePath);
        return Path.Combine(root, "testdata", "firmament", "exports", sourceFileName + ".step");
    }

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root for Firmament export output.");
    }

    private static string NormalizeLf(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
