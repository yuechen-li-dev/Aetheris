using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Results;
using Aetheris.Kernel.Core.Step242;

namespace Aetheris.Sculpture;

public enum SculptureMode
{
    Virtual
}

public sealed record Sol1FrameDefinition(double MajorRadiusMm, double MinorRadiusMm, double ZMm);
public sealed record Sol1EyeDefinition(double MajorRadiusMm, double MinorRadiusMm, double ZMm);
public sealed record Sol1WaveDefinition(double PrimaryAmplitudeMm, double SecondaryAmplitudeMm, int AngularFrequency, double RadialFrequency);
public sealed record Sol1ProminenceDefinition(IReadOnlyList<int> Residues, int Modulus, double StartFraction, double RiseMm);
public sealed record Sol1LatticeDefinition(
    int NodeCount,
    double InnerRadiusMm,
    double OuterRadiusMm,
    double NodeRadiusMm,
    double StrandRadiusMm,
    IReadOnlyList<int> FibonacciOffsets,
    Sol1WaveDefinition Wave,
    Sol1ProminenceDefinition Prominences);

/// <summary>Canonical, bounded source contract for the ART-X0 flagship artwork.</summary>
public sealed record Sol1Definition(
    string Schema,
    string Id,
    string Title,
    SculptureMode Mode,
    Sol1FrameDefinition OuterFrame,
    Sol1EyeDefinition Eye,
    Sol1LatticeDefinition Lattice);

public sealed record Sol1Point(int Index, double RadiusMm, double ThetaRadians, Point3D Position, bool IsProminent);
public sealed record Sol1Connection(int From, int To, int FibonacciOffset, bool IsProminent, double LengthMm);

public sealed record Sol1RepresentationInventory(
    int Planes,
    int Cylinders,
    int Spheres,
    int Tori,
    int BSplineSurfaces,
    int RationalProductSurfaces);

public sealed record Sol1Evidence(
    string Milestone,
    string ArtworkId,
    string Title,
    SculptureMode Mode,
    string ConstructionLaw,
    double GoldenAngleRadians,
    int PhyllotaxisNodes,
    IReadOnlyList<int> FibonacciOffsets,
    int LatticeConnections,
    int ProminentNodes,
    int ProminentConnections,
    int ExactClosedBodyDefinitions,
    int ExactClosedBodyOccurrences,
    int AssemblyOccurrences,
    bool StepAssemblyReimportSucceeded,
    int ReimportedDefinitions,
    int ReimportedOccurrences,
    bool DeterministicSource,
    bool IsManufacturingGeometry,
    string Connectedness,
    Sol1RepresentationInventory SurfaceInventory,
    string StepSha256);

public sealed record Sol1Artifact(
    string Step,
    string PreviewSvg,
    Sol1Evidence Evidence,
    IReadOnlyList<Sol1Point> Points,
    IReadOnlyList<Sol1Connection> Connections);

public static class Sol1Source
{
    private static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static KernelResult<Sol1Definition> Load(string path)
    {
        if (!File.Exists(path)) return Failure($"Sol 1 source was not found: {path}");
        try
        {
            var definition = JsonSerializer.Deserialize<Sol1Definition>(File.ReadAllText(path), JsonOptions);
            return definition is null ? Failure("Sol 1 source deserialized to null.") : Validate(definition);
        }
        catch (JsonException exception)
        {
            return Failure($"Sol 1 source is invalid JSON: {exception.Message}");
        }
    }

