namespace Aetheris.Kernel.Firmament.Materializer;

/// <summary>Compatibility entry point. Canonical witnesses author MakerMark in StandardPart.</summary>
[Obsolete("Author MakerMark, MakerMarkHeight and MakerMarkDepth on the HexBolt StandardPart instead.")]
public static class ThreadedHexBoltMakerMark
{
    public static PlanarCapEngraving.Result Build(string source, string content, double heightMm, double depthMm)
        => PlanarCapEngraving.Build(source, content, heightMm, depthMm);
}
