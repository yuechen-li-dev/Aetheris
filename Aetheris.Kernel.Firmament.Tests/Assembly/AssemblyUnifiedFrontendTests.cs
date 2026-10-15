using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.FirmamentV2.Syntax;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyUnifiedFrontendTests
{
    private static string Read(string relative) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/" + relative));
    private static string Source => Read("Regression/FrontendX1/assembly-modern-contract.firmament");

    [Fact]
    public void IncludedSyntaxErrorsRetainTheOriginalDocumentAndPreciseOffset()
    {
        var project = new FirmamentProjectSnapshot("root.firmasm", new Dictionary<string, string>
        {
            ["root.firmasm"] = "include \"bad.firmament\"; Assembly Root { <Assembly Root></Assembly> }",
            ["bad.firmament"] = "Interface<Axial> Contract { Role A requires ; }"
        });
        var result = new AssemblyM0Parser().ParseProject(project);
        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal("bad.firmament", diagnostic.SourceSpan!.Source);
        Assert.Equal(project.Documents["bad.firmament"].IndexOf(';'), diagnostic.SourceSpan.Start);
        Assert.Null(diagnostic.LocationNote);
        Assert.Contains("identifier", diagnostic.Message);
    }

    [Fact]
    public void ModernContractAndNamedMateBuildSharedGeometryAtTheRequestedWorldTransform()
    {
        var source = Source;
        var first = new AssemblyM1Pipeline().Compile(source, "unified-test");
        Assert.True(first.IsSuccess, string.Join("\n", first.Diagnostics.Select(d => d.Code + ": " + d.Message)));
        Assert.Equal(2, first.Geometry!.InstanceBodies.Count);
        var moving = Assert.Single(first.Ir!.Instances, i => i.Path.ToString() == "Pair.Moving");
        Assert.Equal(15d, moving.ResolvedTransform!.Matrix[12], 8);
        var compatibility = source.Replace("Interface<Fixed> Install { }", """
            Interface<Fixed> Install {
                Role A requires DatumFrameCapable;
                Role B requires DatumFrameCapable;
                Lower FrameCoincident A B;
            }
            """, StringComparison.Ordinal);
        var second = new AssemblyM1Pipeline().Compile(compatibility, "unified-test");
        Assert.True(second.IsSuccess, string.Join("\n", second.Diagnostics.Select(d => d.Message)));
        Assert.Equal(first.Ir.Instances.Select(i => i.StableId), second.Ir!.Instances.Select(i => i.StableId));
        Assert.Equal(moving.ResolvedTransform.Matrix, second.Ir.Instances.Single(i => i.Path.ToString() == "Pair.Moving").ResolvedTransform!.Matrix);
        var before = AssemblyIrAp242Exporter.Export(first); var after = AssemblyIrAp242Exporter.Export(second);
        Assert.True(before.IsSuccess); Assert.True(after.IsSuccess);
        Assert.Equal(before.Value, after.Value);
    }

    [Fact]
    public void RoleCapabilitiesRelationsAndNamedMatesRetainOriginalSyntaxSpans()
    {
        var source = Read("Canonical/Assembly/bearing-module.firmament");
        var parsed = FirmamentSyntaxParser.Parse(source, "bearing-module.firmament");
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics.Select(d => d.Message)));
        var role = FirmamentSyntaxEvidence.Statements(parsed.Document).OfType<RoleRequirementSyntax>().First();
        Assert.Equal(new[] { "AxisCapable", "DimensionalCapable" }, role.Capabilities.Select(c => c.Text));
        Assert.Equal("Role Shaft requires AxisCapable, DimensionalCapable", source.Substring(role.Span.Start, role.Span.Length));
        Assert.NotEmpty(FirmamentSyntaxEvidence.Statements(parsed.Document).OfType<InterfaceRelationSyntax>());
        Assert.NotEmpty(FirmamentSyntaxTraversal.Expressions(parsed.Document).OfType<ProvenanceSyntax>());
        // Normalizing selector links must not capitalize dimensional provenance's `from`.
        Assert.Contains("from \"HousingTable.H6204.SeatDepth\"", FirmamentSourceSpelling.Normalize(source));
        var compilation = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(compilation.IsSuccess, string.Join("\n", compilation.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(compilation.Ir!.DimensionalRelations);
    }

    [Theory]
    [InlineData("Role A requires ;")]
    [InlineData("Role A requires DatumFrameCapable; Role A requires DatumFrameCapable;")]
    [InlineData("Lower FrameCoincident Missing B;")]
    [InlineData("Lower InventedRelation A.Frame B.Frame;")]
    [InlineData("Lower FrameCoincident A B 2mm;")]
    [InlineData("Fit Missing.Diameter inside B.Diameter;")]
    [InlineData("Fillet Ignored { Radius: 2mm }")]
    [InlineData("heigth: 2mm")]
    [InlineData("Gap: 5deg")]
    public void InvalidContractIntentCannotDisappear(string clause)
    {
        var parsed = new AssemblyM0Parser().Parse(Source.Replace("Interface<Fixed> Install { }", "Interface<Fixed> Install { " + clause + " }"));
        Assert.False(parsed.IsSuccess);
        Assert.NotEmpty(parsed.Diagnostics);
        Assert.Contains(parsed.Diagnostics, d => d.Severity == AssemblyDiagnosticSeverity.Error && !string.IsNullOrWhiteSpace(d.Message));
    }

    [Theory]
    [InlineData("A: Pair.Fixed.Mount; A: Pair.Moving.Mount;")]
    [InlineData("A: 10mm; B: Pair.Moving.Mount;")]
    [InlineData("A: Pair.Fixed.Mount; Fillet Lost { Radius: 1mm }")]
    public void InvalidMateAssignmentsRejectBeforeGeometry(string assignments)
    {
        var source = Source.Replace("A: Pair.Fixed.Mount; B: Pair.Moving.Mount;", assignments);
        var result = new AssemblyM1Pipeline().Compile(source);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics, d => d.SourceSpan is { Length: > 0 });
    }

    [Theory]
    [InlineData("Interface<Fixed> Install { }", "Interface<12mm> Install { }")]
    [InlineData("Interface<Fixed> Install { }", "Interface<> Install { }")]
    [InlineData("Interface<Fixed> Install { }", "Interface<kind: Fixed> Install { }")]
    [InlineData("Interface<Fixed> Install { }", "Interface<Fixed> Install on XZ { }")]
    [InlineData("Mate Installation: Install {", "Mate Installation: Install on XZ {")]
    public void UnsupportedContractAndMateHeadersCannotDefaultPlacement(string from, string to)
    {
        var result = new AssemblyM1Pipeline().Compile(Source.Replace(from, to));
        Assert.False(result.IsSuccess);
        Assert.Null(result.Geometry);
        Assert.Contains(result.Diagnostics, d => d.Severity == AssemblyDiagnosticSeverity.Error);
        Assert.Contains(result.Diagnostics, d => d.SourceSpan is { Length: > 0 });
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Unknown Interface family 'Custom'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Canonical/AssemblyInterfaces/executable-machine.firmament")]
    [InlineData("Canonical/AssemblyInterfaces/AuthoringFoundation/product-notes.firmasm")]
    [InlineData("Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm")]
    [InlineData("Canonical/Drawings/bearing-block-production-drawing.firmament")]
    [InlineData("Canonical/FEA/inline-step-cantilever.firmament")]
    [InlineData("Canonical/SheetMetal/power-supply-enclosure.firmament")]
    [InlineData("Canonical/SheetMetal/network-appliance-enclosure.firmament")]
    public void CanonicalStructuralGapsAreClosed(string relative)
    {
        var parsed = FirmamentSyntaxParser.Parse(Read(relative), relative);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics.Select(d => $"{d.Line}:{d.Column} {d.Message}")));
    }
}