    public static KernelResult<Sol1Definition> Validate(Sol1Definition definition)
    {
        var errors = new List<string>();
        if (definition.Schema != "aetheris.virtual-sculpture/1") errors.Add("Schema must be 'aetheris.virtual-sculpture/1'.");
        if (definition.Id != "sol-1" || definition.Title != "Sol 1") errors.Add("ART-X0 admits only the flagship id/title 'sol-1'/'Sol 1'.");
        if (definition.Mode != SculptureMode.Virtual) errors.Add("Sol 1 must be explicitly marked Mode: Virtual.");
        if (!Positive(definition.OuterFrame.MajorRadiusMm) || !Positive(definition.OuterFrame.MinorRadiusMm) || definition.OuterFrame.MajorRadiusMm <= definition.OuterFrame.MinorRadiusMm)
            errors.Add("Outer frame radii must define a non-self-intersecting torus.");
        if (!Positive(definition.Eye.MajorRadiusMm) || !Positive(definition.Eye.MinorRadiusMm) || definition.Eye.MajorRadiusMm <= definition.Eye.MinorRadiusMm)
            errors.Add("Eye radii must define a non-self-intersecting torus.");
        var lattice = definition.Lattice;
        if (lattice.NodeCount is < 34 or > 610) errors.Add("NodeCount must remain in the bounded range 34..610.");
        if (!Positive(lattice.InnerRadiusMm) || !Positive(lattice.OuterRadiusMm) || lattice.OuterRadiusMm <= lattice.InnerRadiusMm)
            errors.Add("Lattice radii must be positive and ordered.");
        if (lattice.InnerRadiusMm <= definition.Eye.MajorRadiusMm + definition.Eye.MinorRadiusMm)
            errors.Add("The lattice must reserve a clean annular gap around the eye.");
        if (lattice.OuterRadiusMm + lattice.NodeRadiusMm >= definition.OuterFrame.MajorRadiusMm - definition.OuterFrame.MinorRadiusMm)
            errors.Add("The lattice must remain inside the exact outer frame.");
        if (!Positive(lattice.NodeRadiusMm) || !Positive(lattice.StrandRadiusMm) || lattice.StrandRadiusMm >= lattice.NodeRadiusMm)
            errors.Add("Node/strand radii must be positive, with strands smaller than nodes.");
        if (lattice.FibonacciOffsets.Count is < 1 or > 3 || lattice.FibonacciOffsets.Any(offset => offset <= 0 || offset >= lattice.NodeCount))
            errors.Add("One to three positive bounded Fibonacci offsets are required.");
        if (lattice.FibonacciOffsets.Distinct().Count() != lattice.FibonacciOffsets.Count) errors.Add("Fibonacci offsets must be unique.");
        if (!Positive(lattice.Wave.PrimaryAmplitudeMm) || lattice.Wave.SecondaryAmplitudeMm < 0d || lattice.Wave.AngularFrequency <= 0 || !double.IsFinite(lattice.Wave.RadialFrequency))
            errors.Add("Wave parameters must be finite and intentional.");
        var prominence = lattice.Prominences;
        if (prominence.Modulus <= 0 || prominence.Residues.Count is < 1 or > 5 || prominence.Residues.Any(r => r < 0 || r >= prominence.Modulus))
            errors.Add("Prominence residues must be a small valid subset of their modulus.");
        if (prominence.StartFraction is < 0.4 or >= 1d || !Positive(prominence.RiseMm)) errors.Add("Prominence rise must begin in the outer field and be positive.");
        return errors.Count == 0
            ? KernelResult<Sol1Definition>.Success(definition)
            : KernelResult<Sol1Definition>.Failure(errors.Select(message => new Aetheris.Kernel.Core.Diagnostics.KernelDiagnostic(
                Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
                Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
                message,
                "VirtualSculpture.Sol1.Source")).ToArray());
    }

    private static bool Positive(double value) => double.IsFinite(value) && value > 0d;
    private static KernelResult<Sol1Definition> Failure(string message) => KernelResult<Sol1Definition>.Failure([
        new(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            message,
            "VirtualSculpture.Sol1.Source")]);
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

/// <summary>
/// ART-X0's sole materializer. It produces a deterministic AP242 exact-body assembly:
/// analytic toroidal frame/eye, spherical phyllotaxis nodes, and cylindrical Fibonacci
/// parastichy chords. The multi-body structure is deliberate virtual sculpture semantics.
/// </summary>
public static class Sol1Materializer
{
    public const double GoldenAngleRadians = double.Pi * (3d - 2.2360679774997896964091736687313d);
    private const double LengthQuantumMm = 0.02d;

