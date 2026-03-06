using Aetheris.Kernel.Core.Brep.Tessellation;

namespace Aetheris.Kernel.Core.Tests.Brep.Tessellation;

public sealed class CircleEdgeTrimAuditWriterTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void ComposeEffectiveForwardSense_UsesExpectedComposition(bool orientedEdgeOrientation, bool edgeCurveSameSense, bool expected)
    {
        var actual = CircleEdgeTrimAuditWriter.ComposeEffectiveForwardSense(orientedEdgeOrientation, edgeCurveSameSense);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Append_IsNoOp_WhenDisabled()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"aetheris-audit-{Guid.NewGuid():N}.jsonl");
        var writer = CircleEdgeTrimAuditWriter.CreateForTesting(enabled: false, path: temp);
        var audit = new CircleEdgeTrimAudit(
            null, null, null, null,
            1, 1, 1, 2,
            "Circle3",
            new AuditPoint(0, 0, 0),
            new AuditPoint(0, 0, 1),
            1,
            new AuditPoint(1, 0, 0),
            new AuditPoint(0, 1, 0),
            new AuditPoint(1, 0, 0),
            new AuditPoint(0, 1, 0),
            0,
            0,
            new AuditPoint(1, 0, 0),
            new AuditPoint(0, 1, 0),
            0,
            1,
            1,
            1,
            2,
            "ShortArc",
            true,
            true,
            true,
            true,
            2,
            new AuditPoint(1, 0, 0),
            new AuditPoint(0, 1, 0),
            false,
            null);

        var shouldStop = writer.Append(audit);

        Assert.False(shouldStop);
        Assert.False(File.Exists(temp));
    }
}
