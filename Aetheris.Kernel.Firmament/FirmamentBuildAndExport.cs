using System.Text;
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
        var exportResult = ExportSource(sourceText);
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


    private static KernelResult<FirmamentStepExportResult> ExportSource(string sourceText)
    {
        var v2Parse = FirmamentV2Parser.Parse(sourceText);
        if (v2Parse.IsSuccess && v2Parse.Document is not null)
        {
            var dfm = FirmamentV2DfmEnforcement.Validate(v2Parse.Document);
            if (!dfm.IsSuccess)
            {
                return KernelResult<FirmamentStepExportResult>.Failure(dfm.Diagnostics);
            }

            if (TryExportV2SemanticHoleBody(v2Parse.Document) is { } semanticHoleExport)
            {
                return semanticHoleExport;
            }

            if (TryExportV2ControlledSideHoleBody(v2Parse.Document) is { } sideHoleExport)
            {
                return sideHoleExport;
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

        return FirmamentStepExporter.Export(new FirmamentCompileRequest(new FirmamentSourceDocument(sourceText)));
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
        var host = new AirHoleSimpleShaftHost(box.Size[0], box.Size[1], -box.Size[2] / 2d, box.Size[2] / 2d);
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

        return KernelResult<FirmamentStepExportResult>.Success(
            new FirmamentStepExportResult(
                step.Value,
                feature.FeatureId,
                0,
                semanticHoles.Count == 1 ? nameof(AirHoleSimpleShaftMaterializer) : nameof(AirHoleCompositeMaterializer),
                semanticHoles.Count == 1 ? feature.Stack.Kind.ToString() : "CompositeSimpleShaft",
                DatumInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.DatumPlane).Select(p => new FirmamentPmiInspectionDatum(p.Name, "planar", p.Target)).ToArray() ?? [],
                DimensionInspection: document.Pmi?.Where(p => p.Kind == FirmamentV2PmiKind.HoleDiameter).Select(p => new FirmamentPmiInspectionDimension("Diameter", p.Target, null, p.Value ?? 0d, "explicit-v2-semantic-pmi", null)).ToArray() ?? []));
    }

    private static IReadOnlyList<Step242SemanticPmi> BuildV2SemanticPmi(FirmamentV2Document document, IReadOnlyList<Core.Air.AirHoleFeature> semanticHoles, string targetSolid)
    {
        if (document.Pmi is null || document.Pmi.Count == 0)
        {
            return [];
        }

        var holeByName = semanticHoles.SelectMany(h => new[] { new KeyValuePair<string, Core.Air.AirHoleFeature>(h.FeatureId, h), new KeyValuePair<string, Core.Air.AirHoleFeature>(h.Name, h) }).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        var targetBinding = document.Solids.Single(s => string.Equals(s.Name, targetSolid, StringComparison.Ordinal));
        var result = new List<Step242SemanticPmi>();
        foreach (var pmi in document.Pmi)
        {
            if (pmi.Kind == FirmamentV2PmiKind.HoleDiameter && pmi.Value.HasValue && holeByName.TryGetValue(pmi.Target, out var pmiHole))
            {
                result.Add(new Step242SemanticPmiHole(pmiHole.FeatureId, pmi.Value.Value, null, "explicit_v2_semantic_hole_diameter", null, null));
            }
            else if (pmi.Kind == FirmamentV2PmiKind.DatumPlane)
            {
                var selector = ResolveV2DatumTarget(targetBinding, pmi.Target);
                result.Add(new Step242SemanticPmiDatum(targetSolid, "plane", pmi.Name, selector));
            }
        }

        return result;
    }

    private static string ResolveV2DatumTarget(FirmamentV2SolidBinding solid, string target)
    {
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
