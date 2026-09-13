using System.Text.Json;
using Aetheris.EditableV8;
using Aetheris.Kernel.Firmament.Assembly;

namespace Aetheris.EditableV8.Tests;

public sealed class EngineTests
{
    [Fact]
    public void RevisionRebuildsGeometryAndMotionWithoutChangingOccurrenceIdentity()
    {
        var baseline = new EngineAuthor(new()); var revised = new EngineAuthor(new(Stroke: 94));
        var source = baseline.Engine(8); var revision = revised.Engine(8);
        Assert.NotEqual(source, revision);
        var a = baseline.Motion(); var b = revised.Motion();
        Assert.Equal(a.Bindings.Select(x => x.Name), b.Bindings.Select(x => x.Name));
        Assert.Equal(3, b.Spec.DeckHeight - a.Spec.DeckHeight);
        Assert.Equal(3, b.Spec.CrankRadius - a.Spec.CrankRadius);
        Assert.NotEqual(a.Spec.CrankcaseFloor, b.Spec.CrankcaseFloor);
        var repeat = new EngineAuthor(new()); Assert.Equal(source, repeat.Engine(8));
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

    [Fact]
    public void PhysicalEngineUsesInterfacePlacementAndDimensionalFits()
    {
        var engine = new EngineAuthor(new()); var source = engine.Engine(8);
        var result = new AssemblyM1Pipeline().Compile(source, "editable-v8-test.firmament");
        Assert.True(result.IsSuccess, string.Join("\n", result.Diagnostics.Select(d => d.Message)));
        Assert.DoesNotContain("LegacyExplicit", source);
        Assert.Equal(engine.Bindings.Count - 1, result.Ir!.Mates.Count);
        Assert.Equal(32, result.Ir.FitResults.Count);
        Assert.All(result.Geometry!.Artifact.MateResiduals, residual => Assert.True(residual.Passed));
        var export = AssemblyDisplayMeshExporter.Export(result);
        Assert.Equal(engine.Bindings.Count, export.Occurrences.Count(o => o.DefinitionId != null));
        Assert.Equal(export.Occurrences.Count, export.Occurrences.Select(o => o.Id).Distinct().Count());
        Assert.True(export.Definitions.Count < engine.Bindings.Count / 4);
        var solved = EngineMotion.BindAssembly(engine.Motion(), result);
        EngineMotion.ValidateSpringAlignment(solved);
        foreach (var binding in solved.Bindings.Where(b => b.SpringAxis != null))
        {
            var atZero = EngineMotion.Evaluate(solved, binding, 0);
            var atCycle = EngineMotion.Evaluate(solved, binding, 720);
            Assert.All(atZero.Zip(atCycle), p => Assert.InRange(double.Abs(p.First - p.Second), 0, 1e-10));
        }
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
}
