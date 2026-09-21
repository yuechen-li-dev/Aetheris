using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242LengthUnitTests
{
    private const string MillimetreUnit = "(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.))";

    [Fact]
    public void Import_InchDeclaredFile_IsScaledToMillimetres()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var millimetreText = Step242Exporter.ExportBody(box).Value;
        Assert.Contains(MillimetreUnit, millimetreText);
        var inchText = ReplaceLengthUnit(millimetreText, MillimetreUnit,
            "(CONVERSION_BASED_UNIT('INCH',#9001)LENGTH_UNIT()NAMED_UNIT(#9002))",
            "#9001=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(0.0254),#9003);\n#9002=DIMENSIONAL_EXPONENTS(1.,0.,0.,0.,0.,0.,0.);\n#9003=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT($,.METRE.));\n");

        var millimetreExtent = MaxAbsVertexCoordinate(Step242Importer.ImportBody(millimetreText).Value);
        var inchExtent = MaxAbsVertexCoordinate(Step242Importer.ImportBody(inchText).Value);

        Assert.Equal(millimetreExtent * 25.4d, inchExtent, 9);
    }

    [Fact]
    public void Import_MetreDeclaredFile_IsScaledToMillimetres()
    {
        var box = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var millimetreText = Step242Exporter.ExportBody(box).Value;
        var metreText = millimetreText.Replace(MillimetreUnit, "(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT($,.METRE.))", StringComparison.Ordinal);

        var millimetreExtent = MaxAbsVertexCoordinate(Step242Importer.ImportBody(millimetreText).Value);
        var metreExtent = MaxAbsVertexCoordinate(Step242Importer.ImportBody(metreText).Value);

        Assert.Equal(millimetreExtent * 1000d, metreExtent, 9);
    }

    [Fact]
    public void Parse_FileWithoutUnitContext_IsLeftUnscaled()
    {
        var parse = Step242SubsetParser.Parse(Step242FixtureCorpus.CylindricalFaceWithTwoLoops);

        Assert.True(parse.IsSuccess);
        Assert.Equal(1d, parse.Value.SourceMillimetresPerUnit);
    }

    private static string ReplaceLengthUnit(string text, string oldUnit, string newUnit, string extraEntities)
    {
        var replaced = text.Replace(oldUnit, newUnit, StringComparison.Ordinal);
        var endsec = replaced.LastIndexOf("ENDSEC;", StringComparison.Ordinal);
        return replaced.Insert(endsec, extraEntities);
    }

    private static double MaxAbsVertexCoordinate(BrepBody body)
    {
        var max = 0d;
        foreach (var vertex in body.Topology.Vertices)
        {
            Assert.True(body.TryGetVertexPoint(vertex.Id, out var point));
            max = double.Max(max, double.Max(double.Abs(point.X), double.Max(double.Abs(point.Y), double.Abs(point.Z))));
        }

        return max;
    }
}
