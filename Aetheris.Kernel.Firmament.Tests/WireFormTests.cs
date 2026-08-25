using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Materializer;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class WireFormTests
{
    [Fact]
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
        var surface = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "cylinder-surface-coil.firmament"))).Value;
        var cylinder = Assert.IsType<WireSurfaceCoilAir>(surface.Operations[0]); Assert.Equal("Cylinder", cylinder.SupportKind); Assert.Equal(1d, cylinder.MeasuredSupportClearanceMm);
        var axisSource = File.ReadAllText(Fixture("Canonical", "WireForm", "axis-coil.firmament")).Replace("Radius: 12mm", "Radius: 12mm").Replace("Turns: 8", "Turns: 6").Replace("Pitch: 5mm", "Pitch: 5mm");
        var axis = Assert.IsType<WireAxisCoilAir>(WireFormAuthoring.Parse(axisSource).Value.Operations[0]);
        for (var i = 0; i <= 100; i++) Assert.True((axis.Evaluate(i / 100d) - cylinder.Evaluate(i / 100d)).Length < 1e-9);
        foreach (var fixture in new[] { "frustum-surface-coil", "sphere-surface-coil" }) { var parsed = WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", fixture + ".firmament"))); Assert.True(parsed.IsSuccess); var coil = Assert.IsType<WireSurfaceCoilAir>(parsed.Value.Operations[0]); Assert.Equal(1d, coil.MeasuredSupportClearanceMm); Assert.True(coil.MinimumSelfClearanceMm > 0d); Assert.True(coil.ApproximationError.MaxMm <= coil.ApproximationToleranceMm); }
        var blender = Assert.IsType<WireSurfaceCoilAir>(WireFormAuthoring.Parse(File.ReadAllText(Fixture("Canonical", "WireForm", "blender-ball-coil.firmament"))).Value.Operations[0]);
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
