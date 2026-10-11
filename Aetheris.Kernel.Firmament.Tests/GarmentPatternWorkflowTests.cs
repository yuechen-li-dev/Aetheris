using System.Numerics;
using System.Xml.Linq;
using Aetheris.Humanoid;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Garment;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentPatternWorkflowTests
{
    [Fact]
    public void FigureFitMeasuresTheSuppliedSurfaceAndChangingItChangesTheCut()
    {
        var body = BoxBody();
        var fit = HumanoidFigureFitting.Measure(body, 1280, 960);
        Assert.Equal(1200, fit.Chest.CircumferenceMm, 6);
        Assert.Equal(1200, fit.Hip.CircumferenceMm, 6);
        Assert.Equal(8, fit.Chest.Segments);
        var original = CompileDress(body);
        var enlarged = CompileDress(BoxBody(1.2));
        Assert.NotEqual(original.Cloth.ContentKey, enlarged.Cloth.ContentKey);
        Assert.NotEqual(original.Cloth.Definition.Positions.ToArray(), enlarged.Cloth.Definition.Positions.ToArray());
        Assert.Equal(original.Source.SourceHash, enlarged.Source.SourceHash);
        Assert.Single(original.Source.FigureFits);
        Assert.Equal("dressLayout.body", original.Source.FigureFits[0].Identity);
        Assert.Equal("synthetic-box", original.Source.FigureFits[0].Measurements.BodyId);
        Assert.Throws<InvalidOperationException>(() => GarmentDraping.Settle(original, BoxBody(1.2), 1));
    }

    [Fact]
    public void OpenBodySectionsAndMissingBindingsStayExplicitFailures()
    {
        var parsed = GarmentAuthoring.Parse(Dress());
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "garment-fit-body-required");
        var body = BoxBody(open: true);
        var failed = GarmentAuthoring.Parse(Dress(), figureBinding: new("Anatolia", body));
        Assert.False(failed.IsSuccess);
        Assert.Contains(failed.Diagnostics, diagnostic => diagnostic.Message.Contains("garment-fit-section-open"));
        var unknown = GarmentAuthoring.Parse(Dress().Replace("body.torsoCircumference", "body.guessedBust"),
            figureBinding: new("Anatolia", BoxBody()));
        Assert.Contains(unknown.Diagnostics, diagnostic => diagnostic.Code == "garment-fit-member-unknown");
    }

    [Fact]
    public void FigureFieldsComeFromTheGeneratedSchemaAndSundressUsesTheSamePipeline()
    {
        string source = "schema Garment\nConcept Struct fit { FigureFit body {\n ";
        var completion = FirmamentLanguageService.Complete(source, "dress.firmament", "1", source.Length);
        Assert.Equal("FigureFit", completion.Context);
        Assert.Contains(completion.Fields, field => field.Name == "ChestHeight");
        string dress = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Garment/sundress.firmament"));
        var result = GarmentCompiler.Compile(dress, figureBinding: new("Anatolia", BoxBody()));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.Equal(4, result.Garment!.Source.Stitches.Length);
        Assert.Contains(result.Garment.Panels[0].Edges.Keys, name => name == "rightArmholeLower");
        Assert.DoesNotContain(result.Garment.Source.Stitches, seam => seam.A.Contains("Armhole", StringComparison.Ordinal));
    }

    [Fact]
    public void ResolvedPreviewIsIdenticalAcrossMeshDensitiesAndRetainsCurvesAndOpenCuffs()
    {
        var binding = new GarmentFigureBinding("Anatolia", BoxBody());
        var coarse = GarmentAuthoring.Parse(Dress(), figureBinding: binding);
        var fine = GarmentAuthoring.Parse(Dress().Replace("meshSize: 55mm", "meshSize: 35mm"), figureBinding: binding);
        Assert.True(coarse.IsSuccess, string.Join("; ", coarse.Diagnostics));
        Assert.True(fine.IsSuccess, string.Join("; ", fine.Diagnostics));
        string svg = GarmentPatternPreview.Svg(coarse.Source!);
        Assert.Equal(svg, GarmentPatternPreview.Svg(fine.Source!));
        var document = XDocument.Parse(svg);
        XNamespace ns = "http://www.w3.org/2000/svg";
        Assert.Contains(document.Descendants(ns + "path"), path => path.Attribute("d")!.Value.Contains(" C "));
        Assert.Contains(document.Descendants(ns + "path"), path => path.Attribute("data-boundary")?.Value == "rightSleeveOpening"
            && path.Attribute("stroke")!.Value == "#3274a0");
        Assert.Equal(8, GarmentPatternInspection.Inspect(coarse.Source!).Count);
        Assert.All(GarmentPatternInspection.Inspect(coarse.Source!), seam => Assert.InRange(seam.Mismatch, 0, 1e-9));
        var compiled = GarmentCompiler.Compile(Dress(), figureBinding: binding);
        var remeshed = GarmentCompiler.Compile(Dress().Replace("meshSize: 55mm", "meshSize: 35mm"), figureBinding: binding);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        Assert.True(remeshed.IsSuccess, string.Join("; ", remeshed.Diagnostics));
        Assert.All(compiled.Garment!.Panels, panel => Assert.Equal(panel.Edges.Keys.Order(),
            remeshed.Garment!.Panels.Single(other => other.Identity == panel.Identity).Edges.Keys.Order()));
        Assert.Empty(compiled.Garment.Cloth.Definition.Pins);
    }

    [Fact]
    public void SemanticEditGroupsCannotStitchOverAlreadyConsumedChildren()
    {
        string source = Dress().Replace("  Drape {", "  Interface<Stitch> duplicate { a: front.edges.rightCut; b: back.edges.leftCut; }\n  Drape {");
        var parsed = GarmentAuthoring.Parse(source, figureBinding: new("Anatolia", BoxBody()));
        Assert.False(parsed.IsSuccess);
        Assert.Contains(parsed.Diagnostics, diagnostic => diagnostic.Code == "garment-stitch-edge-reused");
    }

    [Fact]
    public void PosedDressKeepsTheSameFlatPatternAndExplicitMeasurementPose()
    {
        var binding = new GarmentFigureBinding("Anatolia", BoxBody());
        string Source(string name) => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/Garment/" + name + ".firmament"));
        var baseline = GarmentCompiler.Compile(Source("sundress"), figureBinding: binding);
        var posed = GarmentCompiler.Compile(Source("sundress-posed"), figureBinding: binding);
        Assert.True(baseline.IsSuccess, string.Join("; ", baseline.Diagnostics));
        Assert.True(posed.IsSuccess, string.Join("; ", posed.Diagnostics));
        Assert.Equal(baseline.Garment!.Cloth.ContentKey, posed.Garment!.Cloth.ContentKey);
        Assert.Equal("Rest", Assert.Single(posed.Garment.Source.FigureFits).Pose);
        Assert.Equal("fitting", posed.Garment.Source.Pose);
        Assert.Equal("lowerArms", posed.Garment.Source.Transition);
        var mismatch = GarmentCompiler.Compile(Source("sundress-posed").Replace("pose: fitting;", "pose: wearing;"),
            figureBinding: binding);
        Assert.False(mismatch.IsSuccess);
        Assert.Contains(mismatch.Diagnostics, diagnostic => diagnostic.Code == "garment-pose-transition-binding");
    }

    private static CompiledGarment CompileDress(HumanoidGameplayBody body)
    {
        var result = GarmentCompiler.Compile(Dress(), figureBinding: new("Anatolia", body));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        return result.Garment!;
    }

    private static string Dress()
    {
        return File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/Garment/tshirt-dress.firmament"));
    }

    private static HumanoidGameplayBody BoxBody(double scale = 1, bool open = false)
    {
        Point3D[] positions = [new(-200 * scale, -100 * scale, 800), new(200 * scale, -100 * scale, 800),
            new(200 * scale, 100 * scale, 800), new(-200 * scale, 100 * scale, 800),
            new(-200 * scale, -100 * scale, 1600), new(200 * scale, -100 * scale, 1600),
            new(200 * scale, 100 * scale, 1600), new(-200 * scale, 100 * scale, 1600)];
        int[][] triangles = [[0, 2, 1], [0, 3, 2], [4, 5, 6], [4, 6, 7], [0, 1, 5], [0, 5, 4],
            [1, 2, 6], [1, 6, 5], [2, 3, 7], [2, 7, 6], [3, 0, 4], [3, 4, 7]];
        var vertices = positions.Select((point, index) => new HumanoidVertex("v" + index, point, HumanoidRegionKind.Chest, index)).ToArray();
        var faces = triangles.Select((triangle, index) => new HumanoidFace("f" + index,
            triangle[0], triangle[1], triangle[2], HumanoidRegionKind.Chest, null)).ToArray();
        var surface = new HumanoidSurface("box", "box-bindings", "box-connectivity", vertices, faces, [], null,
            vertices.Select((_, index) => new VertexSkinWeights(index, [new(0, 1)])).ToArray(),
            [new("skin", HumanoidComponentKind.OuterSkin, Enumerable.Range(0, faces.Length).Where(index => !open || index != 4).ToArray(), [])],
            new Dictionary<HumanoidRegionKind, HumanoidRegionKind>());
        var joints = new List<HumanoidJoint>();
        Add(HumanoidJointKind.Root, new(0, 0, 0));
        Add(HumanoidJointKind.Chest, new(0, 0, 1280));
        Add(HumanoidJointKind.Pelvis, new(0, 0, 960));
        Add(HumanoidJointKind.Neck, new(0, 0, 1510));
        Add(HumanoidJointKind.LeftShoulder, new(-150, 0, 1450));
        Add(HumanoidJointKind.RightShoulder, new(150, 0, 1450));
        var skeleton = new HumanoidSkeleton("box-skeleton", "box-rest", "rigid", joints, new Dictionary<HumanoidJointKind, HumanoidJointKind>());
        var provenance = new HumanoidProvenance("test", [], [], "test", "test", "test", "synthetic", []);
        return new(new(HumanoidGameplayBody.Schema, "synthetic-box", surface, skeleton, [], provenance));

        void Add(HumanoidJointKind kind, Point3D point)
        {
            var matrix = Matrix4x4.CreateTranslation((float)point.X, (float)point.Y, (float)point.Z);
            Matrix4x4.Invert(matrix, out var inverse);
            joints.Add(new("joint:" + kind, kind, AnatomicalSide.Center, null,
                HumanoidTransform.FromTranslation(point), matrix, inverse));
        }
    }
}
