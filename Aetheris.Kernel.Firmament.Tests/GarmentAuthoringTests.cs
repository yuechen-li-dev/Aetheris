using System.Collections.Immutable;
using System.Numerics;
using Aetheris.Cloth3D;
using Aetheris.Kernel.Firmament.FirmamentV2;
using Aetheris.Kernel.Firmament.Garment;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class GarmentAuthoringTests
{
    [Theory]
    [InlineData("tunic", 2, 4)]
    [InlineData("shorts", 4, 8)]
    [InlineData("skirt", 2, 2)]
    [InlineData("flared-skirt", 2, 2)]
    public void RealGarmentsCompileThroughSharedRecipesAndRetainRestPatterns(string name, int panels, int seams)
    {
        var first = Compile(name);
        var second = Compile(name);
        Assert.Equal(panels, first.Panels.Length);
        Assert.Equal(seams, first.Source.Stitches.Length);
        Assert.NotEmpty(first.Source.Features);
        Assert.Equal("Profile", first.Source.Features[0].ReturnType);
        Assert.Equal(first.Cloth.ContentKey, second.Cloth.ContentKey);
        Assert.Equal(first.Cloth.Definition.Indices.ToArray(), second.Cloth.Definition.Indices.ToArray());
        Assert.All(first.Cloth.Definition.Positions, point => Assert.Equal(0, point.Z));
        Assert.NotEqual(first.Cloth.Definition.Positions, first.Cloth.Definition.InitialPositions);
        Assert.All(first.Cloth.Definition.Stitches, stitch =>
        {
            Assert.InRange(MathF.Abs(stitch.Weights.Sum()), 0, 1e-6f);
            Assert.InRange(stitch.Vertices.Length, 2, 4);
        });
        Assert.All(first.Cloth.ConstraintColors, color =>
        {
            var vertices = color.SelectMany(index => first.Cloth.Constraints[index].Vertices).ToArray();
            Assert.Equal(vertices.Length, vertices.Distinct().Count());
        });
    }

    [Fact]
    public void RemeshingKeepsSemanticEdgesAndUsesBarycentricStitches()
    {
        var coarse = Compile("shorts");
        var fine = GarmentCompiler.Compile(Source("shorts").Replace("meshSize: 60mm", "meshSize: 35mm"));
        Assert.True(fine.IsSuccess, string.Join("; ", fine.Diagnostics));
        Assert.True(fine.Garment!.Cloth.Definition.Positions.Length > coarse.Cloth.Definition.Positions.Length);
        Assert.Equal(coarse.Source.Stitches.ToArray(), fine.Garment.Source.Stitches.ToArray());
        Assert.All(coarse.Panels, panel => Assert.Equal(panel.Edges.Keys.Order(), fine.Garment.Panels.Single(p => p.Identity == panel.Identity).Edges.Keys.Order()));
        Assert.Contains(coarse.Cloth.Definition.Stitches, stitch => stitch.Vertices.Length > 2);
    }

    [Theory]
    [InlineData("schema Garment", "schema Mechanical", "firmament-schema-mismatch")]
    [InlineData("schema Garment", "", "garment-schema-required")]
    [InlineData("profile: skirtCut", "profile: missing", "garment-profile-invalid")]
    [InlineData("panels.front.edges.Left", "panels.front.edges.missing", "garment-stitch-edge-unknown")]
    [InlineData("orientation: Reversed", "orientation: Twisted", "garment-stitch-orientation")]
    [InlineData("grain: [0, 1]", "grain: [0, 2]", "garment-grain-invalid")]
    [InlineData("v: [0, 0, 1]", "v: [0, 0, 2]", "garment-panel-frame-invalid")]
    [InlineData("meshSize: 65mm", "meshSize: 1mm", "garment-mesh-budget")]
    [InlineData("pose: Rest", "pose: Walk", "garment-pose-unsupported")]
    [InlineData("front => SidePlacement", "front: SidePlacement", "firmament-v2-static-set-entry")]
    public void InvalidAuthoringStopsWithAnOwnedDiagnostic(string before, string after, string code)
    {
        var compiled = GarmentCompiler.Compile(Source("skirt").Replace(before, after));
        Assert.False(compiled.IsSuccess);
        Assert.Contains(compiled.Diagnostics, diagnostic => diagnostic.Code.StartsWith(code, StringComparison.Ordinal));
    }

    [Fact]
    public void FormatterPreservesAuthoredNamesAndProducesTheSameGarment()
    {
        string source = Source("skirt");
        string historical = source.Replace("meshSize:", "MeshSize:").Replace("profile:", "Profile:")
            .Replace("origin: side.origin;", "Origin: side.origin;").Replace("orientation:", "Orientation:");
        var preferred = FirmamentSourceSpelling.Prefer(historical);
        Assert.Contains("Interface<Stitch> leftSide", preferred);
        Assert.Contains("panels.front.edges.Left", preferred);
        Assert.Contains("meshSize:", preferred);
        preferred = FirmamentLanguageAnalysisService.Format(historical, "skirt.firmament", "1").Text;
        var formatted = GarmentCompiler.Compile(preferred);
        Assert.True(formatted.IsSuccess, string.Join("; ", formatted.Diagnostics) + "\n" + preferred);
        Assert.Equal(GarmentCompiler.Compile(source).Garment!.Cloth.ContentKey, formatted.Garment!.Cloth.ContentKey);
        Assert.Empty(FirmamentLanguageAnalysisService.Analyze(source, "skirt.firmament", "1").Diagnostics);
    }

    [Fact]
    public void InitialPlacementCannotRedefineRestLengthsOrMass()
    {
        var original = Compile("skirt").Cloth;
        var moved = (original.Definition with
        {
            InitialPositions = original.Definition.InitialPositions.Select(point => point * 2 + Vector3.One).ToImmutableArray(),
        }).Compile();
        Assert.Equal(original.InverseMasses.ToArray(), moved.InverseMasses.ToArray());
        Assert.Equal(original.Constraints.Select(constraint => constraint.RestLength), moved.Constraints.Select(constraint => constraint.RestLength));
        Assert.NotEqual(original.ContentKey, moved.ContentKey);
        using var solver = new ClothSolver3D(moved);
        Assert.Equal(moved.Definition.InitialPositions.ToArray(), solver.Capture().Positions.ToArray());
    }

    [Fact]
    public void InteriorContactDetectsPenetrationThatVertexChecksMiss()
    {
        var definition = ClothDefinition3D.Grid("interior-contact", 2, 2, new(.4f, .4f), new(-.2f, 0, -.2f));
        var plan = definition.Compile();
        using var solver = new ClothSolver3D(plan);
        var contacts = new ClothContacts3D { BodySurface = new SphereSurface() };
        var initial = solver.Capture();
        Assert.All(initial.Positions, point => Assert.True(contacts.BodySurface.Query(point).SignedDistance > 0));
        float before = ClothState3D.Measure(plan, initial, contacts).MaximumSurfacePenetration;
        Assert.True(before > .09f);
        solver.Step(new() { Gravity = Vector3.Zero, Substeps = 1, Iterations = 8 }, contacts);
        float after = ClothState3D.Measure(plan, solver.Capture(), contacts).MaximumSurfacePenetration;
        Assert.True(after < before * .1f, $"Interior contact did not improve: {before} -> {after}");
    }

    [Fact]
    public void StitchSolverClosesSeparatedEdgesAndReplaysExactly()
    {
        var compiled = Compile("skirt");
        var panel = compiled.Panels[1];
        var definition = compiled.Cloth.Definition with
        {
            Pins = [],
            InitialPositions = compiled.Cloth.Definition.InitialPositions.Select((point, index) =>
                index >= panel.VertexStart ? point + new Vector3(0, .05f, 0) : point).ToImmutableArray(),
        };
        var garment = compiled with { Cloth = definition.Compile() };
        using var solver = new ClothSolver3D(garment.Cloth);
        float before = garment.MaximumSeamGap(solver.Capture());
        var options = new ClothStepOptions3D { Gravity = Vector3.Zero, Substeps = 4, Iterations = 12 };
        for (int tick = 0; tick < 20; tick++)
        {
            solver.Step(options, new());
        }
        Assert.True(garment.MaximumSeamGap(solver.Capture()) < before * .1f);
        var saved = solver.Capture();
        solver.Step(options, new());
        var expected = solver.Capture();
        solver.Restore(saved);
        solver.Step(options, new());
        Assert.Equal(expected.Positions.ToArray(), solver.Capture().Positions.ToArray());
        Assert.Equal(expected.Velocities.ToArray(), solver.Capture().Velocities.ToArray());
    }

    [Fact]
    public void RuntimeArtifactRebuildsThePlanAndRejectsTampering()
    {
        var garment = Compile("skirt");
        using var original = new ClothSolver3D(garment.Cloth);
        original.Step(new() { Gravity = Vector3.Zero }, new());
        string json = GarmentExport.Artifact(garment, original.Capture());
        var loaded = ClothGarmentArtifact3D.Parse(json);
        using var restored = loaded.CreateSolver();
        Assert.Equal(garment.Cloth.ContentKey, loaded.Cloth.ContentKey);
        Assert.Equal(original.Capture().Positions.ToArray(), restored.Capture().Positions.ToArray());
        Assert.Equal(original.Tick, restored.Tick);
        Assert.Throws<InvalidDataException>(() => ClothGarmentArtifact3D.Parse(json.Replace(garment.Cloth.ContentKey, new string('0', 64))));
        Assert.Throws<InvalidDataException>(() => ClothGarmentArtifact3D.Parse(json.Replace("\"Z\"", "\"Y\"")));
    }

    [Fact]
    public void GeneratedGarmentEntryAndNestedCompletionUseTheOwnerSchemas()
    {
        string source = FirmamentSemanticSchemas.Get("Garment")!.Entry!;
        var compiled = GarmentCompiler.Compile(source);
        Assert.True(compiled.IsSuccess, string.Join("; ", compiled.Diagnostics));
        string panel = "schema Garment\nGarment Outfit { Panel front {\n ";
        var fields = FirmamentLanguageService.Complete(panel, "draft.firmament", "1", panel.Length);
        Assert.Equal("Panel", fields.Context);
        Assert.Contains(fields.Fields, field => field.Name == "Profile");
        string seam = "schema Garment\nGarment Outfit { Interface<Stitch> side {\n orientation: ";
        var choices = FirmamentLanguageService.Complete(seam, "draft.firmament", "1", seam.Length);
        Assert.Equal("Value", choices.Context);
        Assert.Contains("Reversed", choices.Values!);
        string root = "schema Garment\nGarment Outfit {\n ";
        var entries = FirmamentLanguageService.Complete(root, "draft.firmament", "1", root.Length);
        Assert.Contains(entries.Entries!, entry => entry.Name == "Panel");
    }

    private static CompiledGarment Compile(string name)
    {
        var result = GarmentCompiler.Compile(Source(name));
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        return result.Garment!;
    }

    private static string Source(string name)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        return File.ReadAllText(Path.Combine(root!.FullName, "fixtures", "Canonical", "Garment", name + ".firmament"));
    }

    private sealed class SphereSurface : IClothBodySurface3D
    {
        public ClothBodySample3D Query(Vector3 point)
        {
            float distance = point.Length();
            Vector3 normal = distance > 1e-8f ? point / distance : Vector3.UnitY;
            return new(normal * .1f, normal, distance - .1f);
        }
    }
}
