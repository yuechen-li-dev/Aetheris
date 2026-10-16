using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFrontendX4Tests
{
    public static IEnumerable<object[]> CorpusCases()
    {
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "expected.json")));
        foreach (var item in manifest.RootElement.GetProperty("cases").EnumerateArray())
            yield return [item.GetProperty("file").GetString()!, item.GetProperty("expect").GetString() == "valid"];
    }

    [Theory]
    [MemberData(nameof(CorpusCases))]
    public void Retest_UsesTheSameAdmissionAndPreservesRecordedTopology(string file, bool accepted)
    {
        var source = File.ReadAllText(Path.Combine(Corpus, file));
        var parse = FirmamentV2Parser.Parse(source);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parse, file), source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.Equal(accepted, build.IsSuccess);
        Assert.Equal(accepted ? "valid" : "invalid", report.Status);
        if (!accepted) return;
        Assert.Equal("Built", report.ValidationLevel);
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(Corpus, "expected.json")));
        var geometry = manifest.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("file").GetString() == file).GetProperty("geometry");
        // This is newly compiled output, so a separate import is the test subject.
        var imported = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(imported.IsSuccess);
        Assert.Equal(geometry.GetProperty("faces").GetInt32(), imported.Value.Topology.Faces.Count());
        Assert.Equal(geometry.GetProperty("edges").GetInt32(), imported.Value.Topology.Edges.Count());
        Assert.Equal(geometry.GetProperty("vertices").GetInt32(), imported.Value.Topology.Vertices.Count());
    }

    [Theory]
    [InlineData("cases/f03-segment-inline-coordinates.firmament", "ProfileSegmentEndpointMustReferenceNamedPoint", 5)]
    public void RejectedIntent_PreservesItsOriginalConstruct(string file, string code, int line)
    {
        var source = File.ReadAllText(Path.Combine(Corpus, file));
        var build = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(build.IsSuccess);
        var presented = build.Diagnostics.Select(d => FirmamentBuildAdmissionValidation.Present(d, file, source)).ToArray();
        Assert.Contains(presented, d => d.Code == code && d.Line == line && d.SourceSpan is not null);
        Assert.DoesNotContain(presented, d => d.Code == "ConceptPathProfileNotAdmittedByCompose");
    }

    [Theory]
    [InlineData("Polygon2 outline { points: [[0mm,0mm],[20mm,0mm],[0mm,10mm]]; heigth: 5mm }")]
    [InlineData("Polygon2 outline { points: [[0mm,0mm],[20mm,0mm],[0mm,10mm]]; rotation: 15deg }")]
    [InlineData("Modify Body { Hole<Shaft> bore { on: Other.Top; center: [0mm,0mm]; diameter: 2mm; end: ThroughAll } }")]
    [InlineData("Modify Body { EdgeFinish<Chamfer> edge { face: +Z; target: Boundary; kind: Fillet; distance: 1mm } }")]
    [InlineData("Static sites: Set<Point2> { bad => [2mm, 1deg] }")]
    [InlineData("Static sites: Set<Point2> { bad => Point3(2mm, 1mm) }")]
    [InlineData("Modify Body { Hole<Shaft> bore { on: +Z; center: Point3(0mm,0mm); diameter: 2mm; end: ThroughAll } }")]
    public void NewlyAdmittedForms_DoNotHideUnknownOrConflictingIntent(string intent)
    {
        var source = "Model Test { units: mm Box Body { size: [40mm,30mm,5mm] } " + intent + " }";
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source)), source);
        Assert.Equal("invalid", report.Status);
    }

    [Fact]
    public void GenericProfileFinish_RejectsAnUnknownFieldBeforeEmittingPlausibleGeometry()
    {
        var source = File.ReadAllText(Path.Combine(Corpus, "cases/d01-edgefinish-generic-variant.firmament"));
        source = source.Replace("Distance: 1mm", "Distance: 1mm; distnce: 2mm", StringComparison.Ordinal);
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("distnce", StringComparison.Ordinal));
    }

    [Fact]
    public void StandaloneConstruction_ProjectsStaticValuesWithoutDependingOnASyntheticWrapperSpan()
    {
        var wrapped = File.ReadAllText(Path.Combine(Corpus, "cases/a05-static-in-compose-interval.firmament"));
        var standalone = wrapped[(wrapped.IndexOf('{') + 1)..wrapped.LastIndexOf('}')];
        var expected = FirmamentBuildAndExport.CompileSource(wrapped);
        var actual = FirmamentBuildAndExport.CompileSource(standalone);
        Assert.True(expected.IsSuccess);
        Assert.True(actual.IsSuccess, string.Join("; ", actual.Diagnostics.Select(d => d.Message)));
        Assert.Equal(expected.Value.StepText, actual.Value.StepText);
    }

    private static string Corpus => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/Regression/FrontendX4"));
}
