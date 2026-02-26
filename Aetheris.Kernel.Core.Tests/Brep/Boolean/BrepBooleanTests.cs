using Aetheris.Kernel.Core.Brep;
using Aetheris.Kernel.Core.Brep.Boolean;
using Aetheris.Kernel.Core.Diagnostics;

namespace Aetheris.Kernel.Core.Tests.Brep.Boolean;

public sealed class BrepBooleanTests
{
    [Fact]
    public void Execute_NullRequest_FailsWithInvalidArgumentDiagnostic()
    {
        var result = BrepBoolean.Execute(request: null);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal("Boolean request must be provided.", diagnostic.Message);
        Assert.Equal("BrepBoolean.ValidateInputs", diagnostic.Source);
    }

    [Fact]
    public void Execute_NullBodyInRequest_FailsPredictably()
    {
        var right = BrepPrimitives.CreateBox(1d, 1d, 1d).Value;
        var request = new BooleanRequest(null!, right, BooleanOperation.Union);

        var result = BrepBoolean.Execute(request);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.InvalidArgument, diagnostic.Code);
        Assert.Equal("Boolean Union: left body must be provided.", diagnostic.Message);
        Assert.Equal("BrepBoolean.ValidateInputs", diagnostic.Source);
    }

    [Fact]
    public void Execute_DifferentBodies_ReturnsNotImplementedWithoutThrowing()
    {
        var left = BrepPrimitives.CreateBox(2d, 2d, 2d).Value;
        var right = BrepPrimitives.CreateBox(3d, 3d, 3d).Value;

        var exception = Record.Exception(() => BrepBoolean.Execute(new BooleanRequest(left, right, BooleanOperation.Union)));

        Assert.Null(exception);

        var result = BrepBoolean.Execute(new BooleanRequest(left, right, BooleanOperation.Union));
        Assert.False(result.IsSuccess);

        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Union: general B-rep boolean intersection/rebuild is not implemented in M12.", diagnostic.Message);
        Assert.Equal("BrepBoolean.RebuildResult", diagnostic.Source);
    }

    [Fact]
    public void Intersect_SameBodyInstance_SucceedsAndReturnsSameInstance()
    {
        var body = BrepPrimitives.CreateBox(2d, 3d, 4d).Value;

        var result = BrepBoolean.Intersect(body, body);

        Assert.True(result.IsSuccess);
        Assert.Same(body, result.Value);

        var validation = BrepBindingValidator.Validate(result.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Union_SameBodyInstance_SucceedsAndReturnsSameInstance()
    {
        var body = BrepPrimitives.CreateCylinder(1d, 2d).Value;

        var result = BrepBoolean.Union(body, body);

        Assert.True(result.IsSuccess);
        Assert.Same(body, result.Value);

        var validation = BrepBindingValidator.Validate(result.Value, requireAllEdgeAndFaceBindings: true);
        Assert.True(validation.IsSuccess);
    }

    [Fact]
    public void Subtract_SameBodyInstance_ReturnsNotImplemented()
    {
        var body = BrepPrimitives.CreateSphere(1d).Value;

        var result = BrepBoolean.Subtract(body, body);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(KernelDiagnosticCode.NotImplemented, diagnostic.Code);
        Assert.Equal("Boolean Subtract: same-body subtraction requires an empty-body representation that is not available in M12.", diagnostic.Message);
        Assert.Equal("BrepBoolean.RebuildResult", diagnostic.Source);
    }
}
