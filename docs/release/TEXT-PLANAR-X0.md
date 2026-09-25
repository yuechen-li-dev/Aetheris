# TEXT-PLANAR-X0 — qualification status

**Verdict: Meaningful progression.** The owned font dependency is integrated and
its Inter outlines lower to exact Aetheris planar curves. A simple `O` with its
counter passes the existing profile validator and exact extrusion emitter. The
requested engraved `CODEX` bolt, embossed `AETHERIS` plate, STEP witnesses, schema,
and LX authoring are not implemented or qualified.

**Later status:** `TEXT-REGION-NORMALIZE-X1.md` resolves the bundled Inter
`D` overlap and `X` crossing into valid exact CAD profiles. The X0 verdict
below records the earlier qualification state and is retained for history.

## Dependency audit

- Copeland source: `src/ThirdParty/Machina.Typography.OpenFont/`.
- NuGet package and assembly: `Machina.Typography.OpenFont`, version `1.0.0`;
  public namespace: `Typography.OpenFont`.
- The Copeland fork pins LayoutFarm Typography commit
  `5877180c7c5271091379a0eaf9f03ab6ebd256b3`. Its relevant fix preserves
  glyph identity for empty TrueType outlines, so spaces use their own advance.
- Aetheris now uses `OpenFontReader.Read(Stream)`, `Typeface.GetGlyphIndex`,
  `GetGlyph`, `GetAdvanceWidthFromGlyphIndex`, and `IGlyphReaderExtensions.Read`
  with `IGlyphTranslator`. This reuses the package's cmap, outline decomposition,
  and metrics. There is no new font parser or cmap implementation in the text path.
- Copeland's `Machina.Fonts.Generation.Typography.TypographyOutlineConversion`
  translator was inspected and used as the small adapter pattern. Its source was
  not copied because it returns Machina presentation types; Aetheris's adapter
  emits `ResolvedProfile2D` and `LineArcCubicBezier2D` directly.
- The default is Aetheris's existing embedded `Resources/Inter-Regular.ttf`.
  `Resources/Inter-LICENSE.txt` records its SIL Open Font License 1.1. No host
  font lookup or new font asset is involved.

## Implemented profile conversion

`PlanarTextProfiles.Build` accepts a Unicode string, a positive millimetre height,
left/center/right alignment, and an optional `ConstructionPlane` support frame.
Height is the font **em** height. Unicode scalars resolve through the owned
typeface. Advances are scaled from font units and placed on one horizontal
baseline. The parsed immutable typeface is loaded once by `Lazy<Typeface>`.

TrueType lines stay lines. Quadratic curves are degree elevated exactly to cubic
Béziers; cubic outlines stay cubic. Aetheris's existing clamped degree-three
B-spline path materializes the exact polynomial spans. Sampling is used only to
classify contour containment and detect overlap; it is never emitted as CAD
geometry. Nested non-overlapping contours become outer loops and counters, one
`ResolvedProfile2D` per material island. Profile segment provenance includes the
text feature, glyph index, Unicode scalar, and UTF-16 source start.

The qualification tests demonstrate `CO`: two glyphs, three source contours,
two material regions, and one `O` counter. The `O` region with its counter also
extrudes through `ResolvedProfile2DValidator.Extrude` with a 0.25 mm height.
The result is an isolated text solid, not a modification of a support body.

## Isolated blockers

1. The bundled Inter `D` uses overlapping contours. Its contours cross rather
   than nesting as a simple outer/counter pair. The adapter reports
   `text-contour-overlap-requires-arrangement` and returns no profiles.
2. The bundled Inter `X` has a self crossing contour. The existing profile
   validator reports the specific crossing segment pairs. Thus `CODEX` cannot
   yet be a valid profile set.
3. `ProfileArrangement2D` and the Boss/Pocket section stack currently accept
   bounded lines and circular arcs, while font outlines contain cubic Béziers.
   Exact polynomial intersection, splitting, fill classification, and section
   reconstruction would be needed before these profiles can enter that path.
   Flattening to coarse line segments would discard the requested curve quality.

These are geometry authority gaps. The text adapter deliberately does not call
Boss or Pocket, register Firmament `Text` syntax, or advertise schema/LX fields
that would imply materialization works. The intended authoring surface, once the
geometry gaps are closed, remains compact:

```firmament
Text MakerMark {
    Surface: face(Bolt.Head.Top)
    Content: "CODEX"
    Height: 3mm
    Depth: 0.25mm
    Operation: Engrave
    Alignment: Center
}
```

This sketch is **not accepted Firmament source** in this revision.

## Verification and missing witnesses

- `dotnet build Aetheris.slnx -c Release -m:1`: passed.
- `dotnet test Aetheris.Kernel.Firmament.Tests -c Release --filter FullyQualifiedName~PlanarTextProfilesTests -m:1`: six tests passed.
- `dotnet test Aetheris.Kernel.Core.Tests -c Release --no-build --filter "Category!=SlowCorpus"`: 1,000 passed.
- `dotnet test Aetheris.slnx -c Release --no-build -m:1 -- RunConfiguration.MaxCpuCount=1`:
  all 1,670 Firmament tests passed, including the six text tests. The suite
  exited 1 with two CLI failures (HexBolt OBJ vertex count 908 vs expected
  905; SheetMetal CLI validation) and five SheetMetal failures. These are the
  known unrelated baseline categories named in the mission.
- `dotnet run --no-build --project Aetheris.CLI -c Release -- inspect
  fixtures/Thread/hexbolt-threaded.firmament --json`: parsed the existing
  `ThreadExternalHexBolt` model successfully. No Text feature is authored there.

There is no engraved bolt or embossed plate artifact, so no text topology
counts, manifold judgment, STEP reimport result, display image, or meaningful
end-to-end performance numbers can be claimed. Font load, profile conversion,
solid feature lowering, tessellation, and STEP export have not been separately
profiled for the requested words.