    public static KernelResult<Sol1Artifact> Build(Sol1Definition definition)
    {
        var validation = Sol1Source.Validate(definition);
        if (!validation.IsSuccess) return KernelResult<Sol1Artifact>.Failure(validation.Diagnostics);

        try
        {
            var points = GeneratePoints(definition);
            var connections = GenerateConnections(definition, points);
            var bodies = new Dictionary<string, BrepBody>(StringComparer.Ordinal);
            var definitions = new List<Step242AssemblyDefinition>();
            var occurrences = new List<Step242AssemblyOccurrence>();
            const string root = "virtual-sculpture:sol-1";
            occurrences.Add(new(root, "Sol 1", null, null, Identity()));

            AddBody("frame", "Sol 1 — exact outer calm", Require(BrepPrimitives.CreateTorus(definition.OuterFrame.MajorRadiusMm, definition.OuterFrame.MinorRadiusMm)));
            AddOccurrence("frame", "Outer analytic frame", TorusToXy(definition.OuterFrame.ZMm));
            AddBody("eye", "Sol 1 — central eye", Require(BrepPrimitives.CreateTorus(definition.Eye.MajorRadiusMm, definition.Eye.MinorRadiusMm)));
            AddOccurrence("eye", "Central analytic eye", TorusToXy(definition.Eye.ZMm));

            var haloMinor = definition.Lattice.StrandRadiusMm * 1.15d;
            var haloMajor = definition.Lattice.OuterRadiusMm + definition.Lattice.NodeRadiusMm + 0.75d;
            AddBody("halo", "Sol 1 — inner corona delimiter", Require(BrepPrimitives.CreateTorus(haloMajor, haloMinor)));
            AddOccurrence("halo", "Inner corona delimiter", TorusToXy(-0.65d));

            foreach (var point in points)
            {
                var kind = point.IsProminent ? "prominent-node" : "node";
                var radius = point.IsProminent ? definition.Lattice.NodeRadiusMm * 1.28d : definition.Lattice.NodeRadiusMm;
                if (!bodies.ContainsKey(kind)) AddBody(kind, point.IsProminent ? "Sol 1 — prominence bead" : "Sol 1 — phyllotaxis bead", Require(BrepPrimitives.CreateSphere(radius)));
                AddOccurrence(kind, $"Phyllotaxis n={point.Index:D3}", Translation(point.Position));
            }

            foreach (var connection in connections)
            {
                var radius = connection.IsProminent ? definition.Lattice.StrandRadiusMm * 1.5d : definition.Lattice.StrandRadiusMm;
                var length = Quantize(connection.LengthMm);
                var key = $"strand-r{Token(radius)}-l{Token(length)}";
                if (!bodies.ContainsKey(key)) AddBody(key, connection.IsProminent ? "Sol 1 — escaping parastichy segment" : "Sol 1 — Fibonacci parastichy segment", Require(BrepPrimitives.CreateCylinder(radius, length)));
                AddOccurrence(key, $"Fibonacci +{connection.FibonacciOffset}: {connection.From:D3}->{connection.To:D3}", SegmentTransform(points[connection.From].Position, points[connection.To].Position));
            }

            var export = Step242AssemblyExporter.Export(new("Sol 1", root, definitions, occurrences));
            if (!export.IsSuccess) return KernelResult<Sol1Artifact>.Failure(export.Diagnostics);
            var step = export.Value;
            var reimport = Step242AssemblyImporter.Import(step);
            if (!reimport.IsSuccess) return KernelResult<Sol1Artifact>.Failure(reimport.Diagnostics);

            var inventory = Inventory(step);
            if (inventory.RationalProductSurfaces != 0)
                return Failure("Sol 1 representation law violated: rational product surfaces were emitted.");

            var evidence = new Sol1Evidence(
                "ART-X0",
                definition.Id,
                definition.Title,
                definition.Mode,
                "r_n=sqrt(r_eye^2+n*(r_outer^2-r_eye^2)/(N-1)); theta_n=n*golden_angle; edges=(n,n+FibonacciOffset); z=two-frequency radial/angular wave plus three residue-selected outer prominence ramps",
                GoldenAngleRadians,
                points.Count,
                definition.Lattice.FibonacciOffsets,
                connections.Count,
                points.Count(point => point.IsProminent),
                connections.Count(connection => connection.IsProminent),
                definitions.Count,
                occurrences.Count - 1,
                occurrences.Count,
                true,
                reimport.Value.Definitions.Count,
                reimport.Value.Occurrences.Count,
                true,
                false,
                "Exact closed bodies are intentionally composed as an AP242 virtual-sculpture assembly. Overlapping node/strand occurrences create a perceptually continuous lattice without claiming a manufacturing boolean union.",
                inventory,
                Sha256(step));
            return KernelResult<Sol1Artifact>.Success(new(step, Sol1Preview.Render(definition, points, connections), evidence, points, connections));

            void AddBody(string stableId, string name, BrepBody body)
            {
                bodies.Add(stableId, body);
                definitions.Add(new($"sol-1:def:{stableId}", name, body));
            }
            void AddOccurrence(string definitionId, string name, IReadOnlyList<double> transform) => occurrences.Add(new(
                $"sol-1:occ:{occurrences.Count:D5}", name, root, $"sol-1:def:{definitionId}", transform));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Failure($"Sol 1 materialization failed: {exception.Message}");
        }
    }

