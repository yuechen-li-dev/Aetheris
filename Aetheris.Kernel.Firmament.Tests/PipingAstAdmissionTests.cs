using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;
using Aetheris.Kernel.Firmament.Piping;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class PipingAstAdmissionTests
{
    [Theory]
    [InlineData("pipe-segment")]
    [InlineData("elbow90")]
    [InlineData("explicit-route")]
    [InlineData("route-around-keepout")]
    [InlineData("autorouted-connection")]
    [InlineData("local-reroute")]
    [InlineData("pump-skid")]
    [InlineData("tee")]
    public void PreferredAndCompatibilityFieldsPreserveExactGeometryAndSemantics(string name)
    {
        var legacy = File.ReadAllText(Path.Combine(Root(), "fixtures/Compatibility/FirmamentV2/Piping", name + ".firmament"));
        var modern = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Piping", name + ".firmament"));
        var a = PipingAuthoring.Compile(legacy); var b = PipingAuthoring.Compile(modern);
        Assert.True(a.IsSuccess, string.Join('\n', a.Diagnostics));
        Assert.True(b.IsSuccess, string.Join('\n', b.Diagnostics));
        Assert.Equal(a.StepText, b.StepText);
        Assert.Equal(a.Report!.Ports, b.Report!.Ports);
        Assert.Equal(a.Report.Equipment, b.Report.Equipment);
        Assert.Equal(a.Report.Assembly.Definitions, b.Report.Assembly.Definitions);
        Assert.Equal(PipingAuthoring.RoutingJson(a.Report), PipingAuthoring.RoutingJson(b.Report));
        Assert.Equal(PipingAuthoring.BomJson(a.Report), PipingAuthoring.BomJson(b.Report));
        Assert.Equal(PipingAuthoring.CutListJson(a.Report), PipingAuthoring.CutListJson(b.Report));
        Assert.Equal(a.Report.Bounds, b.Report.Bounds);
        Assert.Equal(modern, FirmamentSourceSpelling.Prefer(modern));
        Assert.Equal(modern.ReplaceLineEndings("\n"), FirmamentSourceSpelling.Prefer(legacy).ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("PipePolicy", "pipe-segment")]
    [InlineData("Port", "pipe-segment")]
    [InlineData("Connection", "pipe-segment")]
    [InlineData("Route", "pipe-segment")]
    [InlineData("PipingSystem", "pipe-segment")]
    [InlineData("Model", "pipe-segment")]
    [InlineData("Equipment", "pump-skid")]
    [InlineData("KeepOut", "pump-skid")]
    [InlineData("RouteSpace", "pump-skid")]
    [InlineData("RouteRequest", "pump-skid")]
    [InlineData("LocalReroute", "local-reroute")]
    public void EveryOwnedScopeRejectsUnknownFieldsAtTheirActualLocation(string owner, string file)
    {
        var source = File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Piping", file + ".firmament"));
        var node = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).First(d => d.Type.Name.Text == owner);
        var insertion = node.Body!.ContentSpan.Start;
        var invalid = source.Insert(insertion, "\n unexpectedIntent: Weld;\n");
        var result = PipingAuthoring.Compile(invalid);
        Assert.False(result.IsSuccess); Assert.Null(result.StepText); Assert.Null(result.Report);
        var diagnostic = Assert.Single(result.FrontendDiagnostics, d => d.Code == FirmamentV2Parser.CanonicalFieldUnknown);
        Assert.Equal(invalid.IndexOf("unexpectedIntent", StringComparison.Ordinal), diagnostic.SourceSpan!.Start);
        Assert.Contains("unexpectedIntent", diagnostic.Message);
    }

    [Theory]
    [InlineData("provenance: Explicit", "provenance: Explcit")]
    [InlineData("connectionType: Weld", "connectionType: weld")]
    [InlineData("connectionType: Weld", "connectionType: 2")]
    [InlineData("connectionType: Weld", "connectionType: Weld; ConnectionType: Flange")]
    [InlineData("connectionType: Weld", "connectionType: Weld; nozzleLength: nope")]
    [InlineData("connectionType: Weld", "connectionType: Weld; nozzleLength: -1mm")]
    [InlineData("service: \"CoolingWater\"", "service: \"CoolingWater\"; reducer: yes")]
    [InlineData("bendRadius: 50mm", "bendRadius: 0mm")]
    [InlineData("bendRadius: 50mm", "bendRadius: 50mm; lockedSegments: [0, bad]")]
    [InlineData("bendRadius: 50mm", "bendRadius: 50mm; lockedSegments: [99]")]
    [InlineData("direction: [1,0,0]", "direction: [1mm,0mm,0mm]")]
    [InlineData("outerDiameter: 25mm", "outerDiameter: 25deg")]
    [InlineData("Port Inlet {", "Port Inlet on XZ {")]
    [InlineData("Port Inlet {", "Port Inlet { Fillet corners { radius: 2mm; }")]
    [InlineData("PipingSystem SegmentWitness {", "PipingSystem SegmentWitness { Fillet corners { radius: 2mm; }")]
    public void AuthoredOptionalIntentCannotBeDefaultedOrOmitted(string before, string after)
    {
        var source = Segment(); Assert.Contains(before, source);
        var result = PipingAuthoring.Compile(source.Replace(before, after, StringComparison.Ordinal));
        Assert.False(result.IsSuccess); Assert.Null(result.StepText); Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void DuplicateNamesAndMisnestedPortsRejectWithoutOwnerExceptions()
    {
        var source = Segment();
        var duplicate = PipingAuthoring.Compile(source.Replace("Port Outlet", "Port Inlet", StringComparison.Ordinal));
        Assert.False(duplicate.IsSuccess); Assert.Null(duplicate.StepText);
        var nested = PipingAuthoring.Compile(source.Replace("Port Inlet {", "Port Inlet { Port Extra { position: [1mm,2mm,3mm]; }", StringComparison.Ordinal));
        Assert.False(nested.IsSuccess); Assert.Null(nested.StepText);
    }

    [Fact]
    public void CommentsAndStringsCannotSelectPipingFrontend()
    {
        Assert.False(PipingAuthoring.IsPipingSource("// PipingSystem Imaginary { }\nModel Plate { units: mm }"));
        Assert.False(PipingAuthoring.IsPipingSource("Record Info { text: String } Static info: Info { text: \"PipingSystem Imaginary { }\"; }"));
    }

    [Fact]
    public void HistoricalOmittedUnitsAndBareLengthsPreserveGeometry()
    {
        var source = Segment();
        var legacy = source.Replace("units: mm", "", StringComparison.Ordinal).Replace("mm", "", StringComparison.Ordinal);
        var baseline = PipingAuthoring.Compile(source); var admitted = PipingAuthoring.Compile(legacy);
        Assert.True(admitted.IsSuccess, string.Join('\n', admitted.Diagnostics));
        Assert.Equal(baseline.StepText, admitted.StepText);
        var wrongUnit = PipingAuthoring.Compile(source.Replace("units: mm", "units: m", StringComparison.Ordinal));
        Assert.False(wrongUnit.IsSuccess); Assert.Null(wrongUnit.StepText);
    }

    [Fact]
    public void ProposalOnlyCompilationRejectsWithoutEmptyGeometryExceptions()
    {
        var source = Segment();
        var route = FirmamentSyntaxTraversal.Declarations(FirmamentSyntaxParser.Parse(source).Document).Single(d => d.Type.Name.Text == "Route");
        source = source.Remove(route.Span.Start, route.Span.Length).Insert(route.Span.Start, "RouteRequest Candidate { connection: Run; }");
        var result = PipingAuthoring.Compile(source);
        Assert.False(result.IsSuccess); Assert.Null(result.StepText);
        Assert.Contains(result.Diagnostics, d => d.StartsWith("piping-route-required", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Standard.NoSuchModule", "Tee")]
    [InlineData("Standard.Piping", "NoSuchProduct")]
    public void CatalogueBindingsRequireAUsedResolvedExport(string module, string product)
    {
        var source = $"Use {module}\nLet Product = {module}.{product}\n" + Segment();
        var result = PipingAuthoring.Compile(source);
        Assert.False(result.IsSuccess); Assert.Null(result.StepText);
        Assert.Contains(result.FrontendDiagnostics, d => d.SourceSpan is not null && d.Message.Contains(module, StringComparison.Ordinal));
    }

    [Fact]
    public void SharedRecordBindingConsumesDimensionsAndRejectsSchemaBypass()
    {
        const string data = """
            Record PipeStock { od: Length; wall: Length; }
            Static stock: PipeStock { od: 25mm; wall: 2mm; }

            """;
        var source = data + Segment().Replace("outerDiameter: 25mm", "outerDiameter: stock.od", StringComparison.Ordinal)
            .Replace("wallThickness: 2mm", "wallThickness: stock.wall", StringComparison.Ordinal);
        var bound = PipingAuthoring.Compile(source);
        Assert.True(bound.IsSuccess, string.Join('\n', bound.Diagnostics));
        Assert.Equal(PipingAuthoring.Compile(Segment()).StepText, bound.StepText);
        var invalid = PipingAuthoring.Compile(source.Replace("wall: 2mm", "wall: 2mm; unexpectedIntent: 3mm", StringComparison.Ordinal));
        Assert.False(invalid.IsSuccess); Assert.Null(invalid.StepText);
        Assert.NotEmpty(invalid.FrontendDiagnostics);
    }

    private static string Segment() => File.ReadAllText(Path.Combine(Root(), "fixtures/Canonical/Piping/pipe-segment.firmament"));
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException();
    }
}
