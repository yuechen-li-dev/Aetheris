using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Step242;
using System.Globalization;
using System.Text;

namespace Aetheris.Kernel.Core.Tests.Step242;

public sealed class Step242Ftc06SameSenseRegressionTests
{
    [Fact]
    public void ExportBody_ImportedCylindricalFaceWithFalseSameSense_PreservesAdvancedFaceSense()
    {
        const string cylinderFace = @"ISO-10303-21;
HEADER;
ENDSEC;
DATA;
#1=MANIFOLD_SOLID_BREP('solid',#2);
#2=CLOSED_SHELL($,(#3));
#3=ADVANCED_FACE((#4),#5,.F.);
#4=FACE_OUTER_BOUND($,#6,.T.);
#5=CYLINDRICAL_SURFACE($,#30,1.0);
#6=EDGE_LOOP($,(#7,#8,#9,#10));
#7=ORIENTED_EDGE($,$,$,#11,.T.);
#8=ORIENTED_EDGE($,$,$,#12,.T.);
#9=ORIENTED_EDGE($,$,$,#13,.T.);
#10=ORIENTED_EDGE($,$,$,#14,.T.);
#11=EDGE_CURVE($,#15,#16,#17,.T.);
#12=EDGE_CURVE($,#16,#16,#18,.T.);
#13=EDGE_CURVE($,#16,#15,#19,.T.);
#14=EDGE_CURVE($,#15,#15,#20,.T.);
#15=VERTEX_POINT($,#21);
#16=VERTEX_POINT($,#22);
#17=LINE($,#21,#23);
#18=CIRCLE($,#31,1.0);
#19=LINE($,#22,#24);
#20=CIRCLE($,#30,1.0);
#21=CARTESIAN_POINT($,(1,0,0));
#22=CARTESIAN_POINT($,(1,0,1));
#23=VECTOR($,#25,1.0);
#24=VECTOR($,#26,1.0);
#25=DIRECTION($,(0,0,1));
#26=DIRECTION($,(0,0,-1));
#30=AXIS2_PLACEMENT_3D($,#27,#28,#29);
#31=AXIS2_PLACEMENT_3D($,#32,#28,#29);
#32=CARTESIAN_POINT($,(0,0,1));
#27=CARTESIAN_POINT($,(0,0,0));
#28=DIRECTION($,(0,0,1));
#29=DIRECTION($,(1,0,0));
ENDSEC;
END-ISO-10303-21;";

        var import = Step242Importer.ImportBody(cylinderFace);
        Assert.True(import.IsSuccess);

        var curvedFace = Assert.Single(import.Value.Topology.Faces);
        Assert.True(import.Value.Bindings.TryGetFaceBinding(curvedFace.Id, out var binding));
        Assert.False(binding.SameSense);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);

