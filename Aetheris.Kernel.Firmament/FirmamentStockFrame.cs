using Aetheris.Kernel.Firmament.Materializer;

namespace Aetheris.Kernel.Firmament;

/// <summary>
/// Semantic placement authority for an authored stock primitive.  Kernel primitives remain
/// mathematically centered; Firmament stock is bottom-anchored in its authored body frame.
/// </summary>
public sealed record FirmamentStockFrame(
    string Authority,
    double OriginX,
    double OriginY,
    double OriginZ,
    double SizeX,
    double SizeY,
    double SizeZ)
{
    public double XMin => OriginX - SizeX / 2d;
    public double XMax => OriginX + SizeX / 2d;
    public double YMin => OriginY - SizeY / 2d;
    public double YMax => OriginY + SizeY / 2d;
    public double ZMin => OriginZ;
    public double ZMax => OriginZ + SizeZ;
    public double KernelCenteredToStockZ => OriginZ + SizeZ / 2d;

    /// <summary>Current Firmament Box law: XY-centered, Bottom at local Z=0, Top at local Z=Height.</summary>
    public static FirmamentStockFrame ForBox(double width, double depth, double height) =>
        new("FirmamentBoxBottomAnchoredV2", 0d, 0d, 0d, width, depth, height);

    internal AirHoleSimpleShaftHost CreateHoleHost() =>
        new(SizeX, SizeY, ZMin, ZMax, StockFrame: this);
}
