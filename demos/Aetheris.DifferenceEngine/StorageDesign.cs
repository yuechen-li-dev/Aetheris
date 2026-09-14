using System.Globalization;
using System.Text;

namespace Aetheris.DifferenceEngine;

/// <summary>Millimetres; vertical stationary shafts and gravity-biased index pins.
/// Gear teeth and decimal positions are deliberately independent.</summary>
public sealed record StorageDesign
{
    public int Positions => 10;
    public int GearTeeth => 20;
    public double GearModule => 2;
    public double WheelRadius => 32;
    public double IndexRadius => 26;
    public double IndexHoleDiameter => 4.4;
    public double PinDiameter => 4;
    public double ShaftDiameter => 8;
    public double SleeveInsideDiameter => 8.4;
    public double SleeveOutsideDiameter => 12;
    public double BondedBoreDiameter => 12.3;
    public double IndexBottom => 14;
    public double IndexTop => 20;
    public double PinTipReady => 16;
    public double PinLift => 5;
    public double GearBottom => 2;
    public double GearWidth => 8;
    public double SupportTop => 32;
    public double StagePitch => 100;
    private static string N(double x) => x.ToString("G17", CultureInfo.InvariantCulture);
    private static string L(double x) => N(x) + "mm";
    private static string Frame(double x=0, double y=0, double z=0)
        => $"DatumFrame Frame = [{N(x)},{N(y)},{N(z)}] x [1,0,0] y [0,1,0] z [0,0,1];";

