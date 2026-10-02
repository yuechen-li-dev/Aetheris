using System.IO.Compression;
using System.Xml.Linq;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Drawing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class DrawingPptxModel3DTests
{
    [Fact]
    public void CompilerEmbedsExactAssetsWithOffice2019SchemaAndOracleStructure()
    {
        using var temporary = new TemporaryDirectory();
        var source = Path.Combine(temporary.Path, "triangle.glb"); var preview = Path.Combine(temporary.Path, "preview.png");
        var display = new AssemblyDisplayMeshDocument("aetheris/assembly-display-mesh/1", "Triangle", "mm",
            [new("d", "Triangle", [0,0,0,10,0,0,0,10,0], [0,0,1,0,0,1,0,0,1], [0,1,2])],
            [new("o", "Triangle", null, "d", Transform3D.Identity.ToRowMajor())]);
        var glb = AssemblyGlbExporter.Serialize(display); File.WriteAllBytes(source, glb);
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII="); File.WriteAllBytes(preview, png);
        var model = new Model3D(source, preview, 100,60,110,110, new(0,0,0,100,20,30,0), "Triangle <3D>");
        var slides = new[] { new Model3DSlide("Editable title", "Positioning", [model, model with { X=12, Width=70 }]) };
        var path = Path.Combine(temporary.Path,"test.pptx"); var second = Path.Combine(temporary.Path,"second.pptx");
        DrawingPptxWriter.WriteModel3DDeck(slides, path); DrawingPptxWriter.WriteModel3DDeck(slides, second);
        Assert.Equal(File.ReadAllBytes(path), File.ReadAllBytes(second));
        using (var document = PresentationDocument.Open(path, false))
        {
            var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(document).ToArray();
            Assert.True(errors.Length == 0, string.Join("\n", errors.Select(e => $"{e.Part?.Uri}: {e.Path?.XPath}: {e.Description}")));
        }
        using var zip = ZipFile.OpenRead(path);
        byte[] ReadBytes(string name) { using var stream=zip.GetEntry(name)!.Open(); using var buffer=new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray(); }
        Assert.Equal(glb, ReadBytes("ppt/media/model3d-1-1.glb")); Assert.Equal(png, ReadBytes("ppt/media/model3d-1-1.png"));
        var slide = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadBytes("ppt/slides/slide1.xml")));
        var rels = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadBytes("ppt/slides/_rels/slide1.xml.rels")));
        Assert.DoesNotContain(rels.Descendants(), e => (string?)e.Attribute("TargetMode") == "External");
        Assert.Equal(2, rels.Descendants().Count(e => ((string?)e.Attribute("Type"))?.EndsWith("/model3d", StringComparison.Ordinal) == true));
        XNamespace am3d="http://schemas.microsoft.com/office/drawing/2017/model3d";
        var oracle = XDocument.Load(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Presentation3D/office-model3d-oracle.xml"));
        var expected = oracle.Descendants(am3d+"model3d").Single().DescendantsAndSelf().Select(e=>e.Name).ToArray();
        Assert.Equal(expected, slide.Descendants(am3d+"model3d").First().DescendantsAndSelf().Select(e=>e.Name));
        XNamespace mc="http://schemas.openxmlformats.org/markup-compatibility/2006";
        Assert.Equal(2, slide.Descendants(mc+"Fallback").Count());
        Assert.DoesNotContain(temporary.Path, slide.ToString(), StringComparison.Ordinal);
        var valid = File.ReadAllBytes(path);
        Assert.Throws<ArgumentException>(() => DrawingPptxWriter.WriteModel3DDeck([new("Bad", "", [model with { Width = double.NaN }])], path));
        Assert.Equal(valid, File.ReadAllBytes(path));
        File.WriteAllText(source, "invalid glb");
        Assert.Throws<ArgumentException>(() => DrawingPptxWriter.WriteModel3DDeck(slides,path));
        Assert.Equal(valid, File.ReadAllBytes(path));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aetheris-model3d-"+Guid.NewGuid().ToString("N"));
        public TemporaryDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}
