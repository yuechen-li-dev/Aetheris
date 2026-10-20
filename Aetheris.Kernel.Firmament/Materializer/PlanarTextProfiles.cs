using System.Text;
using Typography.OpenFont;

namespace Aetheris.Kernel.Firmament.Materializer;

public enum PlanarTextAlignment { Left, Center, Right }

public sealed record PlanarTextGlyph(int Index, int Codepoint, int SourceStart, int SourceLength,
    ushort FontGlyphIndex, double BaselineX, double Advance, int ContourCount);

/// <summary>Exact font curves grouped into one Aetheris profile per material island.</summary>
public sealed record PlanarTextProfileResult(IReadOnlyList<ResolvedProfile2D> Regions,
    IReadOnlyList<PlanarTextGlyph> Glyphs, int ContourCount, int CounterCount,
    double Advance, IReadOnlyList<string> Diagnostics, int ResolvedIntersections = 0)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

/// <summary>
/// X0 single-line outline conversion. Height denotes the font em in millimetres;
/// glyphs retain their baseline and exact line/cubic polynomial boundaries.
/// </summary>
public static class PlanarTextProfiles
{
    private static readonly Lazy<Typeface> DefaultTypeface = new(LoadInter);

    /// <summary>Presentation contours only. Drawing an outline does not require the
    /// material-island arrangement/Boolean normalization used for engineering text.</summary>
    internal static IReadOnlyList<LineArcProfileCurve2D> PresentationContours(string content, double height)
    {
        if (string.IsNullOrEmpty(content) || content.IndexOfAny(['\r', '\n']) >= 0 || !double.IsFinite(height) || height <= 0)
            throw new InvalidOperationException("pmi-display-text-invalid");
        var typeface = DefaultTypeface.Value;
        var scale = height / typeface.UnitsPerEm;
        var cursor = 0d;
        var curves = new List<LineArcProfileCurve2D>();
        foreach (var rune in content.EnumerateRunes())
        {
            var index = typeface.GetGlyphIndex(rune.Value);
            if (index == 0) throw new InvalidOperationException($"pmi-display-text-unsupported: Missing glyph U+{rune.Value:X4}.");
            var glyph = typeface.GetGlyph(index);
            if (glyph.IsCffGlyph) throw new InvalidOperationException("pmi-display-text-unsupported: CFF glyph.");
            var translator = new OutlineTranslator();
            IGlyphReaderExtensions.Read(translator, glyph.GlyphPoints, glyph.EndPoints);
            curves.AddRange(translator.Contours.SelectMany(contour => contour.Transform(cursor, scale).Curves));
            cursor += typeface.GetAdvanceWidthFromGlyphIndex(index) * scale;
        }
        return curves;
    }

    public static PlanarTextProfileResult Build(string content, double height,
        PlanarTextAlignment alignment = PlanarTextAlignment.Left,
        ConstructionPlane? supportFrame = null, string featureId = "Text")
    {
        // BUILD-PERF-M4: outline conversion and glyph normalization are a pure function of these
        // arguments (one font, immutable records) and cost ~1.3 s per bolt rebuild; recent results
        // are reused. Only frame-free requests are memoized.
        if (supportFrame is not null || content is null) return BuildUncached(content!, height, alignment, supportFrame, featureId);
        var key = (content, BitConverter.DoubleToInt64Bits(height), alignment, featureId);
        lock (RecentBuilds)
        {
            var hit = RecentBuilds.FindIndex(entry => entry.Key == key);
            if (hit >= 0) return RecentBuilds[hit].Result;
        }
        var result = BuildUncached(content, height, alignment, null, featureId);
        lock (RecentBuilds)
        {
            RecentBuilds.Add((key, result));
            if (RecentBuilds.Count > 16) RecentBuilds.RemoveAt(0);
        }
        return result;
    }

    private static readonly List<((string, long, PlanarTextAlignment, string) Key, PlanarTextProfileResult Result)> RecentBuilds = [];

