using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class AssemblyMotionTests
{
    private static string Fixture => FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/articulated-motion.firmament");

    [Fact]
    public void TypedClipSamplesUnwrappedAnglesNestedJointsAndPeriodicLiftWithoutGeometryRebuild()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(Fixture);
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        var clip = Assert.Single(ir.Motions);
        Assert.Equal(2, clip.DurationSeconds);
        Assert.Equal(3, clip.Tracks.Count);
        Assert.All(clip.Tracks, t => Assert.Contains(ir.Joints!, j => j.MateStableId == t.MateStableId));
        // Deliberately random/non-key and reverse time queries.
        foreach (var time in new[] { 1.25, .231, 1.749, 0, 2, -0.75, 3.25, 1.25 })
        {
            var pose = AssemblyMotion.Evaluate(ir, "Cycle", time);
            Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
            var local = (time % 2 + 2) % 2;
            var angle = local * 360;
            Assert.Equal(angle, pose.State["Shoulder"], 10);
            Assert.Equal(angle / 2, pose.State["Articulated.First.Elbow"], 10);
            var lift = Aetheris.Kernel.Core.Mechanisms.PrescribedMotion.ValveLift(angle, 360, 180, 9);
            Assert.Equal(lift, pose.State["Slide"], 10);
            Assert.Equal(lift, pose.Instances.Single(i => i.Path.ToString() == "Articulated.Slider").ResolvedTransform!.Matrix[14], 10);
            Assert.Same(ir.Instances[0].SemanticRoot, pose.Instances[0].SemanticRoot);
        }
        var end = AssemblyMotion.Evaluate(ir, "Cycle", 2, preserveEndpoint: true);
        Assert.Equal(720, end.State["Shoulder"]);
        Assert.Equal(360, end.State["Articulated.First.Elbow"]);
        Assert.False(AssemblyMotion.Evaluate(ir, "Missing", 0).IsSuccess);
        Assert.False(AssemblyMotion.Evaluate(ir, "Cycle", double.NaN).IsSuccess);
    }

    [Theory]
    [InlineData("duration: playback.duration", "duration: 2mm", "assembly-motion-duration-invalid")]
    [InlineData("target: Articulated.First.Bend.angle", "target: Articulated.First.Elbow.angle", "assembly-motion-target-invalid")]
    [InlineData("target: Articulated.Slide.translation", "target: Articulated.Follow.angle", "assembly-motion-target-invalid")]
    [InlineData("target: Articulated.Slide.translation", "target: Articulated.Shoulder.angle", "assembly-motion-multiple-writers")]
    [InlineData("input: Articulated.Shoulder.angle", "input: Articulated.Unknown.angle", "assembly-motion-input-type-mismatch")]
    [InlineData("input: Articulated.Shoulder.angle", "input: Articulated.First.Bend.angle", "assembly-motion-driver-cycle")]
    [InlineData("at: 450deg", "at: 360deg", "assembly-motion-key-order-invalid")]
    [InlineData("value: 9mm", "value: 9deg", "assembly-motion-key-type-mismatch")]
    [InlineData("interpolation: Linear", "interpolation: Magic", "assembly-motion-interpolation-invalid")]
    [InlineData("value: 720deg", "value: 719deg", "assembly-motion-loop-discontinuous")]
    [InlineData("repeat: Loop", "repeat: Loop; speeed: 12", "assembly-motion-field-invalid")]
    public void InvalidIntentIsRejectedWithSourceEvidence(string from, string to, string code)
    {
        var text = File.ReadAllText(Fixture).Replace(from, to);
        var parsed = new AssemblyM0Parser().Parse(text, Fixture);
        var result = parsed.IsSuccess ? new AssemblyM0Compiler().Compile(parsed.Source!) : new(null, parsed.Diagnostics);
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == code && d.SourceSpan is not null);
    }

    [Fact]
    public void ReusableTimeCurveUsesClipTimeWithoutAnArtificialJointInput()
    {
        var source = File.ReadAllText(Fixture).Replace("Keyframes<Angle,Angle> HalfSpeed", "Keyframes<Time,Angle> HalfSpeed")
            .Replace("at: 0deg; value: 0deg", "at: 0s; value: 0deg")
            .Replace("at: 720deg; value: 360deg", "at: 2s; value: 360deg")
            .Replace("values: HalfSpeed; input: Articulated.Shoulder.angle", "values: HalfSpeed");
        var compiled = new AssemblyM1Pipeline().Compile(source, Fixture);
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var pose = AssemblyMotion.Evaluate(compiled.Ir!, "Cycle", 1.25);
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        Assert.Equal(225, pose.State["Articulated.First.Elbow"], 10);
    }

    private static string Evidence(IReadOnlyList<AssemblyDiagnostic> diagnostics) => string.Join("\n", diagnostics.Select(d => d.Code + ": " + d.Message));
}
