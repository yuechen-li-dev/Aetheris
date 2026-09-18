using System.Text.Json;
using System.Text.Json.Serialization;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Reconstruction;

public sealed record PointTorusJsonSample(double[] SourcePoint, double[] SourceNormal, double[] Center, double[] Axis, double MajorRadius, double SignedMinorRadius);
public sealed record PointTorusJsonDocument(
    string Schema,
    string Source,
    string ParameterProducer,
    string? ModelRevision,
    string? ModelHash,
    int NeighborCount,
    double ValidityBandMm,
    PointTorusSignCapability SignCapability,
    PointTorusJsonSample[] Samples,
    Dictionary<string,string>? Properties = null);

public sealed record SampledSdfGridJsonDocument(string Schema, int SizeX, int SizeY, int SizeZ, double[] Origin, double[] Spacing, double[] Values, string Source);

public static class ContinuumInterchange
{
    public const string PointTorusSchema = "aetheris.point-torus-field.v1";
    public const string SdfGridSchema = "aetheris.sampled-sdf-grid.v1";
    public static readonly JsonSerializerOptions JsonOptions = CreateOptions();

    public static PointTorusField ReadPointTorusField(Stream stream)
    {
        var document = JsonSerializer.Deserialize<PointTorusJsonDocument>(stream, JsonOptions) ?? throw new InvalidDataException("Point-torus JSON is empty.");
        if (document.Schema != PointTorusSchema) throw new InvalidDataException($"Expected schema '{PointTorusSchema}'.");
        var samples = document.Samples.Select((s,i) => new PointTorusSample(Point(s.SourcePoint,$"samples[{i}].sourcePoint"), Vector(s.SourceNormal,$"samples[{i}].sourceNormal"),
            Point(s.Center,$"samples[{i}].center"), Vector(s.Axis,$"samples[{i}].axis"), s.MajorRadius, s.SignedMinorRadius));
        return new(samples, new(document.NeighborCount, document.ValidityBandMm, document.SignCapability),
            new(document.Source, document.ParameterProducer, document.ModelRevision, document.ModelHash, document.Properties));
    }

    public static SampledSdfGrid ReadSdfGrid(Stream stream, out string source)
    {
        var document = JsonSerializer.Deserialize<SampledSdfGridJsonDocument>(stream, JsonOptions) ?? throw new InvalidDataException("SDF grid JSON is empty.");
        if (document.Schema != SdfGridSchema) throw new InvalidDataException($"Expected schema '{SdfGridSchema}'.");
        source = document.Source;
        return new(document.SizeX,document.SizeY,document.SizeZ,Point(document.Origin,"origin"),Vector(document.Spacing,"spacing"),document.Values);
    }

    private static Point3D Point(double[] values,string name){Require3(values,name);return new(values[0],values[1],values[2]);}
    private static Vector3D Vector(double[] values,string name){Require3(values,name);return new(values[0],values[1],values[2]);}
    private static void Require3(double[]? values,string name){if(values is null||values.Length!=3||values.Any(v=>!double.IsFinite(v)))throw new InvalidDataException($"'{name}' must contain three finite numbers.");}
    private static JsonSerializerOptions CreateOptions()
    {
        var options=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase,PropertyNameCaseInsensitive=true,WriteIndented=true};
        options.Converters.Add(new JsonStringEnumConverter()); return options;
    }
}