    private static PlanarTextProfileResult BuildUncached(string content, double height,
        PlanarTextAlignment alignment, ConstructionPlane? supportFrame, string featureId)
    {
        var diagnostics = new List<string>();
        if (string.IsNullOrEmpty(content)) diagnostics.Add("text-content-empty");
        if (!double.IsFinite(height) || height <= 0) diagnostics.Add("text-height-must-be-positive");
        if (content is not null && content.IndexOfAny(['\r', '\n']) >= 0) diagnostics.Add("text-multiline-unsupported-x0");
        if (diagnostics.Count > 0) return new([], [], 0, 0, 0, diagnostics);
        content ??= string.Empty;

        var typeface = DefaultTypeface.Value;
        var scale = height / typeface.UnitsPerEm;
        var glyphs = new List<PlanarTextGlyph>();
        var outlines = new List<(int GlyphIndex, int Codepoint, int SourceStart, IReadOnlyList<CurveContour> Contours)>();
        var cursor = 0d;
        var sourceStart = 0;
        foreach (var rune in content.EnumerateRunes())
        {
            var glyphIndex = typeface.GetGlyphIndex(rune.Value);
            if (glyphIndex == 0)
            {
                diagnostics.Add($"text-glyph-missing:U+{rune.Value:X4}");
                break;
            }
            var glyph = typeface.GetGlyph(glyphIndex);
            if (glyph.IsCffGlyph)
            {
                diagnostics.Add($"text-cff-glyph-unsupported-x0:U+{rune.Value:X4}");
                break;
            }
            var translator = new OutlineTranslator();
            IGlyphReaderExtensions.Read(translator, glyph.GlyphPoints, glyph.EndPoints);
            var contours = translator.Contours;
            if (contours.Count == 0 && !Rune.IsWhiteSpace(rune))
            {
                diagnostics.Add($"text-glyph-empty-outline:U+{rune.Value:X4}");
                break;
            }
            var advance = typeface.GetAdvanceWidthFromGlyphIndex(glyphIndex) * scale;
            glyphs.Add(new(glyphs.Count, rune.Value, sourceStart, rune.Utf16SequenceLength,
                glyphIndex, cursor, advance, contours.Count));
            outlines.Add((glyphs.Count - 1, rune.Value, sourceStart, contours));
            cursor += advance;
            sourceStart += rune.Utf16SequenceLength;
        }
        if (diagnostics.Count > 0) return new([], glyphs, 0, 0, cursor, diagnostics);

        var shift = alignment switch
        {
            PlanarTextAlignment.Center => -cursor / 2,
            PlanarTextAlignment.Right => -cursor,
            _ => 0d
        };
        var regions = new List<ResolvedProfile2D>();
        var contourCount = 0;
        var counterCount = 0;
        var resolvedIntersections = 0;
        foreach (var item in outlines)
        {
            var placed = item.Contours.Select(c => c.Transform(
                glyphs[item.GlyphIndex].BaselineX + shift, scale)).ToArray();
            contourCount += placed.Length;
            var normalized = GlyphRegionNormalizer.Normalize(placed.Select(c => c.Curves).ToArray());
            resolvedIntersections += normalized.Intersections;
            diagnostics.AddRange(normalized.Diagnostics.Select(d => $"Glyph[{item.GlyphIndex}]:{d}"));
            for (var i = 0; i < normalized.Regions.Count; i++)
            {
                var region = normalized.Regions[i];
                var profileName = $"{featureId}.Glyph[{item.GlyphIndex}].Region[{i}]";
                var loops = new List<ResolvedProfileLoop2D>
                {
                    ToLoop(new(region.Outer), true, profileName, "Outer", item.GlyphIndex,
                        item.Codepoint, item.SourceStart, glyphs[item.GlyphIndex].SourceLength, supportFrame)
                };
                for (var j = 0; j < region.Holes.Count; j++)
                    loops.Add(ToLoop(new(region.Holes[j]), false, profileName, $"Counter[{j}]",
                        item.GlyphIndex, item.Codepoint, item.SourceStart,
                        glyphs[item.GlyphIndex].SourceLength, supportFrame));
                counterCount += region.Holes.Count;
                var profile = new ResolvedProfile2D(profileName,
                    supportFrame?.StableId ?? "XY", loops, supportFrame);
                var validation = ResolvedProfile2DValidator.Validate(profile);
                if (!validation.IsValid) diagnostics.AddRange(validation.Diagnostics);
                regions.Add(profile);
            }
        }
        return new(diagnostics.Count == 0 ? regions : [], glyphs, contourCount, counterCount,
            cursor, diagnostics, resolvedIntersections);
    }

    private static Typeface LoadInter()
    {
        using var stream = typeof(PlanarTextProfiles).Assembly.GetManifestResourceStream("Aetheris.Drawing.Inter.ttf")
            ?? throw new InvalidOperationException("text-default-font-resource-missing");
        return new OpenFontReader().Read(stream);
    }

