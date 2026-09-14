using System.Globalization;
using System.Text;

namespace Aetheris.DifferenceEngine.Showcase;

/// <summary>Bounded CAD author; presentation consumes its exported occurrence tree.</summary>
public sealed record ShowcaseDesign(double DigitPitch = 80)
{
    public int Digits => 4;
    public int Registers => 3;
    public double RegisterPitch => 128;
    public double FirstLevel => 42;
    public double Top => FirstLevel + 3 * DigitPitch + 64;
    public double GantryBottom => Top + 10;
    public double GantryThickness => 8;
    public double ShaftTop => GantryBottom + GantryThickness + 4;
    public void Validate() { if (!double.IsFinite(DigitPitch) || DigitPitch is < 80 or > 100) throw new ArgumentException("DigitPitch must be 80–100 mm."); }
    private static string N(double x) => x.ToString("G17", CultureInfo.InvariantCulture);
    private static string L(double x) => N(x) + "mm";
    private static string Ring(double ro, double ri, double z0, double z1) => $"Annulus<Rout: {L(ro)}, Rin: {L(ri)}, Zlow: {L(z0)}, Zhigh: {L(z1)}>";
    private static string Rod(double r, double z0, double z1) => $"RoundBar<R: {L(r)}, Zlow: {L(z0)}, Zhigh: {L(z1)}>";
    private static string Box(double w, double d, double h) => $"RectangularStock<W: {L(w)}, D: {L(d)}, H: {L(h)}>";
    private static string StockAt(double x,double y,double z,double w,double d,double h) => $"PositionedStock<X: {L(x)}, Y: {L(y)}, Zlow: {L(z)}, Zhigh: {L(z+h)}, W: {L(w)}, D: {L(d)}>";
    private const string Joint = "Semantic Joint { Axis Axis = [0,0,0] -> [0,0,1]; Plane Seat = [0,0,0] normal [0,0,1]; DatumFrame Frame = [0,0,0] x [1,0,0] y [0,1,0] z [0,0,1]; }";
    private static string Placement(double x, double y, double z, double angle = 0) {
        var c = N(double.Cos(angle)); var s = N(double.Sin(angle)); var ns = N(-double.Sin(angle));
        return $"Placement ImportedOccurrence = [{c},{s},0,0, {ns},{c},0,0, 0,0,1,0, {N(x)},{N(y)},{N(z)},1];";
    }
    private static string Part(string name, string type, double x=0, double y=0, double z=0, double angle=0,string semantics="",bool placed=true)
    {
        // A module's mounting datum is its origin, expressed in this part's
        // local frame. Its actual journal datum remains the separate Joint.
        var c=double.Cos(angle);var s=double.Sin(angle);
        var mount=$"Semantic Mount {{ DatumFrame Frame = [{N(-c*x-s*y)},{N(s*x-c*y)},{N(-z)}] x [{N(c)},{N(-s)},0] y [{N(s)},{N(c)},0] z [0,0,1]; }}";
        return $"<Part {name} = {type}> {Joint} {mount} {semantics} {(placed?Placement(x,y,z,angle):"")} </Part>\n";
    }
    private static string Child(string name, string type, double x=0, double y=0, double z=0)
        => $"<Assembly {name} = {type}> {Placement(x,y,z)} </Assembly>\n";
    private static string Module(string name, string members, string anchor, string relationships, string exposed)
        => $"Subassembly {name} {{\n<Assembly {name}>\n{members}</Assembly>\nAnchor: {name}.{anchor.Replace(".Joint", ".Mount", StringComparison.Ordinal)};\n{relationships}\nExpose {{ {exposed.Replace(".Joint;", ".Mount;", StringComparison.Ordinal)} }}\n}}\n";

