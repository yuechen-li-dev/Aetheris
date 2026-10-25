using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Brep;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class WireFormTests
{
    [Fact]
    public void PaperclipImportExport_PreservesAnalyticGeometryWithoutFittedUvSplines()
    {
        var compiled = FirmamentBuildAndExport.CompileSource(File.ReadAllText(Fixture("Canonical", "WireForm", "paperclip.firmament")));
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics.Select(d => d.Message)));
        var step = compiled.Value.StepText;
        for (var roundtrip = 0; roundtrip < 2; roundtrip++)
        {
            // The source is generated here: exercise recovery, not a cached import.
            var imported = Step242Importer.ImportBody(step);
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Diagnostics.Select(d => d.Message)));
            var body = imported.Value;
            Assert.Equal(9, body.Topology.Faces.Count());
            Assert.Equal(15, body.Topology.Edges.Count());
            Assert.Equal(8, body.Topology.Vertices.Count());
            Assert.Equal(3, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Torus));
            Assert.Equal(4, body.Geometry.Surfaces.Count(s => s.Value.Kind == SurfaceGeometryKind.Cylinder));
            Assert.Equal(30, body.Bindings.PcurveBindings.Count());
            foreach (var binding in body.Bindings.PcurveBindings)
            {
                Assert.Null(binding.Pcurve.PolynomialCurve);
                if (body.GetFaceSurface(binding.FaceId).Kind is SurfaceGeometryKind.Cylinder or SurfaceGeometryKind.Torus)
                    Assert.Equal(PcurveGeometryKind.Line, binding.Pcurve.Kind);
            }
            var evidence = BrepPcurveValidator.Validate(body, 1e-9, requireEveryCoedge: true);
            Assert.True(evidence.IsValid, string.Join("; ", evidence.Diagnostics));
            Assert.True(evidence.MaximumReconstructionDeviation < 1e-10);
            var exported = Step242Exporter.ExportBody(body);
            Assert.True(exported.IsSuccess);
            step = exported.Value;
            Assert.DoesNotContain("B_SPLINE", step, StringComparison.Ordinal);
            Assert.DoesNotContain("POLYLINE", step, StringComparison.Ordinal);
            Assert.Contains("SEAM_CURVE", step, StringComparison.Ordinal);
            Assert.True(step.Length < 20_000, "Analytic paperclip trims must retain a bounded compact representation.");
        }
    }

    [Theory]
    [InlineData("straight-wire.firmament")]
    [InlineData("u-wire.firmament")]
    [InlineData("paperclip.firmament")]
    [InlineData("three-dimensional-bends.firmament")]
    public void ExactSweep_BoundariesAgreeWithOutwardNormalsBeforeAnyImport(string fixture)
    {
        var parsed = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", fixture)));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics.Select(d => d.Message)));
        var built = WireFormBRepMaterializer.Build(parsed.Value);
        Assert.True(built.IsSuccess, string.Join(" | ", built.Diagnostics.Select(d => d.Message)));
        var body = built.Value.Body;
        foreach (var face in body.Topology.Faces)
        {
            var surface = body.GetFaceSurface(face.Id);
            var uses = face.LoopIds.SelectMany(id => body.Topology.GetLoop(id).CoedgeIds)
                .Select(body.Topology.GetCoedge).ToArray();
            foreach (var use in uses.Where(use => body.GetEdgeCurve(use.EdgeId).Kind == CurveGeometryKind.Circle3
                && body.Topology.GetEdge(use.EdgeId).StartVertexId == body.Topology.GetEdge(use.EdgeId).EndVertexId))
            {
                var circle = body.GetEdgeCurve(use.EdgeId).Circle3!.Value;
                var sense = (use.IsReversed ? -1d : 1d)
                    * (body.Bindings.GetEdgeBinding(use.EdgeId).OrientedEdgeSense ? 1d : -1d);
                if (surface.Plane is { } plane)
                {
                    Assert.True(circle.Normal.ToVector().Dot(plane.Normal.ToVector()) * sense > .999999,
                        $"{fixture}, cap {face.Id}: boundary must turn about its outward normal.");
                    continue;
                }
                // At either end ring, n x boundary-tangent must point along the
                // centerline into this segment, rather than into its neighbor.
                var operation = parsed.Value.Operations[face.Id.Value - 1];
                var atStart = (circle.Center - operation.Input.Position).Length < 1e-8;
                var inward = atStart ? operation.Input.Tangent.ToVector() : operation.Output.Tangent.ToVector() * -1d;
                var point = circle.Evaluate(.73);
                var normal = (point - circle.Center) / circle.Radius;
                Assert.True(normal.Cross(circle.Tangent(.73) * sense).Dot(inward) > 0d,
                    $"{fixture}, side {face.Id}: boundary must have the face interior to its left.");
            }
        }

        var step = Step242Exporter.ExportBody(body);
        Assert.True(step.IsSuccess);
        var document = Step242SubsetParser.Parse(step.Value).Value;
        foreach (var face in document.Entities.Where(e => e.Name == "ADVANCED_FACE"))
        {
            var supportId = Step242SubsetDecoder.ReadReference(face, 2, "face surface").Value.TargetId;
            var support = document.Entities.Single(e => e.Id == supportId);
            if (support.Name != "PLANE") continue;
            var plane = Step242SubsetDecoder.ReadPlaneSurface(document, support).Value;
            var boundId = Assert.Single(Step242SubsetDecoder.ReadReferenceList(face, 1, "face bounds").Value).TargetId;
            var bound = document.Entities.Single(e => e.Id == boundId);
            var loopId = Step242SubsetDecoder.ReadReference(bound, 1, "bound loop").Value.TargetId;
            var loop = document.Entities.Single(e => e.Id == loopId);
            var useId = Assert.Single(Step242SubsetDecoder.ReadReferenceList(loop, 1, "loop uses").Value).TargetId;
            var use = document.Entities.Single(e => e.Id == useId);
            var edgeId = Step242SubsetDecoder.ReadReference(use, 3, "oriented edge").Value.TargetId;
            var edge = document.Entities.Single(e => e.Id == edgeId);
            var curveId = Step242SubsetDecoder.ReadReference(edge, 3, "edge basis").Value.TargetId;
            var circle = Step242SubsetDecoder.ReadCircleCurve(document, document.Entities.Single(e => e.Id == curveId)).Value;
            var sign = new[] { (face, 3), (bound, 2), (use, 4), (edge, 4) }
                .Aggregate(1d, (s, item) => s * (Step242SubsetDecoder.ReadBoolean(item.Item1, item.Item2, "orientation").Value ? 1d : -1d));
            Assert.True(circle.Normal.ToVector().Dot(plane.Normal.ToVector()) * sign > .999999);
        }
        Assert.DoesNotContain("TRIMMED_CURVE", step.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentedOperationsDoNotCreateGeometryOrProvenance()
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "straight-wire.firmament"));
        var control = WireFormAuthoring.Parse(source);
        var commented = WireFormAuthoring.Parse(source.Replace("Straight Body {", "/* Straight Phantom { Length: 30mm } */ Straight Body {", StringComparison.Ordinal));
        Assert.True(control.IsSuccess);
        Assert.True(commented.IsSuccess, string.Join("; ", commented.Diagnostics.Select(d => d.Message)));
        Assert.Equal(control.Value.Operations, commented.Value.Operations);
        Assert.Equal("Body", Assert.Single(commented.Value.AuthoredOperations!).Name);
        var built = FirmamentBuildAndExport.CompileSource(source);
        var withComment = FirmamentBuildAndExport.CompileSource(source.Replace("Straight Body {", "/* Straight Phantom { Length: 30mm } */ Straight Body {", StringComparison.Ordinal));
        Assert.True(built.IsSuccess, Messages(built));
        Assert.True(withComment.IsSuccess, Messages(withComment));
        Assert.Equal(built.Value.StepText, withComment.Value.StepText);
    }

    [Fact]
    public void MissingStartFrameBodyProducesAnActionableRefusal()
    {
        var result = WireFormAuthoring.Parse("Model Invalid { units: mm WireForm Wire { Diameter: 2mm StartFrame Empty; Straight Body { Length: 20mm } } }");
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("StartFrame requires a body", StringComparison.Ordinal));
    }

    [Fact]
    public void ThinGuitarStringNutBreakKeepsItsTangentJoinAndExactSweep()
    {
        var source = File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath(
            "fixtures/Canonical/AssemblyInterfaces/GuitarX0/string-low-e.firmament"));
        var parsed = WireFormAuthoring.Parse(source);
        Assert.True(parsed.IsSuccess);
        Assert.Empty(WireFormBRepMaterializer.Validate(parsed.Value));
        var built = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(built.IsSuccess, Messages(built));
        var report = built.Value.WireForm!;
        Assert.Equal(2, report.StraightCount);
        Assert.Equal(1, report.BendCount);
        Assert.True(report.StepReimportedManifold);
        Assert.Equal(0, report.NonRationalBSplineSurfaces);
        Assert.True(report.Cylinders > 0);
        Assert.True(report.Tori > 0);
    }

    [Fact]
    public void Helix_AndAxisCoilShareSemanticIdentityAndGeometry()
    {
        var legacy = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament"));
        var canonical = legacy.Replace("AxisCoil Winding", "Helix Winding", StringComparison.Ordinal);
        var helixResult = WireFormAuthoring.Parse(canonical);
        var legacyResult = WireFormAuthoring.Parse(legacy);
        Assert.True(helixResult.IsSuccess);
        Assert.True(legacyResult.IsSuccess);
        var helix = Assert.IsType<WireAxisCoilAir>(Assert.Single(helixResult.Value.Operations));
        var axisCoil = Assert.IsType<WireAxisCoilAir>(Assert.Single(legacyResult.Value.Operations));
        Assert.Equal("Helix", helix.CoilKind);
        Assert.Equal(axisCoil, helix);
        Assert.Equal(axisCoil.StableId(legacyResult.Value.Name), helix.StableId(helixResult.Value.Name));
        for (var sample = 0; sample <= 32; sample++)
            Assert.True((helix.Evaluate(sample / 32d) - axisCoil.Evaluate(sample / 32d)).Length < 1e-12);
        var built = FirmamentBuildAndExport.CompileSource(canonical);
        Assert.True(built.IsSuccess, Messages(built));
        Assert.Equal("Helix", Assert.Single(built.Value.WireForm!.Operations).Kind);
    }

    [Theory]
    [InlineData("Radius: 12mm", "Radius: -12mm", "wireform-coil-radius-invalid")]
    [InlineData("Pitch: 5mm", "Pitch: -5mm", "wireform-coil-parameters-inconsistent")]
    [InlineData("Pitch: 5mm", "", "wireform-coil-parameters-inconsistent")]
    [InlineData("Pitch: 5mm", "Pitch: 5mm; Height: 39mm", "wireform-coil-parameters-inconsistent")]
    public void Helix_RejectsInvalidOrInconsistentDimensions(string oldText, string newText, string code)
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament"))
            .Replace("AxisCoil Winding", "Helix Winding", StringComparison.Ordinal)
            .Replace(oldText, newText, StringComparison.Ordinal);
        var result = WireFormAuthoring.Parse(source);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void Helix_DerivesPitchFromHeightAndKeepsExplicitHandedness()
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament"))
            .Replace("AxisCoil Winding", "Helix Winding", StringComparison.Ordinal)
            .Replace("Pitch: 5mm", "Height: 40mm", StringComparison.Ordinal)
            .Replace("RightHanded", "LeftHanded", StringComparison.Ordinal);
        var result = WireFormAuthoring.Parse(source);
        Assert.True(result.IsSuccess);
        var helix = Assert.IsType<WireAxisCoilAir>(Assert.Single(result.Value.Operations));
        Assert.Equal(8d, helix.Turns);
        Assert.Equal(5d, helix.PitchMm);
        Assert.Equal(40d, helix.HeightMm);
        Assert.Equal(WireCoilHandedness.LeftHanded, helix.Handedness);
    }

    [Fact]
    [Trait("Category", "SlowCorpus")]
    public void AxisCoil_LowersSemanticHelixToNonRationalSplineTube()
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament"));
        var authored = WireFormAuthoring.Parse(source);
        Assert.True(authored.IsSuccess, string.Join(Environment.NewLine, authored.Diagnostics.Select(x => x.Message)));
        var coil = Assert.IsType<WireAxisCoilAir>(Assert.Single(authored.Value.Operations));
        Assert.Equal(12d, coil.RadiusMm); Assert.Equal(8d, coil.Turns); Assert.Equal(5d, coil.PitchMm); Assert.Equal(40d, coil.HeightMm);
        Assert.Equal(Math.Sqrt(Math.Pow(2d * Math.PI * 12d * 8d, 2d) + 1600d), coil.LengthMm, 10);
        Assert.Equal(3d, coil.MinimumSelfClearanceMm, 10);
        var built = WireFormBRepMaterializer.Build(authored.Value); Assert.True(built.IsSuccess, string.Join(Environment.NewLine, built.Diagnostics.Select(x => x.Message)));
        Assert.All(built.Value.Body.Topology.Edges, edge => Assert.Equal(2, built.Value.Body.Topology.Coedges.Count(use => use.EdgeId == edge.Id)));
        var step = Step242Exporter.ExportBody(built.Value.Body); Assert.True(step.IsSuccess);
        var imported = Step242Importer.ImportBody(step.Value); Assert.True(imported.IsSuccess, string.Join(Environment.NewLine, imported.Diagnostics.Select(x => x.Message)));
        var bad = imported.Value!.Topology.Edges.Select(edge => (edge, count: imported.Value.Topology.Coedges.Count(use => use.EdgeId == edge.Id))).Where(x => x.count != 2).ToArray();
        Assert.True(bad.Length == 0, $"Nonmanifold imported edges: {string.Join(",", bad.Take(20).Select(x => $"{x.edge.Id.Value}:{x.count}"))}; edges={imported.Value.Topology.Edges.Count()}, coedges={imported.Value.Topology.Coedges.Count()}");
    }
    [Fact]
    public void CoilParameterResolution_HandednessAndTerminalCompositionAreDeterministic()
    {
        var right = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament"))).Value;
        var left = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "left-handed-axis-coil.firmament"))).Value;
        Assert.Equal(WireCoilHandedness.RightHanded, Assert.IsType<WireAxisCoilAir>(right.Operations[0]).Handedness);
        var leftCoil = Assert.IsType<WireAxisCoilAir>(left.Operations[0]); Assert.Equal(5d, leftCoil.PitchMm); Assert.Equal(WireCoilHandedness.LeftHanded, leftCoil.Handedness);
        var composed = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "straight-coil-straight.firmament"))).Value;
        Assert.Equal(composed.Operations[0].Output, composed.Operations[1].Input); Assert.Equal(composed.Operations[1].Output, composed.Operations[2].Input);
        Assert.True(Math.Abs(WireFormAuthoring.Dot(composed.Operations[0].Output.Tangent.ToVector(), composed.Operations[1].Input.Tangent.ToVector()) - 1d) < 1e-12);
    }

    [Fact]
    public void CylinderSurfaceCoil_IsAxisCoilEquivalentAndSurfaceFamiliesRetainClearanceLaw()
    {
        var surface = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Regression", "WireFormSurfaceSupport", "cylinder-surface-coil.firmament"))).Value;
        var cylinder = Assert.IsType<WireSurfaceCoilAir>(surface.Operations[0]); Assert.Equal("Cylinder", cylinder.SupportKind); Assert.Equal(1d, cylinder.MeasuredSupportClearanceMm);
        var axisSource = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament")).Replace("Radius: 12mm", "Radius: 12mm").Replace("Turns: 8", "Turns: 6").Replace("Pitch: 5mm", "Pitch: 5mm");
        var axis = Assert.IsType<WireAxisCoilAir>(WireFormAuthoring.Parse(axisSource).Value.Operations[0]);
        for (var i = 0; i <= 100; i++) Assert.True((axis.Evaluate(i / 100d) - cylinder.Evaluate(i / 100d)).Length < 1e-9);
        foreach (var fixture in new[] { "frustum-surface-coil", "sphere-surface-coil" }) { var parsed = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Regression", "WireFormSurfaceSupport", fixture + ".firmament"))); Assert.True(parsed.IsSuccess); var coil = Assert.IsType<WireSurfaceCoilAir>(parsed.Value.Operations[0]); Assert.Equal(1d, coil.MeasuredSupportClearanceMm); Assert.True(coil.MinimumSelfClearanceMm > 0d); Assert.True(coil.ApproximationError.MaxMm <= coil.ApproximationToleranceMm); }
        var blender = Assert.IsType<WireSurfaceCoilAir>(WireFormAuthoring.Parse(File.ReadAllText(Fixture("Regression", "WireFormSurfaceSupport", "blender-ball-coil.firmament"))).Value.Operations[0]);
        Assert.Equal("Sphere", blender.SupportKind); Assert.Equal("CaptiveBallProxy", blender.SupportName); Assert.Equal("LatitudeProgression", blender.ProgressionLaw);
    }

    [Theory]
    [InlineData("coil-radius-zero.firmament", "wireform-coil-radius-invalid")]
    [InlineData("coil-turns-zero.firmament", "wireform-coil-turns-invalid")]
    [InlineData("coil-parameters-inconsistent.firmament", "wireform-coil-parameters-inconsistent")]
    [InlineData("surfacecoil-offset-invalid.firmament", "wireform-surfacecoil-offset-invalid")]
    [InlineData("surfacecoil-pole-singularity.firmament", "wireform-surfacecoil-pole-singularity")]
    [InlineData("surfacecoil-support-unsupported.firmament", "wireform-surfacecoil-support-unsupported")]
    public void InvalidCoils_FailWithTypedActionableDiagnostics(string fixture, string code)
    {
        var result = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Invalid", "WireForm", fixture))); Assert.False(result.IsSuccess); Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Message.Contains(code, StringComparison.Ordinal));
    }

    [Fact]
    public void OverlappingTurnsFailValidation()
    {
        var parsed = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Invalid", "WireForm", "coil-turn-overlap.firmament"))); Assert.True(parsed.IsSuccess); Assert.Contains(WireFormBRepMaterializer.Validate(parsed.Value), diagnostic => diagnostic.Contains("wireform-coil-turn-clearance", StringComparison.Ordinal));
    }
    [Fact]
    public void Paperclip_ReplaysExactSemanticProgramAndStockAccounting()
    {
        var result = FirmamentBuildAndExport.CompileSource(PaperclipTemplateLibrary.Source);
        Assert.True(result.IsSuccess, Messages(result));
        var report = Assert.IsType<FirmamentWireFormReport>(result.Value.WireForm);
        Assert.Equal(["Straight", "Bend", "Straight", "Bend", "Straight", "Bend", "Straight"], report.Operations.Select(x => x.Kind));
        Assert.Equal([14d, 14d, 15d, 15d], report.Operations.Where(x => x.Kind == "Straight").Select(x => x.LengthMm));
        Assert.Equal([3d, 4d, 5d], report.Operations.Where(x => x.Kind == "Bend").Select(x => x.RadiusMm!.Value));
        Assert.Equal(58d, report.TotalStraightLengthMm, 12);
        Assert.Equal(12d * Math.PI, report.TotalBendLengthMm, 12);
        Assert.Equal(95.6991118431d, report.TotalWireLengthMm, 9);
        Assert.Equal(Math.PI * .25d * report.TotalWireLengthMm, report.VolumeMm3, 10);
        Assert.True(report.MassKilograms > 0d);
        Assert.Equal([4, 3, 2], [report.Cylinders, report.Tori, report.Planes]);
        Assert.Equal(11d, report.Bounds[3] - report.Bounds[0], 9); Assert.Equal(25d, report.Bounds[4] - report.Bounds[1], 9); Assert.Equal(1d, report.Bounds[5] - report.Bounds[2], 9);
        Assert.Equal(0, report.OtherSurfaces); Assert.Equal(0, report.RationalProductSurfaces); Assert.Equal(0, report.FacetedFallback);
        Assert.True(report.EnclosedManifold); Assert.True(report.StepReimportSucceeded); Assert.True(report.StepReimportedManifold);
    }

    [Fact]
    public void ThreeDimensionalWitness_TransportsFrameAcrossPerpendicularBendPlanes()
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "three-dimensional-bends.firmament"));
        var authored = WireFormAuthoring.Parse(source);
        Assert.True(authored.IsSuccess, string.Join(Environment.NewLine, authored.Diagnostics.Select(x => x.Message)));
        Assert.Equal(["Up", "Right"], authored.Value.Operations.OfType<WireBendAir>().Select(x => x.Plane));
        AssertPoint(authored.Value.EndState.Position, 25, 25, 15);
        AssertDirection(authored.Value.EndState.Tangent, 0, 0, 1);
        var result = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(result.IsSuccess, Messages(result));
        var report = Assert.IsType<FirmamentWireFormReport>(result.Value.WireForm);
        Assert.Equal([3, 2, 2, 0], [report.Cylinders, report.Tori, report.Planes, report.OtherSurfaces]);
        Assert.Equal(0, report.RationalProductSurfaces);
        Assert.True(Step242Importer.ImportBody(result.Value.StepText).IsSuccess);
    }

    [Theory]
    [InlineData(90d)]
    [InlineData(180d)]
    [InlineData(-90d)]
    [InlineData(37d)]
    public void Bend_SupportsSignedBoundedGeneralAngles(double degrees)
    {
        var source = Simple.Replace("90deg", degrees.ToString(System.Globalization.CultureInfo.InvariantCulture) + "deg", StringComparison.Ordinal);
        var authored = WireFormAuthoring.Parse(source);
        Assert.True(authored.IsSuccess, string.Join(Environment.NewLine, authored.Diagnostics.Select(x => x.Message)));
        var bend = Assert.IsType<WireBendAir>(authored.Value.Operations[1]);
        Assert.Equal(5d * Math.Abs(degrees) * Math.PI / 180d, bend.LengthMm, 12);
        Assert.Equal(bend.Input, authored.Value.Operations[0].Output);
        Assert.Equal(bend.Output, authored.Value.Operations[2].Input);
    }

    [Theory]
    [InlineData("Straight Lead { Length: 20mm }", "Straight Lead { Length: 0mm }", "wireform-straight-length-invalid")]
    [InlineData("Radius: 5mm", "Radius: 0.5mm", "wireform-bend-radius-invalid")]
    [InlineData("Plane: Up", "Plane: GlobalXY", "wireform-bend-plane-invalid")]
    public void InvalidOperations_ProduceTypedDiagnostics(string oldText, string newText, string diagnostic)
    {
        var result = WireFormAuthoring.Parse(Simple.Replace(oldText, newText, StringComparison.Ordinal));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, x => x.Message.Contains(diagnostic, StringComparison.Ordinal));
    }

    [Fact]
    public void NonlocalContact_FailsClosedWhileAdjacentTangencyIsAllowed()
    {
        var source = File.ReadAllText(Fixture("Invalid", "WireForm", "nonlocal-contact.firmament"));
        var authored = WireFormAuthoring.Parse(source);
        Assert.True(authored.IsSuccess);
        var diagnostics = WireFormBRepMaterializer.Validate(authored.Value);
        Assert.Contains(diagnostics, x => x.Contains("wireform-self-intersection:A:Contact", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, x => x.Contains("A:B", StringComparison.Ordinal));
    }

    [Fact]
    public void AdjacentOperations_MayMeetOnlyAtTangentJoin()
    {
        var closedCircle = Simple.Replace("Straight Lead { Length: 20mm }", "Bend FirstHalf { Radius: 5mm; Angle: 180deg; Plane: Up }", StringComparison.Ordinal)
            .Replace("Bend Corner { Radius: 5mm; Angle: 90deg; Plane: Up }", "Bend SecondHalf { Radius: 5mm; Angle: 180deg; Plane: Up }", StringComparison.Ordinal)
            .Replace("Straight Tail { Length: 30mm }", string.Empty, StringComparison.Ordinal);
        var authored = WireFormAuthoring.Parse(closedCircle); Assert.True(authored.IsSuccess);
        Assert.Contains(WireFormBRepMaterializer.Validate(authored.Value), x => x.Contains("wireform-self-intersection:FirstHalf:SecondHalf", StringComparison.Ordinal));
    }

    [Fact]
    public void RepeatExport_IsByteDeterministic()
    {
        var source = File.ReadAllText(Fixture("Canonical", "WireForm", "u-wire.firmament"));
        var first = FirmamentBuildAndExport.CompileSource(source); var second = FirmamentBuildAndExport.CompileSource(source);
        Assert.True(first.IsSuccess, Messages(first)); Assert.True(second.IsSuccess, Messages(second));
        Assert.Equal(first.Value.StepText, second.Value.StepText);
        Assert.Equal(first.Value.WireForm!.StepSha256, second.Value.WireForm!.StepSha256);
    }

    [Fact]
    public void Paperclip_IsNumericallyCoincidentWithRecoveredConceptPathReference()
    {
        var wire = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "paperclip.firmament")));
        var reference = CircularSweepAuthoring.Parse(LegacyPaperclipReference);
        Assert.True(wire.IsSuccess); Assert.True(reference.IsSuccess);
        var deviations = new List<double>();
        for (var segment = 0; segment < wire.Value.Operations.Count; segment++)
            for (var sample = 0; sample <= 100; sample++)
            {
                var t = sample / 100d; var a = Sample(wire.Value.Operations[segment], t); var b = Sample(reference.Value.Path.Segments[segment].Geometry, t);
                deviations.Add((a - b).Length);
            }
        var ordered = deviations.Order().ToArray();
        var rms = Math.Sqrt(deviations.Sum(x => x * x) / deviations.Count); var p95 = ordered[(int)Math.Floor(.95d * (ordered.Length - 1))]; var max = ordered[^1];
        Assert.True(rms < 1e-12, $"RMS {rms:R}"); Assert.True(p95 < 1e-12, $"p95 {p95:R}"); Assert.True(max < 1e-12, $"max {max:R}");
        Assert.True((wire.Value.EndState.Position - Sample(reference.Value.Path.Segments[^1].Geometry, 1d)).Length < 1e-12);
        Assert.Equal(reference.Value.Path.Segments.Sum(x => Length(x.Geometry)), wire.Value.TotalWireLengthMm, 12);
    }

    [Fact]
    public void PaperclipLongerAndWiderVariantsRemainExactAndCollisionFree()
    {
        var canonical = File.ReadAllText(Fixture("Canonical", "WireForm", "paperclip.firmament"));
        var longer = canonical.Replace("Length: 14mm", "Length: 16.1mm", StringComparison.Ordinal).Replace("Length: 15mm", "Length: 17.25mm", StringComparison.Ordinal);
        var wider = canonical.Replace("Bend InnerTop { Radius: 3mm", "Bend InnerTop { Radius: 4mm", StringComparison.Ordinal)
            .Replace("Bend LowerReturn { Radius: 4mm", "Bend LowerReturn { Radius: 5mm", StringComparison.Ordinal)
            .Replace("Bend OuterTop { Radius: 5mm", "Bend OuterTop { Radius: 6mm", StringComparison.Ordinal);
        foreach (var source in new[] { longer, wider })
        {
            var first = FirmamentBuildAndExport.CompileSource(source); var second = FirmamentBuildAndExport.CompileSource(source);
            Assert.True(first.IsSuccess, Messages(first)); Assert.True(second.IsSuccess, Messages(second));
            var report = first.Value.WireForm!; Assert.True(report.EnclosedManifold); Assert.Equal(0, report.RationalProductSurfaces); Assert.Equal(0, report.FacetedFallback);
            Assert.Equal(first.Value.StepText, second.Value.StepText);
        }
    }

    private static Point3D Sample(WireFormOperationAir operation, double t) => operation switch
    {
        WireStraightAir line => line.Input.Position + (line.Output.Position - line.Input.Position) * t,
        WireBendAir bend => bend.Center + Rotate(bend.StartRadial.ToVector(), bend.PlaneNormal.ToVector(), Math.Abs(bend.AngleRadians) * t) * bend.RadiusMm,
        _ => throw new NotSupportedException()
    };
    private static Point3D Sample(LineArcProfileCurve2D curve, double t) => curve switch
    {
        LineArcLineSegment2D line => new(line.Start.X + (line.End.X - line.Start.X) * t, line.Start.Y + (line.End.Y - line.Start.Y) * t, 0),
        LineArcCircularArc2D arc => new(arc.Center.X + arc.Radius * Math.Cos(arc.StartAngleRadians + arc.SweepAngleRadians * t), arc.Center.Y + arc.Radius * Math.Sin(arc.StartAngleRadians + arc.SweepAngleRadians * t), 0),
        _ => throw new NotSupportedException()
    };
    private static double Length(LineArcProfileCurve2D curve) => curve switch { LineArcLineSegment2D line => Math.Sqrt(Math.Pow(line.End.X - line.Start.X, 2) + Math.Pow(line.End.Y - line.Start.Y, 2)), LineArcCircularArc2D arc => arc.Radius * Math.Abs(arc.SweepAngleRadians), _ => 0 };
    private static Vector3D Rotate(Vector3D v, Vector3D a, double angle) => v * Math.Cos(angle) + new Vector3D(a.Y * v.Z - a.Z * v.Y, a.Z * v.X - a.X * v.Z, a.X * v.Y - a.Y * v.X) * Math.Sin(angle) + a * ((a.X * v.X + a.Y * v.Y + a.Z * v.Z) * (1 - Math.Cos(angle)));

    private static string Messages(Aetheris.Kernel.Core.Results.KernelResult<FirmamentStepExportResult> result) => string.Join(Environment.NewLine, result.Diagnostics.Select(x => x.Message));
    private static string Fixture(params string[] pieces) => Path.Combine([RepoRoot(), "fixtures", .. pieces]);
    private static string RepoRoot() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Aetheris.slnx"))) directory = directory.Parent; return directory?.FullName ?? throw new DirectoryNotFoundException(); }
    private static void AssertPoint(Aetheris.Kernel.Core.Math.Point3D p, double x, double y, double z) { Assert.Equal(x, p.X, 9); Assert.Equal(y, p.Y, 9); Assert.Equal(z, p.Z, 9); }
    private static void AssertDirection(Aetheris.Kernel.Core.Math.Direction3D p, double x, double y, double z) { Assert.Equal(x, p.X, 9); Assert.Equal(y, p.Y, 9); Assert.Equal(z, p.Z, 9); }

    private const string Simple = """
        Model WireTest {
            Units: mm
            WireForm Wire {
                Diameter: 1mm
                Material: Standard.Materials.StainlessSteel.304_Annealed
                StartFrame { Origin: [0mm,0mm,0mm]; Tangent: [1,0,0]; Up: [0,0,1] }
                Straight Lead { Length: 20mm }
                Bend Corner { Radius: 5mm; Angle: 90deg; Plane: Up }
                Straight Tail { Length: 30mm }
            }
        }
        """;

    private const string LegacyPaperclipReference = """
        Model PaperclipReference {
            Units: mm
            Concept Path PaperclipPath {
                Start: Point2(8mm, 0mm) Heading: 90deg
                Line InnerRight { Length: 14mm }
                Arc InnerTop { Radius: 3mm; Turn: 180deg }
                Line InnerLeft { Length: 14mm }
                Arc LowerReturn { Radius: 4mm; Turn: 180deg }
                Line OuterRight { Length: 15mm }
                Arc OuterTop { Radius: 5mm; Turn: 180deg }
                Line OuterLeft { Length: 15mm }
            }
            Sweep Reference { Path: PaperclipPath; Diameter: 1mm; Material: Standard.Materials.StainlessSteel.304_Annealed }
        }
        """;
}
