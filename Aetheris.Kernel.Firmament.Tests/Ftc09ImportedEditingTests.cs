using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Features;
using Aetheris.Kernel.Core.Import;
using Aetheris.Kernel.Core.Step242;
using Aetheris.Kernel.Firmament.FirmamentV2;

namespace Aetheris.Kernel.Firmament.Tests;

public sealed class Ftc09ImportedEditingTests
{
    static string DirectoryPath
    {
        get
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Aetheris.slnx"))) root = root.Parent;
            return Path.Combine(root!.FullName, "fixtures/Regression/InlineStep/production");
        }
    }
    static string Source(bool remove) => File.ReadAllText(Path.Combine(DirectoryPath, remove ? "ftc09-round-remove.firmament" : "ftc09-round-move.firmament"));
    static FirmamentV2ValidationReport Validate(string source) => FirmamentBuildAdmissionValidation.Admit(
        FirmamentV2ValidationReportBuilder.Build(FirmamentV2Parser.Parse(source, DirectoryPath), "revision.firmament", sourceText: source), source, DirectoryPath);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GoldenSource_AdmitsAndExportsTheRealEditedBody(bool remove)
    {
        var source = Source(remove); var validation = Validate(source);
        Assert.Equal("valid", validation.Status); Assert.Equal("Built", validation.ValidationLevel);
        var build = FirmamentBuildAndExport.CompileSource(source, DirectoryPath);
        Assert.True(build.IsSuccess, string.Join(" | ", build.Diagnostics.Select(d => d.Message)));
        Assert.Equal(remove ? 156 : 158, build.Value.RuntimeBody!.Topology.Faces.Count());
        if (remove)
        {
            var report = build.Value.ImportedHoleRemoval!;
            Assert.Equal(156, report.PreservedFaces); Assert.Equal(448, report.PreservedEdges);
            Assert.Equal(new[] { 31, 32 }, report.RetiredWallFaces);
            Assert.DoesNotContain(31, report.FaceCorrespondence.Keys);
            Assert.Equal(1, build.Value.InlineStepMigration!.Removals.EmittedCount);
            Assert.Equal(2, build.Value.InlineStepMigration.Removals.RemovedFaceCount);
            Assert.Equal(0, build.Value.InlineStepMigration.Replacements.EmittedCount);
            Assert.Empty(build.Value.InlineStepMigration.ReplacementStates);
            Assert.Empty(Step242SemanticPmiInspector.Inspect(build.Value.StepText).Items);
        }
        else
        {
            var report = build.Value.ImportedHoleRevision!;
            Assert.Equal(156, report.PreservedFaces); Assert.Equal(448, report.PreservedEdges);
            Assert.Equal(6.35, report.Diameter, 8); Assert.Equal(3.03784, report.Depth, 8);
            var pmi = Assert.Single(Step242SemanticPmiInspector.Inspect(build.Value.StepText).Items, i => i.Kind == "Diameter");
            Assert.Equal(report.Diameter, pmi.Value);
            var walls = build.Value.RuntimeBody.Topology.Faces.Where(f => build.Value.RuntimeBody.GetFaceSurface(f.Id).Cylinder is { } c && System.Math.Abs(c.Origin.X + 70) < 1e-8).ToArray();
            Assert.Equal(2, walls.Length);
            Assert.All(walls, f => Assert.Equal(19.1, build.Value.RuntimeBody.GetFaceSurface(f.Id).Cylinder!.Value.Origin.Z, 8));
        }
        Assert.Contains(build.Diagnostics, d => d.Message.StartsWith("firmament-inline-step-source-semantics-excluded"));
        var reimport = Step242Importer.ImportBody(build.Value.StepText);
        Assert.True(reimport.IsSuccess);
        Assert.Equal(ImportQualificationStatus.Qualified, reimport.Value.ImportQualification!.GeometryStatus);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void PrincipalXAndZ_UseTheSameCertificateAfterRigidPlacement(int axis)
    {
        var built = FirmamentBuildAndExport.CompileSource(Source(false), DirectoryPath).Value;
        var transform = axis == 0 ? Core.Math.Transform3D.CreateRotationZ(double.Pi / 2)
            : Core.Math.Transform3D.CreateRotationX(double.Pi / 2);
        var placed = Aetheris.Kernel.Firmament.Assembly.FirmasmAssemblyExecutor.TransformBody(built.RuntimeBody!, transform);
        var discovery = BrepThroughHoleRevision.Discover(placed);
        var expected = transform.Apply(new Core.Math.Point3D(-70, 3.03784, 19.1));
        var hole = Assert.Single(discovery.Candidates, c => c.CanPlan && c.AxisIndex == axis
            && (axis == 0 ? System.Math.Abs(c.Center!.Value.Y - expected.Y) : System.Math.Abs(c.Center!.Value.X - expected.X)) < 1e-6
            && System.Math.Abs(c.Diameter - 6.35) < 1e-6);
        var destination = axis == 0 ? hole.Center!.Value with { Y = -55 } : hole.Center!.Value with { X = -55 };
        var move = BrepThroughHoleRevision.PlanMove(placed, hole.Id, "Placed", destination);
        Assert.True(move.IsSuccess, string.Join(" | ", move.Diagnostics.Select(d => d.Message)));
        Assert.Equal(158, move.Value.Revision.Body.Topology.Faces.Count());
        var remove = BrepThroughHoleRevision.PlanRemove(placed, hole.Id, "Placed");
        Assert.True(remove.IsSuccess, string.Join(" | ", remove.Diagnostics.Select(d => d.Message)));
        Assert.Equal(156, remove.Value.Removal.Body.Topology.Faces.Count());
        var oblique = Aetheris.Kernel.Firmament.Assembly.FirmasmAssemblyExecutor.TransformBody(built.RuntimeBody!,
            Core.Math.Transform3D.CreateRotationX(double.Pi / 4));
        var unsupported = BrepThroughHoleRevision.Discover(oblique);
        Assert.DoesNotContain(unsupported.Candidates, c => c.CanPlan);
        Assert.Contains(unsupported.Candidates, c => c.Diagnostics.Any(d => d.Contains("oblique")));
    }

    [Theory]
    [InlineData("measurement")]
    [InlineData("field")]
    [InlineData("pin")]
    [InlineData("bounds")]
    [InlineData("diameter")]
    [InlineData("off-plane")]
    [InlineData("remove-pmi")]
    [InlineData("remove-field")]
    public void InvalidIntent_ValidationAndBuildRejectWithLocation(string kind)
    {
        var source = Source(kind.StartsWith("remove"));
        source = kind switch
        {
            "measurement" => source.Replace("Source.MountHole.diameter", "Source.MountHole.diammeter"),
            "field" => source.Replace("diameter:", "diammeter:"),
            "pin" => source.Replace("daad9815", "0aad9815"),
            "bounds" => source.Replace("min:", "minimum:"),
            "diameter" => source.Replace("Source.MountHole.diameter", "6.4mm"),
            "off-plane" => source.Replace("Source.MountHole.center.Y", "4mm"),
            "remove-field" => source.Replace("within:", "wthin:"),
            _ => source[..source.LastIndexOf('}')] + "Pmi { HoleDiameter Old { target: Source.region(\"MountHole\"); value: 6.35mm } } }"
        };
        var validation = Validate(source);
        Assert.Equal("invalid", validation.Status);
        Assert.False(FirmamentBuildAndExport.CompileSource(source, DirectoryPath).IsSuccess);
        Assert.Contains(validation.Diagnostics, d => d.SourceSpan is not null && d.Severity is "error" or "fatal");
    }
}