    public static IReadOnlyList<Sol1Point> GeneratePoints(Sol1Definition definition)
    {
        var l = definition.Lattice;
        var result = new Sol1Point[l.NodeCount];
        for (var n = 0; n < l.NodeCount; n++)
        {
            var fraction = n / (double)(l.NodeCount - 1);
            var radius = double.Sqrt(l.InnerRadiusMm * l.InnerRadiusMm + fraction * (l.OuterRadiusMm * l.OuterRadiusMm - l.InnerRadiusMm * l.InnerRadiusMm));
            var theta = n * GoldenAngleRadians;
            var envelope = 0.22d + 0.78d * (1d - fraction);
            var z = l.Wave.PrimaryAmplitudeMm * envelope * double.Sin(l.Wave.AngularFrequency * theta + l.Wave.RadialFrequency * radius)
                + l.Wave.SecondaryAmplitudeMm * double.Sin(13d * theta - 0.11d * radius);
            var residue = n % l.Prominences.Modulus;
            var prominentFamily = l.Prominences.Residues.Contains(residue);
            var prominent = prominentFamily && fraction >= l.Prominences.StartFraction;
            if (prominent)
            {
                var u = (fraction - l.Prominences.StartFraction) / (1d - l.Prominences.StartFraction);
                var smooth = u * u * (3d - 2d * u);
                z += l.Prominences.RiseMm * smooth;
            }
            result[n] = new(n, radius, theta, new(radius * double.Cos(theta), radius * double.Sin(theta), z), prominent);
        }
        return result;
    }

    public static IReadOnlyList<Sol1Connection> GenerateConnections(Sol1Definition definition, IReadOnlyList<Sol1Point> points)
    {
        var result = new List<Sol1Connection>();
        foreach (var offset in definition.Lattice.FibonacciOffsets.Order())
            for (var from = 0; from + offset < points.Count; from++)
            {
                var to = from + offset;
                var prominent = offset == definition.Lattice.Prominences.Modulus && points[from].IsProminent && points[to].IsProminent;
                result.Add(new(from, to, offset, prominent, (points[to].Position - points[from].Position).Length));
            }
        return result;
    }

    private static BrepBody Require(KernelResult<BrepBody> result) => result.IsSuccess ? result.Value : throw new InvalidOperationException(string.Join("; ", result.Diagnostics.Select(d => d.Message)));
    private static double Quantize(double length) => double.Max(LengthQuantumMm, double.Round(length / LengthQuantumMm, MidpointRounding.AwayFromZero) * LengthQuantumMm);
    private static string Token(double value) => value.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', '_');
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static int Count(string text, string token) => (text.Length - text.Replace(token, string.Empty, StringComparison.Ordinal).Length) / token.Length;
    private static Sol1RepresentationInventory Inventory(string step) => new(
        Count(step, "=PLANE("),
        Count(step, "=CYLINDRICAL_SURFACE("),
        Count(step, "=SPHERICAL_SURFACE("),
        Count(step, "=TOROIDAL_SURFACE("),
        Count(step, "=B_SPLINE_SURFACE_WITH_KNOTS("),
        Count(step, "RATIONAL_B_SPLINE_SURFACE"));

    private static IReadOnlyList<double> Identity() => [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1];
    private static IReadOnlyList<double> Translation(Point3D point) => [1,0,0,0, 0,1,0,0, 0,0,1,0, point.X,point.Y,point.Z,1];
    private static IReadOnlyList<double> TorusToXy(double z) => [1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,0,z,1];

    private static IReadOnlyList<double> SegmentTransform(Point3D a, Point3D b)
    {
        var delta = b - a;
        var zAxis = delta / delta.Length;
        var helper = double.Abs(zAxis.Z) < 0.9d ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0);
        var xAxis = Cross(helper, zAxis);
        xAxis /= xAxis.Length;
        var yAxis = Cross(zAxis, xAxis);
        var center = new Point3D((a.X + b.X) / 2d, (a.Y + b.Y) / 2d, (a.Z + b.Z) / 2d);
        return [xAxis.X,xAxis.Y,xAxis.Z,0, yAxis.X,yAxis.Y,yAxis.Z,0, zAxis.X,zAxis.Y,zAxis.Z,0, center.X,center.Y,center.Z,1];
    }

