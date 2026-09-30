using System.Security.Cryptography;
using Aetheris.ThreeDm;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.CLI.Tests;

public sealed class ThreeDmBrepBindLocalTests
{
    [LocalThreeDmFact]
    public void ProductDesignExportsEverySourceBrepAndCombinedReference()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "testdata", "3DM", "cartesian-product-metres.3dm");
        var output = Path.Combine(root, "artifacts", "local", "3dm-bind-x0-tests", "product");
        var report = ThreeDmStepRecovery.Run(source, output, emitCombinedStep: true);

        Assert.Equal(27, report.Bodies.Count);
        Assert.Equal(27, report.QualifiedBodyCount);
        Assert.Equal(0, report.PartialBodyCount);
        Assert.Equal(185, report.BoundFaceCount);
        Assert.Equal(742, report.BoundTrimCount);
        Assert.True(report.CombinedStepReimported, string.Join("; ", report.CombinedDiagnostics));
        Assert.Equal(28, report.CombinedSolidCount);
        Assert.Equal(24, report.Bodies.Count(body => body.Status == ThreeDmStepImportStatus.Qualified));
        Assert.Equal(new[] { 5, 21, 26 }, report.Bodies
            .Where(body => body.Status == ThreeDmStepImportStatus.InspectableRecovered)
            .Select(body => body.ObjectIndex).ToArray());
        Assert.InRange(report.CombinedBoundingBoxDriftMillimetres!.Value, 0, 0.1);
        Assert.True(File.Exists(report.CombinedStepPath));
        Assert.All(report.Bodies.Where(body => body.StepReimported), body =>
        {
            Assert.True(body.BoundIsEnclosed);
            Assert.True(body.BoundOrientationConsistent);
            Assert.Equal(body.SourceFaces, body.BoundFaces);
            Assert.Equal(body.SourceTrims, body.BoundTrims);
            Assert.InRange(body.WorstPcurveResidualMillimetres, 0, 0.1);
            Assert.InRange(body.MaximumBoundingBoxDriftMillimetres!.Value, 0, 0.1);
            Assert.True(File.Exists(body.StepPath));
        });
        Assert.Contains(report.Bodies, body => body.ObjectIndex == 3 && body.StepReimported
            && body.SourcePcurves > 0 && body.RecoveredPcurves > 0);
        Assert.Contains(report.Bodies, body => body.ObjectIndex == 5 && body.StepReimported
            && body.Adjustments.Any(adjustment => adjustment.Kind == "RhinoSingularTrimCollapsedToVertex"
                && adjustment.BeforeMillimetres > 0.4 && adjustment.BeforeMillimetres < adjustment.AllowedMillimetres));
        Assert.Contains(report.Bodies, body => body.ObjectIndex == 21 && body.StepReimported
            && body.Adjustments.Any(adjustment => adjustment.Kind == "SourceVertexToCoincidentEdgeEndpoints"
                && adjustment.BeforeMillimetres is > 0.059 and < 0.061 && adjustment.AfterMillimetres < 1e-6));
        Assert.Contains(report.Bodies, body => body.ObjectIndex == 22 && body.StepReimported
            && body.ComponentCount == 2 && body.StepPaths.Count == 2);
        Assert.Contains(report.Bodies, body => body.ObjectIndex == 26 && body.StepReimported
            && body.Adjustments.Count(adjustment => adjustment.Kind == "RhinoSingularTrimCollapsedToVertex") == 2);

        var nearCoincident = report.Bodies.Single(body => body.ObjectIndex == 21);
        var step = File.ReadAllText(nearCoincident.StepPath!);
        Assert.False(Step242Importer.ImportBody(step).IsSuccess);
        Assert.True(Step242Importer.ImportBody(step,
            new ImportPolicy(AllowBoundedNearCoincidentInnerLoop: true)).IsSuccess);
    }

    [LocalThreeDmFact]
    public void RecoveredProductBodyStepIsDeterministicAndFurnitureBodyReimports()
    {
        var root = RepositoryRoot();
        var directory = Path.Combine(root, "artifacts", "local", "3dm-bind-x0-tests", "focused");
        var product = Path.Combine(root, "testdata", "3DM", "cartesian-product-metres.3dm");
        var first = ThreeDmStepRecovery.Run(product, directory, selectedObjectIndex: 3).Bodies.Single();
        Assert.True(first.StepReimported, string.Join("; ", first.Diagnostics));
        var firstHash = SHA256.HashData(File.ReadAllBytes(first.StepPath!));
        var second = ThreeDmStepRecovery.Run(product, directory, selectedObjectIndex: 3).Bodies.Single();
        Assert.True(second.StepReimported, string.Join("; ", second.Diagnostics));
        Assert.Equal(firstHash, SHA256.HashData(File.ReadAllBytes(second.StepPath!)));

        var furniture = Path.Combine(root, "testdata", "3DM", "cartesian-furniture-metres.3dm");
        var furnitureBody = ThreeDmStepRecovery.Run(furniture, directory, selectedObjectIndex: 1).Bodies.Single();
        Assert.True(furnitureBody.StepReimported, string.Join("; ", furnitureBody.Diagnostics));
        Assert.Equal((6, 24), (furnitureBody.BoundFaces, furnitureBody.BoundTrims));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Aetheris repository root not found.");
    }
}
