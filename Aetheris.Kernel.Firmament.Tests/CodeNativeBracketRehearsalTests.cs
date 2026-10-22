using System.Text;
using System.Text.Json;
using Aetheris.CodeNativeBracket;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class CodeNativeBracketRehearsalTests
{
    [Fact]
    public void TypedFamily_ExportsNominalAndResizedPartThroughRealPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "aetheris-bracket-" + Guid.NewGuid().ToString("N"));
        try
        {
            var specifications = new[] { new BracketDimensions(), new BracketDimensions(100, 75, 25, 24, 15, 70, 12, 8, 14) };
            foreach (var (spec, index) in specifications.Select((spec, i) => (spec, i)))
            {
                var output = Path.Combine(root, index.ToString());
                Aetheris.CodeNativeBracket.Program.MakeBracket(spec, output);
                var step = File.ReadAllText(Path.Combine(output, "Bracket.step"));
                Assert.True(Step242Importer.ImportBody(step).IsSuccess);
                Assert.Equal(2, Step242SemanticPmiInspector.Inspect(step).Items.Count);
                var glb = File.ReadAllBytes(Path.Combine(output, "Bracket.glb"));
                Assert.Equal("glTF", Encoding.ASCII.GetString(glb, 0, 4));
                using var json = JsonDocument.Parse(glb.AsMemory(20, BitConverter.ToInt32(glb, 12)));
                Assert.Equal(2, json.RootElement.GetProperty("asset").GetProperty("extras").GetProperty("pmi").GetArrayLength());
                Assert.Equal("%PDF-", Encoding.ASCII.GetString(File.ReadAllBytes(Path.Combine(output, "Bracket.pdf")), 0, 5));
                var svg = File.ReadAllText(Path.Combine(output, "Bracket.svg"));
                Assert.Contains("O (X0, Y0)", svg); Assert.Contains("CSK", svg);
                Assert.Contains("Process capability / inspection approval pending.", svg);
                using var drawing = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "Bracket.drawing.json")));
                var top = drawing.RootElement.GetProperty("Pages")[0].GetProperty("Views").EnumerateArray().Single(v => v.GetProperty("Identity").GetString() == "TOP");
                Assert.Equal(2, top.GetProperty("HiddenLinePolicy").GetInt32()); // actual Wireframe, not a silently substituted visibility policy
                var source = File.ReadAllText(Path.Combine(output, "bracket.firmament"));
                var rebuilt = FirmamentBuildAndExport.CompileSource(source);
                Assert.True(rebuilt.IsSuccess);
                Assert.Equal(step, rebuilt.Value.StepText);
            }
            Assert.NotEqual(File.ReadAllBytes(Path.Combine(root, "0", "Bracket.step")), File.ReadAllBytes(Path.Combine(root, "1", "Bracket.step")));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void LimitsThatPermitBreakout_AreRejectedBeforeArtifacts()
    {
        var output = Path.Combine(Path.GetTempPath(), "aetheris-bracket-" + Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidOperationException>(() => Aetheris.CodeNativeBracket.Program.MakeBracket(new(HoleY: 6.1), output));
        Assert.False(Directory.Exists(output));
    }
}
