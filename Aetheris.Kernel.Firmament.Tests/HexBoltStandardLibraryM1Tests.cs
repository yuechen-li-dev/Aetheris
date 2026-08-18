using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HexBoltStandardLibraryM1Tests
{
    private static HexBoltDefinition Reference()
    {
        var result = HexBoltBuilder.Create(McMasterHexBoltSpecs.Reference91180A151, "McMaster91180A151");
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        return result.Value;
    }

    [Fact]
    public void RegularHex_DerivesReferenceDimensions()
    {
        var dimensions = HexBoltBuilder.Derive(McMasterHexBoltSpecs.Reference91180A151);
        Assert.Equal(6.5d, dimensions.HeadApothem, 12);
        Assert.Equal(13d / Math.Sqrt(3d), dimensions.HeadCircumradius, 12);
        Assert.Equal(6.175d, dimensions.TopFlatRadius, 12);
        Assert.Equal(65d, dimensions.TopConeSemiAngleDegrees, 12);
        Assert.Equal(34.0625d, dimensions.TipChamferStartX, 12);
        Assert.True(dimensions.TopConeCornerX < 0d);
    }

    [Fact]
    public void ReferenceBody_IsExactManifoldWithHyperbolicConeHexTrims()
    {
        var bolt = Reference();
        Assert.True(BrepExportPreflight.Validate(bolt.Body).IsValid);
        Assert.Equal(6, bolt.Body.Geometry.Curves.Count(pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3));
        Assert.Equal(0, bolt.Body.Geometry.Curves.Count(pair => pair.Value.Kind == CurveGeometryKind.BSpline3));
        Assert.Equal(6, bolt.Body.Topology.Faces.Count(face =>
            bolt.Body.TryGetFaceSurfaceGeometry(face.Id, out var surface)
            && surface?.Kind == SurfaceGeometryKind.Cone
            && Math.Abs(surface.Cone!.Value.SemiAngleRadians - 65d * Math.PI / 180d) < 1e-12));
        Assert.Single(bolt.Body.Geometry.Surfaces.Select(pair => pair.Value), surface =>
            surface.Kind == SurfaceGeometryKind.Cone
            && Math.Abs(surface.Cone!.Value.SemiAngleRadians - 65d * Math.PI / 180d) < 1e-12);
    }

    [Fact]
    public void ReferenceBody_HasStableSemanticDescendantsAndThreadIsCylinderOnly()
    {
        var bolt = Reference();
        var ids = bolt.Semantics.Descendants.Select(descendant => descendant.StableId).ToArray();
        Assert.Contains("McMaster91180A151.Head.TopFlat", ids);
        Assert.Contains("McMaster91180A151.Head.TopChamfer", ids);
        Assert.Contains("McMaster91180A151.Head.Side[5]", ids);
        Assert.Contains("McMaster91180A151.Shank", ids);
        Assert.Contains("McMaster91180A151.ThreadRegion", ids);
        Assert.Contains("McMaster91180A151.TipChamfer", ids);
        Assert.Contains("McMaster91180A151.TipFace", ids);
        var thread = Assert.Single(bolt.Semantics.Descendants, descendant => descendant.StableId.EndsWith(".ThreadRegion", StringComparison.Ordinal));
        Assert.Contains("material-geometry=Cylinder", thread.Metadata, StringComparison.Ordinal);
        Assert.Equal("M8 x 1.25", bolt.Semantics.Metadata["ThreadDesignation"]);
        Assert.Equal("8.8", bolt.Semantics.Metadata["PropertyClass"]);
    }

    [Fact]
    public void ReferenceStep_IsDeterministicAndReimportsWithoutNurbs()
    {
        var bolt = Reference();
        var first = Step242Exporter.ExportBody(bolt.Body);
        var second = Step242Exporter.ExportBody(bolt.Body);
        Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics));
        Assert.Equal(Hash(first.Value), Hash(second.Value));
        Assert.Contains("HYPERBOLA(", first.Value, StringComparison.Ordinal);
        Assert.Equal(6, Regex.Matches(first.Value, "HYPERBOLA\\(", RegexOptions.CultureInvariant).Count);
        Assert.Equal(2, Regex.Matches(first.Value, "CONICAL_SURFACE\\(", RegexOptions.CultureInvariant).Count);
        Assert.Single(Regex.Matches(first.Value, "CYLINDRICAL_SURFACE\\(", RegexOptions.CultureInvariant).Cast<Match>());
        Assert.Single(Regex.Matches(first.Value, "TOROIDAL_SURFACE\\(", RegexOptions.CultureInvariant).Cast<Match>());
        Assert.DoesNotContain("B_SPLINE_SURFACE", first.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("B_SPLINE_CURVE", first.Value, StringComparison.Ordinal);
        var imported = Step242Importer.ImportBody(first.Value);
        Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
        Assert.Single(imported.Value.Topology.Bodies);
        Assert.Single(imported.Value.Topology.Shells);
        Assert.Equal(0, imported.Value.Geometry.Surfaces.Count(pair => pair.Value.Kind == SurfaceGeometryKind.BSplineSurfaceWithKnots));
    }

    [Fact]
    public void InvalidHeadAndTipParameters_AreTypedRejections()
    {
        var reference = McMasterHexBoltSpecs.Reference91180A151;
        var topOutside = HexBoltBuilder.Validate(reference with { TopFlatDiameter = 13d });
        Assert.Contains(topOutside, diagnostic => diagnostic.Code == HexBoltAdmissionCode.TopFlatOutsideHex);
        var consumed = HexBoltBuilder.Validate(reference with { HeadHeight = 0.1d });
        Assert.Contains(consumed, diagnostic => diagnostic.Code == HexBoltAdmissionCode.TopChamferConsumesHead);
        var tip = HexBoltBuilder.Validate(reference with { TipDiameter = 8d });
        Assert.Contains(tip, diagnostic => diagnostic.Code == HexBoltAdmissionCode.TipChamferInvalid);
    }

    [Fact]
    public void ParameterChangeDogfood_ChangesLengthAndDiameterWithoutGeometryCode()
    {
        var changed = McMasterHexBoltSpecs.Reference91180A151 with
        {
            NominalDiameter = 10d,
            Length = 50d,
            HeadAcrossFlats = 17d,
            HeadHeight = 6.4d,
            TopFlatDiameter = 16d,
            TipDiameter = 8d,
            ThreadLength = 26d,
            ThreadDesignation = "M10 x 1.5"
        };
        var result = HexBoltBuilder.Create(changed, "DogfoodM10x50");
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(49.0625d, result.Value.Dimensions.TipChamferStartX, 12);
        Assert.NotEqual(Reference().DeterministicSignature, result.Value.DeterministicSignature);
        Assert.True(Step242Importer.ImportBody(Step242Exporter.ExportBody(result.Value.Body).Value).IsSuccess);
    }

    [Fact]
    public void CanonicalFirmamentFixture_MaterializesStandardLibraryBoltAndPublishesSemantics()
    {
        var sourcePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../fixtures/LegacyV1/Examples/mcmaster_91180a151_threadless_hex_bolt.firmament"));
        var source = File.ReadAllText(sourcePath);
        var parse = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(sourcePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.IsType<FirmamentV2StandardPartRecord>(Assert.Single(parse.Document!.Solids).Primitive);
        Assert.Equal("HexBoltSpec", Assert.Single(parse.Document.StaticAuthoring!.RecordTypes).Name);
        Assert.Equal("HexBolt", Assert.Single(parse.Document.StaticAuthoring.Templates).Name);

        var output = Path.Combine(Path.GetTempPath(), $"aetheris-hexbolt-{Guid.NewGuid():N}.step");
        try
        {
            var build = FirmamentBuildAndExport.Run(sourcePath, output);
            Assert.True(build.IsSuccess, string.Join(Environment.NewLine, build.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var report = Assert.IsType<FirmamentStandardPartReport>(build.Value.Export.StandardPart);
            Assert.Equal("HexBolt", report.Family);
            Assert.Equal("HexBolt", report.Template);
            Assert.Contains(report.SemanticDescendants, descendant => descendant.StableId == "McMaster91180A151.Head.TopChamfer");
            Assert.True(Step242Importer.ImportBody(build.Value.Export.StepText).IsSuccess);
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
