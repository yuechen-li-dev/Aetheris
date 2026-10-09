using Aetheris.Continuum.Backends.Sdf;
using Aetheris.Kernel.Core.Math;

namespace Aetheris.Continuum.Tests.Backends.Sdf;

public sealed class FieldProjectBoundaryTests
{
    [Fact]
    public void Native_fields_have_one_owner_without_a_compiler_or_application_dependency()
    {
        var assembly = typeof(SdfNode).Assembly;
        Assert.Equal("Aetheris.Fields", assembly.GetName().Name);
        Assert.Equal(assembly, typeof(CirVisualTsLowerer).Assembly);
        string[] referenced = assembly.GetReferencedAssemblies().Select(item => item.Name!).ToArray();
        Assert.DoesNotContain(referenced, name => name.StartsWith("Copeland.", StringComparison.Ordinal));
        Assert.DoesNotContain("Aetheris.Continuum", referenced);
        Assert.DoesNotContain("Aetheris.Kernel.StandardLibrary", referenced);
        Assert.Contains("Aetheris.Kernel.Core", referenced);
    }

    [Fact]
    public void Continuum_preserves_existing_assembly_qualified_type_resolution()
    {
        var resolved = Type.GetType("Aetheris.Continuum.Backends.Sdf.SdfSphereNode, Aetheris.Continuum", throwOnError: true);
        Assert.Equal(typeof(SdfSphereNode), resolved);
    }

    [Fact]
    public void Safe_exterior_bound_is_distinct_from_exact_distance_and_retains_provenance()
    {
        var composed = new SdfSubtractNode(new SdfBoxNode(2, 2, 2), new SdfSphereNode(1));
        var result = CirVisualTsLowerer.Lower(composed);
        Assert.True(result.Success);
        Assert.True(result.Program!.ConservativeExteriorStep);
        Assert.Equal("AGPL-3.0-only", result.Program.SourceLicense);
        Assert.False(SdfCapabilityAnalyzer.Analyze(composed).HasFlag(SdfFieldCapabilities.ExactEuclideanSignedDistance));
        var scaled = CirVisualTsLowerer.Lower(new SdfTransformNode(composed, Transform3D.CreateScale(.5)));
        Assert.False(scaled.Success);
        Assert.Contains("transform-not-rigid", scaled.FallbackReason);
    }
}