    public void WriteSources(string destination, string fixtures)
    {
        Validate();
        Directory.CreateDirectory(Path.Combine(destination,"parts")); Directory.CreateDirectory(Path.Combine(destination,"modules"));
        foreach (var file in new[]{"Storage.firmament","Showcase.firmament"}) File.Copy(Path.Combine(fixtures,"parts",file),Path.Combine(destination,"parts",file),true);
        void Write(string file,string text) => File.WriteAllText(Path.Combine(destination,"modules",file),text);
        var stock = "Include \"../parts/Storage.firmament\";\nInclude \"../parts/Showcase.firmament\";\n";

        var wheel = Part("Drum",Ring(34,4.4,14,30)) + Part("LowerRim",Ring(35,4.4,12,14))
            + Part("UpperRim",Ring(35,4.4,30,32)) + Part("Hub",Ring(9,4.4,7,12))
            + Part("IndexRatchet","DecimalRatchet",z:36) + Part("UpperHub",Ring(8,4.4,32,36));
        Write("DigitWheel.firmament",stock+Module("DigitWheel",wheel,"Drum.Joint","","Semantic Mount = Drum.Joint; Gear Ratchet = IndexRatchet;"));

        var pivotX=34-8*.24*double.Cos(double.Pi/12);var pivotY=-8*.24*double.Sin(double.Pi/12);
        var carry = Part("Pawl","CarryPawl",x:34,z:36,angle:double.Pi)
            + Part("Pivot",Rod(2,33,44),pivotX,pivotY) + Part("PivotCollar",Ring(4,2.2,40,42),pivotX,pivotY)
            + Part("Pedestal",StockAt(46,0,8,8,10,25))
            + Part("Tongue",Box(22,10,3),39,0,33)
            + Part("Lever",Box(7,50,4),pivotX,20,42) + Part("LinkPin",Rod(2,44,59),pivotX,40)
            + Part("ResetLink",Box(7,7,17),pivotX,40,59);
        Write("CarryVisualModule.firmament",stock+Module("CarryVisualModule",carry,"Pedestal.Joint","","Semantic Mount = Pedestal.Joint;"));

        var digit = Part("Bearing",Ring(10,4.2,-12,-5)) + Part("DriveGear","RegisterDrive")
            + Part("LowerBridge","BearingBridge<W: 18mm, D: 116mm, H: 6mm, BoreD: 8.4mm>",z:-18)
            + Part("UpperBridge","BearingBridge<W: 18mm, D: 116mm, H: 6mm, BoreD: 8.4mm>",z:54)
            + Part("UpperBearing",Ring(10,4.2,48,54))
            + Child("Wheel","DigitWheel") + Child("Carry","CarryVisualModule");
        foreach(var y in new[]{-52d,52d}) {
            var name=y<0?"Front":"Rear";
            digit += Part(name+"Tie",Rod(2.5,-20,62),0,y)+Part(name+"Cap",Ring(4.5,2.5,60,63),0,y);
        }
        var revolute="Interface<Revolute> DriveJournal { A: DigitModule.DriveGear.Joint; B: DigitModule.Bearing.Joint; }";
        Write("DigitModule.firmament","Include \"DigitWheel.firmament\";\nInclude \"CarryVisualModule.firmament\";\n"
            +Module("DigitModule",digit,"Bearing.Joint",revolute,"Semantic Mount = Bearing.Joint; Gear Drive = DriveGear;"));

        var register = Part("Shaft",Rod(4,-20,ShaftTop-FirstLevel));
        var expose="Semantic Mount = Shaft.Joint;\n";
        for(var i=0;i<Digits;i++) {register+=Child("Digit"+i,"DigitModule",z:i*DigitPitch); expose+=$"Gear Drive{i} = Digit{i}.Drive;\n";}
        register+=Part("Crown",Ring(14,4,Top-FirstLevel-8,Top-FirstLevel))+Part("Foot",Ring(14,4,-20,-12))
            +Part("TopRetainer",Ring(8,4,GantryBottom+GantryThickness-FirstLevel,ShaftTop-FirstLevel));
        Write("Register.firmament","Include \"DigitModule.firmament\";\n"+Module("Register",register,"Shaft.Joint","",expose));

        var transfer="";
        foreach(var (name,x) in new[]{("Input",44d),("Output",84d)}) {
            transfer+=Part(name+"Gear","TransferIdler",x)+Part(name+"Axle",Rod(3,-18,12),x)
                +Part(name+"Bearing",Ring(7,3.2,-12,-5),x)+Part(name+"Collar",Ring(6,3.2,8,11),x);
        }
        transfer+=Part("Bridge","DoubleBearingBridge<W: 56mm>")+Part("RearSupport",Box(12,60,6),64,30,-24);
        Write("TransferModule.firmament",stock+Module("TransferModule",transfer,"Bridge.Joint",
            "Interface<Gear> IdlerMesh { A: TransferModule.InputGear; B: TransferModule.OutputGear; }",
            "Semantic Mount = Bridge.Joint; Gear Input = InputGear; Gear Output = OutputGear;"));
        var bank="";var ports="";
        for(var i=0;i<Digits;i++){bank+=Child("Level"+i,"TransferModule",z:i*DigitPitch);ports+=$"Gear Input{i} = Level{i}.Input; Gear Output{i} = Level{i}.Output;\n";}
        Write("TransferBank.firmament","Include \"TransferModule.firmament\";\n"+Module("TransferBank",bank,"Level0.Mount","",ports));

        var gantrySeats=string.Join("\n",Enumerable.Range(0,3).Select(r=>$"Semantic GantrySeat{r} {{ DatumFrame Frame = [{N(r*RegisterPitch)},0,{N(GantryBottom)}] x [1,0,0] y [0,1,0] z [0,0,1]; }}"));
        var frame=Part("Plinth",StockAt(96,14,-36,484,188,16),semantics:gantrySeats)+Part("BaseDeck",Box(464,168,8),96,14,-20);
        foreach(var (side,x) in new[]{("Left",-128d),("Right",320d)}) foreach(var (depth,y) in new[]{("Front",-60d),("Rear",88d)}) {
            frame+=Part(side+depth+"Post",Rod(7,-12,Top+12),x,y)
                +Part(side+depth+"Foot",Ring(13,7,-14,-6),x,y)+Part(side+depth+"Capital",Ring(13,7,Top,Top+12),x,y)
                +Part(side+depth+"Finial",Rod(9,Top+12,Top+20),x,y);
        }
        frame+=Part("TopFrontRail",Box(462,14,10),96,-60,Top)+Part("TopRearRail",Box(462,14,10),96,88,Top);
        for(var i=0;i<Digits;i++) frame+=Part("RearRail"+i,Box(450,12,8),96,62,FirstLevel+i*DigitPitch-18);
        foreach(var x in new[]{0d,128d,256d}) frame+=Part("RegisterSeat"+N(x),Ring(18,4.2,-12,FirstLevel-20),x,0)
            +Part("CrownBridge"+N(x),$"BearingBridge<W: 20mm, D: 176mm, H: {L(GantryThickness)}, BoreD: 8.4mm>",placed:false);
        frame+=Part("Plaque",Box(144,4,28),96,-82,-8);
        var gantryMates=string.Join("\n",Enumerable.Range(0,3).Select(r=>$"Interface<Fixed> GantryMount{r} {{ A: FrameBay.CrownBridge{N(r*RegisterPitch)}.Joint; B: FrameBay.Plinth.GantrySeat{r}; }}"));
        Write("Frame.firmament",stock+Module("FrameBay",frame,"Plinth.Joint",gantryMates,"Semantic Mount = Plinth.Joint;"));

        var crank=Part("Shaft",Rod(4,-12,GantryBottom+25),-88)+Part("DriveGear","RegisterDrive",-88,0,FirstLevel)
            +Part("Idler","TransferIdler",-44,0,FirstLevel)+Part("IdlerAxle",Rod(3,FirstLevel-18,FirstLevel+12),-44)
            +Part("Bearing","PositionedRing<X: -88mm, Y: 0mm, R: 12mm, BoreR: 4.2mm, Zlow: -12mm, Zhigh: 0mm>")
            +Part("BearingSeat","PositionedRing<X: -88mm, Y: 0mm, R: 16mm, BoreR: 12mm, Zlow: -12mm, Zhigh: 0mm>")
            +Part("UpperBearing",Ring(12,4.2,GantryBottom,GantryBottom+8),-88)
            +Part("Arm",Box(62,12,7),-113,0,GantryBottom+18)+Part("Handle",Rod(7,GantryBottom+25,GantryBottom+56),-138)
            +Part("CrankBridge","BearingBridge<W: 32mm, D: 176mm, H: 8mm, BoreD: 24.2mm>",-88,0,GantryBottom);
        Write("CrankDrive.firmament",stock+Module("CrankDrive",crank,"Bearing.Joint",
            "Interface<Gear> CrankMesh { A: CrankDrive.DriveGear; B: CrankDrive.Idler; }\nInterface<Fixed> BearingHousing { A: CrankDrive.Bearing.Joint; B: CrankDrive.BearingSeat.Joint; }",
            "Gear Output = Idler; Semantic Mount = Bearing.Joint;"));

        var root=new StringBuilder("Units: mm\nInclude \"modules/Register.firmament\";\nInclude \"modules/TransferBank.firmament\";\nInclude \"modules/Frame.firmament\";\nInclude \"modules/CrankDrive.firmament\";\nAssembly DifferenceEngine {\n<Assembly DifferenceEngine>\n");
        root.Append(Child("Frame","FrameBay")); root.Append(Child("MainCrank","CrankDrive"));
        var names=new[]{"ResultRegister","FirstDifferenceRegister","SecondDifferenceRegister"};
        for(var r=0;r<Registers;r++)root.Append(Child(names[r],"Register",r*RegisterPitch,0,FirstLevel));
        for(var b=0;b<2;b++)root.Append(Child("Transfer"+b,"TransferBank",b*RegisterPitch,0,FirstLevel));
        root.AppendLine("</Assembly>\nAnchor: DifferenceEngine.Frame.Mount;");
        root.AppendLine("Interface<Gear> CrankInput { A: DifferenceEngine.MainCrank.Output; B: DifferenceEngine.ResultRegister.Drive0; }");
        for(var b=0;b<2;b++)for(var d=0;d<Digits;d++){
            root.AppendLine($"Interface<Gear> Transfer{b}Input{d} {{ A: DifferenceEngine.{names[b]}.Drive{d}; B: DifferenceEngine.Transfer{b}.Input{d}; }}");
            root.AppendLine($"Interface<Gear> Transfer{b}Output{d} {{ A: DifferenceEngine.Transfer{b}.Output{d}; B: DifferenceEngine.{names[b+1]}.Drive{d}; }}");
        }
        root.AppendLine("}"); File.WriteAllText(Path.Combine(destination,"DifferenceEngine.firmament"),root.ToString());
    }
}
