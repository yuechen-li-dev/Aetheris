using Aetheris.Forge.Abstractions.FirmamentInterop;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.FirmamentInterop;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFrontendX0Tests
{
    [Theory]
    [InlineData("malformed-v2", "UnifiedMechanical")]
    [InlineData("unknown-model-statement", "UnifiedMechanical")]
    [InlineData("unknown-modify-statement", "UnifiedMechanical")]
    [InlineData("unknown-box-field", "UnifiedMechanical")]
    [InlineData("unsupported-xz-layout", "UnifiedMechanical")]
    public void InvalidSource_IsRejectedByParserReportAndEngineeringCompiler(string fixture, string route)
    {
        var source = Read(fixture);
        var parse = FirmamentV2Parser.Parse(source);
        var build = FirmamentBuildAndExport.CompileSource(source);
        var report = FirmamentV2ValidationReportBuilder.Build(parse, fixture);
        Assert.False(parse.IsSuccess);
        Assert.Equal(FirmamentV2ParseDisposition.RecognizedInvalid, parse.Disposition);
        Assert.Equal(route, parse.ParserRoute);
        Assert.False(build.IsSuccess);
        Assert.Equal("invalid", report.Status);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Severity == "fatal");
        Assert.DoesNotContain(build.Diagnostics, diagnostic => diagnostic.Message.Contains("TOON", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("combined-fillet-hole-intersection")]
    [InlineData("unresolved-semantic-source")]
    [InlineData("edgefinish-generic-variant")]
    public void BuildAdmission_RejectsParsedButUnbuildablePrograms(string fixture)
    {
        var source = Read(fixture);
        var parse = FirmamentV2Parser.Parse(source);
        Assert.True(parse.IsSuccess, string.Join("; ", parse.Diagnostics));
        var report = FirmamentBuildAdmissionValidation.Admit(FirmamentV2ValidationReportBuilder.Build(parse), source);
        Assert.Equal("invalid", report.Status);
        Assert.Equal("BuildRejected", report.ValidationLevel);
        Assert.True(report.Summary.FatalDiagnosticCount > 0);
        Assert.False(FirmamentBuildAndExport.CompileSource(source).IsSuccess);
    }

    [Fact]
    public void XzBracket_CannotSilentlyEmitTheXyWitness()
    {
        var xz = Read("unsupported-xz-layout");
        var xy = xz.Replace("On XZ", "On XY", StringComparison.Ordinal);
        var accepted = FirmamentBuildAndExport.CompileSource(xy);
        Assert.True(accepted.IsSuccess, string.Join("; ", accepted.Diagnostics.Select(d => d.Message)));
        var imported = Step242Importer.ImportBody(accepted.Value.StepText);
        Assert.True(imported.IsSuccess);
        var points = imported.Value.Topology.Vertices.Select(vertex =>
        {
            Assert.True(imported.Value.TryGetVertexPoint(vertex.Id, out var point));
            return point;
        }).ToArray();
        Assert.Equal(60d, points.Max(point => point.X) - points.Min(point => point.X), 8);
        Assert.Equal(30d, points.Max(point => point.Y) - points.Min(point => point.Y), 8);
        Assert.Equal(3d, points.Max(point => point.Z) - points.Min(point => point.Z), 8);
        Assert.Equal(8, imported.Value.Topology.Faces.Count());
        Assert.False(FirmamentBuildAndExport.CompileSource(xz).IsSuccess);
        Assert.False(FirmamentBuildAndExport.CompileSource(xy.Replace("Using Layout", "Using MissingFrame", StringComparison.Ordinal)).IsSuccess);
    }

    [Fact]
    public void UnknownField_HasAnOriginalTokenSpan_AndLanguageServiceMarker()
    {
        var source = Read("unknown-box-field");
        var report = FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source), "field.firmament");
        var diagnostic = Assert.Single(report.Diagnostics, diagnostic => diagnostic.Code.StartsWith(FirmamentV2Parser.CanonicalFieldUnknown + ":", StringComparison.Ordinal));
        Assert.Equal("heigth", source.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Equal("field.firmament", diagnostic.SourceFile);
        Assert.True(diagnostic.Line > 0);
        Assert.Null(diagnostic.LocationNote);
        var marker = Assert.Single(FirmamentLanguageAnalysisService.Analyze(source, "field.firmament", "1").Diagnostics,
            diagnostic => diagnostic.Code.StartsWith(FirmamentV2Parser.CanonicalFieldUnknown + ":", StringComparison.Ordinal));
        Assert.Equal("heigth", source.Substring(marker.Start, marker.Length));
    }

    [Fact]
    public void SchemaSelection_PreservesOriginalDiagnosticOffsets()
    {
        var original = "schema Mechanical\n" + Read("unknown-box-field");
        var selection = FirmamentFrontendSchemas.Select(original);
        Assert.Equal(original.Length, selection.Source.Length);
        var report = FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(selection.Source));
        var diagnostic = Assert.Single(report.Diagnostics, diagnostic => diagnostic.Code.StartsWith(FirmamentV2Parser.CanonicalFieldUnknown + ":", StringComparison.Ordinal));
        Assert.Equal("heigth", original.Substring(diagnostic.SourceSpan!.Start, diagnostic.SourceSpan.Length));
        Assert.Equal(4, diagnostic.Line);
    }

    [Fact]
    public void SemanticSourceFailure_PreservesTheMissingSourceAndExplanation()
    {
        var result = FirmamentBuildAndExport.CompileSource(Read("unresolved-semantic-source"));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("No provenance source for:", StringComparison.Ordinal)
            && diagnostic.Message.Contains("AbsentEdge", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownDiagnostic_IsAnError_WhileDeclaredWarningAndTraceRemainNonfatal()
    {
        Assert.Equal(FirmamentDiagnosticSeverity.Fatal, FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode("future-owner-new-error").Severity);
        Assert.Equal(FirmamentDiagnosticSeverity.Warning, FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode(FirmamentV2Parser.ToleranceDroppedThroughArithmetic).Severity);
        Assert.Equal(FirmamentDiagnosticSeverity.Info, FirmamentV2DiagnosticAdapter.FromParserDiagnosticCode("firmament-concept-ir-resolved").Severity);
        var report = FirmamentV2ValidationReportBuilder.Build(FirmamentV2ParseResult.Failure(["future-owner-new-error"]));
        Assert.Equal("invalid", report.Status);
        var emptyFailure = FirmamentV2ValidationReportBuilder.Build(FirmamentV2ParseResult.Failure([]));
        Assert.Equal("invalid", emptyFailure.Status);
        Assert.Equal(1, emptyFailure.Summary.FatalDiagnosticCount);
    }

    internal static string Read(string fixture) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
        "../../../../fixtures/Regression/FrontendX0", fixture + ".firmament"));

    [Theory]
    [InlineData("Model")]
    [InlineData("Template")]
    public void ExplicitV1_DataKeywordsCannotSelectTheV2Frontend(string modelName)
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "../../../../fixtures/Compatibility/LegacyV1/Examples/box_basic.firmament"))
            .Replace("name: box_basic", "name: " + modelName, StringComparison.Ordinal);
        var parsed = FirmamentV2Parser.Parse(source);
        Assert.Equal(FirmamentV2ParseDisposition.NotRecognized, parsed.Disposition);
        Assert.Equal("LegacySerialization", parsed.ParserRoute);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".firmament");
        var output = Path.ChangeExtension(path, ".step");
        try
        {
            File.WriteAllText(path, source);
            var built = FirmamentBuildAndExport.Run(path, output);
            Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics.Select(diagnostic => diagnostic.Message)));
        }
        finally { File.Delete(path); File.Delete(output); }
    }
}
