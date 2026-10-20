using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Core.Brep.Verification;

namespace Aetheris.Kernel.Firmament.Tests;

public class FirmamentSketchTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(Root(), "fixtures", "Regression", "Sketcher", name));
    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "fixtures"))) return directory.FullName;
        throw new DirectoryNotFoundException("Repository fixtures are required.");
    }

    [Fact]
    public void ProfileProjectionAndParameterEditKeepFourOpeningsAndExpressions()
    {
        var source = Fixture("plate.firmament");
        var view = FirmamentSketchService.Project(source, "plate.firmament", "7");
        Assert.Empty(view.Diagnostics);
        Assert.Equal("ProfileResolvedNotBuilt", view.ValidationLevel);
        Assert.Equal(5, view.Loops.Count);
        Assert.Equal(5, view.Shapes.Count);
        var width = view.Shapes.Single(s => s.Name == "Outline").Controls.Single(c => c.Label == "Width");
        Assert.Equal("Width", width.Parameter);
        Assert.True(width.Editable);
        Assert.False(view.Shapes.Single(s => s.Name == "MountA").Controls.Single(c => c.Label == "Center X").Editable);
        var edited = FirmamentSketchService.Edit(source, "plate.firmament", "7", new("dimensions", view.Node,
            Changes: [new(width.Id, 100)]));
        Assert.Contains("Static Width: Length = 100mm", edited.Text);
        Assert.Contains("size: [Width, Height]", edited.Text);
        Assert.Contains("center: [Width / 2 - 8mm", edited.Text);
        var after = FirmamentSketchService.Project(edited.Text, "plate.firmament", "8");
        Assert.Empty(after.Diagnostics);
        Assert.Equal(5, after.Loops.Count);
        Assert.Equal(42, after.Shapes.Single(s => s.Name == "MountB").Center[0], 8);
        Assert.Equal(view.Loops.SelectMany(l => l.Curves).Select(c => c.Id), after.Loops.SelectMany(l => l.Curves).Select(c => c.Id));
        var built = FirmamentBuildAndExport.CompileSource(edited.Text);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measured = new FirmamentMeasurements(built.Value);
        Assert.Equal([-50d, -25, 0], measured.Bounds!.MinimumMm);
        Assert.Equal([50d, 25, 6], measured.Bounds.MaximumMm);
    }

    [Fact]
    public void FourOpeningWorkflowNamesDrivingValuesAndRetainsGeometryThroughResize()
    {
        var source = FirmamentSketchService.Edit("Model Plate { units: mm }", "p.firmament", "0", new("create", "Plate", "Rect2")).Text;
        var view = FirmamentSketchService.Project(source, "p.firmament", "1");
        var reference = Assert.Single(view.OpeningReferences);
        foreach (var anchor in new[] { "BottomLeft", "BottomRight", "TopRight", "TopLeft" })
        {
            source = FirmamentSketchService.Edit(source, "p.firmament", "1", new("opening", view.Node,
                X: 8, Y: 8, Reference: reference.Id, Anchor: anchor)).Text;
            view = FirmamentSketchService.Project(source, "p.firmament", "1");
        }
        Assert.Equal(5, view.Loops.Count);
        Assert.Equal(4, view.Shapes.Count(s => s.Kind == "Circle2"));
        var width = view.Shapes.Single(s => s.Kind == "Rect2").Controls.Single(c => c.Label == "Width");
        Assert.Equal("OutlineWidth", width.Parameter);
        var before = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(before.IsSuccess, string.Join("; ", before.Diagnostics.Select(d => d.Message)));
        var beforeVolume = new FirmamentMeasurements(before.Value).Volume().ValueMm3;
        var resized = FirmamentSketchService.Edit(source, "p.firmament", "1", new("dimensions", view.Node, Changes: [new(width.Id, 100)])).Text;
        var built = FirmamentBuildAndExport.CompileSource(resized);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measured = new FirmamentMeasurements(built.Value);
        Assert.NotNull(beforeVolume);
        Assert.Equal(beforeVolume.Value + 20 * 50 * 6, measured.Volume().ValueMm3!.Value, 5);
        var after = FirmamentSketchService.Project(resized, "p.firmament", "2");
        Assert.All(after.Shapes.Where(s => s.Kind == "Circle2"), s => Assert.Equal(42, Math.Abs(s.Center[0]), 8));
        Assert.Equal(before.Value.StepText, FirmamentBuildAndExport.CompileSource(source).Value.StepText);
        Assert.Throws<FirmamentLanguageEditException>(() => FirmamentSketchService.Edit(resized, "p.firmament", "2",
            new("opening", after.Node, X: 8, Y: 8, Reference: reference.Id, Anchor: "BottomLeft")));
        var removed = FirmamentLanguageEditService.Apply(resized, "p.firmament", "2", [new("removeDeclaration") { Node = reference.Id }]);
        Assert.False(FirmamentBuildAndExport.CompileSource(removed.Text).IsSuccess);
    }

    [Fact]
    public void TracedFrameIsProjectedAndPreservedByDimensionEditing()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures", "Regression", "Profile", "valid", "construction-plane-positive-x.firmament"));
        var view = FirmamentSketchService.Project(source, "side.firmament", "1");
        Assert.Empty(view.Diagnostics);
        Assert.Equal([1d, 0, 0], view.Frame!.Normal);
        Assert.Equal([10d, 2, 3], view.Frame.Origin);
        var width = Assert.Single(view.Shapes).Controls.Single(c => c.Label == "Width");
        source = FirmamentSketchService.Edit(source, "side.firmament", "1", new("dimensions", view.Node, Changes: [new(width.Id, 30)])).Text;
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var bounds = new FirmamentMeasurements(built.Value).Bounds!;
        Assert.Equal([5d, -13, -2], bounds.MinimumMm);
        Assert.Equal([15d, 17, 8], bounds.MaximumMm);
    }

    [Theory]
    [InlineData("XY", -15, -5, 0, 15, 5, 6)]
    [InlineData("XZ", -15, -6, -5, 15, 0, 5)]
    [InlineData("YZ", 0, -15, -5, 6, 15, 5)]
    public void PrincipalLayoutFramesHaveExplicitRightHandedWorldGeometry(string plane,
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var source = $$"""
            Concept Struct Layout on {{plane}} { Rect2 Outline { center: [0mm, 0mm] size: [30mm, 10mm] } }
            Profile Section using Layout { Loop Outer { Outline |> TraceLoop } }
            Struct Part { Extrude Body { profile: Section from: 0mm to: 6mm } }
            """;
        var view = FirmamentSketchService.Project(source, "part.firmament", "1");
        Assert.Empty(view.Diagnostics);
        var normal = plane switch { "XY" => new[] { 0d, 0, 1 }, "XZ" => [0d, -1, 0], _ => [1d, 0, 0] };
        Assert.Equal(normal, view.Frame!.Normal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measurement = new FirmamentMeasurements(built.Value);
        Assert.Equal([minX, minY, minZ], measurement.Bounds!.MinimumMm);
        Assert.Equal([maxX, maxY, maxZ], measurement.Bounds.MaximumMm);
        Assert.Equal(1800, measurement.Volume().ValueMm3!.Value, 6);
        var profile = ProfileAuthoringParser.ResolveNamedProfile(source, "Section", out var diagnostics);
        Assert.Empty(diagnostics); Assert.Equal(1, profile!.EffectiveConstructionPlane.Determinant, 10);
        if (plane == "XY") Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(source.Replace(" on XY", "")).Value.StepText);
    }

    [Theory]
    [InlineData("XY", -15, -5, 0, 15, 5, 6)]
    [InlineData("XZ", -15, -6, -5, 15, 0, 5)]
    [InlineData("YZ", 0, -15, -5, 6, 15, 5)]
    public void ComposeInheritsItsOperandFrame(string plane,
        double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        var source = $$"""
            Concept Struct Layout on {{plane}} { Rect2 Outline { center: [0mm,0mm] size: [30mm,10mm] } }
            Profile Section using Layout { Loop Outer { Outline |> TraceLoop } }
            Struct Part { Compose Body { Base Stock { profile: Section from: 0mm to: 6mm } } }
            """;
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measured = new FirmamentMeasurements(built.Value);
        Assert.Equal([minX, minY, minZ], measured.Bounds!.MinimumMm);
        Assert.Equal([maxX, maxY, maxZ], measured.Bounds.MaximumMm);
        Assert.Equal(1800, measured.Volume().ValueMm3!.Value, 6);
        if (plane == "XY") Assert.Equal(built.Value.StepText, FirmamentBuildAndExport.CompileSource(source.Replace(" on XY", "")).Value.StepText);
    }

    [Fact]
    public void X4BracketHasAbsoluteXzBoundsForBothFrameSpellings()
    {
        foreach (var path in new[] { "f01-xz-layout.firmament", "reference/f01-xz-layout.firmament" })
        {
            var source = File.ReadAllText(Path.Combine(Root(), "fixtures", "Regression", "FrontendX4", "cases", path));
            var built = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
            var measured = new FirmamentMeasurements(built.Value);
            Assert.Equal([0d, -30, 0], measured.Bounds!.MinimumMm);
            Assert.Equal([60d, 0, 40], measured.Bounds.MaximumMm);
            var mass = BrepMassProperties.Evaluate(measured.Body);
            Assert.True(mass.IsEnclosed);
            Assert.Equal(14250, mass.AbsoluteVolume, 6);
        }
    }

    [Fact]
    public void ComposePreservesTracedOriginAndCommonFrameRemoval()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures", "Regression", "Profile", "valid", "construction-plane-positive-x.firmament"));
        source = source.Replace("Extrude Solid { Profile: SideProfile; From: -5mm; To: 5mm }", """
            Profile Cut using PositiveXWorkplane {
                Rect2 Opening { center: [0mm,0mm] size: [4mm,2mm] }
                Opening |> TraceLoop
            }
            Compose Solid {
                Base Stock { profile: SideProfile from: -5mm to: 5mm }
                Remove Opening { profile: Cut from: -5mm to: 5mm }
            }
            """);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(d => d.Message)));
        var measured = new FirmamentMeasurements(built.Value);
        Assert.Equal([5d, -8, -2], measured.Bounds!.MinimumMm);
        Assert.Equal([15d, 12, 8], measured.Bounds.MaximumMm);
        var mass = BrepMassProperties.Evaluate(measured.Body);
        Assert.True(mass.IsEnclosed);
        Assert.Equal(1920, mass.AbsoluteVolume, 6);
    }

    [Theory]
    [InlineData("Remove Cut { profile: Other from: 0mm to: 6mm }", "compose-profile-frame-mismatch")]
    [InlineData("Placement Override { anchor: [0mm,0mm,0mm] profilePlane: XY axis: +Z referenceDirection: +X }", "compose-profile-frame-mismatch")]
    [InlineData("Hole<Shaft> Drill { on: +Z center: [0mm,0mm] diameter: 2mm end: ThroughAll }", "compose-oriented-feature-not-admitted")]
    public void ComposeRejectsConflictingOrUnqualifiedFrameConsumers(string operation, string code)
    {
        var source = $$"""
            Concept Struct Layout on XZ { Rect2 Outline { center: [0mm,0mm] size: [30mm,10mm] } }
            Profile Section using Layout { Loop Outer { Outline |> TraceLoop } }
            Profile Other { Rect2 Cut { center: [0mm,0mm] size: [4mm,2mm] } Cut |> TraceLoop }
            Struct Part { Compose Body { Base Stock { profile: Section from: 0mm to: 6mm } {{operation}} } }
            """;
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains(code, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Concept Struct Layout on Missing { Rect2 Outline { center: [0mm,0mm] size: [30mm,10mm] } }")]
    [InlineData("Concept Struct Layout on XZ { Datum: Plane { Origin: [0mm,0mm,0mm] Normal: [0,0,1] } Rect2 Outline { center: [0mm,0mm] size: [30mm,10mm] } }")]
    public void UnsupportedLayoutIntentNeverFallsBackToXy(string layout)
    {
        var source = layout + "\nProfile Section using Layout { Loop Outer { Outline |> TraceLoop } }\nStruct Part { Extrude Body { profile: Section from: 0mm to: 6mm } }";
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        Assert.Equal("Unresolved", FirmamentSketchService.Project(source, "part.firmament", "1").ValidationLevel);
    }

    [Fact]
    public void UnusedUnknownLayoutFrameIsStillRejected()
    {
        const string source = "Model Part { units: mm Concept Struct Layout on Missing { Rect2 Guide { center: [0mm,0mm] size: [30mm,10mm] } } Box Body { size: [30mm,10mm,6mm] } }";
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("profile-layout-placement-unsupported", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Rect2")]
    [InlineData("Circle2")]
    [InlineData("Slot2")]
    [InlineData("RoundedRect2")]
    public void CreateProducesAnOrdinaryEditableProfile(string shape)
    {
        const string source = "Model NewPart { units: mm }";
        var edited = FirmamentSketchService.Edit(source, "part.firmament", "0", new("create", "NewPart", shape));
        var view = FirmamentSketchService.Project(edited.Text, "part.firmament", "1");
        Assert.Empty(view.Diagnostics);
        Assert.Single(view.Loops);
        Assert.Equal(shape, Assert.Single(view.Shapes).Kind);
        Assert.Empty(view.EmptyModels);
        var compiled = FirmamentBuildAndExport.CompileSource(edited.Text);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics));
    }

    [Fact]
    public void InvalidRadiusAndDrivenCoordinatesAreRefusedWithoutSourceMutation()
    {
        var source = FirmamentSketchService.Edit("Model Part { units: mm }", "p.firmament", "0", new("create", "Part", "RoundedRect2")).Text;
        var view = FirmamentSketchService.Project(source, "p.firmament", "1");
        var radius = view.Shapes.Single().Controls.Single(c => c.Label.Equals("radius", StringComparison.OrdinalIgnoreCase));
        Assert.Throws<FirmamentLanguageEditException>(() => FirmamentSketchService.Edit(source, "p.firmament", "1",
            new("dimensions", view.Node, Changes: [new(radius.Id, 100)])));
        source = Fixture("plate.firmament");
        view = FirmamentSketchService.Project(source, "p.firmament", "1");
        var x = view.Shapes.Single(s => s.Name == "MountA").Controls.Single(c => c.Label == "Center X");
        Assert.Throws<FirmamentLanguageEditException>(() => FirmamentSketchService.Edit(source, "p.firmament", "1",
            new("dimensions", view.Node, Changes: [new(x.Id, 0)])));
    }
}
