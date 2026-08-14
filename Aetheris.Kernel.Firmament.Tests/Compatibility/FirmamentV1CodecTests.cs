using Aetheris.Kernel.Firmament.Compatibility.V1;
using Aetheris.Kernel.Firmament.ParsedModel;

namespace Aetheris.Kernel.Firmament.Tests.Compatibility;

public sealed class FirmamentV1CodecTests
{
    [Fact]
    public void Toon_And_Json_NormalizeToEquivalentHistoricalModel()
    {
        var toon = new FirmamentV1ToonReader().Read("""
            firmament:
              version: 1

            model:
              name: demo
              units: mm

            ops[1]:
              -
                op: box
                id: base
            """);
        var json = new FirmamentV1JsonReader().Read("""
            { "firmament": { "version": "1" }, "model": { "name": "demo", "units": "mm" }, "ops": [ { "op": "box", "id": "base" } ] }
            """);

        Assert.True(toon.IsSuccess, string.Join(Environment.NewLine, toon.Diagnostics));
        Assert.True(json.IsSuccess, string.Join(Environment.NewLine, json.Diagnostics));
        Assert.Equivalent(Snapshot(toon.Value), Snapshot(json.Value));
    }

    [Fact]
    public void ToonWriter_IsLfOnly_AndReadWriteReadStable()
    {
        const string source = "firmament:\r\n  version: 1\r\n\r\nmodel:\r\n  name: demo\r\n  units: mm\r\n\r\nops[0]:\r\n";
        var parsed = new FirmamentV1ToonReader().Read(source);

        Assert.True(parsed.IsSuccess, string.Join(Environment.NewLine, parsed.Diagnostics));
        var writer = new FirmamentV1ToonWriter();
        var canonical = writer.Write(parsed.Value);
        var reparsed = new FirmamentV1ToonReader().Read(canonical);

        Assert.DoesNotContain("\r", canonical, StringComparison.Ordinal);
        Assert.Equal(canonical, writer.Write(reparsed.Value));
        Assert.Equivalent(Snapshot(parsed.Value), Snapshot(reparsed.Value));
    }

    [Fact]
    public void ExplicitJsonReader_DoesNotFallThroughToToon()
    {
        var result = new FirmamentV1JsonReader().Read("firmament:\n  version: 1");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains("V1 JSON compatibility", StringComparison.Ordinal));
    }

    private static object Snapshot(FirmamentParsedDocument document) => new
    {
        document.Firmament.Version,
        document.Model.Name,
        document.Model.Units,
        Ops = document.Ops.Entries.Select(op => new { op.OpName, op.KnownKind, op.Family, Fields = op.RawFields.OrderBy(pair => pair.Key).ToArray() }).ToArray()
    };
}
