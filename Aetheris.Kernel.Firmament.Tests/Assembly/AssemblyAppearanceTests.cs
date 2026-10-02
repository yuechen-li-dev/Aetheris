using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyAppearanceTests
{
    private const string Source = """
        Appearance Bare { Color: [0.5,0.5,0.5]; Metallic: 1; Roughness: 0.2; }
        Material Steel { Identity: "steel"; Appearance: Bare; }
        Template<H: Length> Struct Pad {
         Rect2 R { Center: [0mm,0mm]; Size: [10mm,10mm] }
         Profile P { Loop Outer { R |> TraceLoop } }
         Extrude Body { Profile: P From: 0mm To: H }
        }
        Appearance Paint { Color: [0.9,0.8,0.6]; Metallic: 0; Roughness: 0.3; }
        Assembly Product {
         <Assembly Product>
          <Part A = Pad<H:2mm>> Material: Steel; Placement { From: Origin; To: World; } </Part>
          <Part B = Pad<H:2mm>> Material: Steel with { Appearance: Paint; }; Placement { From: Origin; To: World; TranslateLocal: [20mm,0mm,0mm]; } </Part>
         </Assembly>
         Anchor: Product;
        }
        """;

    [Fact]
    public void PhysicalIdentitySurvivesPaintOverrideAndSharedGeometry()
    {
        var result = new AssemblyM1Pipeline().Compile(Source);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        var parts = result.Ir!.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part).ToArray();
        Assert.All(parts, p => Assert.Equal("steel", p.Appearance!.PhysicalIdentity));
        Assert.Equal("physical-material-default", parts[0].Appearance!.Authority);
        Assert.Equal("with-appearance-override", parts[1].Appearance!.Authority);
        Assert.Single(result.Geometry!.DefinitionBodies);
        var usd = AssemblyUsdExporter.Export(result);
        Assert.Contains("userProperties:aetheris_material = \"steel\"", usd);
        Assert.Contains("bindMaterialAs = \"strongerThanDescendants\"", usd);
        Assert.Contains("userProperties:aetheris_appearance = \"Paint\"", usd);
    }

    [Fact]
    public void LookOnlyEditOfDifferentLengthReusesGeometryAndRefreshesBinding()
    {
        using var session = new FirmamentCompilationSession();
        var before = session.Compile(Source);
        var after = session.Compile(Source.Replace("Roughness: 0.3", "Roughness: 0.125"));
        Assert.True(before.IsSuccess && after.IsSuccess, string.Join("\n", after.Diagnostics));
        Assert.Equal(1, after.Reuse!.ReusedDefinitions);
        Assert.Equal(0, after.Reuse.RebuiltDefinitions);
        Assert.Equal(before.Geometry!.Artifact.DeterministicSha256, after.Geometry!.Artifact.DeterministicSha256);
        Assert.Equal(.125, after.Ir!.Instances.Single(i => i.Path.ToString() == "Product.B").Appearance!.Preview.Roughness);
    }

    [Theory]
    [InlineData("Appearance: Paint", "Appearance: Missing", "unknown-appearance")]
    [InlineData("Material: Steel;", "Material: Missing;", "unknown-material")]
    [InlineData("Metallic: 1;", "Metallic: 2;", "values-invalid")]
    [InlineData("Material: Steel;", "Material: Steel with { Color: [1,0,0]; };", "selection-invalid")]
    public void InvalidAppearanceFailsClosed(string from, string to, string code)
    {
        var result = new AssemblyM1Pipeline().Compile(Source.Replace(from, to));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code.Contains(code));
    }
}