    public void WriteSources(string output, string fixtureDirectory)
    {
        Directory.CreateDirectory(Path.Combine(output, "parts"));
        Directory.CreateDirectory(Path.Combine(output, "modules"));
        File.Copy(Path.Combine(fixtureDirectory, "parts", "Storage.firmament"), Path.Combine(output, "parts", "Storage.firmament"), true);
        File.WriteAllText(Path.Combine(output, "parts", "Gear.firmament"), $$"""
            SpurGear TransmissionGear {
                Module: {{L(GearModule)}} Teeth: {{GearTeeth}} PressureAngle: 20deg
                FaceWidth: {{L(GearWidth)}} BoreDiameter: {{L(BondedBoreDiameter)}} Backlash: 0.15mm Phase: 0deg
            }
            """);
        var members = new List<(string Name, string Type, double X, double Y, double Z, bool Rotates)>
        {
            ("Shaft", $"RoundBar<R: {L(ShaftDiameter/2)}, Zlow: -12mm, Zhigh: 44mm>",0,0,0,false),
            ("Sleeve", $"Annulus<Rout: {L(SleeveOutsideDiameter/2)}, Rin: {L(SleeveInsideDiameter/2)}, Zlow: 0mm, Zhigh: 22mm>",0,0,0,true),
            ("Gear", "TransmissionGear",0,0,GearBottom,true),
            ("Display", $"Annulus<Rout: {L(WheelRadius)}, Rin: {L(BondedBoreDiameter/2)}, Zlow: 10mm, Zhigh: 14mm>",0,0,0,true),
            ("IndexPlate", $"DecimalIndexPlate<Rout: {L(WheelRadius)}, Rin: {L(BondedBoreDiameter/2)}, PitchR: {L(IndexRadius)}, HoleD: {L(IndexHoleDiameter)}, Zlow: {L(IndexBottom)}, Zhigh: {L(IndexTop)}>",0,0,0,true),
            ("Bond", $"Annulus<Rout: {L(BondedBoreDiameter/2)}, Rin: {L(SleeveOutsideDiameter/2)}, Zlow: 2mm, Zhigh: 20mm>",0,0,0,true),
            ("LowerRetainer", $"Annulus<Rout: 7mm, Rin: {L(ShaftDiameter/2)}, Zlow: -3mm, Zhigh: -0.5mm>",0,0,0,false),
            ("UpperRetainer", $"Annulus<Rout: 7mm, Rin: {L(ShaftDiameter/2)}, Zlow: 22.5mm, Zhigh: 25mm>",0,0,0,false),
            ("LowerSupport", "StorageSupport<W: 88mm, D: 80mm, H: 6mm, ShaftD: 8mm, PinX: 26mm, PinD: 4.4mm, MountX: 34mm, MountY: -30mm>",0,0,-10,false),
            ("UpperSupport", "StorageSupport<W: 88mm, D: 80mm, H: 6mm, ShaftD: 8mm, PinX: 26mm, PinD: 4.4mm, MountX: 34mm, MountY: -30mm>",0,0,SupportTop,false),
            ("IndexPin", $"RoundBar<R: {L(PinDiameter/2)}, Zlow: {L(PinTipReady)}, Zhigh: {L(SupportTop+6)}>",IndexRadius,0,0,false),
            ("PinHead", $"RoundBar<R: 5mm, Zlow: {L(SupportTop+6)}, Zhigh: {L(SupportTop+10)}>",IndexRadius,0,0,false)
        };
        foreach (var (side,x) in new[] { ("Left",-34d),("Right",34d) })
        {
            members.Add((side+"Spacer","Annulus<Rout: 6mm, Rin: 2.25mm, Zlow: -4mm, Zhigh: 32mm>",x,-30,0,false));
            members.Add((side+"TieBolt","RoundBar<R: 2mm, Zlow: -10mm, Zhigh: 42mm>",x,-30,0,false));
            members.Add((side+"BoltHead","RoundBar<R: 4mm, Zlow: -14mm, Zhigh: -10mm>",x,-30,0,false));
            members.Add((side+"NutEnvelope","Annulus<Rout: 4mm, Rin: 2mm, Zlow: 38mm, Zhigh: 42mm>",x,-30,0,false));
        }
        var source = new StringBuilder("Include \"../parts/Storage.firmament\";\nInclude \"../parts/Gear.firmament\";\nInterface<Fixed> StorageRestSeat { }\nSubassembly DecimalDigit {\n<Assembly DecimalDigit>\n");
        foreach (var p in members)
        {
            source.AppendLine($"<Part {p.Name} = {p.Type}>");
            source.AppendLine($"Semantic Mount {{ {Frame()} }}");
            if (p.Name == "Shaft")
                foreach (var c in members.Where(c=>c.Name != "Shaft")) source.AppendLine($"Semantic Seat{c.Name} {{ {Frame(c.X,c.Y,c.Z)} }}");
            source.AppendLine("</Part>");
        }
        source.AppendLine("</Assembly>\nAnchor: DecimalDigit.Shaft.Mount;");
        foreach (var p in members.Where(p=>p.Name!="Shaft"))
            source.AppendLine($"Mate Place{p.Name}: StorageRestSeat {{ A: DecimalDigit.{p.Name}.Mount; B: DecimalDigit.Shaft.Seat{p.Name}; }}");
        source.AppendLine("Expose {\nSemantic Mount = Shaft.Mount;\nGear Drive = Gear;");
        // The module owns which leaves move together; consumers resolve only exposed ports.
        foreach (var p in members.Where(p=>p.Rotates || p.Name is "IndexPin" or "PinHead"))
            source.AppendLine($"Semantic {p.Name}Position = {p.Name}.Mount;");
        source.AppendLine("}\n}");
        File.WriteAllText(Path.Combine(output,"modules","DecimalDigit.firmament"),source.ToString());
        File.WriteAllText(Path.Combine(output,"StorageGate.firmament"),$$"""
            Units: mm
            Include "modules/DecimalDigit.firmament";
            Assembly StorageGate {
                <Assembly StorageGate>
                    <Assembly Source = DecimalDigit></Assembly>
                    <Assembly Destination = DecimalDigit>
                        Placement ImportedOccurrence = [1,0,0,0, 0,1,0,0, 0,0,1,0, {{N(StagePitch)}},0,0,1];
                    </Assembly>
                </Assembly>
                Anchor: StorageGate.Source.Mount;
            }
            """);
    }
}
