using Aetheris.Kernel.Core.Mechanisms;
using Aetheris.Kernel.Firmament.Assembly;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Semantics;

namespace Aetheris.DifferenceEngine;

public static class StorageQualification
{
    public static object Run(StorageDesign design, AssemblyM1CompilationResult assembly, string repository)
    {
        var plate = assembly.Geometry!.DefinitionBodies.Single(p => p.Key.StartsWith("DecimalIndexPlate<", StringComparison.Ordinal)).Value;
        var holeCenters = plate.Geometry.Surfaces.Where(p => p.Value.Cylinder is { } c && double.Abs(2*c.Radius-design.IndexHoleDiameter)<1e-8)
            .Select(p => p.Value.Cylinder!.Value.Origin)
            .DistinctBy(p => (double.Round(p.X,7),double.Round(p.Y,7))).ToArray();
        Check(holeCenters.Length == design.Positions, "index-hole-count");
        var definition = new RotaryIndexDefinition(holeCenters.Length, design.IndexTop-design.PinTipReady+.5, design.PinLift);
        var bindings = new List<object>();
        var joints = new List<IndexedRotaryStorage>();
        foreach (var module in assembly.Ir!.Instances.Where(i => i.DefinitionIdentity == "DecimalDigit"))
        {
            Check(module.SemanticRoot.ExposedMembers["Drive"].TryBinding<TypedSemanticAuthorityBinding<GearAir>>(out var gear), "gear-authority-binding");
            var leafPath = module.Path + "." + string.Join('.',gear!.RelativeOccurrencePath!);
            var leaf = assembly.Ir.Instances.Single(i=>i.Path.ToString()==leafPath);
            Check(gear.Authority.Teeth == design.GearTeeth, "gear-authority-teeth");
            bindings.Add(new { module = module.StableId, publicPort = module.Path+".Drive", storageOccurrence = leaf.StableId,
                definition, rest = leaf.ResolvedTransform!.Matrix });
            joints.Add(new(leaf.StableId,definition));
        }
        var source = joints[0]; var destination = joints[1]; var sourceBefore = source.Capture();
        var cases = new List<object>();
        for (var i=0;i<definition.Positions;i++)
        {
            var angle=i*definition.IndexAngleRadians;
            var alignment=holeCenters.Min(p=>double.Sqrt(double.Pow(p.X*double.Cos(angle)-p.Y*double.Sin(angle)-design.IndexRadius,2)
                +double.Pow(p.X*double.Sin(angle)+p.Y*double.Cos(angle),2)));
            var radialClearance=(design.IndexHoleDiameter-design.PinDiameter)/2-alignment;
            Check(radialClearance>.19,"pin-hole-running-clearance");
            destination.SetPinLift(design.PinLift);
            var events=destination.Advance(definition.IndexAngleRadians);
            Check(events.Count==1,"exact-index-crossing");
            destination.SetPinLift(0);
            Check(destination.ReadIndex==(i+1)%definition.Positions,"index-readout");
            Check(sourceBefore==source.Capture(),"independent-storage");
            cases.Add(new { setupIndex=i, alignmentErrorMm=alignment, radialClearanceMm=radialClearance, snapshot=destination.Capture(), events });
        }
        var pinClearance=design.PinTipReady+design.PinLift-design.IndexTop;
        Check(pinClearance>=.5,"withdrawn-pin-clearance");

        // A concrete rejected transfer candidate: using only an input clutch does
        // not release a permanent output mesh on reader return. Consume the real
        // compiled Gear law; do not invent a favorable browser disconnection.
        var pair=new AssemblyM0Pipeline().CompileFile(Path.Combine(repository,"fixtures/Canonical/AssemblyInterfaces/gear-with-ordinary-part.firmament"));
        Check(pair.IsSuccess,"reset-witness-compilation");
        var mesh=pair.Ir!.Mates.Single(m=>m.GearResult is not null).GearResult!;
        var ratio=mesh.RotationSign*mesh.Ratio!.Value;
        var forwardInput=-definition.IndexAngleRadians;
        var destinationAdvance=PrescribedMotion.AngularRatio(forwardInput,ratio);
        var resetBackdrive=PrescribedMotion.AngularRatio(-forwardInput,ratio);
        Check(destinationAdvance>0 && resetBackdrive<0 && double.Abs(destinationAdvance+resetBackdrive)<1e-12,"reset-counterexample");
        return new {
            verdict="Meaningful progression", gate="A: storage geometry and ideal indexing primitive",
            arithmeticVerified=false, completeDifferenceEngine=false, physicalPrototypeTested=false,
            status="Gate B not admitted: output release/freewheel and source-triggered clutch trip remain unqualified",
            bindings, selectedGeometry=new { indexPositions=holeCenters.Length, pinClearanceMm=pinClearance,
                sleeveDiameterClearanceMm=design.SleeveInsideDiameter-design.ShaftDiameter,
                source= "Actual cylinder locations in the compiled indexing plate; analytic rotation at each of ten indices", toleranceMm=1e-7 }, cases,
            rejectedTransferCandidate=new { meshA=mesh.A,meshB=mesh.B,ratio,forwardInputRadians=forwardInput,
                destinationAdvanceRadians=destinationAdvance,resetBackdriveRadians=resetBackdrive,
                diagnostic="permanent-output-mesh-backdrives-storage-on-reader-return",
                required="CAD-bound output disengagement or one-way coupling, separately from the drive-input clutch; source-stop trip, zero case and return must qualify before an adder can run" }
        };
    }

    private static void Check(bool passed,string code)
    {
        if(!passed) throw new InvalidOperationException("storage-gate-"+code);
    }
}
