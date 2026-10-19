using Aetheris.Kernel.Firmament.Drawing;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingDimensionPlanningTests
{
    [Fact]
    public void PlateDefinitionCoversEveryFaceAndPrintsItsIndependentDimensions()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Fixture(), scratch.Path, null, new(false, .1, "6061-T6 aluminium", .5));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var drawing = result.Artifacts!.Drawing; var definition = drawing.DefinitionEvidence!;
        Assert.True(definition.NominalGeometryComplete); Assert.True(definition.DimensionalTolerancesComplete);
        Assert.True(definition.MaterialSpecified); Assert.False(definition.ProcessCapabilityVerified);
        Assert.Equal(16, definition.CoveredFaces.Distinct().Count()); Assert.Empty(definition.MissingRequirements);
        Assert.True(definition.MinimumHoleWebAtDeclaredLimitsMillimetres > 10);
        var page = Assert.Single(drawing.Pages);
        Assert.Contains(page.Annotations, a => a.Identity == "dimension:Width" && a.EngineeringDisplay == "100 ±0.1");
        Assert.Contains(page.Annotations, a => a.Identity == "dimension:Height" && a.EngineeringDisplay == "70 ±0.1");
        Assert.Contains(page.Annotations, a => a.Identity == "dimension:Thickness" && a.EngineeringDisplay == "8 ±0.1");
        var table = Assert.Single(page.Tables); Assert.Equal(DrawingTableKind.HoleCoordinates, table.Kind);
        Assert.Equal(new[] { "HOLE", "X ±0.1", "Y ±0.1", "TYPE" }, table.Columns);
        Assert.Equal(10, table.Rows.Count);
        Assert.Contains(table.Rows, r => r.SequenceEqual(new[] { "H1", "14", "17", "D1" }));
        Assert.Contains(table.Rows, r => r.SequenceEqual(new[] { "H10", "86", "53", "D1" }));
        var group = Assert.Single(page.Annotations, a => a.Identity.StartsWith("group:", StringComparison.Ordinal));
        Assert.Equal("D1: 10X Ø4 +0.05/-0.02 THRU", group.EngineeringDisplay);
        var front = page.Views.Single(v => v.Identity == "FRONT");
        Assert.Equal(10, front.Primitives.Count(p => p.Kind == DrawingPrimitiveKind.Hidden));
        var datum = page.Annotations.Single(a => a.Kind == DrawingAnnotationKind.Datum);
        Assert.Equal("FRONT", datum.AssignedView); Assert.Equal(front.GeometryBounds.Y, datum.ProjectedAnchor.Y, 8);
        var top = page.Views.Single(v => v.Identity == "TOP");
        var xZero = Assert.Single(page.Annotations, a => a.Identity == "coordinate:X0");
        var yZero = Assert.Single(page.Annotations, a => a.Identity == "coordinate:Y0");
        var origin = Assert.Single(page.Annotations, a => a.Identity == "coordinate:origin");
        Assert.Equal("X0", xZero.EngineeringDisplay); Assert.Equal("Y0", yZero.EngineeringDisplay);
        Assert.Equal(top.GeometryBounds.X, xZero.ProjectedAnchor.X, 8);
        Assert.Equal(top.GeometryBounds.Bottom, yZero.ProjectedAnchor.Y, 8);
        Assert.Equal(top.GeometryBounds.X, origin.ProjectedAnchor.X, 8);
        Assert.Equal(top.GeometryBounds.Bottom, origin.ProjectedAnchor.Y, 8);
        Assert.Equal("(0,0)", origin.EngineeringDisplay);
        foreach (var reference in new[] { xZero, yZero, origin })
        {
            Assert.Equal("TOP", reference.AssignedView);
            Assert.Equal(reference.ProjectedAnchor, reference.SelectedCandidate.Leader[0]);
            Assert.NotEmpty(reference.Strokes);
        }
        Assert.Equal(0, drawing.LayoutEvidence.TextTextCollisionsAfter + drawing.LayoutEvidence.TextModelCollisionsAfter + drawing.LayoutEvidence.TextLeaderCollisionsAfter);
        using var presentation = DocumentFormat.OpenXml.Packaging.PresentationDocument.Open(result.Artifacts.PptxPath, false);
        Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(presentation));
    }

    [Fact]
    public void PerturbedNonGridCentersAreMeasuredAndKeepAuthoredIdentities()
    {
        using var scratch = new Scratch();
        var path = System.IO.Path.Combine(scratch.Path, "asymmetric.firmament");
        File.WriteAllText(path, File.ReadAllText(Fixture()).Replace("Point2(-36mm, -18mm)", "Point2(-34.125mm, -17.25mm)", StringComparison.Ordinal));
        var result = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "out"));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var hole = result.Artifacts!.Drawing.DefinitionEvidence!.Holes.Single(h => h.Identity == "H1");
        Assert.Equal(15.875, hole.X, 8); Assert.Equal(17.75, hole.Y, 8);
        Assert.Contains(result.Artifacts.Drawing.Pages.Single().Tables.Single().Rows, r => r.SequenceEqual(new[] { "H1", "15.875", "17.75", "D1" }));
    }

    [Fact]
    public void TableLimitsFollowTheExplicitToleranceWithoutChangingNominalCoordinates()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Fixture(), scratch.Path, null, new(false, .025, "6061-T6 aluminium", .5));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var table = Assert.Single(result.Artifacts!.Drawing.Pages.Single().Tables);
        Assert.Equal(new[] { "HOLE", "X ±0.025", "Y ±0.025", "TYPE" }, table.Columns);
        Assert.Contains(table.Rows, row => row.SequenceEqual(new[] { "H1", "14", "17", "D1" }));
        Assert.Contains(result.Artifacts.Drawing.Pages.Single().Notes, note => note == "TABLE X/Y LIMITS APPLY INDEPENDENTLY TO EACH AXIS.");
    }

    [Theory]
    [InlineData("Model Egg { units: mm Sphere Stock { radius: 10mm } }", false)]
    [InlineData("Model Blind { units: mm Box Stock { size: [40mm, 30mm, 8mm] } Modify Stock { Hole<Shaft> Bore { on: +Z; center: Point2(0mm, 0mm); diameter: 4mm; end: Blind 3mm } } }", true)]
    public void UncoveredGeometryCannotBecomeACompleteDefinition(string source, bool referenceSupported)
    {
        using var scratch = new Scratch(); var path = System.IO.Path.Combine(scratch.Path, "input.firmament"); File.WriteAllText(path, source);
        var compiled = FirmamentBuildAndExport.CompileSource(source); Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
        var result = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "out"));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-definition-incomplete:", StringComparison.Ordinal));
        var reference = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "reference"), null, new(ReferenceOnly: true));
        Assert.Equal(referenceSupported, reference.IsSuccess);
        if (!referenceSupported) { Assert.Contains(reference.Diagnostics, d => d.StartsWith("drawing-projection-unsupported:", StringComparison.Ordinal)); return; }
        Assert.False(reference.Artifacts!.Drawing.DefinitionEvidence!.NominalGeometryComplete);
        Assert.Contains(reference.Artifacts.Drawing.Pages[0].Notes, n => n.Contains("NOT FOR MANUFACTURE", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingRequirementsRemainMissing()
    {
        using var scratch = new Scratch(); var result = FirmamentDrawingCompiler.Generate(Fixture(), scratch.Path);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var evidence = result.Artifacts!.Drawing.DefinitionEvidence!;
        Assert.True(evidence.NominalGeometryComplete); Assert.False(evidence.DimensionalTolerancesComplete);
        Assert.False(evidence.MaterialSpecified); Assert.Null(evidence.LinearToleranceMillimetres); Assert.Null(evidence.AngularToleranceDegrees);
        Assert.Equal(3, evidence.MissingRequirements.Count);
        Assert.Equal(new[] { "HOLE", "X (TOL?)", "Y (TOL?)", "TYPE" }, result.Artifacts.Drawing.Pages[0].Tables.Single().Columns);
        Assert.Contains(result.Artifacts.Drawing.Pages[0].Notes, n => n.Contains("NOT FOR MANUFACTURE", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(.0)]
    [InlineData(-.1)]
    [InlineData(double.NaN)]
    public void InvalidLinearToleranceRejects(double tolerance)
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Fixture(), scratch.Path, null, new(LinearToleranceMillimetres: tolerance));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-linear-tolerance-invalid:", StringComparison.Ordinal));
    }

    [Fact]
    public void ToleranceLimitsCannotPermitEdgeBreakout()
    {
        using var scratch = new Scratch(); var path = System.IO.Path.Combine(scratch.Path, "near-edge.firmament");
        File.WriteAllText(path, File.ReadAllText(Fixture()).Replace("Point2(-36mm, -18mm)", "Point2(-47.5mm, -18mm)", StringComparison.Ordinal));
        var result = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "out"), null, new(false, 1, "6061-T6", .5));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-tolerance-envelope-invalid:", StringComparison.Ordinal));
    }

    [Fact]
    public void ContradictoryDiameterCannotBePrintedAsAControllingSize()
    {
        using var scratch = new Scratch(); var path = System.IO.Path.Combine(scratch.Path, "wrong-size.firmament");
        File.WriteAllText(path, File.ReadAllText(Fixture()).Replace("value: 4mm", "value: 3mm", StringComparison.Ordinal));
        var result = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "out"));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-geometry-mismatch:", StringComparison.Ordinal));
    }

    [Fact]
    public void ReferenceOnlyCannotSilentlyConsumeManufacturingInputs()
    {
        using var scratch = new Scratch();
        var result = FirmamentDrawingCompiler.Generate(Fixture(), scratch.Path, null, new(true, .1));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-reference-options-unsupported:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(3, null)]
    [InlineData(8, 2)]
    public void ContradictoryDepthOrQuantityCannotDisappear(double depth, int? quantity)
    {
        using var scratch = new Scratch();
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture())); Assert.True(compiled.IsSuccess);
        var body = Step242Importer.ImportBody(compiled.Value.StepText).Value;
        var wall = body.Topology.Faces.First(f => body.TryGetFaceSurfaceGeometry(f.Id, out var s) && s?.Cylinder is not null);
        var exported = Step242Exporter.ExportBody(body,
            [new Step242SemanticPmiHole("Stock.H1", 4, depth, "through", .05, .02, quantity) { GeometricFaceIds = [wall.Id.Value] }]);
        Assert.True(exported.IsSuccess);
        var path = System.IO.Path.Combine(scratch.Path, "contradiction.step"); File.WriteAllText(path, exported.Value);
        var result = FirmamentDrawingCompiler.Generate(path, System.IO.Path.Combine(scratch.Path, "out"));
        Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, d => d.StartsWith("drawing-pmi-geometry-mismatch:", StringComparison.Ordinal));
        Assert.False(Directory.Exists(System.IO.Path.Combine(scratch.Path, "out")));
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent;
        return directory!.FullName;
    }
    private static string Fixture() => System.IO.Path.Combine(Root(), "fixtures/Regression/DrawingGeneration/inspection-plate.firmament");
    private sealed class Scratch : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(Root(), "artifacts/local/tests/drawing-definition", Guid.NewGuid().ToString("N"));
        public Scratch() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
