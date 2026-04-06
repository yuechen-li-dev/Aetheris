using System.Globalization;
using Aetheris.Kernel.Firmament.Formatting;
using Aetheris.Kernel.Firmament.ParsedModel;
using Aetheris.Kernel.Firmament.Parsing;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class FirmamentFormatterTests
{
    public static TheoryData<string, string> MessyNormalizationCases => new()
    {
        {
            """
            model:
             name : demo
             units: mm
            
            ops [1]:
               -
                  op : box
                  id: base
                  size[3]: [100,50, 10]
            
            firmament:
             version: 1
            """,
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: box
                id: base
                size[3]:
                  100
                  50
                  10
            """
        },
        {
            """
            ops[1]:
             -
               op: subtract
               id: cut1
               from : base
               with:
                  op : cylinder
                  radius: 5
                  height : 20
            
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            """,
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: subtract
                id: cut1
                from: base
                with:
                  op: cylinder
                  radius: 5
                  height: 20
            """
        },
        {
            """
            firmament:
             version: 1
            
            model:
                name: demo
                units : mm
            
            ops [2]:
                -
                  op : expect_exists
                  target : base
                -
                  op: expect_selectable
                  target: base.faces
                  count : 4
            """,
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[2]:
              -
                op: expect_exists
                target: base
            
              -
                op: expect_selectable
                target: base.faces
                count: 4
            """
        },
        {
            """
            model:
              name: demo
              units: mm
            
            firmament:
              version: 1
            
            ops [1]:
             -
                op: sphere
                id: ball
                radius : 3
                place:
                   on : origin
                   offset [3] : [ 1, 2,3 ]
            """,
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            ops[1]:
              -
                op: sphere
                id: ball
                radius: 3
                place:
                  on: origin
                  offset[3]:
                    1
                    2
                    3
            """
        },
        {
            """
            ops[0]:
            
            schema:
               process : injection_molded
               gate_location:
                  x : 1
                  y: 2
                  z : 3
               draft_angle : 2
            
            firmament:
              version: 1
            
            model:
             name: demo
             units: mm
            """,
            """
            firmament:
              version: 1
            
            model:
              name: demo
              units: mm
            
            schema:
              process: injection_molded
              gate_location:
                x: 1
                y: 2
                z: 3
              draft_angle: 2
            
            ops[0]:
            """
        }
    };

    [Theory]
    [InlineData("testdata/firmament/fixtures/m3a-valid-primitive-only-lower.firmament")]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7a-valid-box-origin-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-cnc.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-additive.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-injection-molded.firmament")]
    public void Format_CanonicalFixtures_RoundTrip_As_ByteForByte_NoOp(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var result = Format(source);

        Assert.True(result.Formatting.IsSuccess);
        Assert.Equal(source, result.Formatting.Value.Text);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m3a-valid-primitive-only-lower.firmament")]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7d-valid-boolean-selector-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8c-valid-schema-cnc-minimum-tool-radius.firmament")]
    public void Format_Is_Deterministic_For_Supported_Canonical_Fixtures(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);

        var first = Format(source);
        var second = Format(source);
        var third = Format(first.Formatting.Value.Text);

        Assert.True(first.Formatting.IsSuccess);
        Assert.True(second.Formatting.IsSuccess);
        Assert.True(third.Formatting.IsSuccess);
        Assert.Equal(first.Formatting.Value.Text, second.Formatting.Value.Text);
        Assert.Equal(first.Formatting.Value.Text, third.Formatting.Value.Text);
    }

    [Theory]
    [MemberData(nameof(MessyNormalizationCases))]
    public void Format_MessyButParseableInput_NormalizesToCanonicalOutput(string source, string expected)
    {
        var result = Format(source);

        Assert.True(result.Formatting.IsSuccess);
        Assert.Equal(FirmamentCorpusHarness.NormalizeLf(expected) + "\n", result.Formatting.Value.Text);
    }

    [Theory]
    [MemberData(nameof(MessyNormalizationCases))]
    public void Format_MessyButParseableInput_Remains_Deterministic(string source, string expected)
    {
        var first = Format(source);
        var second = Format(first.Formatting.Value.Text);

        Assert.True(first.Formatting.IsSuccess);
        Assert.True(second.Formatting.IsSuccess);
        Assert.Equal(FirmamentCorpusHarness.NormalizeLf(expected) + "\n", first.Formatting.Value.Text);
        Assert.Equal(first.Formatting.Value.Text, second.Formatting.Value.Text);
    }

    [Theory]
    [InlineData("testdata/firmament/fixtures/m3b-mixed-primitive-boolean-validation.firmament")]
    [InlineData("testdata/firmament/fixtures/m7d-valid-boolean-selector-placement.firmament")]
    [InlineData("testdata/firmament/fixtures/m8a-valid-schema-injection-molded.firmament")]
    public void Format_Does_Not_Change_Parsed_Meaning_For_Supported_Fixtures(string fixturePath)
    {
        var source = FirmamentCorpusHarness.ReadFixtureText(fixturePath);
        var formatted = Format(source);

        Assert.True(formatted.Formatting.IsSuccess);

        var originalParse = FirmamentTopLevelParser.Parse(source);
        var reparsed = FirmamentTopLevelParser.Parse(formatted.Formatting.Value.Text);

        Assert.True(originalParse.IsSuccess);
        Assert.True(reparsed.IsSuccess);
        Assert.Equivalent(CreateMeaningSnapshot(originalParse.Value), CreateMeaningSnapshot(reparsed.Value));
    }

    [Theory]
    [MemberData(nameof(MessyNormalizationCases))]
    public void Format_MessyButParseableInput_Preserves_Parsed_Meaning(string source, string _)
    {
        var originalParse = FirmamentTopLevelParser.Parse(FirmamentCorpusHarness.NormalizeLf(source));
        var formatted = Format(source);
        var reparsed = FirmamentTopLevelParser.Parse(formatted.Formatting.Value.Text);

        Assert.True(originalParse.IsSuccess);
        Assert.True(formatted.Formatting.IsSuccess);
        Assert.True(reparsed.IsSuccess);
        Assert.Equivalent(CreateMeaningSnapshot(originalParse.Value), CreateMeaningSnapshot(reparsed.Value));
    }

    [Fact]
    public void Format_CanonicalPmiSection_Preserves_EmptyExplicitArrayShape()
    {
        const string source = """
        firmament:
          version: 1
        
        model:
          name: demo
          units: mm
        
        ops[0]:
        
        pmi[0]:
        """;

        var result = Format(source);

        Assert.True(result.Formatting.IsSuccess);
        Assert.Equal(FirmamentCorpusHarness.NormalizeLf(source) + "\n", result.Formatting.Value.Text);
    }

    [Fact]
    public void Format_InvalidInput_Reuses_ParseFailureDiagnostics()
    {
        const string source = """
        firmament
          version: 1
        """;

        var result = Format(source);

        Assert.False(result.Formatting.IsSuccess);
        Assert.NotEmpty(result.Formatting.Diagnostics);
    }

    private static FirmamentFormatResult Format(string source)
    {
        var formatter = new FirmamentFormatter();
        return formatter.Format(new FirmamentFormatRequest(new FirmamentSourceDocument(FirmamentCorpusHarness.NormalizeLf(source))));
    }

    private static FirmamentMeaningSnapshot CreateMeaningSnapshot(FirmamentParsedDocument document)
    {
        return new FirmamentMeaningSnapshot(
            document.Firmament.Version,
            document.Model.Name,
            document.Model.Units,
            document.Schema is null ? null : CreateSchemaSnapshot(document.Schema),
            document.Pmi is not null,
            document.Ops.Entries.Select(CreateOpSnapshot).ToArray());
    }

    private static FirmamentSchemaMeaningSnapshot CreateSchemaSnapshot(FirmamentParsedSchema schema)
    {
        return new FirmamentSchemaMeaningSnapshot(
            schema.Process,
            schema.ProcessRaw,
            schema.MinimumToolRadius,
            schema.MinimumToolRadiusRaw,
            schema.MinimumWallThickness,
            schema.MinimumWallThicknessRaw,
            schema.PartingPlane,
            schema.HasGateLocation,
            schema.GateLocation is null ? null : new FirmamentGateLocationMeaningSnapshot(schema.GateLocation.XRaw, schema.GateLocation.YRaw, schema.GateLocation.ZRaw),
            schema.DraftAngle,
            schema.DraftAngleRaw,
            schema.PrinterResolution,
            schema.PrinterResolutionRaw);
    }

    private static FirmamentOpMeaningSnapshot CreateOpSnapshot(FirmamentParsedOpEntry entry)
    {
        var fields = entry.RawFields
            .Where(pair => !string.Equals(pair.Key, "place", StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new FirmamentFieldMeaningSnapshot(pair.Key, NormalizeValue(pair.Value)))
            .ToArray();

        var placement = entry.Placement is null
            ? null
            : new FirmamentPlacementMeaningSnapshot(
                entry.Placement.On switch
                {
                    FirmamentParsedPlacementOriginAnchor => "origin",
                    FirmamentParsedPlacementSelectorAnchor selector => selector.Selector,
                    _ => throw new InvalidOperationException("Unknown placement anchor.")
                },
                entry.Placement.Offset.Select(value => value.ToString("G", CultureInfo.InvariantCulture)).ToArray());

        return new FirmamentOpMeaningSnapshot(entry.KnownKind, entry.Family, fields, placement);
    }

    private static string NormalizeValue(string raw)
    {
        var value = FirmamentFormatValueParser.Parse(raw);
        return value switch
        {
            FirmamentScalarValue scalar => $"scalar:{scalar.Value}",
            FirmamentArrayValue array => $"array:[{string.Join(",", array.Items.Select(NormalizeValue))}]",
            FirmamentObjectValue obj => $"object:{{{string.Join(",", obj.Members.OrderBy(member => member.Name, StringComparer.Ordinal).Select(member => $"{member.Name}={NormalizeValue(member.Value)}"))}}}",
            _ => throw new InvalidOperationException("Unknown formatter value node.")
        };
    }

    private static string NormalizeValue(FirmamentFormatValue value)
    {
        return value switch
        {
            FirmamentScalarValue scalar => $"scalar:{scalar.Value}",
            FirmamentArrayValue array => $"array:[{string.Join(",", array.Items.Select(NormalizeValue))}]",
            FirmamentObjectValue obj => $"object:{{{string.Join(",", obj.Members.OrderBy(member => member.Name, StringComparer.Ordinal).Select(member => $"{member.Name}={NormalizeValue(member.Value)}"))}}}",
            _ => throw new InvalidOperationException("Unknown formatter value node.")
        };
    }

    private sealed record FirmamentMeaningSnapshot(
        string Version,
        string ModelName,
        string Units,
        FirmamentSchemaMeaningSnapshot? Schema,
        bool HasPmi,
        IReadOnlyList<FirmamentOpMeaningSnapshot> Ops);

    private sealed record FirmamentSchemaMeaningSnapshot(
        FirmamentParsedSchemaProcess Process,
        string? ProcessRaw,
        double? MinimumToolRadius,
        string? MinimumToolRadiusRaw,
        double? MinimumWallThickness,
        string? MinimumWallThicknessRaw,
        string? PartingPlane,
        bool HasGateLocation,
        FirmamentGateLocationMeaningSnapshot? GateLocation,
        double? DraftAngle,
        string? DraftAngleRaw,
        double? PrinterResolution,
        string? PrinterResolutionRaw);

    private sealed record FirmamentGateLocationMeaningSnapshot(string? X, string? Y, string? Z);

    private sealed record FirmamentOpMeaningSnapshot(
        FirmamentKnownOpKind KnownKind,
        FirmamentOpFamily Family,
        IReadOnlyList<FirmamentFieldMeaningSnapshot> Fields,
        FirmamentPlacementMeaningSnapshot? Placement);

    private sealed record FirmamentFieldMeaningSnapshot(string Name, string Value);

    private sealed record FirmamentPlacementMeaningSnapshot(string On, IReadOnlyList<string> Offset);
}
