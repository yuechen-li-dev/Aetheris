using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Curves;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.StandardLibrary;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class HexBoltTemplateM2Tests
{
    private static string FixturePath => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../testdata/firmament/examples/hexbolt_template_m2.firmament"));

    [Fact]
    public void CanonicalTemplate_IsConceptConstrainedTypedRecordAuthoredAndHasNoStandardPartHook()
    {
        var source = File.ReadAllText(FixturePath);
        Assert.DoesNotContain("StandardPart", source, StringComparison.Ordinal);
        var parse = FirmamentV2Parser.Parse(source, Path.GetDirectoryName(FixturePath));
        Assert.True(parse.IsSuccess, string.Join(Environment.NewLine, parse.Diagnostics));
        Assert.IsType<FirmamentV2ExactCoaxialPartRecord>(parse.Document!.Solid.Primitive);
        var instance = Assert.Single(parse.Document.ConceptIr!.TemplateInstantiations!);
        Assert.Equal("HexBolt", instance.Template);
        Assert.Equal("ReferenceBolt", instance.Instance);
        var spec = Assert.Single(instance.RecordArguments!).Value;
        Assert.Equal("HexBoltSpec", spec.RecordType);
        Assert.Equal("McMaster91180A151", spec.StaticValue);
        Assert.Equal("Passed:22mm >= 0mm && 22mm <= 35mm", instance.RequireResults!["ThreadFits"]);
        Assert.Equal("Valid", parse.Document.ConceptIr.MaterializedStruct.Conformance);
        Assert.Equal("BoltConcept", Assert.Single(parse.Document.ConceptIr.MaterializedStruct.Satisfies));
    }

    [Theory]
    [InlineData("McMaster91180A151", 8d, 35d)]
    [InlineData("M10x50", 10d, 50d)]
    [InlineData("Nonstandard825x375", 8.25d, 37.5d)]
    public void SameTemplate_MaterializesReferenceStandardAndNonstandardExactBolts(string staticName, double diameter, double length)
    {
        var source = File.ReadAllText(FixturePath).Replace("Spec: McMaster91180A151", "Spec: " + staticName, StringComparison.Ordinal);
        var tempSource = Path.Combine(Path.GetTempPath(), $"aetheris-hexbolt-m2-{Guid.NewGuid():N}.firmament");
        var tempStep = Path.ChangeExtension(tempSource, ".step");
        File.WriteAllText(tempSource, source);
        try
        {
            var first = FirmamentBuildAndExport.Run(tempSource, tempStep);
            var second = FirmamentBuildAndExport.Run(tempSource, tempStep);
            Assert.True(first.IsSuccess, string.Join(Environment.NewLine, first.Diagnostics.Select(d => d.Message)));
            Assert.True(second.IsSuccess, string.Join(Environment.NewLine, second.Diagnostics.Select(d => d.Message)));
            Assert.Equal(Hash(first.Value.Export.StepText), Hash(second.Value.Export.StepText));
            Assert.DoesNotContain("B_SPLINE", first.Value.Export.StepText, StringComparison.Ordinal);
            Assert.Equal(6, Regex.Matches(first.Value.Export.StepText, "HYPERBOLA\\(", RegexOptions.CultureInvariant).Count);
            Assert.Equal(2, Regex.Matches(first.Value.Export.StepText, "CONICAL_SURFACE\\(", RegexOptions.CultureInvariant).Count);
            var imported = Step242Importer.ImportBody(first.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
            Assert.True(BrepExportPreflight.Validate(imported.Value).IsValid);
            var xs = imported.Value.Topology.Vertices.Select(vertex => imported.Value.TryGetVertexPoint(vertex.Id, out var point) ? point.X : double.NaN).ToArray();
            Assert.Equal(length, xs.Max(), 9);
            Assert.Equal(diameter, double.Parse(first.Value.Export.StandardPart!.Parameters["NominalDiameter"][..^2], System.Globalization.CultureInfo.InvariantCulture), 9);
            Assert.Contains(first.Value.Export.StandardPart.SemanticDescendants, item => item.StableId.EndsWith(".ThreadRegion", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(tempSource)) File.Delete(tempSource);
            if (File.Exists(tempStep)) File.Delete(tempStep);
        }
    }

    [Fact]
    public void ReferenceTemplateAndM1Oracle_AgreeOnExactEngineeringInvariants()
    {
        var output = Path.Combine(Path.GetTempPath(), $"aetheris-hexbolt-m2-{Guid.NewGuid():N}.step");
        try
        {
            var m2 = FirmamentBuildAndExport.Run(FixturePath, output);
            Assert.True(m2.IsSuccess, string.Join(Environment.NewLine, m2.Diagnostics.Select(d => d.Message)));
            var imported = Step242Importer.ImportBody(m2.Value.Export.StepText);
            Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics));
            var m1 = HexBoltBuilder.Create(McMasterHexBoltSpecs.Reference91180A151, "McMaster91180A151").Value;
            var m1Imported = Step242Importer.ImportBody(Step242Exporter.ExportBody(m1.Body).Value).Value;
            Assert.Equal(m1Imported.Topology.Vertices.Count(), imported.Value.Topology.Vertices.Count());
            Assert.Equal(m1Imported.Topology.Edges.Count(), imported.Value.Topology.Edges.Count());
            Assert.Equal(m1Imported.Topology.Faces.Count(), imported.Value.Topology.Faces.Count());
            Assert.Equal(m1Imported.Geometry.Curves.Count(pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3), imported.Value.Geometry.Curves.Count(pair => pair.Value.Kind == CurveGeometryKind.Hyperbola3));
            foreach (var kind in new[] { SurfaceGeometryKind.Plane, SurfaceGeometryKind.Cylinder, SurfaceGeometryKind.Cone, SurfaceGeometryKind.Torus })
                Assert.Equal(m1Imported.Geometry.Surfaces.Count(pair => pair.Value.Kind == kind), imported.Value.Geometry.Surfaces.Count(pair => pair.Value.Kind == kind));
            Assert.Equal(m1.Semantics.Descendants.Select(item => item.StableId), m2.Value.Export.StandardPart!.SemanticDescendants.Select(item => item.StableId));
            Assert.Equal(m1.DeterministicSignature, m2.Value.Export.StandardPart.DeterministicSignature);
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
