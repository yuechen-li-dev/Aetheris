using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242SubsetParserRobustnessTests
{
    [Fact]
    public void Parse_SupportsIsoWrapperMultilineCommentsAndWhitespace()
    {
        const string text = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('sample'),'2;1');
            ENDSEC;
            DATA;
            /* multiline
               comment */
            #1 = cartesian_point
            (
              'A',
              (1E-3, -2.5E+02, .5)
            )
            ;
            ENDSEC;
            END-ISO-10303-21;
            """;

        var result = Step242SubsetParser.Parse(text);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var entity = Assert.Single(result.Value.Entities);
        Assert.Equal("CARTESIAN_POINT", entity.Name);
        var coords = Assert.IsType<Step242ListValue>(entity.Arguments[1]);
        Assert.Collection(
            coords.Items,
            item => Assert.Equal(1e-3d, Assert.IsType<Step242NumberValue>(item).Value, 6),
            item => Assert.Equal(-250d, Assert.IsType<Step242NumberValue>(item).Value, 6),
            item => Assert.Equal(0.5d, Assert.IsType<Step242NumberValue>(item).Value, 6));
    }

    [Fact]
    public void Parse_SupportsEnumsLogicalDerivedOmittedAndNestedLists()
    {
        const string text = """
            DATA;
            #1=TEST_ENTITY((#2,#3),($,.T.,*),(),.UNSPECIFIED.,.POSITIVE.,.F.);
            #2=ITEM();
            #3=ITEM();
            ENDSEC;
            """;

        var result = Step242SubsetParser.Parse(text);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var entity = result.Value.Entities.Single(e => e.Id == 1);

        var group = Assert.IsType<Step242ListValue>(entity.Arguments[1]);
        Assert.Equal(3, group.Items.Count);
        Assert.IsType<Step242OmittedValue>(group.Items[0]);
        Assert.True(Assert.IsType<Step242BooleanValue>(group.Items[1]).Value);
        Assert.IsType<Step242OmittedValue>(group.Items[2]);

        Assert.Equal("UNSPECIFIED", Assert.IsType<Step242EnumValue>(entity.Arguments[3]).Value);
        Assert.Equal("POSITIVE", Assert.IsType<Step242EnumValue>(entity.Arguments[4]).Value);
        Assert.False(Assert.IsType<Step242BooleanValue>(entity.Arguments[5]).Value);
    }

    [Fact]
    public void Parse_SupportsDoubledAndEncodedStringContentAsRaw()
    {
        const string text = """
            DATA;
            #1=ITEM('O''Brien','\X2\03A9\X0\');
            ENDSEC;
            """;

        var result = Step242SubsetParser.Parse(text);

        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        var entity = Assert.Single(result.Value.Entities);
        Assert.Equal("O'Brien", Assert.IsType<Step242StringValue>(entity.Arguments[0]).Value);
        Assert.Equal("\\X2\\03A9\\X0\\", Assert.IsType<Step242StringValue>(entity.Arguments[1]).Value);
    }

    [Fact]
    public void Parse_FailsDeterministically_ForMalformedInputs()
    {
        var cases = new Dictionary<string, string>
        {
            ["unterminated-string"] = "DATA;#1=ITEM('abc);ENDSEC;",
            ["unbalanced-parens"] = "DATA;#1=ITEM((1,2);ENDSEC;",
            ["bad-exponent"] = "DATA;#1=ITEM(1E+);ENDSEC;",
            ["duplicate-id"] = "DATA;#1=ITEM();#1=OTHER();ENDSEC;",
            ["ref-non-int"] = "DATA;#1=ITEM(#A);ENDSEC;",
            ["malformed-enum"] = "DATA;#1=ITEM(.BAD);ENDSEC;"
        };

        foreach (var (name, text) in cases)
        {
            var result = Step242SubsetParser.Parse(text);
            Assert.False(result.IsSuccess, $"Case '{name}' unexpectedly succeeded.");
            Assert.NotEmpty(result.Diagnostics);
            Assert.StartsWith("Parser.", result.Diagnostics[0].Source ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
