using System.Text.Json;
using Aetheris.EditableV8;
using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Geometry;
using Aetheris.Kernel.Core.Geometry.Surfaces;
using Aetheris.Kernel.Core.Topology;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.EditableV8.Tests;

public sealed class EngineTests
{
    [Fact]
    public void RevisionRebuildsGeometryAndMotionWithoutChangingOccurrenceIdentity()
    {
        var baseline = new EngineAuthor(new()); var revised = new EngineAuthor(new(Stroke: 94));
        var source = baseline.Engine(8); var revision = revised.Engine(8);
        Assert.NotEqual(source.Documents["design.firmament"], revision.Documents["design.firmament"]);
        var a = baseline.Motion(); var b = revised.Motion();
        Assert.Equal(a.Bindings.Select(x => x.Name), b.Bindings.Select(x => x.Name));
        Assert.Equal(3, b.Spec.DeckHeight - a.Spec.DeckHeight);
        Assert.Equal(3, b.Spec.CrankRadius - a.Spec.CrankRadius);
        Assert.NotEqual(a.Spec.CrankcaseFloor, b.Spec.CrankcaseFloor);
        var repeat = new EngineAuthor(new()); Assert.Equal(source.Documents.OrderBy(p => p.Key), repeat.Engine(8).Documents.OrderBy(p => p.Key));
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(repeat.Motion()));
        EngineMotion.Validate(a); EngineMotion.Validate(b);
        foreach (var motion in new[] { a, b }) foreach (var binding in motion.Bindings.Where(b => !b.Kind.EndsWith("Spring", StringComparison.Ordinal)))
        {
            var atZero = EngineMotion.Evaluate(motion, binding, 0);
            var atCycle = EngineMotion.Evaluate(motion, binding, 720);
            Assert.All(atZero.Zip(binding.Rest), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
            Assert.All(atZero.Zip(atCycle), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void PhysicalEngineUsesInterfacePlacementAndDimensionalFits()
    {
        var engine = new EngineAuthor(new()); var source = engine.Engine(8);
        var result = new AssemblyM1Pipeline().CompileProject(source.Snapshot);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(21, result.Ir!.Instances.Count(i => i.Kind == AssemblyInstanceKind.Assembly && i.ParentStableId is not null));
        Assert.Contains(result.Ir.Instances, i => i.Path.ToString() == "EditableV8.Crankshaft.Crankpin1");
        Assert.Contains(result.Ir.Instances, i => i.Path.ToString() == "EditableV8.Cylinders.C1.Cylinder.Piston");
        Assert.Equal(32, EngineMotion.Fits(result.Ir).Count);
        Assert.All(EngineMotion.Fits(result.Ir), fit => Assert.True(fit.Compatible));
        Assert.All(result.Geometry!.Artifact.MateResiduals, residual => Assert.True(residual.Passed));
        var localPlacements = result.Ir.AssemblyDefinitions!.SelectMany(d => d.LocalPlacements).ToArray();
        Assert.NotEmpty(localPlacements);
        Assert.All(localPlacements, p => Assert.True(p.Status is PlacementStatus.Anchored or PlacementStatus.Resolved));
        var export = AssemblyDisplayMeshExporter.Export(result);
        Assert.Equal(engine.Bindings.Count, export.Occurrences.Count(o => o.DefinitionId != null));
        Assert.Equal(export.Occurrences.Count, export.Occurrences.Select(o => o.Id).Distinct().Count());
        Assert.Equal(21, export.Definitions.Count);
        var solved = EngineMotion.BindAssembly(engine.Motion(), result);
        AssertValveRetainerFit(result);
        EngineMotion.ValidateSpringAlignment(solved);
        Assert.Equal(55, solved.Bindings.Single(b => b.Name == "MainJournal1").Rest[14] - engine.Bindings.Single(b => b.Name == "MainJournal1").Rest[14], 8);
        Assert.All(solved.Bindings, b => Assert.Equal("assembly-instance:" + b.SourcePath, b.OccurrenceId));
        foreach (var binding in solved.Bindings.Where(b => b.SpringAxis != null))
        {
            var atZero = EngineMotion.Evaluate(solved, binding, 0);
            var atCycle = EngineMotion.Evaluate(solved, binding, 720);
            Assert.All(atZero.Zip(atCycle), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
        }
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void ValveStackStageCompilesBothSpringOccurrences()
    {
        var stage = new EngineAuthor(new()).StageA(withValves: true);
        var result = new AssemblyM1Pipeline().CompileProject(stage.Snapshot);
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));

        var springs = result.Ir!.Instances.Where(instance =>
            instance.Path.Segments[^1] is "IntakeSpring1" or "ExhaustSpring1").ToArray();
        Assert.Equal(2, springs.Length);
        var definition = Assert.Single(springs.Select(instance => instance.DefinitionIdentity).Distinct());
        Assert.Contains(definition, result.Geometry!.DefinitionBodies.Keys);
    }

    [Fact]
    public void InvalidDesignFailsBeforeArtifactGeneration()
    {
        Assert.Throws<ArgumentException>(() => new V8Spec(Stroke: double.NaN).Validate());
        Assert.Throws<ArgumentException>(() => new V8Spec(BankAngle: 60).Validate());
        var unsafeDesign = new EngineAuthor(new(Bore: 80, Stroke: 100, RodLength: 135));
        unsafeDesign.Engine(8);
        var failure = Assert.Throws<InvalidOperationException>(() => EngineMotion.Validate(unsafeDesign.Motion()));
        Assert.Contains("engine-sweep-failed", failure.Message);
    }

    private static void AssertValveRetainerFit(AssemblyM1CompilationResult result)
    {
        var parts = result.Ir!.Instances.Where(instance => instance.Kind == AssemblyInstanceKind.Part).ToArray();
        var retainers = parts.Where(instance => instance.Path.Segments[^1] is "IntakeRetainer" or "ExhaustRetainer").ToArray();
        var stems = parts.Where(instance => instance.Path.Segments[^1] is "IntakeStem" or "ExhaustStem").ToArray();
        Assert.Equal(16, retainers.Length);
        Assert.Equal(retainers.Length, stems.Length);

        var definitions = result.Geometry!.DefinitionBodies;
        var retainer = definitions[Assert.Single(retainers.Select(instance => instance.DefinitionIdentity).Distinct())];
        var stem = definitions[Assert.Single(stems.Select(instance => instance.DefinitionIdentity).Distinct())];

        var stemWall = Assert.Single(stem.Topology.Faces.Select(face => Surface(stem, face.Id).Cylinder)
            .OfType<CylinderSurface>());
        var walls = retainer.Topology.Faces
            .Where(face => Surface(retainer, face.Id).Cylinder.HasValue)
            .ToArray();
        Assert.Equal(2, walls.Length);
        var bore = Assert.Single(walls, face => !retainer.Bindings.GetFaceBinding(face.Id).Orientation.IsAlignedWithSurface);
        var outside = Assert.Single(walls, face => retainer.Bindings.GetFaceBinding(face.Id).Orientation.IsAlignedWithSurface);
        var boreSurface = Surface(retainer, bore.Id).Cylinder!.Value;
        LengthNear(4, stemWall.Radius);
        LengthNear(12, Surface(retainer, outside.Id).Cylinder!.Value.Radius);
        LengthNear(.2, boreSurface.Radius - stemWall.Radius);
        Assert.InRange(double.Hypot(boreSurface.Origin.X, boreSurface.Origin.Y), 0, 1e-8);
        Assert.InRange(double.Hypot(boreSurface.Axis.X, boreSurface.Axis.Y), 0, 1e-8);

        var capStations = retainer.Topology.Faces
            .Where(face => Surface(retainer, face.Id).Plane.HasValue)
            .Select(face =>
            {
                Assert.Equal(2, face.LoopIds.Count);
                return Surface(retainer, face.Id).Plane!.Value.Origin.Z;
            }).Order().ToArray();
        Assert.Collection(capStations, z => LengthNear(-1.5, z), z => LengthNear(1.5, z));

        static SurfaceGeometry Surface(BrepBody body, FaceId face) =>
            body.Geometry.GetSurface(body.Bindings.GetFaceBinding(face).SurfaceGeometryId);

        static void LengthNear(double expected, double measured)
        {
            const double toleranceMm = 1e-8;
            Assert.InRange(measured, expected - toleranceMm, expected + toleranceMm);
        }
    }
}
