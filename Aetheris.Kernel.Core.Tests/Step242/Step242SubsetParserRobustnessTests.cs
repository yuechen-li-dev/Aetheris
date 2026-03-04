using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242SubsetParserRobustnessTests
{
    [Fact]
    public void ImportBody_DoesNotFailInParser_ForIsoWrapperCommentsWhitespaceAndExponentNumberForms()
    {
        const string text = """
            ISO-10303-21;
            HEADER;
            FILE_DESCRIPTION(('sample'),'2;1');
            ENDSEC;
            DATA;
            /* multiline
               comment */
            #1 = TEST_ENTITY
            (
              'A',
              (1E-3, -2.5E+02, .5, 3., 0.0)
            )
            ;
            ENDSEC;
            END-ISO-10303-21;
            """;

        var result = Step242Importer.ImportBody(text);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => (d.Source ?? string.Empty).StartsWith("Parser", StringComparison.Ordinal));
    }

    [Fact]
    public void ImportBody_DoesNotFailInParser_ForEnumLogicalDerivedOmittedNestedListsAndEncodedStrings()
    {
        const string text = """
            DATA;
            #1=TEST_ENTITY((#2,#3),($,.T.,*),(),.UNSPECIFIED.,.POSITIVE.,.F.,'O''Brien','\X2\03A9\X0\');
            #2=ITEM();
            #3=ITEM();
            ENDSEC;
            """;

        var result = Step242Importer.ImportBody(text);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.DoesNotContain(result.Diagnostics, d => (d.Source ?? string.Empty).StartsWith("Parser", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("DATA;#1=ITEM('abc);ENDSEC;")]
    [InlineData("DATA;#1=ITEM((1,2);ENDSEC;")]
    [InlineData("DATA;#1=ITEM(1E+);ENDSEC;")]
    [InlineData("DATA;#1=ITEM(#A);ENDSEC;")]
    [InlineData("DATA;#1=ITEM(.BAD);ENDSEC;")]
    [InlineData("DATA;#1=ITEM();#1=OTHER();ENDSEC;")]
    public void ImportBody_FailsInParserDeterministically_ForMalformedInputs(string text)
    {
        var result = Step242Importer.ImportBody(text);

        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.Diagnostics);
        Assert.StartsWith("Parser.", result.Diagnostics[0].Source ?? string.Empty, StringComparison.Ordinal);
    }
}
