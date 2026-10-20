using System.Text.Json;
using Aetheris.Kernel.Core.Brep.Tessellation;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.Export;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Visualization;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class ImportedPartPmiTests
{
    private static string Repo()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "Aetheris.slnx"))) d = d.Parent;
        return d!.FullName;
    }
    private static string Source() => File.ReadAllText(Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi/annotated-part.firmament"));
    private static string Step() => File.ReadAllText(Path.Combine(Repo(), "testdata/firmament/inline-step/canonical-through-hole.step"));
    [Fact]
    public void SnapshotAssets_AreExplicitAndRemainImmutableAfterAdmission()
    {
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "assets/body.step");
        var assets = new Dictionary<string, string> { ["assets/body.step"] = Step() };
        var project = new FirmamentProjectSnapshot("part.firmament", assets.Append(new KeyValuePair<string, string>("part.firmament", source)));
        Assert.Single(project.SourceDocuments);
        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(project, "part.firmament", "1").Diagnostics.Where(d => d.Severity == "error"));
        var compiled = FirmamentBuildAndExport.CompileSource(source, resources: project.Documents);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        var admitted = Assert.IsType<FirmamentV2InlineStepRecord>(compiled.Value.RuntimeAdmissionDocument!.Solids.Single().Primitive);
        Assert.Equal(Step(), admitted.SourceText);
        assets["assets/body.step"] = "corrupt";
        Assert.Equal(Step(), admitted.SourceText);
        var missing = FirmamentBuildAndExport.CompileSource(Source(), Repo(), new Dictionary<string, string>());
        Assert.False(missing.IsSuccess); // Even an existing disk resource cannot escape an explicit snapshot.
        Assert.Contains(missing.Diagnostics, d => d.Message.Contains(FirmamentV2Parser.InlineStepPathInvalid) || d.Message.Contains(FirmamentV2Parser.InlineStepFileMissing));
    }
    [Theory]
    [InlineData("#51", "#191")]
    [InlineData("#191", "#51")]
    public void IncompatibleImportedPmiSurface_IsRejected(string from, string to)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source().Replace($"face(\"{from}\")", $"face(\"{to}\")"),
            Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("imported-pmi-target-kind"));
    }
    [Fact]
    public void ExistingImportedAnnotations_AreRejectedInsteadOfSilentlyStripped()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess);
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "annotated.step");
        source = source[..source.IndexOf("    Pmi {", StringComparison.Ordinal)] + "}";
        var imported = FirmamentBuildAndExport.CompileSource(source, resources: new Dictionary<string, string> { ["annotated.step"] = built.Value.StepText });
        Assert.False(imported.IsSuccess);
        Assert.Contains(imported.Diagnostics, d => d.Message.Contains("existing-pmi-unsupported"));
    }
    [Fact]
    public void ImportedTopology_UsesProvenanceRatherThanEntityLineOrder()
    {
        var step = Step();
        var records = System.Text.RegularExpressions.Regex.Matches(step, @"(?m)^#\d+=ADVANCED_FACE[^\r\n]+").Select(m => m.Value).ToArray();
        var shuffled = step.Replace(records[0], "__FIRST_FACE__").Replace(records[^1], records[0]).Replace("__FIRST_FACE__", records[^1]);
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "body.step");
        var result = FirmamentBuildAndExport.CompileSource(source, resources: new Dictionary<string, string> { ["body.step"] = shuffled });
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        var map = result.Value.RuntimeAdmissionDocument!.Solids.Single().InlineStep!.TopologyMap;
        var body = result.Value.RuntimeBody!;
        foreach (var binding in body.Bindings.FaceBindings)
            Assert.Equal($"face-{binding.FaceId.Value}", map.FaceEntityToFaceId["#" + binding.SourceStepEntityId]);
    }
    [Theory]
    [InlineData(24)]
    [InlineData(32)]
    public void AnnotatedGlb_RetainsBodyBuffersAndCarriesSemanticValuesAndSeparatePresentation(int segments)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics));
        var body = built.Value.RuntimeBody!;
        var options = new DisplayTessellationOptions(2*Math.PI/segments, .05, segments, segments);
        var tess = BrepDisplayTessellator.Tessellate(body, options).Value;
        var positions = new List<double>(); var normals = new List<double>(); var indices = new List<int>();
        foreach (var patch in tess.FacePatches)
        {
            var start = positions.Count / 3;
            positions.AddRange(patch.Positions.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            normals.AddRange(patch.Normals.SelectMany(p => new[] { p.X, p.Y, p.Z }));
            indices.AddRange(patch.TriangleIndices.Select(i => i + start));
        }
        var display = new AssemblyDisplayMeshDocument("aetheris/display-mesh/1", "Imported", "mm",
            [new("part", "part", positions.ToArray(), normals.ToArray(), indices.ToArray())],
            [new("part:occurrence", "part", null, "part", [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1])]) { TessellationOptions = options };
        var baseline = AssemblyGlbExporter.Serialize(display);
        var file = Assert.Single(ProductArtifactExporter.Export(new("Imported", built.Value.StepText, display), ["glb"], Path.GetTempPath()));
        using var json = JsonDocument.Parse(file.Bytes.AsMemory(20, BitConverter.ToInt32(file.Bytes, 12)));
        var root = json.RootElement;
        var records = root.GetProperty("asset").GetProperty("extras").GetProperty("pmi");
        Assert.Equal(2, records.GetArrayLength());
        var diameter = records.EnumerateArray().Single(r => r.GetProperty("kind").GetString() == "Diameter");
        Assert.Equal(2, diameter.GetProperty("value").GetDouble());
        Assert.Equal(.1, diameter.GetProperty("tolerancePlus").GetDouble());
        Assert.Equal(.05, diameter.GetProperty("toleranceMinus").GetDouble());
        Assert.Equal(1, diameter.GetProperty("stepFaceEntities").GetArrayLength());
        var target = diameter.GetProperty("target");
        var targetPoints = target.GetProperty("positions").EnumerateArray().Select(p => p.GetDouble()).ToArray();
        var targetNormals = target.GetProperty("normals").EnumerateArray().Select(p => p.GetDouble()).ToArray();
        var offset = target.GetProperty("surfaceOffset").GetDouble();
        for (var i = 0; i < targetPoints.Length; i += 3)
        {
            var point = new Point3D(targetPoints[i]-offset*targetNormals[i], targetPoints[i+1]-offset*targetNormals[i+1], targetPoints[i+2]-offset*targetNormals[i+2]);
            Assert.Contains(tess.FacePatches.SelectMany(p => p.Positions), p => (p-point).Length < 1e-8);
        }
        Assert.Equal(3, root.GetProperty("meshes").GetArrayLength());
        Assert.All(root.GetProperty("meshes").EnumerateArray().Skip(1), mesh => Assert.Equal(1, mesh.GetProperty("primitives")[0].GetProperty("mode").GetInt32()));
        Assert.All(root.GetProperty("meshes").EnumerateArray().Skip(1), mesh =>
            Assert.Contains(mesh.GetProperty("primitives").EnumerateArray(), p => p.GetProperty("mode").GetInt32() == 4));
        Assert.Equal("BLEND", root.GetProperty("materials")[0].GetProperty("alphaMode").GetString());
        Assert.Equal(.78, root.GetProperty("materials")[0].GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor")[3].GetDouble());
        // Body data is a byte-identical prefix; annotation curves are appended after it.
        var beforeOffset = 28 + BitConverter.ToInt32(baseline, 12);
        var afterOffset = 28 + BitConverter.ToInt32(file.Bytes, 12);
        var bodyLength = BitConverter.ToInt32(baseline, beforeOffset - 8);
        Assert.Equal(baseline.AsSpan(beforeOffset, bodyLength).ToArray(), file.Bytes.AsSpan(afterOffset, bodyLength).ToArray());
        Assert.All(PmiDisplayProjection.FromStep(built.Value.StepText), annotation => Assert.NotEmpty(annotation.Lines));
    }

    [Theory]
    [InlineData("#51")]
    [InlineData("#102")]
    public void Presentation_AttachesInsideTheSelectedSupport_NotACornerOrCylinderChord(string datum)
    {
        var source = Source().Replace("face(\"#51\")", $"face(\"{datum}\")");
        var built = FirmamentBuildAndExport.CompileSource(source, Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics));
        var body = Step242Importer.ImportBody(built.Value.StepText).Value;
        var tess = BrepDisplayTessellator.Tessellate(body).Value;
        foreach (var annotation in PmiDisplayProjection.FromStep(built.Value.StepText))
        {
            var binding = body.Bindings.FaceBindings.Single(b => b.SourceStepEntityId == annotation.StepFaceEntities.Single());
            body.TryGetFaceSurfaceGeometry(binding.FaceId, out var surface);
            var anchor = Point(annotation.Anchor);
            var target = Assert.IsType<PmiDisplayTarget>(annotation.Target);
            var normal = new Vector3D(target.Normal[0], target.Normal[1], target.Normal[2]);
            Assert.Equal(1, normal.Length, 9);
            Assert.True((anchor - Point(annotation.Lines[0][1])).Cross(normal).Length < 1e-8);
            Assert.True((Point(annotation.Lines[0][1]) - anchor).Dot(normal) > 0);
            var patch = tess.FacePatches.Single(p => p.FaceId == binding.FaceId);
            Assert.DoesNotContain(patch.Positions, p => (p-anchor).Length < 1e-6);
            Assert.InRange(target.SurfaceOffset, 0, annotation.TextHeight*.002);
            // Presentation is lifted very slightly to avoid coincident-surface GLB striping.
            // Remove the disclosed offset when proving its relationship to exact geometry.
            var marker = target.Positions.Chunk(3).Zip(target.Normals.Chunk(3), (p,n) =>
                Point(p) - new Vector3D(n[0],n[1],n[2])*target.SurfaceOffset).ToArray();
            Assert.NotEmpty(target.Indices);
            if (surface!.Plane is { } plane)
            {
                Assert.Equal("PlanePatch", target.Kind);
                Assert.All(marker.Append(anchor), p => Assert.True(Math.Abs((p-plane.Origin).Dot(plane.Normal.ToVector())) < 1e-8));
                // Every marker corner is in material on this actual trimmed face, including
                // its hole exclusion, not merely in the plane or the global box.
                var seat = new Aetheris.Kernel.Core.Brep.Queries.BrepPlanarSeatQuery(body);
                Assert.All(marker.Append(anchor), p => Assert.Contains(binding.FaceId, seat.FindFaces(p, normal)));
            }
            else
            {
                var cylinder = surface.Cylinder!.Value;
                Assert.Equal("CylinderWall", target.Kind);
                Assert.All(marker.Append(anchor), p => {
                    var delta = p-cylinder.Origin; var axial = delta.Dot(cylinder.Axis.ToVector());
                    Assert.Equal(cylinder.Radius, (delta-cylinder.Axis.ToVector()*axial).Length, 8);
                });
                var values = patch.Positions.Select(p => (p-cylinder.Origin).Dot(cylinder.Axis.ToVector())).ToArray();
                var v = (anchor-cylinder.Origin).Dot(cylinder.Axis.ToVector());
                Assert.InRange(v, values.Min() + (values.Max()-values.Min())*.2, values.Max() - (values.Max()-values.Min())*.2);
                Assert.True(target.StemLength < cylinder.Radius);
                Assert.NotEmpty(target.Lines);
                Assert.All(target.Lines, line => {
                    var axial = line.Select(p => (Point(p)-cylinder.Origin).Dot(cylinder.Axis.ToVector())).ToArray();
                    Assert.True(axial.Max()-axial.Min() < 1e-8, "Only physical rims, never the axial parameter seam, should be highlighted.");
                });
            }
        }
        static Point3D Point(double[] p) => new(p[0], p[1], p[2]);
    }
}
