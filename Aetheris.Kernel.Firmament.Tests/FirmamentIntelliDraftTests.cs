using Aetheris.Kernel.Firmament.Analysis;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public class FirmamentIntelliDraftTests
{
    [Fact]
    public void RetainedRectanglesResolveTheirSharedDimensionsThroughNormalCompilation()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        var source = File.ReadAllText(Path.Combine(root!.FullName, "fixtures", "Regression", "IntelliDraft", "rectangles-l.firmament"));
        var view = FirmamentIntelliDraft.Project(source, "rectangles.firmament");
        Assert.Empty(view.Diagnostics);
        Assert.Equal(2, view.Shapes.Count);
        var resolved = ProfileAuthoringParser.ResolveNamedProfile(source, "section", out var diagnostics);
        Assert.True(resolved is not null, string.Join(";", diagnostics));
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4416, new FirmamentMeasurements(result.Value).Volume().ValueMm3!.Value, 6);
    }
    private sealed class Draft
    {
        public string Source = "Model Part { units: mm }";
        public FirmamentDraftConstruction View => FirmamentIntelliDraft.Project(Source, "draft.firmament");
        public string Id(string name) => View.Elements.Single(e => e.Name == name).Id;
        public void Add(FirmamentDraftOperation operation) => Source = FirmamentSketchService.Edit(Source, "draft.firmament", "0", new("construction", Construction: operation)).Text;
    }

    [Fact]
    public void RectangleRelationshipsAndTracesSurviveUpstreamDimensionChanges()
    {
        var draft = new Draft(); draft.Add(new("layout", Name: "layout"));
        draft.Add(new("rectangle", draft.Id("layout"), "outer", Width: 60, Height: 40));
        draft.Add(new("rectangle", draft.Id("layout"), "notch", Reference: draft.Id("outer"), InsetX: 8));
        Assert.Equal(2, draft.View.Shapes.Count);
        Assert.DoesNotContain("Point2", draft.Source); Assert.DoesNotContain("Line2", draft.Source);
        FirmamentDraftTrace Span(string reference, string from, string to)
        {
            Assert.Contains(draft.View.Spans, s => s.Reference == reference && s.From == from && s.To == to);
            return new(reference, from, to, Connect: true);
        }
        var trace = new[] {
            Span("outer.Bottom", "outer.BottomLeft", "outer.BottomRight"),
            Span("outer.Right", "outer.BottomRight", "notch.BottomRight"),
            Span("notch.Bottom", "notch.BottomLeft", "notch.BottomRight"),
            Span("notch.Left", "notch.TopLeft", "notch.BottomLeft"),
            Span("outer.Top", "notch.TopLeft", "outer.TopLeft"),
            Span("outer.Left", "outer.TopLeft", "outer.BottomLeft") };
        draft.Add(new("trace", draft.Id("layout"), "section", Trace: trace));
        draft.Add(new("extrude", draft.Id("section"), "body", Length: 6));
        GeometryMatches(draft.Source, "rectangles-l.firmament");
        var before = FirmamentSketchService.Project(draft.Source, "draft.firmament", "0").Loops[0].Curves.Select(c => c.Id).ToArray();
        var width = draft.View.Shapes.Single(s => s.Name == "outer").Controls.Single(c => c.Label == "Width");
        draft.Source = FirmamentSketchService.Edit(draft.Source, "draft.firmament", "1", new("constructionDimensions", Changes: [new(width.Id, 80)])).Text;
        Assert.Equal(72, draft.View.Shapes.Single(s => s.Name == "notch").Controls.Single(c => c.Label == "Width").Value);
        draft.Add(new("parameter", draft.Id("notchWall"), Value: 10));
        var built = FirmamentBuildAndExport.CompileSource(draft.Source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(6600, new FirmamentMeasurements(built.Value).Volume().ValueMm3!.Value, 6);
        Assert.Equal(before, FirmamentSketchService.Project(draft.Source, "draft.firmament", "2").Loops[0].Curves.Select(c => c.Id));
        var previous = draft.Source;
        Assert.Throws<FirmamentLanguageEditException>(() => draft.Add(new("parameter", draft.Id("notchWall"), Value: 100)));
        Assert.Equal(previous, draft.Source);
        draft.Add(new("trace", draft.Id("layout"), "section", Trace: trace, TargetProfile: draft.Id("section")));
        Assert.True(FirmamentBuildAndExport.CompileSource(draft.Source).IsSuccess);
    }

    [Fact]
    public void WholeRectangleTracingRetainsTheShapeAndDoesNotInventPoints()
    {
        var draft = new Draft(); draft.Add(new("layout", Name: "layout", Plane: "XZ"));
        draft.Add(new("rectangle", draft.Id("layout"), "plate", Width: 60, Height: 40));
        draft.Add(new("trace", draft.Id("layout"), "section", Trace: [new("plate", WholeLoop: true)]));
        draft.Add(new("extrude", draft.Id("section"), "body", Length: 6));
        var built = FirmamentBuildAndExport.CompileSource(draft.Source); Assert.True(built.IsSuccess);
        Assert.Equal([-30d, -6, -20], new FirmamentMeasurements(built.Value).Bounds!.MinimumMm);
        Assert.Equal([30d, 0, 20], new FirmamentMeasurements(built.Value).Bounds!.MaximumMm);
        Assert.Contains("Rect2 plate", draft.Source); Assert.Contains("plate |> TraceLoop", draft.Source);
        Assert.DoesNotContain("Line2", draft.Source);
    }

    [Fact]
    public void NamedLineConstructionTracesAnLWithoutFlatteningReferences()
    {
        var draft = new Draft();
        draft.Add(new("layout", Name: "layout"));
        var points = new[] { ("a", 0d, 0d), ("b", 60d, 0d), ("c", 60d, 8d), ("d", 8d, 8d), ("e", 8d, 40d), ("f", 0d, 40d) };
        foreach (var (name, x, y) in points) draft.Add(new("point", draft.Id("layout"), name, X: x, Y: y));
        var names = new[] { "bottom", "right", "ledge", "upright", "top", "left" };
        for (var i = 0; i < 6; i++) draft.Add(new("line", draft.Id("layout"), names[i], From: points[i].Item1, To: points[(i + 1) % 6].Item1));
        Assert.Equal("ConstructionResolvedNotTraced", draft.View.ValidationLevel);
        Assert.Equal(6, draft.View.Guides.Count);
        Assert.False(FirmamentBuildAndExport.CompileSource(draft.Source).IsSuccess);
        draft.Add(new("trace", draft.Id("layout"), "section", Trace: names.Select(n => new FirmamentDraftTrace(n)).ToArray()));
        draft.Add(new("extrude", draft.Id("section"), "body", Length: 6));
        var built = FirmamentBuildAndExport.CompileSource(draft.Source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        Assert.Equal(4416, new FirmamentMeasurements(built.Value).Volume().ValueMm3!.Value, 6);
        Assert.Contains("from: a", draft.Source);
        Assert.Contains("bottom |> right |> ledge", draft.Source);
        Assert.All(FirmamentSketchService.Project(draft.Source, "draft.firmament", "1").Loops[0].Curves, c => Assert.NotNull(c.Provenance));
        GeometryMatches(draft.Source, "named-lines-l.firmament");
        var side = FirmamentBuildAndExport.CompileSource(draft.Source.Replace("on XY", "on XZ"));
        Assert.True(side.IsSuccess);
        Assert.Equal([0d, -6, 0], new FirmamentMeasurements(side.Value).Bounds!.MinimumMm);
        Assert.Equal([60d, 0, 40], new FirmamentMeasurements(side.Value).Bounds!.MaximumMm);
    }

    [Fact]
    public void ArcChordAndPathUseTheSameResolvedGeometryAsCode()
    {
        var draft = new Draft();
        draft.Add(new("layout", Name: "layout"));
        foreach (var (name, y) in new[] { ("center", 0d), ("lower", -15d), ("upper", 15d) })
            draft.Add(new("point", draft.Id("layout"), name, Y: y));
        draft.Add(new("circle", draft.Id("layout"), "rim", Radius: 15, From: "center"));
        draft.Add(new("line", draft.Id("layout"), "chord", From: "upper", To: "lower"));
        draft.Add(new("trace", draft.Id("layout"), "section", Trace: [new("rim", "lower", "upper", "arc"), new("chord")]));
        draft.Add(new("extrude", draft.Id("section"), "body", Length: 6));
        var result = FirmamentBuildAndExport.CompileSource(draft.Source);
        Assert.True(result.IsSuccess, string.Join(";", result.Diagnostics.Select(d => d.Message)));
        var dView = FirmamentSketchService.Project(draft.Source, "draft.firmament", "1");
        var arc = Assert.Single(dView.Loops[0].Curves, c => c.Kind == "Arc");
        Assert.Equal(15, arc.Radius);
        Assert.Equal(Math.PI, arc.Sweep, 10);
        Assert.Equal(4, new FirmamentMeasurements(result.Value).Topology.Faces);
        GeometryMatches(draft.Source, "arc-chord-d.firmament");

        var path = new Draft();
        path.Add(new("path", Name: "outline"));
        path.Add(new("step", path.Id("outline"), "bottom", "Line", Length: 40));
        path.Add(new("step", path.Id("outline"), "right", "Line", Length: 20, Turn: 90));
        path.Add(new("step", path.Id("outline"), "corner", "Arc", Radius: 10, Turn: 90));
        path.Add(new("step", path.Id("outline"), "top", "Line", Length: 30));
        path.Add(new("step", path.Id("outline"), "left", "Close"));
        path.Add(new("trace", path.Id("outline"), "section"));
        path.Add(new("extrude", path.Id("section"), "body", Length: 2));
        var built = FirmamentBuildAndExport.CompileSource(path.Source);
        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Message)));
        var pathView = FirmamentSketchService.Project(path.Source, "draft.firmament", "1");
        Assert.Equal(5, pathView.Loops[0].Curves.Count);
        var corner = Assert.Single(pathView.Loops[0].Curves, c => c.Kind == "Arc");
        Assert.Equal(10, corner.Radius);
        Assert.Equal(Math.PI / 2, corner.Sweep, 10);
        Assert.Equal(7, new FirmamentMeasurements(built.Value).Topology.Faces);
        GeometryMatches(path.Source, "line-arc-path.firmament");
    }

    [Fact]
    public void FailedTraceAndInvalidInputDoNotChangeConstruction()
    {
        var draft = new Draft(); draft.Add(new("path", Name: "outline"));
        draft.Add(new("step", draft.Id("outline"), "bottom", "Line"));
        var before = draft.Source;
        Assert.Throws<FirmamentLanguageEditException>(() => draft.Add(new("trace", draft.Id("outline"), "section")));
        Assert.Equal(before, draft.Source);
        Assert.Throws<FirmamentLanguageEditException>(() => draft.Add(new("step", draft.Id("outline"), "bad", "Arc", Radius: -2)));
        Assert.Throws<FirmamentLanguageEditException>(() => draft.Add(new("path", Name: "bad } Model Inject {")));
    }

    [Fact]
    public void PointAndPathDimensionsEditUpstreamWithoutFlatteningReferences()
    {
        var draft = new Draft(); draft.Add(new("layout", Name: "layout"));
        draft.Add(new("point", draft.Id("layout"), "a"));
        draft.Add(new("point", draft.Id("layout"), "b", X: 40));
        draft.Add(new("line", draft.Id("layout"), "edge", From: "a", To: "b"));
        draft.Add(new("movePoint", draft.Id("b"), X: 50));
        Assert.Equal(50, draft.View.Guides.Single().Points[1][0]);
        Assert.Contains("to: b", draft.Source);
        draft.Add(new("path", Name: "outline"));
        draft.Add(new("step", draft.Id("outline"), "bottom", "Line"));
        draft.Add(new("dimension", draft.Id("bottom"), Field: "length", Value: 60));
        Assert.Equal(60, draft.View.Guides.Single(g => g.Name == "outline.bottom").Points[1][0]);
        var before = draft.Source;
        Assert.Throws<FirmamentLanguageEditException>(() => draft.Add(new("dimension", draft.Id("bottom"), Field: "length", Value: -2)));
        Assert.Equal(before, draft.Source);
    }

    private static void GeometryMatches(string authored, string fixture)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var reference = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Path.Combine(root.FullName, "fixtures", "Regression", "IntelliDraft", fixture)));
        var actual = FirmamentBuildAndExport.CompileSource(authored);
        Assert.True(reference.IsSuccess); Assert.True(actual.IsSuccess);
        var a = new FirmamentMeasurements(actual.Value); var b = new FirmamentMeasurements(reference.Value);
        Assert.Equal(b.Topology, a.Topology);
        Assert.Equal(b.Body.Geometry.Curves.Select(c => c.Value.ToString()).Order(), a.Body.Geometry.Curves.Select(c => c.Value.ToString()).Order());
        Assert.Equal(b.Body.Geometry.Surfaces.Select(c => c.Value.ToString()).Order(), a.Body.Geometry.Surfaces.Select(c => c.Value.ToString()).Order());
        Assert.Equal(b.Body.Topology.Vertices.Select(v => b.Body.TryGetVertexPoint(v.Id, out var p) ? p.ToString() : "missing").Order(),
            a.Body.Topology.Vertices.Select(v => a.Body.TryGetVertexPoint(v.Id, out var p) ? p.ToString() : "missing").Order());
    }
}
