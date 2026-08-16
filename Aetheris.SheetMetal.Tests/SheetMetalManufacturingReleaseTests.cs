using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;
using Xunit;

namespace Aetheris.SheetMetal.Tests;

public sealed class SheetMetalManufacturingReleaseTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static string ReleaseModel => Path.Combine(RepoRoot, "docs/modules/sheetmetal/artifacts/ctc03-manufacturing-release/ctc03-manufacturing.firmament");
    private static string M8DesignBasis => Path.Combine(RepoRoot, "docs/modules/sheetmetal/artifacts/m8/ctc03-final.firmament");

    [Fact]
    public void Ctc03MetricRelease_BindsManufacturingPmiToStableSheetMetalTargets()
    {
        var result = SheetMetalFirmament.CompileFile(ReleaseModel);
        Assert.True(result.IsSuccess, string.Join('\n', result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(SheetMetalProvenanceCategory.Authored, result.Spec!.Authority);
        Assert.Equal(2d, result.Part!.Thickness);
        Assert.Equal(15, result.Part.Regions.Count);
        Assert.Equal(7, result.Part.Bends.Count);
        Assert.Equal(17, result.Part.Features.Count);
        Assert.Equal(3, result.Spec.Manufacturing.Datums.Count);
        Assert.Equal(13, result.Spec.Manufacturing.Dimensions.Count);
        Assert.Equal(5, result.Spec.Manufacturing.GeometricTolerances.Count);
        Assert.Equal(8, result.Spec.Manufacturing.Annotations.Count);
        Assert.Contains(result.Spec.Manufacturing.Dimensions, dimension => dimension.Target == "Ctc03Layout.BaseFastenerPattern" && dimension.Value == 16d && dimension.Quantity == 4);
        Assert.Contains(result.Spec.Manufacturing.GeometricTolerances, tolerance => tolerance.Name == "FrontMountPosition" && tolerance.DatumReferences.SequenceEqual(["A", "B", "C"]));
        Assert.NotNull(result.FlatPattern!.ExactBlankContour);
        Assert.Equal(17, result.FlatPattern.CutLoops.Count);
        Assert.True(BrepExportPreflight.Validate(result.Part.FormedBody!).IsValid);
        Assert.Equal(SheetMetalDfmStatus.Pass, SheetMetalDfm.Evaluate(result.Part, result.FlatPattern).Overall);
    }

    [Fact]
    public void Ctc03MetricRelease_Ap242ReimportsAndSemanticPmiReinspectsDeterministically()
    {
        var result = SheetMetalFirmament.CompileFile(ReleaseModel);
        Assert.True(result.IsSuccess, string.Join('\n', result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var semanticPmi = result.Spec!.Manufacturing.ToStep242SemanticPmi(result.Part);
        var options = new Step242ExportOptions { ProductName = "Ctc03", BrepExportPreflightMode = BrepExportPreflightMode.Enforce };
        var first = Step242Exporter.ExportBody(result.Part!.FormedBody!, semanticPmi, options);
        var second = Step242Exporter.ExportBody(result.Part.FormedBody!, semanticPmi, options);
        Assert.True(first.IsSuccess, string.Join('\n', first.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(first.Value, second.Value);
        Assert.True(Step242Importer.ImportBody(first.Value).IsSuccess);

        var inspection = Step242SemanticPmiInspector.Inspect(first.Value);
        Assert.True(inspection.Success, string.Join('\n', inspection.Diagnostics));
        Assert.Equal(3, inspection.DatumCount);
        Assert.Equal(13, inspection.DimensionCount);
        Assert.Equal(5, inspection.GeometricToleranceCount);
        Assert.Equal(8, inspection.AnnotationCount);
        Assert.Contains(inspection.Items, item => item.Kind == "Diameter" && item.Target == "Ctc03Layout.BaseFastenerPattern" && item.Value == 16d && item.Quantity == 4);
        Assert.Contains(inspection.Items, item => item.Kind == "Position" && item.Name == "FrontMountPosition" && item.Value == 0.8d && item.DatumReferences.SequenceEqual(["A", "B", "C"]));
        Assert.Contains(inspection.Items, item => item.Kind == "Annotation" && item.Name == "CutAndDeburr" && item.Target == "Ctc03");
        Assert.Contains(inspection.Items, item => item.Kind == "Datum" && item.Name == "A" && item.GeometricFaceEntityIds.Count == 1);
        Assert.Contains(inspection.Items, item => item.Kind == "Diameter" && item.Target == "Ctc03Layout.BaseFastenerPattern" && item.GeometricFaceEntityIds.Count == 4);
        Assert.Contains(inspection.Items, item => item.Kind == "Position" && item.Name == "FrontMountPosition" && item.GeometricFaceEntityIds.Count == 2);
        Assert.Contains(inspection.Items, item => item.Kind == "Annotation" && item.Name == "ProtectDatumA" && item.GeometricFaceEntityIds.Count == 1);
        Assert.Contains("DATUM_FEATURE('firmament-datum:A'", first.Value, StringComparison.Ordinal);
        Assert.Contains("POSITION_TOLERANCE()", first.Value, StringComparison.Ordinal);
        Assert.Contains("GEOMETRIC_ITEM_SPECIFIC_USAGE", first.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("ANNOTATION_PLANE", first.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Ctc03MetricRelease_PreservesM8TopologyAndBoundsNormalizationDepartures()
    {
        var basis = SheetMetalFirmament.CompileFile(M8DesignBasis);
        var release = SheetMetalFirmament.CompileFile(ReleaseModel);
        Assert.True(basis.IsSuccess);
        Assert.True(release.IsSuccess);
        var comparison = SheetMetalIntentComparer.Compare(basis.Part!, release.Part!);

        Assert.Equal(7, comparison.Bends.Count);
        Assert.All(comparison.Bends, bend =>
        {
            Assert.Equal(bend.SourceBendId, bend.IntentBendId);
            Assert.True(bend.AdjacencyMatches);
            Assert.Equal(0d, bend.AxisAngleResidualDegrees, 8);
            Assert.Equal(0d, bend.BendAngleResidualDegrees, 8);
            Assert.Equal(0.35d, bend.RadiusResidual, 8);
        });
        Assert.Equal(17, comparison.Features.Count);
        Assert.True(comparison.Features.Max(feature => feature.CenterResidual) < 3.2d);
        Assert.True(comparison.Features.Max(feature => feature.SizeResidual) <= 1.1d + 1e-8d);
        Assert.True(comparison.SourceToIntent.P95 < 2.9d);
        Assert.True(comparison.FlatPattern.Contour.P95 < 3.3d);
        Assert.Equal(0, comparison.FlatPattern.BendLineCountDelta);
        Assert.False(comparison.FlatPattern.HasOverlap);
    }

    [Fact]
    public void ManufacturingPmi_GenericPanelPathRejectsUnknownTargetsBeforeGeometryExport()
    {
        const string source = """
        Concept Struct Layout {
            Pattern Mounts { On: Panel; Feature: Circle { Diameter: 6mm; }; Center: Panel.Center; Count: 2; Pitch: (20mm, 0mm); }
        }
        Manufacturing Release { MaterialSpecification: "5052-H32"; }
        Pmi ReleasePmi {
            DatumFeature A { Target: Panel; }
            Dimension MountDiameter { Kind: Diameter; Target: Layout.Missing; Value: 6mm; Tolerance: PlusMinus(0.1mm, 0.1mm); Quantity: 2; }
        }
        SheetMetal GenericPanel {
            Thickness: 1.5mm;
            Base Panel { Profile: Rectangle { Width: 100mm; Height: 60mm; }; }
            Flange Lip { From: Panel.Rear; Height: 15mm; Angle: 90deg; Radius: 2mm; }
        }
        """;

        var result = SheetMetalFirmament.Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "sheetmetal-manufacturing-pmi-invalid" && diagnostic.Message.Contains("Layout.Missing", StringComparison.Ordinal));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