    private static Vector3D Cross(Vector3D a, Vector3D b) => new(a.Y*b.Z-a.Z*b.Y, a.Z*b.X-a.X*b.Z, a.X*b.Y-a.Y*b.X);
    private static KernelResult<Sol1Artifact> Failure(string message) => KernelResult<Sol1Artifact>.Failure([
        new(Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticCode.ValidationFailed,
            Aetheris.Kernel.Core.Diagnostics.KernelDiagnosticSeverity.Error,
            message,
            "VirtualSculpture.Sol1.Materializer")]);
}

public static class Sol1Preview
{
    public static string Render(Sol1Definition definition, IReadOnlyList<Sol1Point> points, IReadOnlyList<Sol1Connection> connections)
    {
        const double size = 1200d;
        const double center = size / 2d;
        var scale = 500d / (definition.OuterFrame.MajorRadiusMm + definition.OuterFrame.MinorRadiusMm);
        var minZ = points.Min(p => p.Position.Z); var maxZ = points.Max(p => p.Position.Z);
        var builder = new StringBuilder();
        builder.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1200\" viewBox=\"0 0 1200 1200\">");
        builder.AppendLine("<defs><radialGradient id=\"bg\"><stop offset=\"0\" stop-color=\"#21120d\"/><stop offset=\"0.55\" stop-color=\"#090a12\"/><stop offset=\"1\" stop-color=\"#020308\"/></radialGradient><filter id=\"glow\"><feGaussianBlur stdDeviation=\"2.2\" result=\"b\"/><feMerge><feMergeNode in=\"b\"/><feMergeNode in=\"SourceGraphic\"/></feMerge></filter></defs>");
        builder.AppendLine("<rect width=\"1200\" height=\"1200\" fill=\"url(#bg)\"/><g filter=\"url(#glow)\" stroke-linecap=\"round\">");
        foreach (var edge in connections.OrderBy(edge => (points[edge.From].Position.Z + points[edge.To].Position.Z) / 2d))
        {
            var a = Project(points[edge.From].Position); var b = Project(points[edge.To].Position);
            var z = (points[edge.From].Position.Z + points[edge.To].Position.Z) / 2d;
            var t = (z - minZ) / double.Max(0.001d, maxZ - minZ);
            var color = edge.IsProminent ? "#fff3b0" : Blend(t);
            var width = edge.IsProminent ? 3.6d : 1.45d;
            builder.Append(CultureInfo.InvariantCulture, $"<line x1=\"{a.X:F2}\" y1=\"{a.Y:F2}\" x2=\"{b.X:F2}\" y2=\"{b.Y:F2}\" stroke=\"{color}\" stroke-width=\"{width:F2}\" opacity=\"0.92\"/>\n");
        }
        foreach (var point in points.OrderBy(point => point.Position.Z))
        {
            var p = Project(point.Position); var t = (point.Position.Z - minZ) / double.Max(0.001d, maxZ - minZ);
            var radius = point.IsProminent ? 5.1d : 3.0d;
            builder.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{p.X:F2}\" cy=\"{p.Y:F2}\" r=\"{radius:F2}\" fill=\"{(point.IsProminent ? "#fff7cf" : Blend(t))}\"/>\n");
        }
        var outer = definition.OuterFrame.MajorRadiusMm * scale;
        var outerWidth = definition.OuterFrame.MinorRadiusMm * 2d * scale;
        var eye = definition.Eye.MajorRadiusMm * scale;
        var eyeWidth = definition.Eye.MinorRadiusMm * 2d * scale;
        builder.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{center:F1}\" cy=\"{center:F1}\" r=\"{outer:F2}\" fill=\"none\" stroke=\"#ffd879\" stroke-width=\"{outerWidth:F2}\"/>\n");
        builder.Append(CultureInfo.InvariantCulture, $"<circle cx=\"{center:F1}\" cy=\"{center:F1}\" r=\"{eye:F2}\" fill=\"#03040a\" stroke=\"#fff0a3\" stroke-width=\"{eyeWidth:F2}\"/>\n");
        builder.AppendLine("</g><text x=\"600\" y=\"1140\" text-anchor=\"middle\" fill=\"#ffe6a0\" font-family=\"serif\" font-size=\"34\" letter-spacing=\"12\">SOL 1</text></svg>");
        return builder.ToString();

        (double X, double Y) Project(Point3D point) => (center + point.X * scale, center - point.Y * scale - point.Z * scale * 0.18d);
    }

    private static string Blend(double t)
    {
        t = double.Clamp(t, 0d, 1d);
        var r = (int)double.Round(220d + 35d * t);
        var g = (int)double.Round(82d + 157d * t);
        var b = (int)double.Round(31d + 120d * t);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
