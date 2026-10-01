using Aetheris.Kernel.Core.Math;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Semantics;

namespace Aetheris.Kernel.Firmament.Tests.Assembly;

public sealed class CoaxialDatumTests
{
    private const string Fixture = "fixtures/Canonical/AssemblyInterfaces/coaxial-knob.firmasm";
    private static string Source => File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/hardware-definitions.firmament"))
        + File.ReadAllText(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/knob.firmament"))
        + string.Join('\n', File.ReadAllLines(FirmamentCorpusHarness.ResolveFixtureFullPath(Fixture)).Where(l => !l.StartsWith("Include")));

    [Fact]
    public void CompleteKnobRotatesAsOneRigidOccurrenceWithNoGeometryRebuild()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath(Fixture));
        Assert.True(compiled.IsSuccess, Evidence(compiled.Diagnostics));
        var ir = compiled.Ir!;
        Assert.Equal(5, ir.Instances.Count); // Concept target is not a product occurrence.
        Assert.Equal(3, compiled.Geometry!.Artifact.Definitions.Count);
        var seat = Assert.Single(ir.AxisSeats!);
        Assert.True(seat.Passed);
        Assert.Equal(1, seat.DegreesOfFreedom);
        Assert.Equal(7, seat.Seating.StationMm);
        Assert.Equal(30, seat.Seating.ClockingDegrees);
        Assert.Equal(3, Assert.Single(ir.AssemblyDefinitions!).LocalAxisSeats!.Count);
        Assert.All(ir.AssemblyDefinitions![0].LocalAxisSeats!, e => { Assert.True(e.Passed); Assert.Equal(0,e.DegreesOfFreedom); });
        var joint = Assert.Single(ir.Joints!);
        Assert.Equal("VolumeRotation", joint.Name);
        Assert.Equal(MechanicalInterfaceFamily.Revolute, joint.Family);
        var zero = AssemblyKinematics.Evaluate(ir, new Dictionary<string,double>());
        var pose = AssemblyKinematics.Evaluate(ir, new Dictionary<string,double> { ["VolumeRotation"] = 90 });
        Assert.True(pose.IsSuccess, Evidence(pose.Diagnostics));
        var zeroKnob = Instance(zero.Instances,"Controls.VolumeKnob");
        var posedKnob = Instance(pose.Instances,"Controls.VolumeKnob");
        Assert.Equal(10,zeroKnob.ResolvedTransform!.Matrix[12],6); // Station follows X, not world Z.
        Assert.Equal(4,zeroKnob.ResolvedTransform.Matrix[13],6);
        Assert.Equal(5,zeroKnob.ResolvedTransform.Matrix[14],6);
        Assert.NotEqual(zeroKnob.ResolvedTransform.Matrix[0..12],posedKnob.ResolvedTransform!.Matrix[0..12]);
        foreach (var original in ir.Instances.Where(i => i.Kind == AssemblyInstanceKind.Part))
        {
            var posed = pose.Instances.Single(i => i.StableId == original.StableId);
            Assert.Same(original.SemanticRoot,posed.SemanticRoot);
            Assert.Equal(original.DefinitionIdentity,posed.DefinitionIdentity);
            var before = Matrix(original) * Matrix(zeroKnob).Inverse();
            var after = Matrix(posed) * Matrix(posedKnob).Inverse();
            AssertMatrix(before,after);
            AssertMatrix(Matrix(original),Matrix(zero.Instances.Single(i => i.StableId == original.StableId)));
        }
    }

    [Fact]
    public void DatumOnlyEditReusesAllExactBodiesAndSignedStationIsDeterministic()
    {
        using var session = new FirmamentCompilationSession();
        var first = session.Compile(Source);
        Assert.True(first.IsSuccess,Evidence(first.Diagnostics));
        var shifted = session.Compile(Source.Replace("Origin: [3mm,4mm,5mm]","Origin: [13mm,4mm,5mm]").Replace("At: 7mm","At: -7mm"));
        Assert.True(shifted.IsSuccess,Evidence(shifted.Diagnostics));
        Assert.Equal(3,shifted.Reuse!.ReusedDefinitions);
        Assert.Equal(6,Instance(shifted.Ir!.Instances,"Controls.VolumeKnob").ResolvedTransform!.Matrix[12],6);
        Assert.Equal(first.Geometry!.Artifact.Definitions.Select(d=>d.StepSha256),shifted.Geometry!.Artifact.Definitions.Select(d=>d.StepSha256));
    }

    [Fact]
    public void CompositePublicAxisAndFrameFollowSolvedLocalChildPlacement()
    {
        var source = Source.Replace("At: 0mm; Clocking: 0deg;","At: 2mm; Clocking: 0deg;");
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess,Evidence(result.Diagnostics));
        var knob = Instance(result.Ir!.Instances,"Controls.VolumeKnob");
        Assert.Equal(8,knob.ResolvedTransform!.Matrix[12],6); // Public seat is 2mm from definition origin.
        var pub = knob.SemanticRoot.ExposedMembers["Spindle"];
        Assert.True(pub.ExposedMembers["Frame"].TryBinding<ExactDatumFrameBinding>(out var frame));
        Assert.Equal(2,frame.OriginZ,6);
        Assert.True(pub.ExposedMembers["Axis"].TryBinding<ExactAxisBinding>(out var axis));
        Assert.Equal(2,axis.OriginZ,6);
        Assert.True(Assert.Single(result.Ir.AxisSeats!).Passed);
    }

    [Fact]
    public void OpposedDirectionReversesMemberAxisWithoutReversingStation()
    {
        var source = Source.Replace("At: 7mm; Clocking: 30deg;","At: 7mm; Clocking: 30deg; Orientation: OpposedDirection;");
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess,Evidence(result.Diagnostics));
        var knob = Instance(result.Ir!.Instances,"Controls.VolumeKnob");
        Assert.Equal(10,knob.ResolvedTransform!.Matrix[12],6);
        Assert.Equal(-1,knob.ResolvedTransform.Matrix[8],6);
        AssertMatrix(Matrix(knob),Matrix(Instance(AssemblyKinematics.Evaluate(result.Ir,new Dictionary<string,double>()).Instances,"Controls.VolumeKnob")));
        Assert.True(Assert.Single(result.Ir.AxisSeats!).Passed);
    }

    [Fact]
    public void ClockingDefaultsToZeroAndReferenceProjectsPerpendicularToDirection()
    {
        var source = Source.Replace("Clocking: 30deg;", "").Replace("Reference: [0,3,1]", "Reference: [9,3,1]");
        var result = new AssemblyM0Compiler().Compile(new AssemblyM0Parser().Parse(source).Source!);
        Assert.True(result.IsSuccess,Evidence(result.Diagnostics));
        var m=Instance(result.Ir!.Instances,"Controls.VolumeKnob").ResolvedTransform!.Matrix;
        Assert.Equal(0,m[0],6);
        Assert.Equal(3/Math.Sqrt(10),m[1],6);
        Assert.Equal(1/Math.Sqrt(10),m[2],6);
        Assert.Equal(0,Assert.Single(result.Ir.AxisSeats!).Seating.ClockingDegrees);
    }

    [Theory]
    [InlineData("Direction: [2,0,0]","Direction: [0,0,0]","assembly-axis-invalid-basis")]
    [InlineData("Reference: [0,3,1]","Reference: [2,0,0]","assembly-axis-invalid-basis")]
    [InlineData("At: 7mm;","","assembly-axis-station-missing")]
    [InlineData("At: 7mm","At: 7deg","assembly-datum-measure-invalid")]
    [InlineData("Clocking: 30deg","Clocking: 30mm","assembly-datum-measure-invalid")]
    [InlineData("At: 7mm","At: 7mm; At: 8mm","assembly-datum-measure-invalid")]
    [InlineData("Mate GripOnSpindle:","Mate SkirtOnSpindle:","assembly-datum-mate-duplicate")]
    [InlineData("Clocking: 30deg","Clocking: 30deg; Support: true","assembly-datum-fields-invalid")]
    [InlineData("Reference: [0,3,1]","Reference: [0,NaN,1]","assembly-datum-vector-invalid")]
    [InlineData("Controls.VolumeKnob.Spindle]","Controls.VolumeKnob.Mount]","assembly-datum-member-outside-contract")]
    [InlineData("Member: Controls.VolumeKnob.Spindle","Member: Controls.VolumeKnob.KnobSkirt.SpindleSeat","assembly-datum-member-scope")]
    [InlineData("[Controls.VolumeKnob.Spindle]","[Controls.VolumeKnob.Spindle,Controls.Other.Spindle]","assembly-axis-revolute-member-count")]
    [InlineData("-> [0,0,1]","-> [1,0,0]","assembly-axis-port-invalid")]
    [InlineData("Axis Axis = [0mm,0mm,0mm]","Axis Axis = [1mm,0mm,0mm]","assembly-axis-port-invalid")]
    [InlineData("<Assembly VolumeKnob = ControlKnob></Assembly>","<Assembly VolumeKnob = ControlKnob> Placement { From: Origin; To: World; } </Assembly>","assembly-placement-authority-conflict")]
    [InlineData("Interface<Fixed> CoaxialStack","Interface<Revolute> CoaxialStack","assembly-axis-internal-motion-unsupported")]
    public void InvalidAxisContractsFailClosed(string from,string to,string code)
    {
        var result = new AssemblyM0Parser().Parse(Source.Replace(from,to));
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics,d=>d.Code==code);
    }

    [Fact]
    public void GuitarHasFourInstancesOfOneCoaxialKnobDefinition()
    {
        var compiled = new AssemblyM1Pipeline().CompileFile(FirmamentCorpusHarness.ResolveFixtureFullPath("fixtures/Canonical/AssemblyInterfaces/GuitarX0/guitar.firmasm"));
        Assert.True(compiled.IsSuccess,Evidence(compiled.Diagnostics));
        var ir=compiled.Ir!;
        var knobs=ir.Instances.Where(i=>i.DefinitionIdentity=="ControlKnob").ToArray();
        Assert.Equal(4,knobs.Length);
        var definition=Assert.Single(ir.AssemblyDefinitions!,d=>d.DefinitionIdentity=="ControlKnob");
        Assert.Equal(new double[]{0,3,12},definition.LocalAxisSeats!.Select(e=>e.Seating.StationMm).Order());
        Assert.All(definition.LocalAxisSeats!,e=>Assert.True(e.Passed));
        Assert.Equal(91,compiled.Geometry!.Artifact.Instances.Count);
        Assert.Equal(53,compiled.Geometry.Artifact.Definitions.Count);
    }

    private static AssemblyInstanceIr Instance(IReadOnlyList<AssemblyInstanceIr> instances,string path)=>instances.Single(i=>i.Path.ToString()==path);
    private static Transform3D Matrix(AssemblyInstanceIr instance)=>Transform3D.FromRowMajor(instance.ResolvedTransform!.Matrix);
    private static string Evidence(IReadOnlyList<AssemblyDiagnostic> diagnostics)=>string.Join('\n',diagnostics.Select(d=>d.Code+": "+d.Message));
    private static void AssertMatrix(Transform3D expected,Transform3D actual)
    {
        var a=expected.ToRowMajor(); var b=actual.ToRowMajor();
        for(var i=0;i<16;i++) Assert.Equal(a[i],b[i],6);
    }
}
