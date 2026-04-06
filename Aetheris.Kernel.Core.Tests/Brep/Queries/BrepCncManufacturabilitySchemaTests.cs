using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Brep.Queries;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Kernel.Core.Tests.Brep.Queries;

public sealed class BrepCncManufacturabilitySchemaTests
{
    [Fact]
    public void Evaluate_PassingCanonicalBody_ReturnsPassWithoutIssues()
    {
        var body = CreateOccupiedCellBody([
            new AxisAlignedBoxExtents(0d, 4d, 0d, 3d, 0d, 2d),
        ]);

        var result = BrepCncManufacturabilitySchema.Evaluate(body, new CncManufacturabilitySchemaInput(0.25d, 0.5d));

        Assert.True(result.IsPass);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_LShapedInternalSharpCorner_ViolatesMinimumToolRadius()
    {
        var body = CreateOccupiedCellBody([
            new AxisAlignedBoxExtents(0d, 2d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(2d, 4d, 0d, 2d, 0d, 1d),
            new AxisAlignedBoxExtents(0d, 2d, 2d, 4d, 0d, 1d),
        ]);

        var result = BrepCncManufacturabilitySchema.Evaluate(body, new CncManufacturabilitySchemaInput(0.75d, 0.5d));

        Assert.False(result.IsPass);
        var violation = Assert.Single(result.Issues.Where(issue => issue.Kind == CncManufacturabilityIssueKind.Violation && issue.RuleId == CncManufacturabilityRuleIds.MinimumInternalCornerRadius));
        Assert.Equal(0d, violation.MeasuredValue);
        Assert.Equal(0.75d, violation.RequiredThreshold);
        Assert.StartsWith("edge:", violation.Location, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ThinWall_ViolatesMinimumWallThickness()
    {
        var body = CreateOccupiedCellBody([
            new AxisAlignedBoxExtents(0d, 8d, 0d, 0.5d, 0d, 2d),
        ]);

        var result = BrepCncManufacturabilitySchema.Evaluate(body, new CncManufacturabilitySchemaInput(0.25d, 1d));

        Assert.False(result.IsPass);
        Assert.Contains(result.Issues, issue =>
            issue.Kind == CncManufacturabilityIssueKind.Violation
            && issue.RuleId == CncManufacturabilityRuleIds.MinimumWallThickness
            && issue.MeasuredValue.HasValue
            && issue.MeasuredValue.Value < 1d);
    }

    [Fact]
    public void Evaluate_NonPlanarBody_ReturnsExplicitUnsupportedDiagnostic()
    {
        var sphere = BrepPrimitives.CreateSphere(2d).Value;

        var result = BrepCncManufacturabilitySchema.Evaluate(sphere, new CncManufacturabilitySchemaInput(0.5d, 1d));

        Assert.False(result.IsPass);
        Assert.Contains(result.Issues, issue =>
            issue.Kind == CncManufacturabilityIssueKind.Unsupported
            && issue.RuleId == CncManufacturabilityRuleIds.MinimumWallThickness
            && issue.Message.Contains("planar", StringComparison.OrdinalIgnoreCase));
    }

    private static BrepBody CreateOccupiedCellBody(IReadOnlyList<AxisAlignedBoxExtents> cells)
    {
        var built = BrepBooleanOrthogonalUnionBuilder.BuildFromCells(cells);
        Assert.True(built.IsSuccess);

        var minX = cells.Min(c => c.MinX);
        var maxX = cells.Max(c => c.MaxX);
        var minY = cells.Min(c => c.MinY);
        var maxY = cells.Max(c => c.MaxY);
        var minZ = cells.Min(c => c.MinZ);
        var maxZ = cells.Max(c => c.MaxZ);

        var composition = new SafeBooleanComposition(
            new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ),
            [],
            SafeBooleanRootDescriptor.FromBox(new AxisAlignedBoxExtents(minX, maxX, minY, maxY, minZ, maxZ)),
            OccupiedCells: cells);

        return new BrepBody(
            built.Value.Topology,
            built.Value.Geometry,
            built.Value.Bindings,
            built.Value.Topology.Vertices
                .Where(v => built.Value.TryGetVertexPoint(v.Id, out _))
                .ToDictionary(v => v.Id, v =>
                {
                    built.Value.TryGetVertexPoint(v.Id, out var point);
                    return point;
                }),
            composition,
            built.Value.ShellRepresentation);
    }
}
