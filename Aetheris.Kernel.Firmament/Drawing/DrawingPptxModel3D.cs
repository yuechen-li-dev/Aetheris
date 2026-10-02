using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Aetheris.Kernel.Firmament.Drawing;

/// <summary>Deliberate static Office view. Coordinates are glTF metres; presentation
/// normalization affects camera framing only, never the embedded engineering-derived asset.</summary>
public sealed record Model3DView(double CenterXMetres, double CenterYMetres, double CenterZMetres,
    double PresentationScale, double RotationXDegrees, double RotationYDegrees, double RotationZDegrees,
    double CameraDistanceMetres = 1.8, double FieldOfViewDegrees = 45);

/// <summary>A real embedded interactive 3D object, with a required PNG preview.
/// Slide placement is in millimetres, like the existing drawing compiler.</summary>
public sealed record Model3D(string Source, string Preview, double X, double Y, double Width, double Height,
    Model3DView Camera, string Name = "Aetheris 3D Model");
public sealed record Model3DSlide(string Title, string Subtitle, IReadOnlyList<Model3D> Models, string? Caption = null);

public static partial class DrawingPptxWriter
{
    private const string ModelNamespace = "http://schemas.microsoft.com/office/drawing/2017/model3d";
    private const string ModelRelationship = "http://schemas.microsoft.com/office/2017/06/relationships/model3d";

