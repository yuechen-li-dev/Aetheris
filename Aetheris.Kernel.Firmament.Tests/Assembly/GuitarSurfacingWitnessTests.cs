using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class GuitarSurfacingWitnessTests
{
    [Fact]
    public void MultiFileProjectCompilesWithoutFilesystemResolutionAndKeepsPublicNeckPorts()
    {
        var root = FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var documents = Directory.GetFiles(Path.GetDirectoryName(root)!, "*.firmament")
            .Append(root).Select(path => new KeyValuePair<string, string>(Path.GetFileName(path), File.ReadAllText(path))).ToArray();
        var result = new AssemblyM1Pipeline().CompileProject(new FirmamentProjectSnapshot("guitar.firmasm", documents));
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics));
        Assert.Equal(56, result.Geometry!.DefinitionBodies.Count);
        Assert.Equal(99, result.Geometry.InstanceBodies.Count);
        Assert.Equal(10, result.Ir!.AssemblyDefinitions!.Count);
        var neck = result.Ir.Instances.Single(i => i.Path.ToString() == "GuitarX0.Neck");
        Assert.True(neck.IsEncapsulatedDefinition);
        Assert.True(neck.SemanticRoot.ExposedMembers.ContainsKey("NutMount"));
        var nut = Assert.IsType<Aetheris.Semantics.ExactDatumFrameBinding>(AssemblyWorldQuery.Resolve(result.Ir,
            neck.SemanticRoot.ExposedMembers["NutMount"].StableIdentity));
        Assert.Equal(660, nut.OriginY, 6);
        Assert.Equal(55, nut.OriginZ, 6);
        Assert.DoesNotContain("Headstock", neck.SemanticRoot.ExposedMembers.Keys);
        Assert.Equal(22, result.Ir.SourceDependencies!.Count);
        Assert.Contains(result.Ir.SourceDependencies, d => d.Path == "neck-profile.firmament");
        Assert.Contains(result.Ir.SourceDependencies, d => d.Path == "materials.firmament");
        Assert.Contains(result.Ir.SourceDependencies, d => d.Path == "tuners-assembly.firmament");
    }
    [Fact]
    public void SingleCutCarveAndBackExportClosedOrientedMeshesThroughOrdinaryAssembly()
    {
        var path = FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm");
        var compiled = new AssemblyM1Pipeline().CompileFile(path);
        Assert.True(compiled.IsSuccess, string.Join("\n", compiled.Diagnostics.Select(d => d.Message)));
        var mesh = AssemblyDisplayMeshExporter.Export(compiled);
        Assert.Equal(99, mesh.Occurrences.Count(o => o.DefinitionId is not null));
        Assert.Equal(6, compiled.Ir!.Patterns!.Count);
        var inlays = compiled.Ir.Instances.Where(i => i.Path.ToString().StartsWith("GuitarX0.Neck.PearlInlays.", StringComparison.Ordinal)
            && i.Path.ToString().EndsWith(".Inlay", StringComparison.Ordinal)).ToArray();
        Assert.Equal(9, inlays.Length);
        Assert.Single(inlays.Select(i => i.DefinitionIdentity).Distinct());
        Assert.All(inlays, i => Assert.Equal(62, i.ResolvedTransform!.Matrix[14], 10));
        var inlay3 = Assert.Single(inlays, i => i.Path.ToString() == "GuitarX0.Neck.PearlInlays.Fret3.Inlay");
        Assert.Equal(573.8, inlay3.ResolvedTransform!.Matrix[13], 10);
        Assert.Empty(compiled.Ir.Joints!);
        Assert.Equal(10, compiled.Ir.AssemblyDefinitions!.Count);
        Assert.Single(compiled.Ir.AssemblyDefinitions.Single(d => d.DefinitionIdentity == "GuitarNeck").LocalMates,
            mate => mate.Name == "VeneerSeat");
        Assert.DoesNotContain(File.ReadAllText(path), "LegacyExplicit");
        Assert.Contains(compiled.Ir.Instances, i => i.Path.ToString() == "GuitarX0.Neck.Tuners.BassRow.Low.Tuner.TunerPost"
            && i.Provenance.Any(p => p.Stage == "assembly-pattern"));
        var tunerUnits = compiled.Ir.Instances.Where(i => i.DefinitionIdentity == "GuitarTuner").ToArray();
        Assert.Equal(6, tunerUnits.Length);
        Assert.All(tunerUnits, t => Assert.True(t.SemanticRoot.ExposedMembers.ContainsKey("Mount")));
        Assert.Equal(4, compiled.Ir.Instances.Count(i => i.DefinitionIdentity == "ControlKnob"));
        Assert.Single(compiled.Ir.Instances, i => i.DefinitionIdentity == "PickupSelector");
        var pickups = compiled.Ir.Instances.Where(i => i.DefinitionIdentity.StartsWith("Humbucker<", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, pickups.Length);
        Assert.All(pickups, p => Assert.Equal(53, p.ResolvedTransform!.Matrix[14], 6));
        Assert.Contains(pickups, p => p.Path.ToString() == "GuitarX0.Electronics.Pickups.NeckPickup" && Math.Abs(p.ResolvedTransform!.Matrix[13] - 98) < 1e-9);
        Assert.Single(mesh.Definitions, d => d.Identity.StartsWith("Humbucker<", StringComparison.Ordinal));
        var head = compiled.Ir.Instances.Single(i => i.Path.ToString() == "GuitarX0.Neck.Headstock");
        Assert.Equal(660, head.ResolvedTransform!.Matrix[13], 6);
        Assert.Equal(48, head.ResolvedTransform.Matrix[14], 6);
        foreach (var tuner in tunerUnits)
        {
            var m = tuner.ResolvedTransform!.Matrix;
            // Washer underside lies on the actual veneer top: head-local z = 15mm.
            var dy = m[13] - head.ResolvedTransform.Matrix[13];
            var dz = m[14] - head.ResolvedTransform.Matrix[14];
            Assert.Equal(15, dy * head.ResolvedTransform.Matrix[9] + dz * head.ResolvedTransform.Matrix[10], 7);
            Assert.Equal(head.ResolvedTransform.Matrix[9], m[9], 8);
            Assert.Equal(head.ResolvedTransform.Matrix[10], m[10], 8);
            var stem = compiled.Ir.Instances.Single(i => i.Path.ToString() == tuner.Path + ".ButtonStem");
            var local = Transform3D.FromRowMajor(stem.ResolvedTransform!.Matrix)
                * Transform3D.FromRowMajor(m).Inverse();
            var buttonEnd = local.Apply(new Point3D(0, 0, 0));
            var headEnd = local.Apply(new Point3D(0, 0, 16));
            // Stem overlaps the button's inner edge (-14.5mm) and reaches the post axis.
            Assert.InRange(buttonEnd.X, -29.5, -14.5);
            Assert.Equal(0, headEnd.X, 7);
            Assert.Equal(0, buttonEnd.Y, 7);
            Assert.Equal(-4, buttonEnd.Z, 7);
            Assert.Equal(-4, headEnd.Z, 7);
        }
        Assert.Single(mesh.Definitions, d => d.Identity == "Drum<R:2.5mm,H:16mm>");
        var rail = compiled.Ir.Instances.Single(i => i.Path.ToString() == "GuitarX0.Bridge.StringRail");
        var railTransform = Transform3D.FromRowMajor(rail.ResolvedTransform!.Matrix);
        // Extruded semicircle: diameter rests on the tailpiece at z=60; crown at z=65.
        Assert.Equal(60, railTransform.Apply(new Point3D(0, 0, 0)).Z, 7);
        Assert.Equal(65, railTransform.Apply(new Point3D(5, 0, 0)).Z, 7);
        var bridgeUnit = compiled.Ir.Instances.Single(i => i.Path.ToString() == "GuitarX0.Bridge");
        var crown = Assert.IsType<Aetheris.Semantics.ExactDatumFrameBinding>(AssemblyWorldQuery.Resolve(compiled.Ir,
            bridgeUnit.SemanticRoot.ExposedMembers["TailStringPlane"].StableIdentity));
        Assert.Equal(-75, crown.OriginY, 7);
        Assert.Equal(65, crown.OriginZ, 7);
        Assert.Equal(6, mesh.Definitions.Count(d => d.Identity.StartsWith("GuitarString<", StringComparison.Ordinal)));
        var surfaced = mesh.Definitions.Where(d => d.Identity.StartsWith("SectionChainFile", StringComparison.Ordinal)).ToArray();
        Assert.Equal(4, surfaced.Length);
        var strings = mesh.Definitions.Where(d => d.Identity.StartsWith("GuitarString<", StringComparison.Ordinal)).ToArray();
        Assert.All(strings, d => Assert.Equal("SurfaceMeshIr", d.MeshPipeline));
        var joinDetails = mesh.Definitions.Where(d => d.Identity.StartsWith("TailpieceRail<", StringComparison.Ordinal)
            || d.Identity.StartsWith("HeadstockScarf<", StringComparison.Ordinal) || d.Identity == "Drum<R:2.5mm,H:16mm>").ToArray();
        Assert.Equal(3, joinDetails.Length);
        foreach (var definition in surfaced.Concat(strings).Concat(joinDetails))
        {
            if (definition.Identity.StartsWith("SectionChainFile", StringComparison.Ordinal))
                Assert.Equal("StructuredSpline", definition.MeshPipeline);
            Point3D Position(int i) => new(definition.Positions[i * 3], definition.Positions[i * 3 + 1], definition.Positions[i * 3 + 2]);
            string Key(int i)
            {
                var p = Position(i);
                return FormattableString.Invariant($"{p.X:F6},{p.Y:F6},{p.Z:F6}");
            }
            var edges = new Dictionary<(string, string), int>();
            double volume = 0;
            for (var i = 0; i < definition.Indices.Length; i += 3)
            {
                var ids = definition.Indices.Skip(i).Take(3).ToArray();
                var a = Position(ids[0]); var b = Position(ids[1]); var c = Position(ids[2]);
                var cross = (b - a).Cross(c - a);
                var normal = new Vector3D(definition.Normals[ids[0] * 3], definition.Normals[ids[0] * 3 + 1], definition.Normals[ids[0] * 3 + 2]);
                Assert.True(cross.Dot(normal) > 0, $"{definition.Identity}: triangle {i / 3} has normal dot {cross.Dot(normal):R}; every triangle must agree with its outward face normal.");
                volume += (a - new Point3D(0, 0, 0)).Dot((b - new Point3D(0, 0, 0)).Cross(c - new Point3D(0, 0, 0))) / 6;
                for (var j = 0; j < 3; j++)
                {
                    var from = Key(ids[j]); var to = Key(ids[(j + 1) % 3]);
                    var edge = string.CompareOrdinal(from, to) < 0 ? (from, to) : (to, from);
                    edges[edge] = edges.GetValueOrDefault(edge) + 1;
                }
            }
            Assert.All(edges.Values, uses => Assert.Equal(2, uses));
            Assert.True(volume > 0, "Each exact part must retain a closed positive-volume display witness.");
        }
        var top = Assert.Single(surfaced.Where(d => d.Identity.Contains("CarvedMaple", StringComparison.Ordinal)));
        var heights = top.Positions.Where((_, i) => i % 3 == 2).ToArray();
        Assert.Equal(40, heights.Min(), 6);
        Assert.Equal(53, heights.Max(), 6);
        Assert.True(heights.Any(z => z > 44 && z < 50), "The top must have real intermediate surface height.");
        var neck = Assert.Single(surfaced.Where(d => d.Identity.Contains("Neck.firmament", StringComparison.Ordinal)));
        var neckPoints = Enumerable.Range(0, neck.Positions.Length / 3)
            .Select(i => new Point3D(neck.Positions[i * 3], neck.Positions[i * 3 + 1], neck.Positions[i * 3 + 2])).ToArray();
        Assert.All(neckPoints.Where(p => p.Y <= 630), p => Assert.True(p.Z <= 55.000001, "Main neck must stay below the flat fretboard seat."));
        var nutPoints = neckPoints.Where(p => System.Math.Abs(p.Y - 660) < 0.000001).ToArray();
        Assert.NotEmpty(nutPoints);
        Assert.Equal(48, nutPoints.Min(p => p.Z), 6);
        Assert.Equal(55, nutPoints.Max(p => p.Z), 6);
        Assert.True(neckPoints.Any(p => p.Y < 200 && p.Z < 20), "The rounded heel must extend into the body back.");
        foreach (var p in neckPoints.Where(p => p.Y >= 240 && p.Y <= 630))
        {
            var straightBack = 42 + (p.Y - 240) / 70;
            Assert.True(p.Z >= straightBack - 0.002, "The main neck must not bulge behind its straight tapered back.");
            // The nut cap legitimately contains interior points between back and seat.
            if (p.Y < 659.999999 && System.Math.Abs(p.X) < 0.000001)
                Assert.True(System.Math.Abs(p.Z - straightBack) < 0.002 || System.Math.Abs(p.Z - 55) < 0.002,
                    "Centerline samples must lie on the straight back or flat seat.");
        }
        // End cap is inside the headstock's solid, rather than exposed at the nut.
        var headInverse = Transform3D.FromRowMajor(head.ResolvedTransform.Matrix).Inverse();
        var merge = neckPoints.Select(headInverse.Apply).Where(p => System.Math.Abs(p.Y - 18) < 0.000001).ToArray();
        Assert.NotEmpty(merge);
        Assert.All(merge, p => {
            Assert.InRange(p.Z, -0.000001, 14.000001);
            var halfWidth = 21.5 + 12.5 * p.Y / 40;
            Assert.True(System.Math.Abs(p.X) <= halfWidth + 0.000001, "Neck transition cap must be buried inside headstock outline.");
        });
        Assert.Contains("subdivisionScheme = \"none\"", AssemblyUsdExporter.Serialize(compiled.Ir!, mesh));
        var exported = AssemblyIrAp242Exporter.Export(compiled);
        Assert.True(exported.IsSuccess, string.Join("\n", exported.Diagnostics));
        var imported = Aetheris.Kernel.Core.Step242.Step242AssemblyImporter.Import(exported.Value);
        Assert.True(imported.IsSuccess, string.Join("\n", imported.Diagnostics));
        var geometryIds = imported.Value.Definitions.Where(d => d.Geometry is not null).Select(d => d.StableId).ToHashSet();
        Assert.Equal(56, geometryIds.Count);
        Assert.Equal(99, imported.Value.Occurrences.Count(o => geometryIds.Contains(o.DefinitionStableId!)));
    }
}