        var advancedFace = Assert.Single(ParseAdvancedFacesBySurfaceType(export.Value, "CYLINDRICAL_SURFACE"));
        Assert.False(advancedFace.SameSense);
    }

    [Fact]
    public void Step242Ftc06Import_DoesNotRegress()
    {
        var source = LoadFixture("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp");

        var import = Step242Importer.ImportBody(source);

        Assert.True(import.IsSuccess);
        Assert.Equal(187, import.Value.Topology.Faces.Count());
        Assert.Equal(476, import.Value.Topology.Edges.Count());
        Assert.Equal(310, import.Value.Topology.Vertices.Count());
    }

    [Fact]
    public void Step242Ftc06Export_HasStableTopologySummary()
    {
        var source = LoadFixture("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp");

        var import = Step242Importer.ImportBody(source);
        Assert.True(import.IsSuccess);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);

        var roundTrip = Step242Importer.ImportBody(export.Value);
        Assert.True(roundTrip.IsSuccess);

        Assert.Equal(import.Value.Topology.Faces.Count(), roundTrip.Value.Topology.Faces.Count());
        Assert.Equal(import.Value.Topology.Edges.Count(), roundTrip.Value.Topology.Edges.Count());
        Assert.Equal(import.Value.Topology.Vertices.Count(), roundTrip.Value.Topology.Vertices.Count());
        Assert.Equal(BuildSurfaceFamilyCounts(import.Value), BuildSurfaceFamilyCounts(roundTrip.Value));
    }

    [Fact]
    public void Step242Ftc06ProblemFace_AdvancedFaceSameSense_Regression()
    {
        var source = LoadFixture("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp");
        var sourceSummary = SummarizeAdvancedFaceSense(source);

        Assert.Equal(65, sourceSummary[("CYLINDRICAL_SURFACE", false)]);
        Assert.Equal(12, sourceSummary[("TOROIDAL_SURFACE", false)]);
        Assert.Equal(4, sourceSummary[("SPHERICAL_SURFACE", false)]);
        Assert.Equal(4, sourceSummary[("CONICAL_SURFACE", false)]);

        var import = Step242Importer.ImportBody(source);
        Assert.True(import.IsSuccess);

        var importedBindingSummary = SummarizeCurvedFaceBindingSense(import.Value);
        Assert.Equal(sourceSummary[("CYLINDRICAL_SURFACE", false)], importedBindingSummary[("CYLINDRICAL_SURFACE", false)]);
        Assert.Equal(sourceSummary[("TOROIDAL_SURFACE", false)], importedBindingSummary[("TOROIDAL_SURFACE", false)]);
        Assert.Equal(sourceSummary[("SPHERICAL_SURFACE", false)], importedBindingSummary[("SPHERICAL_SURFACE", false)]);
        Assert.Equal(sourceSummary[("CONICAL_SURFACE", false)], importedBindingSummary[("CONICAL_SURFACE", false)]);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);

        var exportSummary = SummarizeAdvancedFaceSense(export.Value);
        Assert.Equal(sourceSummary, exportSummary);
    }

    [Fact]
    public void Step242Ftc06_DiagnosticsIdentifyNoKnownInvalidTrimCondition()
    {
        var source = LoadFixture("testdata/step242/nist/FTC/nist_ftc_06_asme1_ap242-e2.stp");
        var import = Step242Importer.ImportBody(source);
        Assert.True(import.IsSuccess);

        var export = Step242Exporter.ExportBody(import.Value);
        Assert.True(export.IsSuccess);

        var mismatches = FindCurvedAdvancedFaceSameSenseLosses(source, export.Value);
        Assert.Empty(mismatches);
    }

    private static Dictionary<(string SurfaceType, bool SameSense), int> SummarizeAdvancedFaceSense(string stepText)
    {
        var summary = new Dictionary<(string SurfaceType, bool SameSense), int>();
        foreach (var face in ParseAdvancedFaces(stepText))
        {
            var key = (face.SurfaceType, face.SameSense);
            summary[key] = summary.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return summary;
    }

    private static Dictionary<(string SurfaceType, bool SameSense), int> SummarizeCurvedFaceBindingSense(BrepBody body)
    {
        var summary = new Dictionary<(string SurfaceType, bool SameSense), int>();
        foreach (var face in body.Topology.Faces.OrderBy(face => face.Id.Value))
        {
            Assert.True(body.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            Assert.NotNull(surface);

            var surfaceType = surface!.Kind switch
            {
                SurfaceGeometryKind.Cylinder => "CYLINDRICAL_SURFACE",
                SurfaceGeometryKind.Cone => "CONICAL_SURFACE",
                SurfaceGeometryKind.Sphere => "SPHERICAL_SURFACE",
                SurfaceGeometryKind.Torus => "TOROIDAL_SURFACE",
                _ => null
            };

            if (surfaceType is null)
            {
                continue;
            }

            var key = (surfaceType, binding.SameSense);
            summary[key] = summary.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return summary;
    }

    private static IReadOnlyList<string> FindCurvedAdvancedFaceSameSenseLosses(string sourceStepText, string exportedStepText)
    {
        var sourceSummary = SummarizeAdvancedFaceSense(sourceStepText);
        var exportSummary = SummarizeAdvancedFaceSense(exportedStepText);
        var interestingSurfaceTypes = new[]
        {
            "CYLINDRICAL_SURFACE",
            "CONICAL_SURFACE",
            "SPHERICAL_SURFACE",
            "TOROIDAL_SURFACE"
        };

        var losses = new List<string>();
        foreach (var surfaceType in interestingSurfaceTypes)
        {
            var sourceFalse = sourceSummary.GetValueOrDefault((surfaceType, false));
            var exportFalse = exportSummary.GetValueOrDefault((surfaceType, false));
            if (sourceFalse != exportFalse)
            {
                losses.Add($"{surfaceType}: source false-sense faces={sourceFalse}, export false-sense faces={exportFalse}");
            }
        }

        return losses;
    }

    private static Dictionary<string, int> BuildSurfaceFamilyCounts(BrepBody body)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var face in body.Topology.Faces)
        {
            Assert.True(body.Bindings.TryGetFaceBinding(face.Id, out var binding));
            Assert.True(body.Geometry.TryGetSurface(binding.SurfaceGeometryId, out var surface));
            Assert.NotNull(surface);

            var key = surface!.Kind.ToString();
            counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return counts;
    }

    private static IReadOnlyList<AdvancedFaceRecord> ParseAdvancedFacesBySurfaceType(string stepText, string surfaceType) =>
        ParseAdvancedFaces(stepText).Where(face => string.Equals(face.SurfaceType, surfaceType, StringComparison.Ordinal)).ToArray();

    private static IReadOnlyList<AdvancedFaceRecord> ParseAdvancedFaces(string stepText)
    {
        var entityMap = ParseEntityMap(stepText);
        var faces = new List<AdvancedFaceRecord>();
        foreach (var (entityId, rhs) in entityMap)
        {
            if (!rhs.StartsWith("ADVANCED_FACE(", StringComparison.Ordinal))
            {
                continue;
            }

            var surfaceEntityId = ReadLastReferenceBeforeSameSense(rhs);
            var sameSense = rhs.EndsWith(".T.)", StringComparison.Ordinal)
                ? true
                : rhs.EndsWith(".F.)", StringComparison.Ordinal)
                    ? false
                    : throw new InvalidOperationException($"Could not parse ADVANCED_FACE same_sense from '{rhs}'.");

            var surfaceRhs = entityMap[surfaceEntityId];
            var surfaceType = surfaceRhs[..surfaceRhs.IndexOf('(')];
            faces.Add(new AdvancedFaceRecord(entityId, surfaceType, sameSense));
        }

        return faces;
    }

    private static Dictionary<int, string> ParseEntityMap(string stepText)
    {
        var map = new Dictionary<int, string>();
        foreach (var rawLine in stepText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!rawLine.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var equalsIndex = rawLine.IndexOf('=');
            if (equalsIndex <= 1)
            {
                continue;
            }

            var id = int.Parse(rawLine.AsSpan(1, equalsIndex - 1), CultureInfo.InvariantCulture);
            map[id] = rawLine[(equalsIndex + 1)..^1];
        }

        return map;
    }

    private static int ReadLastReferenceBeforeSameSense(string rhs)
    {
        var markerIndex = rhs.LastIndexOf(",.", StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException($"Could not find same_sense marker in '{rhs}'.");
        }

        var referenceIndex = rhs.LastIndexOf('#', markerIndex);
        if (referenceIndex < 0)
        {
            throw new InvalidOperationException($"Could not find surface reference in '{rhs}'.");
        }

        var end = referenceIndex + 1;
        while (end < rhs.Length && char.IsDigit(rhs[end]))
        {
            end++;
        }

        return int.Parse(rhs.AsSpan(referenceIndex + 1, end - referenceIndex - 1), CultureInfo.InvariantCulture);
    }

    private static string LoadFixture(string relativePath)
    {
        var path = Path.Combine(Step242CorpusManifestRunner.RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private sealed record AdvancedFaceRecord(int FaceEntityId, string SurfaceType, bool SameSense);
}