    /// <summary>Bounded presentation API over the existing slide builder and OPC writer.
    /// Packaging follows the desktop-authored PRESENTATION-3D-X0 reference oracle.</summary>
    public static void WriteModel3DDeck(IReadOnlyList<Model3DSlide> slides, string path,
        string title = "Aetheris interactive 3D", double widthMm = 338.6667, double heightMm = 190.5)
    {
        if (slides.Count == 0 || !double.IsFinite(widthMm) || !double.IsFinite(heightMm) || widthMm <= 0 || heightMm <= 0)
            throw new ArgumentException("A finite, nonempty deck is required.");
        var media = new List<(int Slide, int Index, byte[] Model, byte[] Preview)>();
        var xml = new List<string>();
        for (var s = 0; s < slides.Count; s++)
        {
            var slide = slides[s]; var builder = new SlideBuilder(widthMm, heightMm);
            builder.Text("Presentation.Title", slide.Title, 12, 10, widthMm - 24, 25, 11.3, bold: true);
            builder.Text("Presentation.Positioning", slide.Subtitle, 12, 39, widthMm - 24, 18, 6);
            for (var m = 0; m < slide.Models.Count; m++)
            {
                var model = slide.Models[m]; ValidateModelView(model, widthMm, heightMm);
                var sourcePath = Path.GetFullPath(model.Source); var previewPath = Path.GetFullPath(model.Preview);
                if (Path.GetFullPath(path).Equals(sourcePath, StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(path).Equals(previewPath, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("PPTX output must differ from its source assets.");
                var modelBytes = File.ReadAllBytes(sourcePath); var previewBytes = File.ReadAllBytes(previewPath);
                if (modelBytes.Length < 28 || BinaryPrimitives.ReadUInt32LittleEndian(modelBytes) != 0x46546C67u
                    || BinaryPrimitives.ReadUInt32LittleEndian(modelBytes.AsSpan(4)) != 2
                    || BinaryPrimitives.ReadUInt32LittleEndian(modelBytes.AsSpan(8)) != modelBytes.Length)
                    throw new ArgumentException("Model3D requires a glTF 2.0 GLB asset.");
                if (previewBytes.Length < 24 || !previewBytes.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
                    throw new ArgumentException("Model3D requires a PNG fallback preview.");
                media.Add((s, m, modelBytes, previewBytes));
                builder.Model(model, $"rId{2 + m * 2}", $"rId{3 + m * 2}");
            }
            if (slide.Caption is not null) builder.Text("Presentation.Caption", slide.Caption, 12, heightMm - 17, widthMm - 24, 14, 3.2);
            xml.Add(builder.Build());
        }
        // All assets and views are checked before the existing package writer touches output.
        WritePackage(path, xml, widthMm, heightMm, title);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
        XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = XDocument.Parse(Read(archive.GetEntry("[Content_Types].xml")!));
        types.Root!.Add(new XElement(ct + "Default", new XAttribute("Extension", "glb"), new XAttribute("ContentType", "model/gltf.binary")));
        types.Root.Add(new XElement(ct + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")));
        archive.GetEntry("[Content_Types].xml")!.Delete(); Add(archive, "[Content_Types].xml", types.ToString(SaveOptions.DisableFormatting));
        for (var s = 0; s < slides.Count; s++)
        {
            XNamespace rel = "http://schemas.openxmlformats.org/package/2006/relationships";
            var relationships = XDocument.Parse(SlideRelationships());
            foreach (var item in media.Where(p => p.Slide == s))
            {
                var suffix = $"{s+1}-{item.Index+1}";
                var modelName = "model3d-" + suffix + ".glb"; var imageName = "model3d-" + suffix + ".png";
                AddBinary(archive, "ppt/media/" + modelName, item.Model); AddBinary(archive, "ppt/media/" + imageName, item.Preview);
                relationships.Root!.Add(new XElement(rel + "Relationship", new XAttribute("Id", $"rId{2+item.Index*2}"), new XAttribute("Type", ModelRelationship), new XAttribute("Target", "../media/" + modelName)));
                relationships.Root.Add(new XElement(rel + "Relationship", new XAttribute("Id", $"rId{3+item.Index*2}"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"), new XAttribute("Target", "../media/" + imageName)));
            }
            var name = $"ppt/slides/_rels/slide{s+1}.xml.rels";
            archive.GetEntry(name)!.Delete(); Add(archive, name, relationships.ToString(SaveOptions.DisableFormatting));
        }
    }

    private static void AddBinary(ZipArchive archive, string name, byte[] bytes)
    { var entry = archive.CreateEntry(name, CompressionLevel.Optimal); entry.LastWriteTime = PackageTime; using var stream = entry.Open(); stream.Write(bytes); }

    private static void ValidateModelView(Model3D m, double width, double height)
    {
        var v = m.Camera ?? throw new ArgumentException("Model3D requires a deliberate camera.");
        if (new[] { m.X,m.Y,m.Width,m.Height,v.CenterXMetres,v.CenterYMetres,v.CenterZMetres,v.PresentationScale,v.RotationXDegrees,v.RotationYDegrees,v.RotationZDegrees,v.CameraDistanceMetres,v.FieldOfViewDegrees }.Any(n => !double.IsFinite(n))
            || m.X < 0 || m.Y < 0 || m.Width <= 0 || m.Height <= 0 || m.X + m.Width > width || m.Y + m.Height > height
            || v.PresentationScale <= 0 || v.CameraDistanceMetres <= 0 || v.FieldOfViewDegrees <= 0 || v.FieldOfViewDegrees >= 180)
            throw new ArgumentException("Model3D placement or camera is invalid.");
    }

    private sealed partial class SlideBuilder
    {
        public void Model(Model3D m, string modelId, string imageId)
        {
            var shapeId = id++; var v = m.Camera;
            static string Number(double value) => checked((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
            string Metres(double value) => Number(value * 36_000_000);
            string Degrees(double value) => Number(value * 60_000);
            var offset = $"<a:off x=\"{Mm(m.X)}\" y=\"{Mm(m.Y)}\"/>";
            var extent = $"<a:ext cx=\"{Mm(m.Width)}\" cy=\"{Mm(m.Height)}\"/>";
            var nonvisual = $"<p:cNvPr id=\"{shapeId}\" name=\"{X(m.Name)}\"/>";
            // Namespace, child order, lighting preset, raster relationship and
            // AlternateContent branch mirror the desktop-authored reference package.
            shapes.Append($"""
                <mc:AlternateContent xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006" xmlns:am3d="{ModelNamespace}">
                  <mc:Choice Requires="am3d"><p:graphicFrame><p:nvGraphicFramePr>{nonvisual}<p:cNvGraphicFramePr><a:graphicFrameLocks noChangeAspect="1"/></p:cNvGraphicFramePr><p:nvPr/></p:nvGraphicFramePr>
                  <p:xfrm>{offset}{extent}</p:xfrm><a:graphic><a:graphicData uri="{ModelNamespace}"><am3d:model3d r:embed="{modelId}">
                  <am3d:spPr><a:xfrm><a:off x="0" y="0"/>{extent}</a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></am3d:spPr>
                  <am3d:camera><am3d:pos x="0" y="0" z="{Metres(v.CameraDistanceMetres)}"/><am3d:up dx="0" dy="36000000" dz="0"/><am3d:lookAt x="0" y="0" z="0"/><am3d:perspective fov="{Degrees(v.FieldOfViewDegrees)}"/></am3d:camera>
                  <am3d:trans><am3d:meterPerModelUnit n="{Number(v.PresentationScale*1_000_000)}" d="1000000"/><am3d:preTrans dx="{Metres(-v.CenterXMetres*v.PresentationScale)}" dy="{Metres(-v.CenterYMetres*v.PresentationScale)}" dz="{Metres(-v.CenterZMetres*v.PresentationScale)}"/><am3d:scale><am3d:sx n="1000000" d="1000000"/><am3d:sy n="1000000" d="1000000"/><am3d:sz n="1000000" d="1000000"/></am3d:scale><am3d:rot ax="{Degrees(v.RotationXDegrees)}" ay="{Degrees(v.RotationYDegrees)}" az="{Degrees(v.RotationZDegrees)}"/><am3d:postTrans dx="0" dy="0" dz="0"/></am3d:trans>
                  <am3d:raster rName="Aetheris.Blender" rVer="PRESENTATION-3D-X0"><am3d:blip r:embed="{imageId}"/></am3d:raster><am3d:objViewport viewportSz="{Mm(Math.Min(m.Width,m.Height))}"/>
                  <am3d:ambientLight><am3d:clr><a:scrgbClr r="50000" g="50000" b="50000"/></am3d:clr><am3d:illuminance n="500000" d="1000000"/></am3d:ambientLight>
                  <am3d:ptLight rad="0"><am3d:clr><a:scrgbClr r="100000" g="75000" b="50000"/></am3d:clr><am3d:intensity n="9765625" d="1000000"/><am3d:pos x="21959998" y="70920001" z="16344003"/></am3d:ptLight>
                  <am3d:ptLight rad="0"><am3d:clr><a:scrgbClr r="40000" g="60000" b="95000"/></am3d:clr><am3d:intensity n="12250000" d="1000000"/><am3d:pos x="-37964106" y="51130435" z="57631972"/></am3d:ptLight>
                  <am3d:ptLight rad="0"><am3d:clr><a:scrgbClr r="86837" g="72700" b="100000"/></am3d:clr><am3d:intensity n="3125000" d="1000000"/><am3d:pos x="-37739122" y="58056624" z="-34769649"/></am3d:ptLight>
                  </am3d:model3d></a:graphicData></a:graphic></p:graphicFrame></mc:Choice>
                  <mc:Fallback><p:pic><p:nvPicPr>{nonvisual}<p:cNvPicPr><a:picLocks noChangeAspect="1"/></p:cNvPicPr><p:nvPr/></p:nvPicPr><p:blipFill><a:blip r:embed="{imageId}"/><a:stretch><a:fillRect/></a:stretch></p:blipFill><p:spPr><a:xfrm>{offset}{extent}</a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></p:spPr></p:pic></mc:Fallback>
                </mc:AlternateContent>
                """);
        }
    }
}
