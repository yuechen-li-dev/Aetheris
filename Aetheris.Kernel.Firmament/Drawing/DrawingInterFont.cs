using System.Reflection;

namespace Aetheris.Kernel.Firmament.Drawing;

internal sealed class DrawingInterFont
{
    private readonly byte[] data;
    private readonly Dictionary<string, (int Offset, int Length)> tables;
    private readonly int hMetrics;
    private readonly int glyphCount;
    private readonly int cmapOffset;

    private DrawingInterFont(byte[] data)
    {
        this.data = data; tables = new(StringComparer.Ordinal);
        var count = U16(4);
        for (var index = 0; index < count; index++)
        {
            var at = 12 + index * 16; var tag = System.Text.Encoding.ASCII.GetString(data, at, 4);
            tables[tag] = ((int)U32(at + 8), (int)U32(at + 12));
        }
        UnitsPerEm = U16(tables["head"].Offset + 18); XMin = I16(tables["head"].Offset + 36); YMin = I16(tables["head"].Offset + 38); XMax = I16(tables["head"].Offset + 40); YMax = I16(tables["head"].Offset + 42);
        Ascent = I16(tables["hhea"].Offset + 4); Descent = I16(tables["hhea"].Offset + 6); hMetrics = U16(tables["hhea"].Offset + 34); glyphCount = U16(tables["maxp"].Offset + 4);
        cmapOffset = SelectCmap();
    }

    public static DrawingInterFont Load()
    {
        using var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Aetheris.Drawing.Inter.ttf") ?? throw new InvalidOperationException("drawing-inter-font-resource-missing");
        using var memory = new MemoryStream(); stream.CopyTo(memory); return new(memory.ToArray());
    }

    public byte[] Bytes => data; public int UnitsPerEm { get; } public short Ascent { get; } public short Descent { get; }
    public short XMin { get; } public short YMin { get; } public short XMax { get; } public short YMax { get; }
    public int Glyph(int unicode)
    {
        var format = U16(cmapOffset);
        if (format == 12)
        {
            var groups = U32(cmapOffset + 12); var at = cmapOffset + 16;
            for (var index = 0; index < groups; index++, at += 12) { var first = U32(at); var last = U32(at + 4); if (unicode >= first && unicode <= last) return (int)(U32(at + 8) + unicode - first); }
            return 0;
        }
        var segments = U16(cmapOffset + 6) / 2; var endCodes = cmapOffset + 14; var startCodes = endCodes + segments * 2 + 2; var deltas = startCodes + segments * 2; var offsets = deltas + segments * 2;
        for (var index = 0; index < segments; index++)
        {
            var end = U16(endCodes + index * 2); var start = U16(startCodes + index * 2); if (unicode < start || unicode > end) continue;
            var delta = I16(deltas + index * 2); var range = U16(offsets + index * 2); if (range == 0) return (unicode + delta) & 0xffff;
            var glyphAt = offsets + index * 2 + range + (unicode - start) * 2; var glyph = U16(glyphAt); return glyph == 0 ? 0 : (glyph + delta) & 0xffff;
        }
        return 0;
    }

    public int Width1000(int unicode)
    {
        var glyph = Math.Clamp(Glyph(unicode), 0, glyphCount - 1); var metric = Math.Min(glyph, hMetrics - 1); var width = U16(tables["hmtx"].Offset + metric * 4);
        return (int)Math.Round(width * 1000d / UnitsPerEm);
    }

    public double MeasureMillimetres(string text, double sizeMillimetres) => text.EnumerateRunes().Sum(rune => Width1000(rune.Value)) * sizeMillimetres / 1000d;

    private int SelectCmap()
    {
        var table = tables["cmap"].Offset; var count = U16(table + 2); int? format12 = null;
        for (var index = 0; index < count; index++)
        {
            var at = table + 4 + index * 8; var platform = U16(at); var encoding = U16(at + 2); var offset = table + (int)U32(at + 4); var format = U16(offset);
            if (platform == 3 && encoding == 1 && format == 4) return offset;
            if (platform == 3 && encoding == 10 && format == 12) format12 = offset;
        }
        return format12 ?? throw new InvalidOperationException("drawing-inter-font-cmap-unsupported");
    }

    private ushort U16(int at) => (ushort)((data[at] << 8) | data[at + 1]);
    private short I16(int at) => unchecked((short)U16(at));
    private uint U32(int at) => ((uint)data[at] << 24) | ((uint)data[at + 1] << 16) | ((uint)data[at + 2] << 8) | data[at + 3];
}
