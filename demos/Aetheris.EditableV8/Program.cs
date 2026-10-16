using System.Diagnostics;
using System.Security.Cryptography;
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
            var path = source.WriteTo(output, "stage-a");
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
            var stageBPath = new EngineAuthor(spec).StageA(withValves: true).WriteTo(output, "stage-b");
            var stageB = new AssemblyM1Pipeline().CompileFile(stageBPath);
            if (!stageB.IsSuccess) throw new InvalidOperationException(string.Join("\n", stageB.Diagnostics.Select(d => d.Code + ": " + d.Message)));
            File.WriteAllText(Path.Combine(output, "stage-b.mesh.json"), AssemblyDisplayMeshExporter.Serialize(AssemblyDisplayMeshExporter.Export(stageB)));
            Console.WriteLine($"Stage B: {stageB.Geometry!.DefinitionBodies.Count} definitions, {stageB.Geometry.InstanceBodies.Count} occurrences.");
            foreach (var (label, cylinders, design) in new[] { ("stage-c", 4, spec), ("baseline", 8, spec), ("revised", 8, spec with { Stroke = spec.Stroke + 6 }) })
            {
                design.Validate();
                var engine = new EngineAuthor(design);
                var project = engine.Engine(cylinders);
                var file = project.WriteTo(output, label);
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
                    fits = EngineMotion.Fits(built.Ir), localDefinitions = built.Ir.AssemblyDefinitions!.Select(d => new { d.DefinitionIdentity, d.LocalMates, d.LocalPlacements, d.LocalFitResults }), residuals = built.Geometry!.Artifact.MateResiduals, authority = built.Ir.Instances.Select(i => new { i.StableId, i.PlacementAuthority }) }, new JsonSerializerOptions { WriteIndented = true }));
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
            var files = Directory.GetFiles(output, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(f) != "hashes.json").OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
            var hashes = files.Where(f => Path.GetFileName(f) != "performance.json").ToDictionary(f => Path.GetRelativePath(output, f).Replace('\\', '/'), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))));
            File.WriteAllText(Path.Combine(output, "hashes.json"), JsonSerializer.Serialize(hashes, new JsonSerializerOptions { WriteIndented = true }));
            foreach (var file in files.Append(Path.Combine(output, "hashes.json"))) {
                var target = Path.Combine(destination, Path.GetRelativePath(output, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

public sealed class EngineAuthor(V8Spec spec)
{
    public List<EngineBinding> Bindings { get; } = [];
    public List<EngineCylinder> Cylinders { get; } = [];
    private IReadOnlyDictionary<string, string> occurrencePaths = new Dictionary<string, string>();
    private EngineProject Project(string stage) { var project = EngineProject.Create(spec, Bindings, Cylinders, stage); occurrencePaths = project.OccurrencePaths; return project; }

    private void Part(string name, string definition, double x, double y, double z, double angle = 0, bool axialY = false,
        string motion = "Fixed", int cylinder = 0, string category = "Metal")
    {
        var s = double.Sin(angle); var c = double.Cos(angle);
        double[] transform = axialY
            ? [c,-s,0,0, 0,0,-1,0, s,c,0,0, x,y,z,1]
            : [c,-s,0,0, s,c,0,0, 0,0,1,0, x,y,z,1];
        Bindings.Add(new(name, motion, cylinder, category, transform, DefinitionExport: definition));
    }

    public EngineProject StageA(bool withValves = false)
    {
        Bindings.Clear(); Cylinders.Clear();
        var pose = new SliderCrank(spec.CrankRadius, spec.RodLength).Evaluate(0);
        var r = spec.CrankRadius;
        const string rod = "ConnectingRod";
        const string crown = "PistonCrown";
        const string lug = "PistonLug";
        const string pin = "WristPin";
        const string sleeve = "CylinderSleeve";
        const string journal = "Crankpin";
        const string web = "CrankWeb";
        const string main = "MainJournal";
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
            const string head = "CylinderHead";
            Part("Head1", head, 0, spec.DeckHeight + 13, 0, axialY: true);
            const string intake = "IntakeValve";
            const string exhaust = "ExhaustValve";
            const string stem = "ValveStem";
            const string retainer = "SpringRetainer";
            const string spring = "ValveSpring";
            foreach (var (name, offset, valve) in new[] { ("Intake", -20d, intake), ("Exhaust", 20d, exhaust) })
            {
                Part(name + "Valve1", valve, 0, spec.DeckHeight + 6, offset, axialY: true);
                Part(name + "Stem1", stem, 0, spec.DeckHeight + 38, offset, axialY: true);
                Part(name + "Retainer1", retainer, 0, spec.DeckHeight + 58.5, offset, axialY: true);
                Part(name + "Spring1", spring, 0, spec.DeckHeight + 20, offset, axialY: true);
            }
        }
        return Project(withValves ? "stage-b" : "stage-a");
    }

    public EngineProject Engine(int cylinderCount)
    {
        if (cylinderCount is not (4 or 8)) throw new ArgumentOutOfRangeException(nameof(cylinderCount));
        Bindings.Clear(); Cylinders.Clear();
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
                => Part(name + number, definition, double.Sin(b) * distance, double.Cos(b) * distance, z + offsetZ, b, axial, motion, number, category);
            Part("Rod" + number, "ConnectingRod", pose.CrankPin.X, pose.CrankPin.Y, z, pose.RodAngleRadians, motion: "Rod", cylinder: number, category: "Rod");
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
        const string crankpin = "PairedCrankpin";
        const string main = "LongMainJournal";
        for (var station = 0; station < 4; station++)
        {
            var p = phases[station] * double.Pi / 180;
            Part("Crankpin" + (station + 1), crankpin, double.Sin(p) * spec.CrankRadius, double.Cos(p) * spec.CrankRadius, station * spec.CylinderSpacing, motion: "Crank", category: "Crank");
            foreach (var side in new[] { -1, 1 })
                Part("CrankWeb" + (station + 1) + (side < 0 ? "A" : "B"), "CrankWeb", 0, 0, station * spec.CylinderSpacing + side * 22, p, motion: "Crank", category: "Crank");
        }
        const string support = "MainSupport";
        for (var station = 0; station < 5; station++)
        {
            var z = (station - .5) * spec.CylinderSpacing;
            Part("MainJournal" + (station + 1), main, 0, 0, z, motion: "Crank", category: "Crank");
            Part("MainSupport" + (station + 1), support, 0, 0, z, category: "Block");
        }
        var length = 4 * spec.CylinderSpacing + 16;
        const string rail = "CrankcaseRail";
        Part("CrankcaseRailA", rail, -26, spec.CrankcaseFloor - 8, spec.CylinderSpacing * 1.5, category: "Block");
        Part("CrankcaseRailB", rail, 26, spec.CrankcaseFloor - 8, spec.CylinderSpacing * 1.5, category: "Block");
        const string flywheel = "Flywheel";
        Part("Flywheel", flywheel, 0, 0, -spec.CylinderSpacing + 20, motion: "Crank", category: "Crank");
        const string cam = "Camshaft";
        const string lobe = "TimingLobe";
        foreach (var bank in cylinderCount == 8 ? new[] { 1, -1 } : new[] { 1 })
        {
            var b = bank * spec.BankAngle / 2 * double.Pi / 180;
            var height = spec.DeckHeight + 80;
            Part("Camshaft" + (bank > 0 ? "Right" : "Left"), cam, double.Sin(b) * height, double.Cos(b) * height, spec.CylinderSpacing * 1.5, motion: "Cam", category: "Cam");
            foreach (var cylinder in Cylinders.Where(c => double.Sign(c.BankRadians) == bank))
            foreach (var offset in new[] { -20, 20 })
                Part("TimingLobe" + cylinder.Number + (offset < 0 ? "In" : "Ex"), lobe, double.Sin(b) * height, double.Cos(b) * height, cylinder.Z + offset, motion: "Cam", category: "Cam");
        }
        return Project(cylinderCount == 8 ? "baseline" : "stage-c");
    }

    public EngineMotionManifest Motion() => new("aetheris/engine-motion/1", spec, 720, .5, [1,5,7,3,6,8,4,2], Cylinders,
        Bindings.Select(b => b with { SourcePath = occurrencePaths[b.Name], DefinitionExport = b.Cylinder > 0 && b.Name.Contains("Spring", StringComparison.Ordinal) ? "ValveSpring" : b.DefinitionExport, CylinderMembership = b.Cylinder > 0 ? [b.Cylinder]
            : b.Kind == "Crank" ? Cylinders.Where(c => double.Abs(c.Z - b.Rest[14]) < spec.CylinderSpacing * .6).Select(c => c.Number).ToArray()
            : b.Kind == "Cam" ? Cylinders.Where(c => double.Sign(c.BankRadians) == double.Sign(b.Rest[12])).Select(c => c.Number).ToArray() : [] }).ToArray());

}