    private static ResolvedProfileLoop2D ToLoop(CurveContour contour, bool outer, string profile,
        string loopName, int glyphIndex, int codepoint, int sourceStart, int sourceLength,
        ConstructionPlane? frame)
    {
        var curves = contour.SignedArea * (outer ? 1 : -1) > 0
            ? contour.Curves : contour.Curves.AsEnumerable().Reverse().Select(Reverse).ToArray();
        return new(loopName, outer, curves.Select((curve, index) =>
        {
            var id = $"{profile}.{loopName}.Segment[{index}]";
            var provenance = new ProfileSegmentProvenance(id, profile,
                $"source-char:{sourceStart}..{sourceStart + sourceLength}",
                $"font-outline:Glyph[{glyphIndex}]:U+{codepoint:X4}",
                frame?.StableId ?? "XY");
            return new ResolvedProfileSegment2D($"{loopName}.Segment[{index}]", curve, provenance);
        }).ToArray());
    }

    private static LineArcProfileCurve2D Reverse(LineArcProfileCurve2D curve) => curve switch
    {
        LineArcLineSegment2D line => new LineArcLineSegment2D(line.End, line.Start),
        LineArcCubicBezier2D cubic => new LineArcCubicBezier2D(cubic.End, cubic.Control2, cubic.Control1, cubic.Start),
        _ => throw new NotSupportedException("text-outline-curve-unsupported")
    };

    private sealed record CurveContour(IReadOnlyList<LineArcProfileCurve2D> Curves)
    {
        public double SignedArea => Curves.Sum(ResolvedProfile2DValidator.SignedAreaContribution);

        public CurveContour Transform(double offsetX, double scale)
        {
            (double X, double Y) P((double X, double Y) p) => (offsetX + p.X * scale, p.Y * scale);
            return new(Curves.Select(c => c switch
            {
                LineArcLineSegment2D line => (LineArcProfileCurve2D)new LineArcLineSegment2D(P(line.Start), P(line.End)),
                LineArcCubicBezier2D cubic => new LineArcCubicBezier2D(P(cubic.Start), P(cubic.Control1), P(cubic.Control2), P(cubic.End)),
                _ => throw new NotSupportedException("text-outline-curve-unsupported")
            }).ToArray());
        }

    }

    // Copeland's TypographyOutlineConversion.OutlineTranslator is the template here:
    // IGlyphReaderExtensions owns TrueType contour decomposition, including implied points.
    private sealed class OutlineTranslator : IGlyphTranslator
    {
        private readonly List<CurveContour> contours = [];
        private readonly List<LineArcProfileCurve2D> current = [];
        private (double X, double Y)? start;
        private (double X, double Y)? position;
        public IReadOnlyList<CurveContour> Contours => contours;
        public void BeginRead(int contourCount) { }
        public void EndRead() => CloseContour();
        public void MoveTo(float x0, float y0) { CloseContour(); start = position = (x0, y0); }
        public void LineTo(float x1, float y1)
        {
            var end = ((double)x1, (double)y1);
            current.Add(new LineArcLineSegment2D(position ?? throw new InvalidOperationException("text-contour-missing-moveto"), end));
            position = end;
        }
        public void Curve3(float x1, float y1, float x2, float y2)
        {
            var a = position ?? throw new InvalidOperationException("text-contour-missing-moveto");
            var control = ((double)x1, (double)y1); var end = ((double)x2, (double)y2);
            current.Add(new LineArcCubicBezier2D(a,
                (a.X + (control.Item1 - a.X) * 2 / 3, a.Y + (control.Item2 - a.Y) * 2 / 3),
                (end.Item1 + (control.Item1 - end.Item1) * 2 / 3, end.Item2 + (control.Item2 - end.Item2) * 2 / 3), end));
            position = end;
        }
        public void Curve4(float x1, float y1, float x2, float y2, float x3, float y3)
        {
            var a = position ?? throw new InvalidOperationException("text-contour-missing-moveto");
            var end = ((double)x3, (double)y3);
            current.Add(new LineArcCubicBezier2D(a, (x1, y1), (x2, y2), end));
            position = end;
        }
        public void CloseContour()
        {
            if (start is { } first && position is { } last)
            {
                if (first != last) current.Add(new LineArcLineSegment2D(last, first));
                if (current.Count > 0) contours.Add(new([.. current]));
            }
            current.Clear(); start = position = null;
        }
    }
}
