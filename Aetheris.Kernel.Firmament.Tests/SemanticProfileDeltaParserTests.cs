using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class SemanticProfileDeltaParserTests
{
    [Fact]
    public void ProfileDeltaFields_AcceptOptionalSemicolons()
    {
        const string withSemicolons = """
            ProfileDelta ServiceTab {
                On: Wall.Outer;
                Anchor: CenteredAt 50mm;
                Side: Outward;
                Level Carrier { Offset: 0mm; }
                Level Extended { Offset: 8mm; }
                Transition Enter { Kind: Step; To: Extended; }
                Span Crown { Run: 32mm; At: Extended; }
                Transition Exit { Kind: Step; To: Carrier; }
            }
            """;
        const string withoutSemicolons = """
            ProfileDelta ServiceTab {
                On: Wall.Outer
                Anchor: CenteredAt 50mm
                Side: Outward
                Level Carrier { Offset: 0mm }
                Level Extended { Offset: 8mm }
                Transition Enter { Kind: Step To: Extended }
                Span Crown { Run: 32mm At: Extended }
                Transition Exit { Kind: Step To: Carrier }
            }
            """;

        var punctuated = SemanticProfileDeltaParser.Parse(withSemicolons);
        var relaxed = SemanticProfileDeltaParser.Parse(withoutSemicolons);

        Assert.True(punctuated.IsSuccess, string.Join(Environment.NewLine, punctuated.Diagnostics));
        Assert.True(relaxed.IsSuccess, string.Join(Environment.NewLine, relaxed.Diagnostics));
        var expected = punctuated.Deltas.Single();
        var actual = relaxed.Deltas.Single();
        Assert.Equal(expected.OwnerPath, actual.OwnerPath);
        Assert.Equal(expected.Delta.Anchor, actual.Delta.Anchor);
        Assert.Equal(expected.Delta.Side, actual.Delta.Side);
        Assert.Equal(expected.Delta.Levels, actual.Delta.Levels);
        Assert.Equal(expected.Delta.Members, actual.Delta.Members);
    }
}
