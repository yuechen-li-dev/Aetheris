using Aetheris.Kernel.Core.Step242;
using System.Globalization;
using System.Text.RegularExpressions;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242PmiTransferTests
{
    public static IEnumerable<object[]> NistFiles() => Directory.EnumerateFiles(Step242Corpus.Path("testdata/step242/nist"), "*.stp", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith("-tg.stp", StringComparison.Ordinal)).Order()
        .Select(path => new object[] { "testdata/step242/nist/" + System.IO.Path.GetRelativePath(Step242Corpus.Path("testdata/step242/nist"), path).Replace('\\', '/') });

    [Theory, MemberData(nameof(NistFiles)), Trait("Category", "SlowCorpus")]
    public void NistSemanticGraphPreservesRecordsUnitsAndTopologyAssociations(string path)
    {
        var imported = Step242Corpus.Import(path);
        Assert.True(imported.IsSuccess, string.Join(";", imported.Diagnostics.Select(d => d.Message)));
        var transfer = Step242PmiTransfer.Read(Step242Corpus.Text(path), imported.Value);
        Assert.True(transfer.IsSuccess, string.Join(";", transfer.Diagnostics.Select(d => d.Message)));
        var output = Step242Exporter.ExportBody(imported.Value, new Step242ExportOptions { ImportedPmi = transfer.Value });
        Assert.True(output.IsSuccess, path + ": " + string.Join(";", output.Diagnostics.Select(d => d.Message)));
        var baseline = Step242Exporter.ExportBody(imported.Value).Value;
        Assert.StartsWith(baseline[..baseline.LastIndexOf("ENDSEC;", StringComparison.Ordinal)], output.Value);
        var parsed = Step242SubsetParser.Parse(output.Value);
        Assert.True(parsed.IsSuccess);
        var ids = parsed.Value.Entities.Select(e => e.Id).ToHashSet();
        Assert.All(parsed.Value.Entities.SelectMany(e => Step242PmiTransfer.References(e.Instance)), id => Assert.Contains(id, ids));
        var before = Step242SubsetParser.Parse(Step242Corpus.Text(path)).Value;
        Assert.Equal(before.Entities.Count(Step242PmiTransfer.IsSemanticRoot), parsed.Value.Entities.Count(Step242PmiTransfer.IsSemanticRoot));
        Assert.DoesNotContain(parsed.Value.Entities, e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "CONVERSION_BASED_UNIT") is { } c
            && c.Arguments.FirstOrDefault() is Step242StringValue s && s.Value.Contains("inch", StringComparison.OrdinalIgnoreCase));
        var again = Step242Importer.ImportBody(output.Value);
        Assert.True(again.IsSuccess, string.Join(";", again.Diagnostics.Select(d => d.Message)));
        Assert.Equal(imported.Value.Topology.Faces.Count(), again.Value.Topology.Faces.Count());
        Assert.Equal(imported.Value.Topology.Edges.Count(), again.Value.Topology.Edges.Count());
        Assert.Equal(imported.Value.Geometry.Surfaces.Select(s => s.Value.Kind).Order(), again.Value.Geometry.Surfaces.Select(s => s.Value.Kind).Order());
        var sourcePmi = Step242SemanticPmiInspector.Inspect(Step242Corpus.Text(path));
        var roundtripPmi = Step242SemanticPmiInspector.Inspect(output.Value);
        AssertEdgeUsages(sourcePmi.Items, imported.Value, roundtripPmi.Items, again.Value);
        Assert.Equal(sourcePmi.DatumCount, roundtripPmi.DatumCount); Assert.Equal(sourcePmi.DimensionCount, roundtripPmi.DimensionCount);
        Assert.Equal(sourcePmi.GeometricToleranceCount, roundtripPmi.GeometricToleranceCount);
        var expectedTargets = AssociationKeys(sourcePmi.Items, imported.Value);
        var actualTargets = AssociationKeys(roundtripPmi.Items, again.Value);
        var expectedGeometry = GeometryKeys(sourcePmi.Items, imported.Value, before);
        var actualGeometry = GeometryKeys(roundtripPmi.Items, again.Value, parsed.Value);
        var expected = sourcePmi.Items.Select(item => Semantics(item, expectedTargets, expectedGeometry)).ToArray();
        var actual = roundtripPmi.Items.Select(item => Semantics(item, actualTargets, actualGeometry)).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        var differences = expected.Zip(actual).Select((pair, index) => (pair, index)).Where(item => item.pair.First != item.pair.Second).ToArray();
        Assert.True(differences.Length == 0, path + "\n" + string.Join("\n", differences.Take(3).Select(item => $"{item.index}: expected {item.pair.First}\nactual {item.pair.Second}")));
    }
    private static Dictionary<int, string> AssociationKeys(IReadOnlyList<Step242SemanticPmiInspectionItem> items, BrepBody body)
    {
        var keys = body.Bindings.FaceBindings.Where(binding => binding.SourceStepEntityId.HasValue)
            .ToDictionary(binding => binding.SourceStepEntityId!.Value, binding => "body:" + binding.FaceId.Value);
        // Edge-adjacency presentation can include auxiliary surfaces outside the solid;
        // direct source usage identity is qualified separately below.
        foreach (var id in items.SelectMany(item => item.GeometricFaceEntityIds).Distinct().Where(id => !keys.ContainsKey(id)))
            keys.Add(id, "auxiliary");
        return keys;
    }
    private static Dictionary<int, string> GeometryKeys(IReadOnlyList<Step242SemanticPmiInspectionItem> items, BrepBody body, Step242ParsedDocument document)
    {
        var geometry = body.Bindings.FaceBindings.Where(b => b.SourceStepEntityId.HasValue).ToDictionary(b => b.SourceStepEntityId!.Value, b => "face:" + b.FaceId.Value);
        foreach (var edge in body.Bindings.EdgeBindings.Where(b => b.SourceStepEntityId.HasValue)) geometry.Add(edge.SourceStepEntityId!.Value, EdgeKey(body, edge));
        foreach (var solid in document.Entities.Where(e => e.Name is "MANIFOLD_SOLID_BREP" or "BREP_WITH_VOIDS")) geometry.Add(solid.Id, "solid");
        var auxiliary = items.SelectMany(item => item.GeometricItemEntityIds).Distinct().Where(id => !geometry.ContainsKey(id)).Order().ToArray();
        for (var i = 0; i < auxiliary.Length; i++) geometry.Add(auxiliary[i], "auxiliary:" + i);
        return geometry;
    }
    private static string EdgeKey(BrepBody body, EdgeGeometryBinding binding)
    {
        // Import allocates edge ids in traversal order, which can change after export.
        // Qualify the associated physical edge rather than its temporary allocation number.
        var edge = body.Topology.GetEdge(binding.EdgeId);
        Assert.True(body.TryGetEdgeCurveGeometry(edge.Id, out var curve));
        var adjacent = body.Topology.Faces.Where(face => face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
            .Any(id => body.Topology.GetCoedge(id).EdgeId == edge.Id)).Select(face => face.Id.Value).Order();
        return "edge:" + curve!.Kind + ":" + string.Join(",", adjacent);
    }
    private static void AssertEdgeUsages(IReadOnlyList<Step242SemanticPmiInspectionItem> before, BrepBody original,
        IReadOnlyList<Step242SemanticPmiInspectionItem> after, BrepBody emitted)
    {
        static Dictionary<int, (string Key, Aetheris.Kernel.Core.Math.Point3D Start, Aetheris.Kernel.Core.Math.Point3D End, Aetheris.Kernel.Core.Math.Point3D? Mid)> Witnesses(BrepBody body)
            => body.Bindings.EdgeBindings.Where(binding => binding.SourceStepEntityId.HasValue).ToDictionary(binding => binding.SourceStepEntityId!.Value, binding =>
            {
                var edge = body.Topology.GetEdge(binding.EdgeId);
                Assert.True(body.TryGetVertexPoint(edge.StartVertexId, out var start));
                Assert.True(body.TryGetVertexPoint(edge.EndVertexId, out var end));
                Assert.True(body.TryGetEdgeCurveGeometry(edge.Id, out var curve));
                var mid = binding.TrimInterval is { } interval ? BrepPcurveValidator.Evaluate(curve!, (interval.Start + interval.End) / 2) : null;
                return (EdgeKey(body, binding), start, end, mid);
            });
        var source = Witnesses(original); var output = Witnesses(emitted);
        Assert.Equal(before.Count, after.Count);
        foreach (var pair in before.Zip(after))
        {
            var unmatched = pair.Second.GeometricItemEntityIds.Where(output.ContainsKey).Select(id => output[id]).ToList();
            foreach (var witness in pair.First.GeometricItemEntityIds.Where(source.ContainsKey).Select(id => source[id]))
            {
                var index = unmatched.FindIndex(candidate => candidate.Key == witness.Key
                    && (((candidate.Start - witness.Start).Length < 1e-7 && (candidate.End - witness.End).Length < 1e-7)
                        || ((candidate.Start - witness.End).Length < 1e-7 && (candidate.End - witness.Start).Length < 1e-7))
                    && (candidate.Mid.HasValue == witness.Mid.HasValue)
                    && (!candidate.Mid.HasValue || (candidate.Mid.Value - witness.Mid!.Value).Length < 1e-7));
                Assert.True(index >= 0, $"{pair.First.Name}: direct edge usage lost its physical witness.");
                unmatched.RemoveAt(index);
            }
            Assert.Empty(unmatched);
        }
    }
    private static string Semantics(Step242SemanticPmiInspectionItem item, IReadOnlyDictionary<int, string> targets, IReadOnlyDictionary<int, string> geometry) => string.Join("|", item.Kind, item.Value?.ToString("R", CultureInfo.InvariantCulture), item.ValueUnit,
        item.TolerancePlus?.ToString("R", CultureInfo.InvariantCulture), item.ToleranceMinus?.ToString("R", CultureInfo.InvariantCulture),
        string.Join(",", item.DatumReferences), string.Join(",", item.Modifiers), string.Join(",", item.ZoneForms),
        string.Join(";", item.DatumReferenceFrames.Select(frame => string.Join(",", frame.Labels) + ":" + string.Join(",", frame.Modifiers))),
        // Exact source correspondence, rather than a face-count check that could miss a swapped target.
        // Only material-face adjacency belongs to the geometry witness. Unreferenced auxiliary
        // edge neighbours need not be copied; direct auxiliary usage identity is checked below.
        string.Join(",", item.GeometricFaceEntityIds.Select(id => targets[id]).Where(key => key != "auxiliary").Order(StringComparer.Ordinal)),
        string.Join(",", item.GeometricItemEntityIds.Select(id => geometry[id]).Order(StringComparer.Ordinal)));

    private const string Ftc11 = "testdata/step242/nist/FTC/nist_ftc_11_asme1_ap242-e2.stp";
    [Theory]
    [InlineData(2, 645.16)]
    [InlineData(3, 16387.064)]
    public void CompoundLengthUnitsRespectTheirDimensionalExponent(int exponent, double expected)
    {
        var source = InchTolerance(.001);
        var document = Step242SubsetParser.Parse(source).Value;
        var inches = document.Entities.Single(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "CONVERSION_BASED_UNIT") is { } unit
            && unit.Arguments.FirstOrDefault() is Step242StringValue { Value: "inch" }).Id;
        var fresh = document.Entities.Max(e => e.Id) + 1;
        source = source.Insert(source.LastIndexOf("ENDSEC;", StringComparison.Ordinal),
            $"#{fresh}=DERIVED_UNIT_ELEMENT(#{inches},{exponent});\n#{fresh + 1}=DERIVED_UNIT((#{fresh}));\n");
        var entities = Step242SubsetParser.Parse(source).Value.Entities.ToDictionary(e => e.Id);
        Assert.True(Step242LengthUnitNormalizer.TryResolvePmiScale(entities, fresh + 1, [], out var scale));
        Assert.Equal(expected, scale, 9);
    }
    [Theory]
    [InlineData(.001, .0254)] [InlineData(.000001, .0000254)] [InlineData(1e-12, 2.54e-11)]
    public void MixedUnitNistToleranceConvertsIndependentlyWithoutAnAuthoringGrid(double inches, double expected)
    {
        var source = InchTolerance(inches);
        // The semantic-unit specimen differs from the corpus source, so it deliberately needs its own import receipt.
        var original = Step242Importer.ImportBody(source).Value;
        var before = Assert.Single(Step242SemanticPmiInspector.Inspect(source).Items, i => i.Kind == "Flatness");
        Assert.InRange(System.Math.Abs(before.Value!.Value - expected), 0, System.Math.Abs(expected) * 1e-14);
        var transfer = Step242PmiTransfer.Read(source, original); Assert.True(transfer.IsSuccess, string.Join(";", transfer.Diagnostics.Select(d => d.Message)));
        var output = Step242Exporter.ExportBody(original, new Step242ExportOptions { ImportedPmi = transfer.Value });
        Assert.True(output.IsSuccess, string.Join(";", output.Diagnostics.Select(d => d.Message)));
        var after = Assert.Single(Step242SemanticPmiInspector.Inspect(output.Value).Items, i => i.Kind == "Flatness");
        Assert.True(after.Value > 0); Assert.Equal(before.Value, after.Value); Assert.Equal("mm", after.ValueUnit);
        Assert.Equal(before.Modifiers, after.Modifiers); Assert.Equal(before.GeometricFaceEntityIds.Count, after.GeometricFaceEntityIds.Count);
        var body = Step242Importer.ImportBody(output.Value); Assert.True(body.IsSuccess);
        var baseline = Step242Exporter.ExportBody(original).Value;
        Assert.StartsWith(baseline[..baseline.LastIndexOf("ENDSEC;", StringComparison.Ordinal)], output.Value);
    }
    [Fact]
    public void TransferRejectsChangedBodyAndUnresolvableUnit()
    {
        var source = InchTolerance(.001); var body = Step242Importer.ImportBody(source).Value;
        var transfer = Step242PmiTransfer.Read(source, body).Value;
        var changed = body.Clone(); changed.Topology.AddVertex(new(new(999999)));
        var rejected = Step242Exporter.ExportBody(changed, new Step242ExportOptions { ImportedPmi = transfer });
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("pmi-source-body-changed"));
        source = source.Replace("LENGTH_MEASURE(25.4)", "LENGTH_MEASURE(-25.4)", StringComparison.Ordinal);
        body = Step242Importer.ImportBody(source).Value;
        transfer = Step242PmiTransfer.Read(source, body).Value;
        rejected = Step242Exporter.ExportBody(body, new Step242ExportOptions { ImportedPmi = transfer });
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("Cannot resolve length unit"));
    }
    [Fact]
    public void SourceReceiptAndClosedSemanticOwnershipPreventAccidentalPartialTransfer()
    {
        var source = InchTolerance(.001);
        var wrongBody = Step242Corpus.Body(Ftc11);
        var mismatch = Step242PmiTransfer.Read(source, wrongBody);
        Assert.False(mismatch.IsSuccess); Assert.Contains(mismatch.Diagnostics, d => d.Message.Contains("pmi-source-body-mismatch"));
        var document = Step242SubsetParser.Parse(source).Value;
        var control = document.Entities.First(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "GEOMETRIC_TOLERANCE") is not null);
        var fresh = document.Entities.Max(e => e.Id) + 1;
        source = source.Insert(source.LastIndexOf("ENDSEC;", StringComparison.Ordinal), $"#{fresh}=UNQUALIFIED_PMI_OWNER(#{control.Id});\n");
        var body = Step242Importer.ImportBody(source).Value;
        var rejected = Step242PmiTransfer.Read(source, body);
        Assert.False(rejected.IsSuccess); Assert.Contains(rejected.Diagnostics, d => d.Message.Contains("UNQUALIFIED_PMI_OWNER"));
    }
    [Fact]
    public void InchGeometryAndItsControlBothExportInMillimetres()
    {
        var box = BrepPrimitives.CreateBox(2, 3, 4).Value;
        var authored = new Step242SemanticPmiGeometricTolerance("Body.Seat", "Seat", "Flatness", .001, []) { GeometricFaceIds = [box.Topology.Faces.First().Id.Value] };
        var source = Step242Exporter.ExportBody(box, [authored]).Value;
        var document = Step242SubsetParser.Parse(source).Value;
        var unit = document.Entities.Single(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "LENGTH_UNIT") is not null).Id;
        var fresh = document.Entities.Max(e => e.Id) + 1;
        source = Regex.Replace(source, $@"#{unit}\s*=.*?;", $"#{unit}=(CONVERSION_BASED_UNIT('inch',#{fresh})LENGTH_UNIT()NAMED_UNIT(*));", RegexOptions.Singleline);
        source = source.Insert(source.LastIndexOf("ENDSEC;", StringComparison.Ordinal), $"#{fresh}=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(25.4),#{fresh + 1});\n#{fresh + 1}=(LENGTH_UNIT()NAMED_UNIT(*)SI_UNIT(.MILLI.,.METRE.));\n");
        var imported = Step242Importer.ImportBody(source); Assert.True(imported.IsSuccess);
        var transfer = Step242PmiTransfer.Read(source, imported.Value); Assert.True(transfer.IsSuccess);
        var output = Step242Exporter.ExportBody(imported.Value, new Step242ExportOptions { ImportedPmi = transfer.Value }); Assert.True(output.IsSuccess);
        var body = Step242Importer.ImportBody(output.Value).Value;
        var points = body.Topology.Vertices.Select(v => { Assert.True(body.TryGetVertexPoint(v.Id, out var point)); return point; }).ToArray();
        Assert.Equal(50.8, points.Max(p => p.X) - points.Min(p => p.X));
        Assert.Equal(76.2, points.Max(p => p.Y) - points.Min(p => p.Y), 10);
        Assert.Equal(101.6, points.Max(p => p.Z) - points.Min(p => p.Z));
        var control = Assert.Single(Step242SemanticPmiInspector.Inspect(output.Value).Items, i => i.Kind == "Flatness");
        Assert.Equal(.0254, control.Value); Assert.Equal("mm", control.ValueUnit); Assert.Single(control.GeometricFaceEntityIds);
    }
    [Fact]
    public void NistFtc11HasReadableControlsAndDatumPrecedenceIndependentOfVendorLabels()
    {
        var inspection = Step242SemanticPmiInspector.Inspect(Step242Corpus.Text(Ftc11));
        Assert.Equal(4, inspection.GeometricToleranceCount); Assert.Equal(6, inspection.DimensionCount); Assert.Equal(2, inspection.DatumCount);
        var profile = Assert.Single(inspection.Items, i => i.Kind == "SurfaceProfile");
        Assert.Equal(new[] { "A", "B" }, profile.DatumReferences); Assert.Contains("FREE_STATE", profile.Modifiers);
        Assert.NotEmpty(profile.GeometricFaceEntityIds);
    }
    private static string InchTolerance(double inches)
    {
        var source = Step242Corpus.Text(Ftc11); var document = Step242SubsetParser.Parse(source).Value;
        var control = document.Entities.Single(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "FLATNESS_TOLERANCE") is not null);
        var magnitude = (Step242EntityReference)Step242SubsetDecoder.TryGetConstructor(control.Instance, "GEOMETRIC_TOLERANCE")!.Arguments[2];
        var mm = document.Entities.Single(e => Step242SubsetDecoder.TryGetConstructor(e.Instance, "LENGTH_UNIT") is not null && Step242SubsetDecoder.TryGetConstructor(e.Instance, "SI_UNIT")?.Arguments.FirstOrDefault() is Step242EnumValue { Value: "MILLI" }).Id;
        var fresh = document.Entities.Max(e => e.Id) + 1;
        var original = Regex.Match(source, $@"#{control.Id}\s*=.*?;", RegexOptions.Singleline).Value;
        source = source.Replace(original, Regex.Replace(original, $@"#{magnitude.TargetId}\b", "#" + fresh), StringComparison.Ordinal);
        var declarations = $"#{fresh}=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE({inches.ToString("R", CultureInfo.InvariantCulture)}),#{fresh + 1});\n"
            + $"#{fresh + 1}=(CONVERSION_BASED_UNIT('inch',#{fresh + 2})LENGTH_UNIT()NAMED_UNIT(*));\n"
            + $"#{fresh + 2}=LENGTH_MEASURE_WITH_UNIT(LENGTH_MEASURE(25.4),#{mm});\n";
        return source.Insert(source.LastIndexOf("ENDSEC;", StringComparison.Ordinal), declarations);
    }
}
