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
    [Fact]
    public void ADiameterOnAPlane_IsRejected()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source().Replace("target: part.face(\"#191\")", "target: part.face(\"#51\")"),
            Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("imported-pmi-target-kind"));
    }
    [Fact]
    public void ADatumOnACylinder_IsItsAxis()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source().Replace("Datum A { target: part.face(\"#51\") }", "Datum A { target: part.face(\"#191\") }"),
            Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics));
        Assert.Contains("semantic datum axis target=", built.Value.StepText);
        Assert.Equal(1, Step242SemanticPmiInspector.Inspect(built.Value.StepText).DatumCount);
    }
    private static string WithPmi(string records) => Source()[..Source().IndexOf("    Pmi {", StringComparison.Ordinal)] + "    Pmi {\n" + records + "\n    }\n}\n";
    [Fact]
    public void ADistanceBetweenOpposedFaces_IsAThicknessAndChecksItsNominal()
    {
        // The part is 6 mm thick between its bottom (#51) and top (#102).
        var exact = FirmamentBuildAndExport.CompileSource(WithPmi("""
                    Distance Thickness { between: [part.face("#102"), part.face("#51")] value: 6mm tolerance: PlusMinus(0.1mm, 0.1mm) }
                    Note Material { text: "PA12, black" }
            """), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(exact.IsSuccess, string.Join("; ", exact.Diagnostics));
        Assert.DoesNotContain(exact.Diagnostics, d => d.Message.Contains("pmi-nominal-outside-geometry"));
        Assert.Contains("DIMENSIONAL_SIZE(", exact.Value.StepText);
        Assert.Contains("'thickness'", exact.Value.StepText);
        var inspection = Step242SemanticPmiInspector.Inspect(exact.Value.StepText);
        var thickness = Assert.Single(inspection.Items, i => i.Kind == "Dimension");
        Assert.Equal(6d, thickness.Value); Assert.Equal(2, thickness.GeometricFaceEntityIds.Count);
        Assert.Contains(inspection.Items, i => i.Text == "PA12, black");

        // The viewport shows it as a size between the two faces.
        var shown = Assert.Single(PmiDisplayProjection.FromStep(exact.Value.StepText), a => a.Name == "Thickness");
        Assert.Equal(6d, shown.Value);

        var off = FirmamentBuildAndExport.CompileSource(WithPmi("""
                    Distance Thickness { between: [part.face("#102"), part.face("#51")] value: 6.5mm tolerance: PlusMinus(0.1mm, 0.1mm) }
            """), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(off.IsSuccess);
        var warning = Assert.Single(off.Diagnostics, d => d.Message.Contains("pmi-nominal-outside-geometry"));
        Assert.Contains("measures 6 mm", warning.Message);
    }
    [Fact]
    public void ADistanceNeedsTwoParallelPlanes()
    {
        var built = FirmamentBuildAndExport.CompileSource(WithPmi("""
                    Distance Skew { between: [part.face("#51"), part.face("#191")] value: 6mm tolerance: PlusMinus(0.1mm, 0.1mm) }
            """), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.False(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("imported-pmi-distance-faces"));
    }
    private static string Ctc01Holes(string h4, string pmi) => $$"""
        Model Ctc {
            units: mm
            InlineStep part { path: "nist_ctc_01_asme1_ap242-e1.stp" textSha256: "341cea888fb316673ed683e59391c11b4f96cff2542d9387e135f06682dbd745" importTolerance: 0.01mm geometryOnly: true }
            Recognize part {
                Region H1 { kind: HoleShaft stepFaceEntities: ["#835", "#836"] }
                Region H2 { kind: HoleShaft stepFaceEntities: ["#837", "#838"] }
                Region H3 { kind: HoleShaft stepFaceEntities: ["#839", "#840"] }
                Region H4 { kind: HoleShaft stepFaceEntities: [{{h4}}] }
            }
            Pmi {
                Datum A { target: part.face("#766") }
                {{pmi}}
            }
        }
        """;
    [Fact]
    public void RecognizedHoles_TakeOneQuantityCallout()
    {
        // Four Ø35 holes on NIST CTC-01, each exported as two half-cylinders.
        var built = FirmamentBuildAndExport.CompileSource(Ctc01Holes("\"#853\", \"#854\"", """
                HoleDiameter Bolt { target: [H1, H2, H3, H4] value: 35mm tolerance: PlusMinus(0.1mm, 0mm) }
                Position BoltPos { target: [H1, H2, H3, H4] tolerance: 0.2mm zone: Cylindrical datumRefs: [A] }
            """), Path.Combine(Repo(), "testdata/step242/nist/CTC"));
        Assert.True(built.IsSuccess, string.Join("; ", built.Diagnostics));
        var items = Step242SemanticPmiInspector.Inspect(built.Value.StepText).Items;
        Assert.All(items.Where(i => i.Kind is "Diameter" or "Position"), i => { Assert.Equal(4, i.Quantity); Assert.Equal(8, i.GeometricFaceEntityIds.Count); });
        Assert.Contains(PmiDisplayProjection.FromStep(built.Value.StepText), a => a.Name == "Bolt" && a.Text.StartsWith("4X", StringComparison.Ordinal));
    }
    [Fact]
    public void RecognizedHoles_MustBeAlikeAndEachOneCylinder()
    {
        var differ = FirmamentBuildAndExport.CompileSource(Ctc01Holes("\"#855\", \"#856\"", """
                HoleDiameter Bolt { target: [H1, H2, H3, H4] value: 35mm tolerance: PlusMinus(0.1mm, 0mm) }
            """), Path.Combine(Repo(), "testdata/step242/nist/CTC"));
        Assert.Contains(differ.Diagnostics, d => d.Message.Contains("firmament-v2-imported-holes-differ"));
        var two = FirmamentBuildAndExport.CompileSource(Ctc01Holes("\"#853\", \"#855\"", """
                HoleDiameter Bolt { target: part.region("H4") value: 35mm tolerance: PlusMinus(0.1mm, 0mm) }
            """), Path.Combine(Repo(), "testdata/step242/nist/CTC"));
        Assert.Contains(two.Diagnostics, d => d.Message.Contains("firmament-v2-imported-hole-not-one-cylinder"));
    }
    [Fact]
    public void AnOutOfBandDiameter_Warns()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source().Replace("value: 2mm", "value: 2.5mm"), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess);
        Assert.Contains(built.Diagnostics, d => d.Message.Contains("pmi-nominal-outside-geometry: 'Bore'"));
    }
    [Fact]
    public void ExistingImportedAnnotations_ArePreservedWithTheirTargets()
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(), Path.Combine(Repo(), "fixtures/Regression/ImportedPartPmi"));
        Assert.True(built.IsSuccess);
        var source = Source().Replace("../../../testdata/firmament/inline-step/canonical-through-hole.step", "annotated.step");
        source = source[..source.IndexOf("    Pmi {", StringComparison.Ordinal)] + "}";
        var imported = FirmamentBuildAndExport.CompileSource(source, resources: new Dictionary<string, string> { ["annotated.step"] = built.Value.StepText });
        Assert.True(imported.IsSuccess, string.Join(";", imported.Diagnostics.Select(d => d.Message)));
        var before = Step242SemanticPmiInspector.Inspect(built.Value.StepText);
        var after = Step242SemanticPmiInspector.Inspect(imported.Value.StepText);
        Assert.Equal(before.DatumCount, after.DatumCount); Assert.Equal(before.DimensionCount, after.DimensionCount);
        Assert.All(after.Items, annotation => Assert.NotEmpty(annotation.GeometricFaceEntityIds));
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
        // GLB metadata keeps semantics; the highlighted support is geometry in the buffers.
        var projected = PmiDisplayProjection.FromStep(built.Value.StepText, options).Single(a => a.Kind == "Diameter").Target!;
        var targetPoints = projected.Positions; var targetNormals = projected.Normals; var offset = projected.SurfaceOffset;
        Assert.NotEmpty(targetPoints);
        for (var i = 0; i < targetPoints.Length; i += 3)
        {
            var point = new Point3D(targetPoints[i]-offset*targetNormals[i], targetPoints[i+1]-offset*targetNormals[i+1], targetPoints[i+2]-offset*targetNormals[i+2]);
            Assert.Contains(tess.FacePatches.SelectMany(p => p.Positions), p => (p-point).Length < 1e-8);
        }
        // Body, two annotations and the model edges drawn over the translucent part.
        Assert.Equal(4, root.GetProperty("meshes").GetArrayLength());
        var annotationMeshes = root.GetProperty("meshes").EnumerateArray().Where(m => m.TryGetProperty("extras", out var extras)
            && extras.TryGetProperty("role", out var role) && role.GetString() == "PMI presentation").ToArray();
        Assert.Equal(2, annotationMeshes.Length);
        Assert.All(annotationMeshes, mesh => Assert.Equal(1, mesh.GetProperty("primitives")[0].GetProperty("mode").GetInt32()));
        Assert.All(annotationMeshes, mesh => Assert.Contains(mesh.GetProperty("primitives").EnumerateArray(), p => p.GetProperty("mode").GetInt32() == 4));
        Assert.Single(root.GetProperty("meshes").EnumerateArray(), m => m.GetProperty("name").GetString() == "edges");
        Assert.Equal("BLEND", root.GetProperty("materials")[0].GetProperty("alphaMode").GetString());
        Assert.Equal(.55, root.GetProperty("materials")[0].GetProperty("pbrMetallicRoughness").GetProperty("baseColorFactor")[3].GetDouble());
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
            // The leader starts on the support itself.
            var leader = Assert.Single(annotation.Lines, line => (Point(line[0]) - anchor).Length < 1e-9);
            var patch = tess.FacePatches.Single(p => p.FaceId == binding.FaceId);
            Assert.InRange(target.SurfaceOffset, 0, annotation.TextHeight*.002);
            // Presentation is lifted very slightly to avoid coincident-surface GLB striping.
            // Remove the disclosed offset when proving its relationship to exact geometry.
            var marker = target.Positions.Chunk(3).Zip(target.Normals.Chunk(3), (p,n) =>
                Point(p) - new Vector3D(n[0],n[1],n[2])*target.SurfaceOffset).ToArray();
            Assert.NotEmpty(target.Indices);
            if (surface!.Plane is { } plane)
            {
                Assert.Equal("PlanePatch", target.Kind);
                // A planar feature is shown edge-on: the stem leaves along the face normal.
                Assert.True((Point(leader[1]) - anchor).Cross(normal).Length < 1e-8);
                Assert.True((Point(leader[1]) - anchor).Dot(normal) > 0);
                Assert.All(marker.Append(anchor), p => Assert.True(Math.Abs((p-plane.Origin).Dot(plane.Normal.ToVector())) < 1e-8));
                // Every highlighted point is in material on this actual trimmed face, including
                // its hole exclusion, not merely in the plane or the global box.
                var seat = new Aetheris.Kernel.Core.Brep.Queries.BrepPlanarSeatQuery(body);
                Assert.All(marker, p => Assert.Contains(binding.FaceId, seat.FindFaces(p, normal)));
                Assert.DoesNotContain(patch.Positions, p => (p-anchor).Length < 1e-6);
            }
            else
            {
                var cylinder = surface.Cylinder!.Value;
                Assert.Equal("CylinderWall", target.Kind);
                Assert.All(marker.Append(anchor), p => {
                    var delta = p-cylinder.Origin; var axial = delta.Dot(cylinder.Axis.ToVector());
                    Assert.Equal(cylinder.Radius, (delta-cylinder.Axis.ToVector()*axial).Length, 6);
                });
                // The anchor is on the wall's actual axial extent (its visible rim), never a chord.
                var values = patch.Positions.Select(p => (p-cylinder.Origin).Dot(cylinder.Axis.ToVector())).ToArray();
                Assert.InRange((anchor-cylinder.Origin).Dot(cylinder.Axis.ToVector()), values.Min() - 1e-8, values.Max() + 1e-8);
            }
        }
        static Point3D Point(double[] p) => new(p[0], p[1], p[2]);
    }
}
