using System.Globalization;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aetheris.Kernel.Core.Mechanisms;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.EditableV8;

public sealed record V8Spec(double Bore = 90, double Stroke = 88, double RodLength = 145,
    double BankAngle = 90, double CylinderSpacing = 110, double ValveLift = 9)
{
    public double CrankRadius => Stroke / 2;
    public double PistonCompressionHeight => 40;
    public double DeckHeight => RodLength + CrankRadius + PistonCompressionHeight + 3;
    public double CrankWebEnvelope => double.Sqrt(30 * 30 + double.Pow(CrankRadius + 30, 2));
    public double CrankcaseFloor => -CrankWebEnvelope - 6;
    public double SpringFreeHeight => 36;
    public double IntakeOpeningDegrees => 360;
    public double ExhaustOpeningDegrees => 180;
    public double ValveDurationDegrees => 180;
    public void Validate()
    {
        if (new[] { Bore, Stroke, RodLength, BankAngle, CylinderSpacing, ValveLift }.Any(v => !double.IsFinite(v))
            || Bore < 80 || Bore > 100 || Stroke < 70 || Stroke > 100 || RodLength < 135 || RodLength > 170
            || BankAngle != 90 || CylinderSpacing < Bore + 16 || ValveLift < 1 || ValveLift > 10)
            throw new ArgumentException("engine-spec-outside-admitted-envelope");
    }
}

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var destination = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/local/demos/editable-v8");
            // Preserve the last successful artifact set when any generation stage fails.
            var output = Path.Combine(destination, ".generation");
            var performance = new List<object>();
            var spec = args.Length > 1 ? JsonSerializer.Deserialize<V8Spec>(File.ReadAllText(args[1]))! : new V8Spec();
            spec.Validate();
            Directory.CreateDirectory(output);
            var author = new EngineAuthor(spec);
            var source = author.StageA();
            var path = Path.Combine(output, "stage-a.firmament");
            File.WriteAllText(path, source);
            File.WriteAllText(Path.Combine(output, "spec.json"), JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true }));
            var result = new AssemblyM1Pipeline().CompileFile(path);
            foreach (var diagnostic in result.Diagnostics) Console.WriteLine($"{diagnostic.Code}: {diagnostic.Message}");
            if (!result.IsSuccess) return 1;
            var step = AssemblyIrAp242Exporter.Export(result);
            if (!step.IsSuccess) throw new InvalidOperationException(string.Join("\n", step.Diagnostics.Select(d => d.Message)));
            File.WriteAllText(Path.Combine(output, "stage-a.step"), step.Value);
            var mesh = AssemblyDisplayMeshExporter.Export(result);
            File.WriteAllText(Path.Combine(output, "stage-a.mesh.json"), AssemblyDisplayMeshExporter.Serialize(mesh));
            Console.WriteLine($"Stage A: {result.Geometry!.DefinitionBodies.Count} definitions, {result.Geometry.InstanceBodies.Count} occurrences.");
            var stageBPath = Path.Combine(output, "stage-b.firmament");
            File.WriteAllText(stageBPath, new EngineAuthor(spec).StageA(withValves: true));
            var stageB = new AssemblyM1Pipeline().CompileFile(stageBPath);
            if (!stageB.IsSuccess) throw new InvalidOperationException(string.Join("\n", stageB.Diagnostics.Select(d => d.Code + ": " + d.Message)));
            File.WriteAllText(Path.Combine(output, "stage-b.mesh.json"), AssemblyDisplayMeshExporter.Serialize(AssemblyDisplayMeshExporter.Export(stageB)));
            Console.WriteLine($"Stage B: {stageB.Geometry!.DefinitionBodies.Count} definitions, {stageB.Geometry.InstanceBodies.Count} occurrences.");
            foreach (var (label, cylinders, design) in new[] { ("stage-c", 4, spec), ("baseline", 8, spec), ("revised", 8, spec with { Stroke = spec.Stroke + 6 }) })
            {
                design.Validate();
                var engine = new EngineAuthor(design);
                var file = Path.Combine(output, label + ".firmament");
                File.WriteAllText(file, engine.Engine(cylinders));
                var motion = engine.Motion();
                var watch = Stopwatch.StartNew();
                var validation = EngineMotion.Validate(motion);
                var sweepMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
                var built = new AssemblyM1Pipeline().CompileFile(file);
                if (!built.IsSuccess) throw new InvalidOperationException(string.Join("\n", built.Diagnostics.Select(d => d.Code + ": " + d.Message)));
                var compileMs = watch.Elapsed.TotalMilliseconds; watch.Restart();
                var display = AssemblyDisplayMeshExporter.Export(built);
                var meshMs = watch.Elapsed.TotalMilliseconds;
                motion = EngineMotion.BindAssembly(motion, built);
                var springValidation = EngineMotion.ValidateSpringAlignment(motion);
                File.WriteAllText(Path.Combine(output, label + ".spring-validation.json"), JsonSerializer.Serialize(springValidation));
                File.WriteAllText(Path.Combine(output, label + ".assembly.json"), JsonSerializer.Serialize(new { instances = built.Ir!.Instances.Count, mates = built.Ir.Mates.Count,
                    fits = built.Ir.FitResults, residuals = built.Geometry!.Artifact.MateResiduals, authority = built.Ir.Instances.Select(i => new { i.StableId, i.PlacementAuthority }) }, new JsonSerializerOptions { WriteIndented = true }));
                File.WriteAllText(Path.Combine(output, label + ".mesh.json"), AssemblyDisplayMeshExporter.Serialize(display));
                watch.Restart();
                var export = AssemblyIrAp242Exporter.Export(built);
                var stepMs = watch.Elapsed.TotalMilliseconds;
                if (!export.IsSuccess) throw new InvalidOperationException(string.Join("\n", export.Diagnostics.Select(d => d.Message)));
                File.WriteAllText(Path.Combine(output, label + ".step"), export.Value);
                var json = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
                File.WriteAllText(Path.Combine(output, label + ".motion.json"), JsonSerializer.Serialize(motion, json));
                File.WriteAllText(Path.Combine(output, label + ".validation.json"), JsonSerializer.Serialize(validation, json));
                var samples = new[] { 0d, 45d, 90d, 180d, 270d, 360d, 450d, 540d, 630d, 720d }
                    .Select(degrees => new { degrees, transforms = motion.Bindings.Select(binding => new { id = binding.OccurrenceId, matrix = EngineMotion.Evaluate(motion, binding, degrees) }).ToArray() }).ToArray();
                File.WriteAllText(Path.Combine(output, label + ".samples.json"), JsonSerializer.Serialize(samples, json));
                performance.Add(new { label, compileMs, meshMs, stepMs, sweepMs, meshBytes = new FileInfo(Path.Combine(output, label + ".mesh.json")).Length,
                    definitions = display.Definitions.Count, occurrences = display.Occurrences.Count, triangles = display.Definitions.Sum(d => d.Indices.Length / 3) });
                Console.WriteLine($"{label}: {display.Definitions.Count} definitions, {display.Occurrences.Count} occurrences, {display.Definitions.Sum(d => d.Indices.Length / 3)} triangles; {JsonSerializer.Serialize(validation)}");
            }
            File.WriteAllText(Path.Combine(output, "performance.json"), JsonSerializer.Serialize(performance, new JsonSerializerOptions { WriteIndented = true }));
            var files = Directory.GetFiles(output).Where(f => Path.GetFileName(f) != "hashes.json").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            var hashes = files.Where(f => Path.GetFileName(f) != "performance.json").ToDictionary(f => Path.GetFileName(f)!, f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            File.WriteAllText(Path.Combine(output, "hashes.json"), JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var file in files.Append(Path.Combine(output, "hashes.json"))) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

public sealed class EngineAuthor(V8Spec spec)
{
    private readonly StringBuilder definitions = new();
    private readonly Dictionary<string, string> references = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> partDefinitions = new(StringComparer.Ordinal);
    public List<EngineBinding> Bindings { get; } = [];
    public List<EngineCylinder> Cylinders { get; } = [];
    private static string N(double value) => value.ToString("G17", CultureInfo.InvariantCulture);

    private string Define(string name, string body, string parameters, string arguments)
    {
        definitions.AppendLine($"Template < {parameters} > Struct {name} {{ {body} }}");
        var reference = name + "<" + arguments + ">";
        references.Add(name, reference);
        return reference;
    }

    private string Ring(string name, double outer, double inner, double height)
        => Define(name, """
            Circle2 Outside { Center: [0mm,0mm] Radius: Rout }
            Circle2 Inside { Center: [0mm,0mm] Radius: Rin }
            Profile Section { Loop Outer { Outside |> TraceLoop } Loop Inner { Inside |> TraceLoop } }
            Extrude Body { Profile: Section From: Zmin To: Zmax }
            """, "Rout: Length, Rin: Length, Zmin: Length, Zmax: Length", $"Rout: {N(outer)}mm, Rin: {N(inner)}mm, Zmin: {N(-height / 2)}mm, Zmax: {N(height / 2)}mm");

    private string Disc(string name, double radius, double height)
        => Define(name, """
            Circle2 Outside { Center: [0mm,0mm] Radius: R }
            Profile Section { Loop Outer { Outside |> TraceLoop } }
            Extrude Body { Profile: Section From: Zmin To: Zmax }
            """, "R: Length, Zmin: Length, Zmax: Length", $"R: {N(radius)}mm, Zmin: {N(-height / 2)}mm, Zmax: {N(height / 2)}mm");

    private string Plate(string name, double width, double height, double centerY, double depth, params (double Y, double Diameter)[] holes)
    {
        var cuts = string.Join("\n", holes.Select((hole, i) => $"Hole<Shaft> Pin{i} {{ On: +Z Center: Point2(0mm,HoleY{i}) Diameter: HoleD{i} End: ThroughAll }}"));
        var parameters = "W: Length, H: Length, C: Length, Zmin: Length, Zmax: Length" + string.Concat(holes.Select((_,i) => $", HoleY{i}: Length, HoleD{i}: Length"));
        var arguments = $"W: {N(width)}mm, H: {N(height)}mm, C: {N(centerY)}mm, Zmin: {N(-depth / 2)}mm, Zmax: {N(depth / 2)}mm" + string.Concat(holes.Select((hole,i) => $", HoleY{i}: {N(hole.Y)}mm, HoleD{i}: {N(hole.Diameter)}mm"));
        return Define(name, $$"""
            Rect2 Outside { Center: [0mm,C] Size: [W,H] }
            Profile Section { Loop Outer { Outside |> TraceLoop } }
            Compose Body {
                Base Stock { Profile: Section From: Zmin To: Zmax Role: Stock }
                {{cuts}}
            }
            """, parameters, arguments);
    }

    private void Part(string name, string definition, double x, double y, double z, double angle = 0, bool axialY = false,
        string motion = "Fixed", int cylinder = 0, string category = "Metal")
    {
        var s = double.Sin(angle); var c = double.Cos(angle);
        double[] transform = axialY
            ? [c,-s,0,0, 0,0,-1,0, s,c,0,0, x,y,z,1]
            : [c,-s,0,0, s,c,0,0, 0,0,1,0, x,y,z,1];
        Bindings.Add(new(name, motion, cylinder, category, transform));
        partDefinitions.Add(name, definition);
    }

    public string StageA(bool withValves = false)
    {
        var pose = new SliderCrank(spec.CrankRadius, spec.RodLength).Evaluate(0);
        var r = spec.CrankRadius;
        var rod = Define("ConnectingRod", """
            Polygon2<Trapezoid> Outside { Center: [0mm,C] BottomWidth: 52mm TopWidth: 28mm Height: H }
            Profile Section { Loop Outer { Outside |> TraceLoop } }
            Compose Body {
                Base Stock { Profile: Section From: -6mm To: 6mm Role: Stock }
                Hole<Shaft> BigEnd { On: +Z Center: Point2(0mm,0mm) Diameter: 44.4mm End: ThroughAll }
                Hole<Shaft> SmallEnd { On: +Z Center: Point2(0mm,L) Diameter: 16.4mm End: ThroughAll }
            }
            """, "L: Length, H: Length, C: Length", $"L: {N(spec.RodLength)}mm, H: {N(spec.RodLength + 48)}mm, C: {N(spec.RodLength / 2)}mm");
        var crown = Disc("PistonCrown", spec.Bore / 2 - .3, 15);
        var lug = Plate("PistonLug", 24, 37, 6.5, 6, (0, 16.4));
        var pin = Disc("WristPin", 8, 44);
        var sleeve = Ring("CylinderSleeve", spec.Bore / 2 + 6, spec.Bore / 2, spec.Stroke + 29);
        var journal = Disc("Crankpin", 22, 26);
        var web = Plate("CrankWeb", 60, r + 60, r / 2, 12);
        var main = Disc("MainJournal", 25, 20);
        Part("Rod1", rod, pose.CrankPin.X, pose.CrankPin.Y, 0, pose.RodAngleRadians);
        Part("Piston1", crown, 0, pose.PistonPositionMm + 32.5, 0, axialY: true);
        Part("PistonLug1A", lug, 0, pose.PistonPositionMm, -16);
        Part("PistonLug1B", lug, 0, pose.PistonPositionMm, 16);
        Part("WristPin1", pin, 0, pose.PistonPositionMm, 0);
        Part("Cylinder1", sleeve, 0, spec.DeckHeight - (spec.Stroke + 29) / 2, 0, axialY: true);
        Part("Crankpin1", journal, 0, r, 0);
        Part("CrankWeb1A", web, 0, 0, -19);
        Part("CrankWeb1B", web, 0, 0, 19);
        Part("MainJournal1", main, 0, 0, -35);
        Part("MainJournal2", main, 0, 0, 35);
        if (withValves)
        {
            var head = Plate("CylinderHead", spec.Bore + 12, 70, 0, 10, (-20, 8.4), (20, 8.4));
            Part("Head1", head, 0, spec.DeckHeight + 13, 0, axialY: true);
            var intake = Disc("IntakeValve", 16, 4);
            var exhaust = Disc("ExhaustValve", 14, 4);
            var stem = Disc("ValveStem", 4, 60);
            var retainer = Ring("SpringRetainer", 12, 4.2, 3);
            var spring = Define("ValveSpring", """
                WireForm Spring {
                    Diameter: 2mm
                    Material: Standard.Materials.StainlessSteel.304_Annealed
                    StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [1,0,0]; Up: [0,0,1] }
                    AxisCoil Winding { Radius: 9mm; Turns: 6; Pitch: P; Handedness: RightHanded; StartPhase: 0deg }
                }
                """, "P: Length", "P: 6mm");
            foreach (var (name, offset, valve) in new[] { ("Intake", -20d, intake), ("Exhaust", 20d, exhaust) })
            {
                Part(name + "Valve1", valve, 0, spec.DeckHeight + 6, offset, axialY: true);
                Part(name + "Stem1", stem, 0, spec.DeckHeight + 38, offset, axialY: true);
                Part(name + "Retainer1", retainer, 0, spec.DeckHeight + 58.5, offset, axialY: true);
                Part(name + "Spring1", spring, 0, spec.DeckHeight + 20, offset, axialY: true);
            }
        }
        return MatedAssembly("SliderCrankStageA");
    }

    public string Engine(int cylinderCount)
    {
        if (cylinderCount is not (4 or 8)) throw new ArgumentOutOfRangeException(nameof(cylinderCount));
        // Share exactly the same admitted part catalog as the independently built single-cylinder stage.
        StageA(withValves: true);
        Bindings.Clear(); partDefinitions.Clear();
        var skirt = Ring("PistonSkirt", spec.Bore / 2 - .3, spec.Bore / 2 - 4.3, 25);
        var compressedSpring = Define("CompressedValveSpring", $$"""
            WireForm Spring {
                Diameter: 2mm
                Material: Standard.Materials.StainlessSteel.304_Annealed
                StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [1,0,0]; Up: [0,0,1] }
                AxisCoil Winding { Radius: 9mm; Turns: 6; Pitch: P; Handedness: RightHanded; StartPhase: 0deg }
            }
            """, "P: Length", $"P: {N((36 - spec.ValveLift) / 6)}mm");
        var phases = new[] { 45d, 135d, 315d, 225d };
        var firing = new[] { 1, 5, 7, 3, 6, 8, 4, 2 };
        for (var station = 0; station < 4; station++)
        foreach (var bank in cylinderCount == 8 ? new[] { 1, -1 } : new[] { 1 })
        {
            var number = 2 * station + (bank == 1 ? 1 : 2);
            var b = bank * spec.BankAngle / 2 * double.Pi / 180;
            var p = phases[station] * double.Pi / 180;
            var z = station * spec.CylinderSpacing + bank * 8;
            var ignition = Array.IndexOf(firing, number) * 90d;
            Cylinders.Add(new(number, b, p, z, ignition));
            var pose = new SliderCrank(spec.CrankRadius, spec.RodLength).Evaluate(0, b, p);
            void OnAxis(string name, string definition, double distance, double offsetZ = 0, bool axial = true, string motion = "Fixed", string category = "Metal")
                => Part(name + number, references[definition], double.Sin(b) * distance, double.Cos(b) * distance, z + offsetZ, b, axial, motion, number, category);
            Part("Rod" + number, references["ConnectingRod"], pose.CrankPin.X, pose.CrankPin.Y, z, pose.RodAngleRadians, motion: "Rod", cylinder: number, category: "Rod");
            OnAxis("Piston", "PistonCrown", pose.PistonPositionMm + 32.5, motion: "Piston", category: "Piston");
            OnAxis("PistonSkirt", "PistonSkirt", pose.PistonPositionMm + 12.5, motion: "Piston", category: "Piston");
            OnAxis("PistonLugA", "PistonLug", pose.PistonPositionMm, -16, false, "Piston");
            OnAxis("PistonLugB", "PistonLug", pose.PistonPositionMm, 16, false, "Piston");
            OnAxis("WristPin", "WristPin", pose.PistonPositionMm, axial: false, motion: "Piston");
            OnAxis("Cylinder", "CylinderSleeve", spec.DeckHeight - (spec.Stroke + 29) / 2, category: "Block");
            OnAxis("Head", "CylinderHead", spec.DeckHeight + 13, category: "Head");
            foreach (var (name, offset) in new[] { ("Intake", -20d), ("Exhaust", 20d) })
            {
                var lift = PrescribedMotion.ValveLift(-ignition, name == "Intake" ? 360 : 180, 180, spec.ValveLift);
                OnAxis(name + "Valve", name + "Valve", spec.DeckHeight + 6 - lift, offset, motion: name, category: name);
                OnAxis(name + "Stem", "ValveStem", spec.DeckHeight + 38 - lift, offset, motion: name);
                OnAxis(name + "Retainer", "SpringRetainer", spec.DeckHeight + 58.5 - lift, offset, motion: name);
                OnAxis(name + "Spring", lift > 0 ? "CompressedValveSpring" : "ValveSpring", spec.DeckHeight + 20, offset, motion: name + "Spring", category: "Spring");
            }
        }
        var crankpin = Disc("PairedCrankpin", 22, 32);
        var main = Disc("LongMainJournal", 25, spec.CylinderSpacing - 56);
        for (var station = 0; station < 4; station++)
        {
            var p = phases[station] * double.Pi / 180;
            Part("Crankpin" + (station + 1), crankpin, double.Sin(p) * spec.CrankRadius, double.Cos(p) * spec.CrankRadius, station * spec.CylinderSpacing, motion: "Crank", category: "Crank");
            foreach (var side in new[] { -1, 1 })
                Part("CrankWeb" + (station + 1) + (side < 0 ? "A" : "B"), references["CrankWeb"], 0, 0, station * spec.CylinderSpacing + side * 22, p, motion: "Crank", category: "Crank");
        }
        var support = Plate("MainSupport", 72, 36 - spec.CrankcaseFloor, (36 + spec.CrankcaseFloor) / 2, 16, (0, 50.8));
        for (var station = 0; station < 5; station++)
        {
            var z = (station - .5) * spec.CylinderSpacing;
            Part("MainJournal" + (station + 1), main, 0, 0, z, motion: "Crank", category: "Crank");
            Part("MainSupport" + (station + 1), support, 0, 0, z, category: "Block");
        }
        var length = 4 * spec.CylinderSpacing + 16;
        var rail = Plate("CrankcaseRail", 20, 16, 0, length);
        Part("CrankcaseRailA", rail, -26, spec.CrankcaseFloor - 8, spec.CylinderSpacing * 1.5, category: "Block");
        Part("CrankcaseRailB", rail, 26, spec.CrankcaseFloor - 8, spec.CylinderSpacing * 1.5, category: "Block");
        var flywheel = Disc("Flywheel", 75, 16);
        Part("Flywheel", flywheel, 0, 0, -spec.CylinderSpacing + 20, motion: "Crank", category: "Crank");
        var cam = Disc("Camshaft", 8, length);
        var lobe = Ring("TimingLobe", 12, 8.2, 8);
        foreach (var bank in cylinderCount == 8 ? new[] { 1, -1 } : new[] { 1 })
        {
            var b = bank * spec.BankAngle / 2 * double.Pi / 180;
            var height = spec.DeckHeight + 80;
            Part("Camshaft" + (bank > 0 ? "Right" : "Left"), cam, double.Sin(b) * height, double.Cos(b) * height, spec.CylinderSpacing * 1.5, motion: "Cam", category: "Cam");
            foreach (var cylinder in Cylinders.Where(c => double.Sign(c.BankRadians) == bank))
            foreach (var offset in new[] { -20, 20 })
                Part("TimingLobe" + cylinder.Number + (offset < 0 ? "In" : "Ex"), lobe, double.Sin(b) * height, double.Cos(b) * height, cylinder.Z + offset, motion: "Cam", category: "Cam");
        }
        return MatedAssembly("EditableV8");
    }

    public EngineMotionManifest Motion() => new("aetheris/engine-motion/1", spec, 720, .5, [1,5,7,3,6,8,4,2], Cylinders,
        Bindings.Select(b => b with { CylinderMembership = b.Cylinder > 0 ? [b.Cylinder]
            : b.Kind == "Crank" ? Cylinders.Where(c => double.Abs(c.Z - b.Rest[14]) < spec.CylinderSpacing * .6).Select(c => c.Number).ToArray()
            : b.Kind == "Cam" ? Cylinders.Where(c => double.Sign(c.BankRadians) == double.Sign(b.Rest[12])).Select(c => c.Number).ToArray() : [] }).ToArray());

    private string MatedAssembly(string root)
    {
        const string anchor = "MainJournal1";
        var byName = Bindings.ToDictionary(b => b.Name, StringComparer.Ordinal);
        static bool IsSpring(EngineBinding b) => b.Name.StartsWith("IntakeSpring", StringComparison.Ordinal) || b.Name.StartsWith("ExhaustSpring", StringComparison.Ordinal);
        string Parent(EngineBinding b)
        {
            if (IsSpring(b)) return b.Name.Replace("Spring", "Stem", StringComparison.Ordinal);
            var n = b.Cylinder;
            if (n > 0)
            {
                if (b.Name.StartsWith("Rod", StringComparison.Ordinal)) return "Crankpin" + ((n + 1) / 2);
                if (b.Name.StartsWith("WristPin", StringComparison.Ordinal)) return "Rod" + n;
                if (b.Name.StartsWith("Piston", StringComparison.Ordinal)) return "WristPin" + n;
                if (b.Name.StartsWith("Cylinder", StringComparison.Ordinal)) return "MainSupport" + ((n + 1) / 2);
                if (b.Name.StartsWith("Head", StringComparison.Ordinal)) return "Cylinder" + n;
                if (b.Name.Contains("Valve", StringComparison.Ordinal) || b.Name.Contains("Retainer", StringComparison.Ordinal))
                    return (b.Name.StartsWith("Intake", StringComparison.Ordinal) ? "IntakeStem" : "ExhaustStem") + n;
                return "Head" + n;
            }
            if (b.Name.StartsWith("Crankpin", StringComparison.Ordinal)) return "CrankWeb" + b.Name[8..] + "A";
            return anchor;
        }
        var parent = Bindings.Where(b => b.Name != anchor).ToDictionary(b => b.Name, b => Parent(b), StringComparer.Ordinal);
        // The stage-A witness has no block support. Its parts register to the shaft datum.
        foreach (var key in parent.Keys.ToArray()) if (!byName.ContainsKey(parent[key])) parent[key] = anchor;
        var identity = Aetheris.Kernel.Core.Math.Transform3D.Identity.ToRowMajor();
        string Frame(double[] m) => $"DatumFrame Frame = [{N(m[12])},{N(m[13])},{N(m[14])}] x [{N(m[0])},{N(m[1])},{N(m[2])}] y [{N(m[4])},{N(m[5])},{N(m[6])}] z [{N(m[8])},{N(m[9])},{N(m[10])}];";
        var text = new StringBuilder(definitions.ToString());
        text.AppendLine("""
            // Interfaces qualify the indexed rest assembly. The exported analytical mechanism
            // drives its revolute/slider coordinates; these are not general dynamic mates.
            Interface SpringOnStem {
                Role Spring requires AxisCapable, DatumFrameCapable, DimensionalCapable;
                Role Stem requires AxisCapable, DatumFrameCapable, DimensionalCapable;
                Lower AxisCoincident Spring.Axis Stem.Axis;
                Lower FrameCoincident Spring.Frame Stem.Frame SameDirection;
                Fit Stem.Diameter inside Spring.ClearDiameter;
            }
            Interface RegisteredSeat {
                Role Moving requires DatumFrameCapable;
                Role Fixed requires DatumFrameCapable;
                Lower FrameCoincident Moving Fixed SameDirection;
            }
            Interface JournalBoreSeat {
                Role Moving requires DatumFrameCapable, DimensionalCapable;
                Role Fixed requires DatumFrameCapable, DimensionalCapable;
                Lower FrameCoincident Moving Fixed SameDirection;
                Fit Fixed.Diameter inside Moving.Diameter;
            }
            Interface WristPinSeat {
                Role Moving requires DatumFrameCapable, DimensionalCapable;
                Role Fixed requires DatumFrameCapable, DimensionalCapable;
                Lower FrameCoincident Moving Fixed SameDirection;
                Fit Moving.Diameter inside Fixed.Diameter;
            }
            """);
        text.AppendLine($"Assembly {root} {{\n<Assembly {root}>");
        foreach (var b in Bindings)
        {
            text.AppendLine($"<Part {b.Name} = {partDefinitions[b.Name]}>");
            var diameter = b.Kind == "Rod" ? " Dimension Diameter = 44.4mm;" : b.Name.StartsWith("WristPin", StringComparison.Ordinal) ? " Dimension Diameter = 16mm;" : "";
            if (!IsSpring(b)) text.AppendLine($"Semantic Mount {{ {Frame(identity)}{diameter} }}");
            var inverse = Aetheris.Kernel.Core.Math.Transform3D.FromRowMajor(b.Rest).Inverse();
            foreach (var child in Bindings.Where(child => parent.GetValueOrDefault(child.Name) == b.Name))
            {
                var local = Aetheris.Kernel.Core.Math.Transform3D.FromRowMajor(child.Rest) * inverse;
                var seatDiameter = child.Kind == "Rod" ? " Dimension Diameter = 44mm;" : child.Name.StartsWith("WristPin", StringComparison.Ordinal) && child.Cylinder > 0 ? " Dimension Diameter = 16.4mm;" : "";
                var m = local.ToRowMajor();
                var axis = IsSpring(child) ? $" Axis Axis = [{N(m[12])},{N(m[13])},{N(m[14])}] -> [{N(m[8])},{N(m[9])},{N(m[10])}]; Dimension Diameter = 8mm;" : "";
                text.AppendLine($"Semantic Seat{child.Name} {{ {Frame(m)}{seatDiameter}{axis} }}");
            }
            text.AppendLine("</Part>");
        }
        text.AppendLine($"</Assembly>\nAnchor: {root}.{anchor}.Mount;");
        foreach (var b in Bindings.Where(b => b.Name != anchor))
        {
            if (IsSpring(b))
            {
                text.AppendLine($"Mate Install{b.Name}: SpringOnStem {{ Spring: {root}.{b.Name}.Winding; Stem: {root}.{parent[b.Name]}.Seat{b.Name}; }}");
                continue;
            }
            var contract = b.Kind == "Rod" ? "JournalBoreSeat" : b.Name.StartsWith("WristPin", StringComparison.Ordinal) && b.Cylinder > 0 ? "WristPinSeat" : "RegisteredSeat";
            text.AppendLine($"Mate Install{b.Name}: {contract} {{ Moving: {root}.{b.Name}.Mount; Fixed: {root}.{parent[b.Name]}.Seat{b.Name}; }}");
        }
        text.AppendLine("}");
        return text.ToString();
    }
}
