using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyFrameValueTests
{
    private const string Source = """
        Record Site { x: Length; y: Length; up: Float }
        Static site: Site = Site { x: 12mm; y: 7mm; up: 1 }
        Assembly Demo {
          <Assembly Demo>
            <Part Post = Panel<L:10mm,W:5mm,H:2mm,R:0mm>>
              Placement { from: Origin; to: World; translateLocal: [site.x,site.y,0mm]; normal: [0,0,site.up]; up: [0,site.up,0]; }
            </Part>
          </Assembly>
          Anchor: Demo;
        }
        """;

    [Fact]
    public void TypedRecordFieldsReachOrdinaryFramePlacement()
    {
        var parsed = new AssemblyM0Parser().Parse(Source);
        Assert.True(parsed.IsSuccess, string.Join("\n", parsed.Diagnostics.Select(d => d.Message)));
        var compiled = new AssemblyM0Compiler().Compile(parsed.Source!);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var post = Assert.Single(compiled.Ir!.Instances, i => i.Kind == AssemblyInstanceKind.Part);
        Assert.Equal(12, post.ResolvedTransform!.Matrix[12], 9);
        Assert.Equal(7, post.ResolvedTransform.Matrix[13], 9);
    }

    [Fact]
    public void MistypedRecordFieldRejectsBeforeFrameLowering()
    {
        var parsed = new AssemblyM0Parser().Parse(Source.Replace("x: 12mm", "x: 12deg"));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, d => d.Message.Contains("record-field-type-mismatch", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("12mm", true)]
    [InlineData("12", true)]
    [InlineData("12deg", false)]
    public void SemanticFrameOriginPreservesLengthAndLegacyUnits(string x, bool accepted)
    {
        var source = $$"""
            Assembly Demo {
              <Assembly Demo>
                Semantic Seat { DatumFrame Frame = [{{x}},0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
              </Assembly>
              Anchor: Demo;
            }
            """;
        var parsed = new AssemblyM0Parser().Parse(source);
        Assert.Equal(accepted, parsed.IsSuccess);
        if (!accepted) Assert.Contains(parsed.Diagnostics, d => d.Code == "assembly-datum-frame-invalid");
        else {
            var semantic = Assert.Single(parsed.Source!.Root.ExposedSemantics);
            Assert.Equal("Seat", semantic.ExposedName);
        }
    }

    [Fact]
    public void DescendantPortsStayOwnedAndDuplicateAssemblyPortsReject()
    {
        const string source = """
            Assembly Demo {
              <Assembly Demo>
                <Part Post = Panel<L:10mm,W:5mm,H:2mm,R:0mm>>
                  Semantic Mount { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }
                </Part>
              </Assembly>
              Anchor: Demo;
            }
            """;
        var child = new AssemblyM0Parser().Parse(source);
        Assert.True(child.IsSuccess);
        Assert.Empty(child.Source!.Root.ExposedSemantics);
        const string port = "Semantic Seat { DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }";
        var duplicate = new AssemblyM0Parser().Parse(source.Replace("<Assembly Demo>", "<Assembly Demo>" + port + port));
        Assert.False(duplicate.IsSuccess);
        Assert.Contains(duplicate.Diagnostics, d => d.Code == "assembly-semantic-duplicate-name");
    }

    [Fact]
    public void CamelCaseWireFieldsLowerToTheSameCoilAir()
    {
        const string source = """
            WireForm Spring {
              Diameter: 2mm
              StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [1,0,0]; Up: [0,0,1] }
              Helix Winding { Radius: 9mm; Turns: 1; Pitch: 6mm; Handedness: RightHanded }
            }
            """;
        var lower = source.Replace("Diameter:","diameter:").Replace("Origin:","origin:").Replace("Tangent:","tangent:")
            .Replace("Up:","up:").Replace("Radius:","radius:").Replace("Turns:","turns:").Replace("Pitch:","pitch:").Replace("Handedness:","handedness:");
        var before = WireFormAuthoring.Parse(source); var after = WireFormAuthoring.Parse(lower);
        Assert.True(before.IsSuccess); Assert.True(after.IsSuccess);
        var expected = Assert.IsType<WireAxisCoilAir>(Assert.Single(before.Value!.Operations));
        var actual = Assert.IsType<WireAxisCoilAir>(Assert.Single(after.Value!.Operations));
        Assert.Equal(expected, actual);
    }
}
