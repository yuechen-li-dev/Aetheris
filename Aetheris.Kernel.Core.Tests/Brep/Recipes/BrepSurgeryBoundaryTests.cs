using System.Reflection;
using System.Runtime.CompilerServices;
using Aetheris.Kernel.Core.Brep.Surgery;

namespace Aetheris.Kernel.Core.Tests.Brep.Recipes;

public sealed class BrepSurgeryBoundaryTests
{
    [Fact]
    public void SurgeryRemainsInternalAndIsNotFriendExposedToForgeHostOrKernelSdk()
    {
        var surgeryType = typeof(BrepSurgeryValidation);
        Assert.False(surgeryType.IsPublic);

        var friends = surgeryType.Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0])
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Aetheris.Forge.Host", friends);
        Assert.DoesNotContain("Aetheris.Forge.KernelSdk", friends);
        Assert.DoesNotContain("Aetheris.Forge.KernelSDK", friends);
    }
}
