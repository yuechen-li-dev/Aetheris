using System.Text;
using System.Security.Cryptography;
using Aetheris.Kernel.Core.Air;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
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
        var v2Parse = FirmamentV2Parser.Parse(sourceText, sourceDirectory);
        if (v2Parse.IsSuccess && v2Parse.Document is not null)
        {
            var dfm = FirmamentV2DfmEnforcement.Validate(v2Parse.Document);
            if (!dfm.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(dfm.Diagnostics);
            }

            if (TryExportV2AirChamferBody(v2Parse.Document) is { } airChamferExport)
            {
                return airChamferExport;
            }

            if (TryExportV2SemanticHoleBody(v2Parse.Document) is { } semanticHoleExport)
            {
                return semanticHoleExport;
            }

            if (TryExportV2ControlledSideHoleBody(v2Parse.Document) is { } sideHoleExport)
            {
                return sideHoleExport;
            }

            if (TryExportV2InlineStepReplacementBody(v2Parse.Document) is { } replacementExport)
            {
                return replacementExport;
            }

            if (TryExportV2InlineStepBody(v2Parse.Document) is { } inlineStepExport)
            {
                return inlineStepExport;
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

        return FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
    }

    private static KernelResult<FirmamentStepExportResult>? TryExportV2AirChamferBody(FirmamentV2Document document)
    {
        var finishes = (document.ModifyBlocks ?? []).SelectMany(m => (m.EdgeFinishes ?? []).Select(f => (Modify: m, Finish: f))).ToArray();
        if (finishes.Length == 0) return null;
        if (finishes.Length != 1 || document.Solids.Count != 1 || document.ModifyBlocks!.Count != 1
            || document.ModifyBlocks[0].Regions.Count != 0 || document.ModifyBlocks[0].SemanticHoles.Count != 0)
            return AirChamferFailure("air-chamfer-production-route-requires-one-box-and-one-edge-finish");

        var (modify, finish) = finishes[0];
        var solid = document.Solids.SingleOrDefault(s => s.Name == modify.TargetSolid);
        if (solid?.Primitive is not FirmamentV2BoxRecord box || box.Size.Count != 3)
            return AirChamferFailure("air-chamfer-history-known-rectangular-prism-required");

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
        var step = Step242Exporter.ExportBody(body, semanticPmi);
        if (!step.IsSuccess)
        {
            return KernelResult<FirmamentStepExportResult>.Failure(step.Diagnostics);
        }

        var featureReports = semanticHoles.Select(h => new FirmamentHoleFeatureReport(
            h.Name,
            "Hole",
            h.FeatureId,
            h.Shaft.Diameter,
            h.Placement.U,
            h.Placement.V,
            h.Placement.ResolvedPoint3 is { } p ? new[] { p.X, p.Y, p.Z } : null,
            h.Placement.ResolvedPoint3 is { } sourcePoint ? sourcePoint.Ordinal is { } ordinal ? $"{sourcePoint.SourceMember}[{ordinal}]" : sourcePoint.SourceMember : null,
            h.Placement.ResolvedPoint3?.StableId,
            h.Placement.ResolvedPoint3?.Ordinal,
            h.Placement.ResolvedPoint3?.PlacementFace ?? h.Placement.EntryFaceName,
            h.Placement.ResolvedPoint3?.SourceSpan,
            semanticHoles.Count == 1 ? nameof(AirHoleSimpleShaftMaterializer) : nameof(AirHoleCompositeMaterializer))).ToArray();
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
